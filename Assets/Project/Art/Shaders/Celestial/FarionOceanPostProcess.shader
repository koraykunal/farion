Shader "Hidden/Farion/Celestial/Ocean Post Process"
{
    Properties
    {
        [NoScaleOffset] _FarionOceanWaveNormalA("Wave Normal A", 2D) = "bump" {}
        [NoScaleOffset] _FarionOceanWaveNormalB("Wave Normal B", 2D) = "bump" {}
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
            Name "FarionOceanPostProcess"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define FARION_MAX_OCEAN_EFFECTS 8

            int _FarionOceanEffectCount;
            float4 _FarionOceanSpheres[FARION_MAX_OCEAN_EFFECTS];
            float4 _FarionPlanetSpheres[FARION_MAX_OCEAN_EFFECTS];
            half4 _FarionOceanDeepColors[FARION_MAX_OCEAN_EFFECTS];
            half4 _FarionOceanShallowColors[FARION_MAX_OCEAN_EFFECTS];
            half4 _FarionOceanFresnelColors[FARION_MAX_OCEAN_EFFECTS];
            half4 _FarionOceanSpecularColors[FARION_MAX_OCEAN_EFFECTS];
            half4 _FarionOceanUnderwaterColors[FARION_MAX_OCEAN_EFFECTS];
            float4 _FarionOceanOpticalParams[FARION_MAX_OCEAN_EFFECTS];
            float4 _FarionOceanUnderwaterParams[FARION_MAX_OCEAN_EFFECTS];
            float4 _FarionOceanWaveParams[FARION_MAX_OCEAN_EFFECTS];
            float4 _FarionOceanLightingParams[FARION_MAX_OCEAN_EFFECTS];
            float4 _FarionOceanExposureParams[FARION_MAX_OCEAN_EFFECTS];

            float4 _FarionStarPositionWS;
            float4 _FarionStarDirectionWS;
            half4 _FarionStarColor;
            float _FarionStarIntensity;
            half4 _FarionAmbientColor;

            TEXTURE2D(_FarionOceanWaveNormalA);
            SAMPLER(sampler_FarionOceanWaveNormalA);
            TEXTURE2D(_FarionOceanWaveNormalB);
            SAMPLER(sampler_FarionOceanWaveNormalB);

            float2 RaySphere(float3 centre, float radius, float3 rayOrigin, float3 rayDirection)
            {
                float3 offset = rayOrigin - centre;
                float b = dot(offset, rayDirection);
                float c = dot(offset, offset) - radius * radius;
                float discriminant = b * b - c;
                if (discriminant < 0.0)
                {
                    return float2(-1.0, 0.0);
                }

                float s = sqrt(discriminant);
                float dstToSphere = max(-b - s, 0.0);
                float dstOutSphere = -b + s;
                if (dstOutSphere < 0.0)
                {
                    return float2(-1.0, 0.0);
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

            float3 TriplanarWeights(float3 normalDirection)
            {
                float3 weights = pow(abs(normalDirection), 4.0);
                return weights / max(dot(weights, 1.0), 0.0001);
            }

            half3 SampleTriplanarNormal(
                TEXTURE2D_PARAM(normalTexture, normalSampler),
                float3 position,
                float3 normal,
                float scale,
                float2 offset)
            {
                float3 weights = TriplanarWeights(normal);
                float3 axisSign = sign(normal);

                half3 normalX = UnpackNormal(SAMPLE_TEXTURE2D(normalTexture, normalSampler, position.zy * scale + offset));
                half3 normalY = UnpackNormal(SAMPLE_TEXTURE2D(normalTexture, normalSampler, position.xz * scale + offset));
                half3 normalZ = UnpackNormal(SAMPLE_TEXTURE2D(normalTexture, normalSampler, position.xy * scale + offset));

                normalX = half3(normalX.z * axisSign.x, normalX.y, normalX.x);
                normalY = half3(normalY.x, normalY.z * axisSign.y, normalY.y);
                normalZ = half3(normalZ.x, normalZ.y, normalZ.z * axisSign.z);
                return normalize(normalX * weights.x + normalY * weights.y + normalZ * weights.z);
            }

            half3 SampleWaveNormal(int index, float3 localOceanPosition, float3 sphereNormal, float bodyRadius)
            {
                float scale = _FarionOceanWaveParams[index].x / max(bodyRadius, 0.001);
                float speed = _FarionOceanWaveParams[index].y;
                half strength = saturate(_FarionOceanWaveParams[index].z);

                float time = _Time.y * speed;
                float2 waveOffsetA = float2(time, time * 0.8);
                float2 waveOffsetB = float2(time * -0.8, time * -0.3);

                half3 waveNormalA = SampleTriplanarNormal(
                    TEXTURE2D_ARGS(_FarionOceanWaveNormalA, sampler_FarionOceanWaveNormalA),
                    localOceanPosition,
                    sphereNormal,
                    scale,
                    waveOffsetA);
                half3 waveNormalB = SampleTriplanarNormal(
                    TEXTURE2D_ARGS(_FarionOceanWaveNormalB, sampler_FarionOceanWaveNormalB),
                    localOceanPosition,
                    sphereNormal,
                    scale,
                    waveOffsetB);
                half3 waveNormal = normalize(waveNormalA + waveNormalB - sphereNormal);

                return normalize(lerp(sphereNormal, waveNormal, strength));
            }

            half SchlickFresnel(half cosTheta)
            {
                const half f0 = 0.0204h;
                return f0 + (1.0h - f0) * pow(saturate(1.0h - cosTheta), 5.0h);
            }

            bool TryBuildWaterSegment(
                float2 oceanHit,
                bool cameraInsideOcean,
                float sceneDistance,
                out float segmentStart,
                out float segmentEnd,
                out float segmentLength,
                out bool entersFromAir,
                out bool exitsToAir)
            {
                float hitStart = oceanHit.x;
                float hitEnd = oceanHit.x + oceanHit.y;
                segmentStart = cameraInsideOcean ? 0.0 : hitStart;
                segmentEnd = min(hitEnd, sceneDistance);
                segmentLength = segmentEnd - segmentStart;
                entersFromAir = !cameraInsideOcean;
                exitsToAir = cameraInsideOcean && sceneDistance >= hitEnd - 0.01;
                return segmentLength > 0.0;
            }

            half3 ApplyWaterVolume(
                half3 sourceColor,
                half3 waterColor,
                half3 underwaterColor,
                half3 waterVolumeLight,
                float segmentLength,
                float bodyRadius,
                float extinctionMultiplier,
                half surfaceTransmission)
            {
                half transmittance = saturate(exp(-segmentLength / bodyRadius * extinctionMultiplier));
                half fog = 1.0h - transmittance;
                half3 tint = saturate(lerp(waterColor, underwaterColor * 5.0h + 0.12h, 0.5h));
                half3 transmittedScene = sourceColor * transmittance * tint * surfaceTransmission;
                half3 inScatteredWater = waterColor * waterVolumeLight * fog;
                return transmittedScene + inScatteredWater;
            }

            half3 ApplyOcean(
                half3 sourceColor,
                int index,
                float3 rayOrigin,
                float3 rayDirection,
                float sceneDistance,
                bool sceneIsSky)
            {
                float oceanRadius = _FarionOceanSpheres[index].w;
                if (oceanRadius <= 0.0)
                {
                    return sourceColor;
                }

                float3 centre = _FarionOceanSpheres[index].xyz;
                float bodyRadius = max(_FarionPlanetSpheres[index].w, 0.001);
                bool cameraInsideOcean = length(rayOrigin - centre) < oceanRadius - 0.001;
                float2 oceanHit = RaySphere(centre, oceanRadius, rayOrigin, rayDirection);
                if (oceanHit.x < 0.0)
                {
                    return sourceColor;
                }

                float segmentStart;
                float segmentEnd;
                float segmentLength;
                bool entersFromAir;
                bool exitsToAir;
                if (!TryBuildWaterSegment(
                    oceanHit,
                    cameraInsideOcean,
                    sceneDistance,
                    segmentStart,
                    segmentEnd,
                    segmentLength,
                    entersFromAir,
                    exitsToAir))
                {
                    return sourceColor;
                }

                half waterVisibility = 1.0h;
                if (!sceneIsSky && !cameraInsideOcean)
                {
                    float surfaceOcclusionWidth = max(bodyRadius * 0.00075, 0.01);
                    waterVisibility = smoothstep(0.0, surfaceOcclusionWidth, sceneDistance - segmentStart);
                    if (waterVisibility <= 0.001h)
                    {
                        return sourceColor;
                    }
                }

                float surfaceDistance = entersFromAir ? segmentStart : segmentEnd;
                float3 hitPosition = rayOrigin + rayDirection * surfaceDistance;
                float3 localOceanPosition = hitPosition - centre;
                float3 sphereNormal = normalize(localOceanPosition);
                half3 waveNormal = SampleWaveNormal(index, localOceanPosition, sphereNormal, bodyRadius);

                float depthMultiplier = _FarionOceanOpticalParams[index].x;
                float alphaMultiplier = _FarionOceanOpticalParams[index].y;
                float underwaterDensity = _FarionOceanUnderwaterParams[index].x;
                half underwaterSurfaceStrength = saturate(_FarionOceanUnderwaterParams[index].y);
                half underwaterSpecularStrength = saturate(_FarionOceanUnderwaterParams[index].z);
                half smoothness = saturate(_FarionOceanLightingParams[index].x);
                half specularStrength = _FarionOceanLightingParams[index].y;
                half fresnelStrength = saturate(_FarionOceanLightingParams[index].z);

                float extinctionMultiplier = cameraInsideOcean ? underwaterDensity : alphaMultiplier;
                half depth01 = saturate(1.0h - exp(-segmentLength / bodyRadius * depthMultiplier));

                half3 starDirection = normalize(_FarionStarDirectionWS.xyz);
                half3 viewDirection = -rayDirection;
                half diffuseLighting = saturate(dot(sphereNormal, starDirection));
                half waveDiffuseLighting = saturate(dot(waveNormal, starDirection));

                half3 oceanColor = lerp(_FarionOceanShallowColors[index].rgb, _FarionOceanDeepColors[index].rgb, depth01);
                half3 ambient = max(_FarionAmbientColor.rgb, 0.025h);
                float referenceLightIntensity = max(_FarionOceanExposureParams[index].x, 0.001);
                half3 starRadiance = _FarionStarColor.rgb * (max(_FarionStarIntensity, 0.0) / referenceLightIntensity);
                half3 halfDirection = normalize(starDirection + viewDirection);
                half specularPower = lerp(64.0h, 512.0h, smoothness);
                half specular = pow(saturate(dot(waveNormal, halfDirection)), specularPower) * specularStrength;
                specular *= cameraInsideOcean ? underwaterSpecularStrength : 1.0h;

                half3 waterVolumeLight = ambient + starRadiance * (0.12h + diffuseLighting * 0.55h);
                half3 surfaceReflectionLight = ambient + starRadiance * (0.14h + waveDiffuseLighting * 0.42h);
                half3 reflectedSurface = _FarionOceanFresnelColors[index].rgb * surfaceReflectionLight
                    + _FarionOceanSpecularColors[index].rgb * starRadiance * specular * 0.25h;

                bool hasSurfaceInterface = entersFromAir || exitsToAir;
                half interfaceCos = entersFromAir
                    ? saturate(dot(waveNormal, viewDirection))
                    : saturate(dot(waveNormal, rayDirection));
                half interfaceFresnel = SchlickFresnel(interfaceCos);
                half totalInternalReflection = 0.0h;

                if (exitsToAir)
                {
                    half etaWaterToAir = 1.333h;
                    half sin2Transmitted = etaWaterToAir * etaWaterToAir * (1.0h - interfaceCos * interfaceCos);
                    totalInternalReflection = step(1.0h, sin2Transmitted);
                }

                half surfaceReflection = hasSurfaceInterface
                    ? saturate(totalInternalReflection + (1.0h - totalInternalReflection) * interfaceFresnel * fresnelStrength)
                    : 0.0h;
                half surfaceTransmission = hasSurfaceInterface
                    ? (1.0h - surfaceReflection)
                    : 1.0h;
                surfaceTransmission *= exitsToAir ? underwaterSurfaceStrength : 1.0h;
                half specularVisibility = hasSurfaceInterface
                    ? saturate(0.18h + interfaceFresnel * 2.5h)
                    : 0.0h;
                half3 surfaceGlint = _FarionOceanSpecularColors[index].rgb * starRadiance * specular * specularVisibility;

                half3 volumeColor = cameraInsideOcean
                    ? _FarionOceanUnderwaterColors[index].rgb
                    : oceanColor;
                half3 volumeLight = cameraInsideOcean
                    ? ambient + starRadiance * (0.04h + diffuseLighting * 0.16h)
                    : waterVolumeLight;
                half3 transmittedWater = ApplyWaterVolume(
                    sourceColor,
                    volumeColor,
                    _FarionOceanUnderwaterColors[index].rgb,
                    volumeLight,
                    segmentLength,
                    bodyRadius,
                    extinctionMultiplier,
                    surfaceTransmission);

                half3 waterColor = lerp(transmittedWater, reflectedSurface, surfaceReflection) + surfaceGlint;
                return lerp(sourceColor, waterColor, waterVisibility);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);
                float depthForPosition = IsSkyDepth(rawDepth) ? UNITY_RAW_FAR_CLIP_VALUE : rawDepth;
                float3 scenePositionWS = ComputeWorldSpacePosition(uv, depthForPosition, UNITY_MATRIX_I_VP);
                float3 rayOrigin = _WorldSpaceCameraPos.xyz;
                float3 rayDirection = normalize(scenePositionWS - rayOrigin);
                float sceneDistance = IsSkyDepth(rawDepth)
                    ? _ProjectionParams.z
                    : length(scenePositionWS - rayOrigin);

                half3 color = source.rgb;
                bool sceneIsSky = IsSkyDepth(rawDepth);
                for (int i = 0; i < _FarionOceanEffectCount; i++)
                {
                    color = ApplyOcean(color, i, rayOrigin, rayDirection, sceneDistance, sceneIsSky);
                }

                return half4(color, source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
