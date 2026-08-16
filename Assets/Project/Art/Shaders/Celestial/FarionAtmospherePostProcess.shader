Shader "Hidden/Farion/Celestial/Atmosphere Post Process"
{
    Properties
    {
        [NoScaleOffset] _FarionAtmosphereBakedOpticalDepth("Baked Optical Depth", 2D) = "white" {}
        [NoScaleOffset] _FarionAtmosphereBlueNoise("Blue Noise", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FarionAtmospherePostProcess"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define FARION_MAX_ATMOSPHERE_EFFECTS 8
            #define FARION_MAX_SCATTER_STEPS 32
            #define FARION_MAX_FLOAT 3.402823466e+38
            #define FARION_SHADOW_PENUMBRA 0.04

            int _FarionAtmosphereEffectCount;
            float4 _FarionAtmosphereSpheres[FARION_MAX_ATMOSPHERE_EFFECTS];
            float4 _FarionAtmospherePlanetSpheres[FARION_MAX_ATMOSPHERE_EFFECTS];
            float4 _FarionAtmosphereSurfaceRadii[FARION_MAX_ATMOSPHERE_EFFECTS];
            float4 _FarionAtmosphereScatteringCoefficients[FARION_MAX_ATMOSPHERE_EFFECTS];
            float4 _FarionAtmosphereOpticalParams[FARION_MAX_ATMOSPHERE_EFFECTS];
            float4 _FarionAtmosphereSampleParams[FARION_MAX_ATMOSPHERE_EFFECTS];
            float4 _FarionAtmosphereMieParams[FARION_MAX_ATMOSPHERE_EFFECTS];
            float4 _FarionAtmosphereOzoneCoefficients[FARION_MAX_ATMOSPHERE_EFFECTS];
            float4 _FarionAtmosphereOzoneShape[FARION_MAX_ATMOSPHERE_EFFECTS];

            float4 _FarionStarPositionWS;
            float4 _FarionStarDirectionWS;
            half4 _FarionStarColor;
            float _FarionStarIntensity;

            TEXTURE2D(_FarionAtmosphereBakedOpticalDepth);
            SAMPLER(sampler_FarionAtmosphereBakedOpticalDepth);
            TEXTURE2D(_FarionAtmosphereBlueNoise);
            SAMPLER(sampler_FarionAtmosphereBlueNoise);

            float2 RaySphere(float3 centre, float radius, float3 rayOrigin, float3 rayDirection)
            {
                float3 offset = rayOrigin - centre;
                float b = dot(offset, rayDirection);
                float c = dot(offset, offset) - radius * radius;
                float discriminant = b * b - c;
                if (discriminant < 0.0)
                {
                    return float2(FARION_MAX_FLOAT, 0.0);
                }

                float s = sqrt(discriminant);
                float dstToSphere = max(-b - s, 0.0);
                float dstOutSphere = -b + s;
                if (dstOutSphere < 0.0)
                {
                    return float2(FARION_MAX_FLOAT, 0.0);
                }

                return float2(dstToSphere, dstOutSphere - dstToSphere);
            }

            bool IsSkyDepth(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth <= 0.000001;
            #else
                return rawDepth >= 0.999999;
            #endif
            }

            float HeightAboveSurface01(int index, float3 samplePoint)
            {
                float3 centre = _FarionAtmosphereSpheres[index].xyz;
                float planetRadius = _FarionAtmospherePlanetSpheres[index].w;
                float atmosphereRadius = _FarionAtmosphereSpheres[index].w;
                float atmosphereThickness = max(atmosphereRadius - planetRadius, 0.0001);
                float heightAboveSurface = length(samplePoint - centre) - planetRadius;
                return saturate(heightAboveSurface / atmosphereThickness);
            }

            float3 DensityAtPoint(int index, float3 samplePoint)
            {
                float height01 = HeightAboveSurface01(index, samplePoint);
                float rayleighFalloff = _FarionAtmosphereOpticalParams[index].x;
                float mieFalloff = _FarionAtmosphereMieParams[index].w;
                float ozonePeak = _FarionAtmosphereOzoneShape[index].x;
                float ozoneWidth = max(_FarionAtmosphereOzoneShape[index].y, 0.0001);

                float rayleigh = exp(-height01 * rayleighFalloff) * (1.0 - height01);
                float mie = exp(-height01 * mieFalloff) * (1.0 - height01);
                float ozoneOffset = (height01 - ozonePeak) / ozoneWidth;
                float ozone = exp(-ozoneOffset * ozoneOffset) * (1.0 - height01);

                return float3(rayleigh, mie, ozone);
            }

            float3 ExtinctionCoefficients(int index)
            {
                float3 rayleigh = _FarionAtmosphereScatteringCoefficients[index].xyz;
                float mie = _FarionAtmosphereMieParams[index].x * _FarionAtmosphereMieParams[index].z;
                float3 ozone = _FarionAtmosphereOzoneCoefficients[index].xyz;
                return rayleigh + mie.xxx + ozone;
            }

            float3 ApplyExtinction(int index, float3 opticalDepth)
            {
                float3 rayleigh = _FarionAtmosphereScatteringCoefficients[index].xyz * opticalDepth.x;
                float mie = _FarionAtmosphereMieParams[index].x
                    * _FarionAtmosphereMieParams[index].z
                    * opticalDepth.y;
                float3 ozone = _FarionAtmosphereOzoneCoefficients[index].xyz * opticalDepth.z;
                return rayleigh + mie.xxx + ozone;
            }

            float RayleighPhase(float cosineAngle)
            {
                return 0.75 * (1.0 + cosineAngle * cosineAngle);
            }

            float MiePhase(float cosineAngle, float anisotropy)
            {
                float g = clamp(anisotropy, -0.95, 0.95);
                float gSquared = g * g;
                float denominator = 1.0 + gSquared - 2.0 * g * cosineAngle;
                return (1.0 - gSquared) / max(pow(max(denominator, 0.0001), 1.5), 0.0001);
            }

            float2 SquareUV(float2 uv)
            {
                float scale = 1000.0;
                return float2(uv.x * _ScreenParams.x / scale, uv.y * _ScreenParams.y / scale);
            }

            float3 OpticalDepthBaked(int index, float3 rayOrigin, float3 rayDirection)
            {
                float3 centre = _FarionAtmosphereSpheres[index].xyz;
                float height01 = HeightAboveSurface01(index, rayOrigin);
                float uvX = 1.0 - (dot(normalize(rayOrigin - centre), rayDirection) * 0.5 + 0.5);
                return SAMPLE_TEXTURE2D_LOD(
                    _FarionAtmosphereBakedOpticalDepth,
                    sampler_FarionAtmosphereBakedOpticalDepth,
                    float2(uvX, height01),
                    0).rgb;
            }

            float PlanetShadow(float3 samplePoint, float3 centre, float planetRadius, float3 dirToStar)
            {
                float3 toSample = samplePoint - centre;
                float along = dot(toSample, dirToStar);
                if (along >= 0.0)
                {
                    return 1.0;
                }

                float perpendicular = length(toSample - dirToStar * along);
                return smoothstep(
                    planetRadius - planetRadius * FARION_SHADOW_PENUMBRA,
                    planetRadius,
                    perpendicular);
            }

            float3 OpticalDepthBakedBetweenPoints(int index, float3 rayOrigin, float3 rayDirection, float rayLength)
            {
                float3 centre = _FarionAtmosphereSpheres[index].xyz;
                float3 endPoint = rayOrigin + rayDirection * rayLength;
                float d = dot(rayDirection, normalize(rayOrigin - centre));

                const float blendStrength = 1.5;
                float w = saturate(d * blendStrength + 0.5);
                float3 d1 = OpticalDepthBaked(index, rayOrigin, rayDirection)
                    - OpticalDepthBaked(index, endPoint, rayDirection);
                float3 d2 = OpticalDepthBaked(index, endPoint, -rayDirection)
                    - OpticalDepthBaked(index, rayOrigin, -rayDirection);
                return max(lerp(d2, d1, w), 0.0);
            }

            half3 ApplyAtmosphere(
                half3 sourceColor,
                int index,
                float3 rayOrigin,
                float3 rayDirection,
                float sceneDistance,
                float2 screenUV)
            {
                float3 centre = _FarionAtmosphereSpheres[index].xyz;
                float atmosphereRadius = _FarionAtmosphereSpheres[index].w;
                float planetRadius = max(_FarionAtmospherePlanetSpheres[index].w, 0.001);
                float surfaceRadius = max(_FarionAtmosphereSurfaceRadii[index].x, planetRadius);

                float2 atmosphereHit = RaySphere(centre, atmosphereRadius, rayOrigin, rayDirection);
                if (atmosphereHit.y <= 0.0)
                {
                    return sourceColor;
                }

                float2 surfaceHit = RaySphere(centre, surfaceRadius, rayOrigin, rayDirection);
                float surfaceDistance = min(sceneDistance, surfaceHit.x);

                float dstToAtmosphere = atmosphereHit.x;
                float dstThroughAtmosphere = min(atmosphereHit.y, surfaceDistance - dstToAtmosphere);
                if (dstThroughAtmosphere <= 0.0)
                {
                    return sourceColor;
                }

                const float epsilon = 0.0001;
                float3 scatterOrigin = rayOrigin + rayDirection * (dstToAtmosphere + epsilon);
                float scatterLength = max(0.0, dstThroughAtmosphere - epsilon * 2.0);
                int inStepCount = max(2, (int)_FarionAtmosphereSampleParams[index].x);
                float stepSize = scatterLength / max(inStepCount, 1);

                float3 dirToStar = normalize(_FarionStarDirectionWS.xyz);
                float3 rayleighCoefficients = _FarionAtmosphereScatteringCoefficients[index].xyz;
                float mieScattering = _FarionAtmosphereMieParams[index].x;
                float mieAnisotropy = _FarionAtmosphereMieParams[index].y;
                float intensity = _FarionAtmosphereOpticalParams[index].y;
                float ditherStrength = _FarionAtmosphereOpticalParams[index].z;
                float ditherScale = _FarionAtmosphereOpticalParams[index].w;
                float blueNoise = SAMPLE_TEXTURE2D_LOD(
                    _FarionAtmosphereBlueNoise,
                    sampler_FarionAtmosphereBlueNoise,
                    SquareUV(screenUV) * ditherScale,
                    0).r;
                float dither = (blueNoise - 0.5) * ditherStrength;

                float cosineAngle = dot(rayDirection, dirToStar);
                float rayleighPhase = RayleighPhase(cosineAngle);
                float miePhase = MiePhase(cosineAngle, mieAnisotropy);

                float3 inScatteredLight = 0.0;
                float shadowSum = 0.0;
                int shadowSamples = 0;
                float sampleOffset = saturate(0.5 + dither) * stepSize;
                float3 samplePoint = scatterOrigin + rayDirection * sampleOffset;

                for (int i = 0; i < FARION_MAX_SCATTER_STEPS; i++)
                {
                    if (i >= inStepCount)
                    {
                        break;
                    }

                    float3 localDensity = DensityAtPoint(index, samplePoint);
                    float3 sunRayOpticalDepth = OpticalDepthBaked(index, samplePoint, dirToStar);
                    float sampleDistance = min(sampleOffset + stepSize * i, scatterLength);
                    float3 viewRayOpticalDepth = OpticalDepthBakedBetweenPoints(
                        index,
                        scatterOrigin,
                        rayDirection,
                        sampleDistance);

                    float sampleShadow = PlanetShadow(samplePoint, centre, planetRadius, dirToStar);
                    shadowSum += sampleShadow;
                    shadowSamples++;

                    float3 transmittance = exp(
                        -ApplyExtinction(index, sunRayOpticalDepth + viewRayOpticalDepth) * intensity)
                        * sampleShadow;
                    float mieScattered = mieScattering * localDensity.y * miePhase;
                    float3 scattered =
                        rayleighCoefficients * localDensity.x * rayleighPhase
                        + mieScattered.xxx;
                    inScatteredLight += scattered * transmittance;
                    samplePoint += rayDirection * stepSize;
                }

                float referenceLightIntensity = max(_FarionAtmosphereSampleParams[index].z, 0.001);
                float3 starRadiance = _FarionStarColor.rgb * (max(_FarionStarIntensity, 0.0) / referenceLightIntensity);
                inScatteredLight *= intensity * starRadiance * stepSize / planetRadius;

                float3 extinction = ExtinctionCoefficients(index);
                float maxExtinction = max(max(extinction.x, extinction.y), extinction.z);
                float3 scatterTint = extinction / max(maxExtinction, 0.001);
                float3 finalViewOpticalDepth = OpticalDepthBakedBetweenPoints(index, scatterOrigin, rayDirection, scatterLength);
                float3 viewTransmittance = exp(-ApplyExtinction(index, finalViewOpticalDepth) * intensity);
                float airMass = 1.0 - saturate(dot(viewTransmittance, float3(0.2126, 0.7152, 0.0722)));
                inScatteredLight += scatterTint * starRadiance * airMass * 0.018
                    * (shadowSum / max(shadowSamples, 1));

                return sourceColor * viewTransmittance + inScatteredLight;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);
                bool depthIsSky = IsSkyDepth(rawDepth);
                float depthForPosition = depthIsSky ? UNITY_RAW_FAR_CLIP_VALUE : rawDepth;
                float3 scenePositionWS = ComputeWorldSpacePosition(uv, depthForPosition, UNITY_MATRIX_I_VP);
                float3 rayOrigin = _WorldSpaceCameraPos.xyz;
                float3 rayDirection = normalize(scenePositionWS - rayOrigin);
                float sceneDistance = depthIsSky
                    ? _ProjectionParams.z
                    : length(scenePositionWS - rayOrigin);

                half3 color = source.rgb;
                for (int i = 0; i < _FarionAtmosphereEffectCount; i++)
                {
                    color = ApplyAtmosphere(color, i, rayOrigin, rayDirection, sceneDistance, uv);
                }

                return half4(color, source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
