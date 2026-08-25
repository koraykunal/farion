Shader "Hidden/Farion/Celestial/Ocean Post Process"
{
    Properties
    {
        [NoScaleOffset] _FarionOceanWaveNormalA("Wave Normal A", 2D) = "bump" {}
        [NoScaleOffset] _FarionOceanWaveNormalB("Wave Normal B", 2D) = "bump" {}
        [NoScaleOffset] _FarionOceanAtmosphereOpticalDepth("Atmosphere Optical Depth", 2D) = "white" {}
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
            #include "FarionAtmosphereLighting.hlsl"
            #include "FarionOceanSurface.hlsl"

            // One body per pass: the feature chains a pass per ocean, so the shader
            // never needs to loop.
            float4 _FarionOceanSphere;
            float4 _FarionPlanetSphere;
            half4 _FarionOceanDeepColor;
            half4 _FarionOceanShallowColor;
            half4 _FarionOceanFresnelColor;
            half4 _FarionOceanHorizonColor;
            half4 _FarionOceanSpecularColor;
            half4 _FarionOceanUnderwaterColor;
            half4 _FarionOceanFoamColor;
            float _FarionOceanDepthMultiplier;
            float _FarionOceanReferenceLightIntensity;
            // xyz: extinction per world unit, w: specular strength.
            float4 _FarionOceanUnderwaterOptics;
            // x: detail normal scale, y: detail speed, z: detail strength.
            float4 _FarionOceanWaveParams;
            // xyz: wrapped wave phases.
            float4 _FarionOceanSwellPhases;
            // x: amplitude, y: wave length, z: foam width, w: foam strength.
            float4 _FarionOceanSwellShape;
            float4x4 _FarionOceanWorldToLocal;
            // x: signed camera distance to the displaced surface,
            // y: longest underwater spot light range, z: underwater visibility distance.
            float4 _FarionOceanCameraSurfaceParams;
            // x: smoothness, y: specular strength, z: index of refraction, w: scatter strength.
            float4 _FarionOceanLightingParams;
            float4 _FarionOceanAtmosphereParams;
            float4 _FarionOceanAtmosphereRayleigh;
            float4 _FarionOceanAtmosphereOzone;

            float4 _FarionStarPositionWS;
            float4 _FarionStarDirectionWS;
            half4 _FarionStarColor;
            float _FarionStarIntensity;
            half4 _FarionAmbientColor;

            TEXTURE2D(_FarionOceanWaveNormalA);
            SAMPLER(sampler_FarionOceanWaveNormalA);
            TEXTURE2D(_FarionOceanWaveNormalB);
            SAMPLER(sampler_FarionOceanWaveNormalB);
            TEXTURE2D(_FarionOceanAtmosphereOpticalDepth);
            SAMPLER(sampler_FarionOceanAtmosphereOpticalDepth);

            #define FARION_MAX_UNDERWATER_LIGHTS 4
            int _FarionUnderwaterLightCount;
            float4 _FarionUnderwaterLightPositionRange[FARION_MAX_UNDERWATER_LIGHTS];
            float4 _FarionUnderwaterLightDirectionOuterCos[FARION_MAX_UNDERWATER_LIGHTS];
            half4 _FarionUnderwaterLightColorStrength[FARION_MAX_UNDERWATER_LIGHTS];
            float _FarionUnderwaterLightInnerConeCos[FARION_MAX_UNDERWATER_LIGHTS];

            FarionOceanSurfaceField OceanField()
            {
                return FarionBuildOceanSurfaceField(
                    _FarionOceanSphere.xyz,
                    _FarionOceanSphere.w,
                    _FarionOceanSwellShape.y,
                    _FarionOceanSwellShape.x,
                    _FarionOceanSwellPhases.xyz,
                    _FarionOceanWorldToLocal);
            }

            bool IsPointInsideOcean(float3 worldPosition)
            {
                return FarionOceanSurfaceRadialError(OceanField(), worldPosition, 1.0) < 0.0;
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

            // Detail ripples on top of the swell. Their contribution fades with
            // distance, otherwise the normal map aliases into boiling specular.
            half3 SampleDetailNormal(float3 localOceanPosition, float3 baseNormal, half detailFade)
            {
                float scale = _FarionOceanWaveParams.x;
                float speed = _FarionOceanWaveParams.y;
                half strength = saturate(_FarionOceanWaveParams.z) * detailFade;
                if (strength <= 0.001h)
                {
                    return baseNormal;
                }

                float time = _Time.y * speed;
                float2 waveOffsetA = float2(time, time * 0.8);
                float2 waveOffsetB = float2(time * -0.8, time * -0.3);

                half3 detailA = SampleTriplanarNormal(
                    TEXTURE2D_ARGS(_FarionOceanWaveNormalA, sampler_FarionOceanWaveNormalA),
                    localOceanPosition,
                    baseNormal,
                    scale,
                    waveOffsetA);
                half3 detailB = SampleTriplanarNormal(
                    TEXTURE2D_ARGS(_FarionOceanWaveNormalB, sampler_FarionOceanWaveNormalB),
                    localOceanPosition,
                    baseNormal,
                    scale * 0.63,
                    waveOffsetB);
                half3 detailNormal = normalize(detailA + detailB - baseNormal);
                return normalize(lerp(baseNormal, detailNormal, strength));
            }

            half SchlickFresnel(half cosTheta, half indexOfRefraction)
            {
                half ratio = (indexOfRefraction - 1.0h) / (indexOfRefraction + 1.0h);
                half f0 = ratio * ratio;
                return f0 + (1.0h - f0) * pow(saturate(1.0h - cosTheta), 5.0h);
            }

            half3 SampleSkyReflection(float3 reflectDirection, float3 sphereNormal, half3 lighting)
            {
                half upness = saturate(dot(reflectDirection, sphereNormal));
                half3 sky = lerp(_FarionOceanHorizonColor.rgb, _FarionOceanFresnelColor.rgb, pow(upness, 0.6h));
                return sky * lighting;
            }

            bool TryBuildWaterSegment(
                float2 oceanHit,
                bool cameraInsideOcean,
                float sceneDistance,
                bool scenePointInsideOcean,
                out float segmentStart,
                out float segmentEnd,
                out float segmentLength,
                out bool entersFromAir,
                out bool exitsToAir)
            {
                float hitStart = oceanHit.x;
                float hitEnd = oceanHit.x + oceanHit.y;
                segmentStart = cameraInsideOcean ? 0.0 : hitStart;
                exitsToAir = cameraInsideOcean
                    && (!scenePointInsideOcean || sceneDistance >= hitEnd - 0.01);
                segmentEnd = exitsToAir ? hitEnd : min(hitEnd, sceneDistance);
                segmentLength = segmentEnd - segmentStart;
                entersFromAir = !cameraInsideOcean;
                return segmentLength > 0.0;
            }

            half3 ApplyWaterVolume(
                half3 sourceColor,
                half3 waterColor,
                half3 waterVolumeLight,
                float segmentLength,
                float3 extinctionCoefficients)
            {
                half3 transmittance = saturate(exp(-segmentLength * extinctionCoefficients));
                half3 transmittedScene = sourceColor * transmittance;
                half3 inScatteredWater = waterColor * waterVolumeLight * (1.0h - transmittance);
                return transmittedScene + inScatteredWater;
            }

            half3 EvaluateWaterVolumeLight(
                float3 worldPosition,
                float3 centre,
                float oceanRadius,
                float3 extinctionCoefficients,
                half3 ambient,
                half3 starRadiance,
                half3 starDirection,
                half scatterStrength)
            {
                float3 relativePosition = worldPosition - centre;
                float radialDistance = length(relativePosition);
                float3 radialNormal = relativePosition / max(radialDistance, 0.0001);
                float waterDepth = max(0.0, oceanRadius - radialDistance);
                half sunCosine = saturate(dot(radialNormal, starDirection));
                float sunPathLength = waterDepth / max((float)sunCosine, 0.08);
                half3 ambientTransmission = exp(-extinctionCoefficients * (waterDepth * 0.35));
                half3 sunTransmission = exp(-extinctionCoefficients * sunPathLength);
                return ambient * ambientTransmission
                    + starRadiance * sunTransmission * scatterStrength * sunCosine;
            }

            half3 ApplySegmentedWaterVolume(
                half3 sourceColor,
                half3 waterColor,
                half3 nearVolumeLight,
                half3 farVolumeLight,
                float segmentLength,
                float3 extinctionCoefficients)
            {
                float halfLength = segmentLength * 0.5;
                half3 farHalf = ApplyWaterVolume(
                    sourceColor,
                    waterColor,
                    farVolumeLight,
                    halfLength,
                    extinctionCoefficients);
                return ApplyWaterVolume(
                    farHalf,
                    waterColor,
                    nearVolumeLight,
                    halfLength,
                    extinctionCoefficients);
            }

            half3 EvaluateUnderwaterSpotLightsAt(
                float3 samplePosition,
                float viewDistance,
                float3 viewRay,
                float3 extinctionCoefficients)
            {
                half3 lightSum = 0.0h;
                [loop]
                for (int i = 0; i < _FarionUnderwaterLightCount; i++)
                {
                    float4 positionRange = _FarionUnderwaterLightPositionRange[i];
                    float3 lightToSample = samplePosition - positionRange.xyz;
                    float lightDistance = length(lightToSample);
                    float range = max(positionRange.w, 0.001);
                    if (lightDistance >= range)
                    {
                        continue;
                    }

                    float3 lightDirection = _FarionUnderwaterLightDirectionOuterCos[i].xyz;
                    float3 sampleDirection = lightToSample / max(lightDistance, 0.001);
                    half cone = smoothstep(
                        _FarionUnderwaterLightDirectionOuterCos[i].w,
                        max(
                            _FarionUnderwaterLightInnerConeCos[i],
                            _FarionUnderwaterLightDirectionOuterCos[i].w + 0.0001),
                        dot(lightDirection, sampleDirection));
                    float normalizedDistance = lightDistance / range;
                    half rangeAttenuation = saturate(1.0h - normalizedDistance * normalizedDistance);
                    rangeAttenuation = rangeAttenuation * rangeAttenuation
                        / (1.0h + normalizedDistance * normalizedDistance * 8.0h);
                    half phase = lerp(
                        0.28h,
                        1.0h,
                        pow(saturate(dot(lightDirection, viewRay)), 6.0h));
                    half3 transmission = exp(
                        -extinctionCoefficients * (lightDistance + viewDistance));
                    half4 colorStrength = _FarionUnderwaterLightColorStrength[i];
                    lightSum += colorStrength.rgb
                        * colorStrength.a
                        * cone
                        * rangeAttenuation
                        * phase
                        * transmission;
                }

                return lightSum;
            }

            half3 EvaluateUnderwaterSpotLightScattering(
                float3 rayOrigin,
                float3 rayDirection,
                float segmentLength,
                float3 extinctionCoefficients)
            {
                if (_FarionUnderwaterLightCount <= 0 || segmentLength <= 0.0)
                {
                    return 0.0h;
                }

                const int stepCount = 4;
                float integrationLength = min(
                    segmentLength,
                    _FarionOceanCameraSurfaceParams.y);
                float stepLength = integrationLength / stepCount;
                float minimumExtinction = max(
                    min(extinctionCoefficients.x, min(extinctionCoefficients.y, extinctionCoefficients.z)),
                    0.0001);
                half scatterFraction = 1.0h - exp(-minimumExtinction * stepLength);
                half3 scattering = 0.0h;
                [unroll]
                for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
                {
                    float sampleDistance = (stepIndex + 0.5) * stepLength;
                    scattering += EvaluateUnderwaterSpotLightsAt(
                        rayOrigin + rayDirection * sampleDistance,
                        sampleDistance,
                        rayDirection,
                        extinctionCoefficients) * scatterFraction;
                }

                return scattering;
            }

            bool TryProjectDirectionSample(
                float3 interfacePosition,
                float3 direction,
                float travelDistance,
                out float2 sampleUv,
                out half edgeFade)
            {
                float3 samplePosition = interfacePosition + direction * max(travelDistance, 1.0);
                float4 clipPosition = TransformWorldToHClip(samplePosition);
                if (clipPosition.w <= 0.0001)
                {
                    sampleUv = 0.0;
                    edgeFade = 0.0h;
                    return false;
                }

                float4 screenPosition = ComputeScreenPos(clipPosition);
                sampleUv = screenPosition.xy / screenPosition.w;
                float2 edgeDistance = min(sampleUv, 1.0 - sampleUv);
                edgeFade = smoothstep(0.0, 0.025, min(edgeDistance.x, edgeDistance.y));
                return edgeFade > 0.0h;
            }

            half3 SampleReflectedWater(
                float3 interfacePosition,
                float3 direction,
                float travelDistance,
                half3 fallbackColor)
            {
                float2 sampleUv;
                half edgeFade;
                if (!TryProjectDirectionSample(
                    interfacePosition,
                    direction,
                    travelDistance,
                    sampleUv,
                    edgeFade))
                {
                    return fallbackColor;
                }

                float sampleDepth = SampleSceneDepth(sampleUv);
                if (FarionIsSkyDepth(sampleDepth))
                {
                    return fallbackColor;
                }

                float3 samplePositionWS = ComputeWorldSpacePosition(
                    sampleUv,
                    sampleDepth,
                    UNITY_MATRIX_I_VP);
                if (!IsPointInsideOcean(samplePositionWS))
                {
                    return fallbackColor;
                }

                // ponytail: screen-space reflection cannot see off-screen geometry;
                // replace with a dedicated reflection buffer only if that gap is visible.
                half3 reflectedColor = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    sampleUv).rgb;
                return lerp(fallbackColor, reflectedColor, edgeFade);
            }

            float3 AtmosphereSunTransmittance(float3 worldPosition, float3 directionToStar)
            {
                return FarionAtmosphereSunTransmittance(
                    worldPosition,
                    _FarionOceanSphere.xyz,
                    _FarionOceanSphere.w,
                    _FarionOceanAtmosphereParams.x,
                    _FarionOceanAtmosphereParams.y,
                    _FarionOceanAtmosphereParams.z,
                    _FarionOceanAtmosphereRayleigh.rgb,
                    _FarionOceanAtmosphereOzone.rgb,
                    _FarionOceanAtmosphereParams.w,
                    directionToStar,
                    TEXTURE2D_ARGS(
                        _FarionOceanAtmosphereOpticalDepth,
                        sampler_FarionOceanAtmosphereOpticalDepth));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half3 sourceColor = source.rgb;

                float rawDepth = SampleSceneDepth(uv);
                bool sceneIsSky = FarionIsSkyDepth(rawDepth);
                float depthForPosition = sceneIsSky ? UNITY_RAW_FAR_CLIP_VALUE : rawDepth;
                float3 scenePositionWS = ComputeWorldSpacePosition(uv, depthForPosition, UNITY_MATRIX_I_VP);
                float3 rayOrigin = _WorldSpaceCameraPos.xyz;
                float3 rayDirection = normalize(scenePositionWS - rayOrigin);
                float sceneDistance = sceneIsSky
                    ? _ProjectionParams.z
                    : length(scenePositionWS - rayOrigin);

                float oceanRadius = _FarionOceanSphere.w;
                if (oceanRadius <= 0.0)
                {
                    return half4(sourceColor, source.a);
                }

                float3 centre = _FarionOceanSphere.xyz;
                float bodyRadius = max(_FarionPlanetSphere.w, 0.001);
                bool cameraInsideOcean = _FarionOceanCameraSurfaceParams.x < 0.0;
                bool scenePointInsideOcean = !sceneIsSky;
                if (cameraInsideOcean && scenePointInsideOcean)
                {
                    scenePointInsideOcean = IsPointInsideOcean(scenePositionWS);
                }

                FarionOceanSurfaceIntersection surfaceIntersection;
                if (!FarionTryIntersectOceanSurface(
                    OceanField(),
                    rayOrigin,
                    rayDirection,
                    cameraInsideOcean,
                    surfaceIntersection))
                {
                    return half4(sourceColor, source.a);
                }

                float surfaceDisplacementFade = surfaceIntersection.displacementFade;
                float2 oceanHit = float2(
                    surfaceIntersection.entryDistance,
                    surfaceIntersection.exitDistance - surfaceIntersection.entryDistance);

                float segmentStart;
                float segmentEnd;
                float segmentLength;
                bool entersFromAir;
                bool exitsToAir;
                if (!TryBuildWaterSegment(
                    oceanHit,
                    cameraInsideOcean,
                    sceneDistance,
                    scenePointInsideOcean,
                    segmentStart,
                    segmentEnd,
                    segmentLength,
                    entersFromAir,
                    exitsToAir))
                {
                    return half4(sourceColor, source.a);
                }

                half waterVisibility = 1.0h;
                if (!sceneIsSky && !cameraInsideOcean)
                {
                    float surfaceOcclusionWidth = max(fwidth(segmentStart), 0.001);
                    waterVisibility = smoothstep(0.0, surfaceOcclusionWidth, sceneDistance - segmentStart);
                    if (waterVisibility <= 0.001h)
                    {
                        return half4(sourceColor, source.a);
                    }
                }

                float surfaceDistance = entersFromAir ? segmentStart : segmentEnd;
                float3 hitPosition = rayOrigin + rayDirection * surfaceDistance;
                float3 relativeOceanPosition = hitPosition - centre;
                float3 sphereNormal = normalize(relativeOceanPosition);
                float3 localOceanPosition = mul(
                    (float3x3)_FarionOceanWorldToLocal,
                    relativeOceanPosition);

                half swellFade = saturate(surfaceDisplacementFade);
                half detailFade = saturate(
                    rcp(max(_FarionOceanWaveParams.x, 0.0001)) * 8.0 / max(surfaceDistance, 0.001));

                float3 swellGradient;
                float swellHeight;
                if (surfaceIntersection.wasRefined)
                {
                    swellHeight = surfaceIntersection.surfaceHeight;
                    swellGradient = surfaceIntersection.surfaceGradient;
                }
                else
                {
                    swellHeight = FarionSampleOceanSurfaceHeight(
                        OceanField(),
                        relativeOceanPosition,
                        swellGradient);
                }
                float3 swellNormal = swellFade > 0.001h
                    ? normalize(lerp(sphereNormal, FarionApplyWaveNormal(sphereNormal, swellGradient), swellFade))
                    : sphereNormal;
                bool hasSurfaceInterface = entersFromAir || exitsToAir;
                half3 localSwellNormal = mul((float3x3)_FarionOceanWorldToLocal, swellNormal);
                half3 localWaveNormal = localSwellNormal;
                if (hasSurfaceInterface)
                {
                    localWaveNormal = SampleDetailNormal(
                        localOceanPosition,
                        localSwellNormal,
                        detailFade);
                }

                half3 waveNormal = normalize(mul(
                    transpose((float3x3)_FarionOceanWorldToLocal),
                    localWaveNormal));

                float depthMultiplier = _FarionOceanDepthMultiplier;
                float referenceLightIntensity = max(_FarionOceanReferenceLightIntensity, 0.001);
                float3 extinctionCoefficients = max(_FarionOceanUnderwaterOptics.xyz, 0.0);
                half underwaterSpecularStrength = saturate(_FarionOceanUnderwaterOptics.w);
                half surfaceFade = max(swellFade, detailFade);
                half smoothness = saturate(_FarionOceanLightingParams.x) * lerp(0.1h, 1.0h, surfaceFade);
                half specularStrength = _FarionOceanLightingParams.y;
                half indexOfRefraction = max((half)_FarionOceanLightingParams.z, 1.0001h);
                half scatterStrength = max(_FarionOceanLightingParams.w, 0.0h);

                float seafloorDepth = bodyRadius;
                if (!sceneIsSky)
                {
                    float3 floorRelativePosition = scenePositionWS - centre;
                    seafloorDepth = max(
                        0.0,
                        oceanRadius + swellHeight * surfaceDisplacementFade - length(floorRelativePosition));
                }

                float minimumExtinction = max(
                    min(extinctionCoefficients.x, min(extinctionCoefficients.y, extinctionCoefficients.z)),
                    0.0001);
                half volumeDepthBlend = saturate(1.0h - exp(-segmentLength * minimumExtinction));
                half radialDepthBlend = saturate(1.0h - exp(-seafloorDepth / bodyRadius * depthMultiplier));
                half depth01 = max(radialDepthBlend, volumeDepthBlend);

                half3 starDirection = normalize(_FarionStarDirectionWS.xyz);
                half3 viewDirection = -rayDirection;
                half waveDiffuseLighting = saturate(dot(waveNormal, starDirection));
                half daylight = smoothstep(-0.15h, 0.15h, dot(sphereNormal, starDirection));

                half3 oceanColor = lerp(_FarionOceanShallowColor.rgb, _FarionOceanDeepColor.rgb, depth01);
                half3 ambient = max(_FarionAmbientColor.rgb, 0.0h);
                half3 starRadiance = _FarionStarColor.rgb
                    * AtmosphereSunTransmittance(hitPosition, starDirection)
                    * (max(_FarionStarIntensity, 0.0) / referenceLightIntensity);
                half3 halfDirection = normalize(starDirection + viewDirection);
                half specularPower = lerp(64.0h, 512.0h, smoothness);
                half normalDotHalf = saturate(dot(waveNormal, halfDirection));
                half specular = (pow(normalDotHalf, specularPower)
                    + pow(normalDotHalf, specularPower * 0.08h) * 0.12h) * specularStrength;
                specular *= cameraInsideOcean ? underwaterSpecularStrength : 1.0h;
                specular *= daylight * lerp(0.12h, 1.0h, surfaceFade);

                half3 surfaceReflectionLight = ambient + starRadiance * scatterStrength * (0.25h * daylight + waveDiffuseLighting * 0.76h);

                float3 interfaceNormal = entersFromAir ? waveNormal : -waveNormal;
                half interfaceCos = saturate(dot(-rayDirection, interfaceNormal));
                half interfaceFresnel = SchlickFresnel(interfaceCos, indexOfRefraction);
                half interfaceEta = entersFromAir ? rcp(indexOfRefraction) : indexOfRefraction;
                float3 refractedDirection = hasSurfaceInterface
                    ? refract(rayDirection, interfaceNormal, interfaceEta)
                    : rayDirection;
                float refractedLengthSquared = dot(refractedDirection, refractedDirection);
                half criticalReflection = 0.0h;
                if (exitsToAir)
                {
                    half transmittedSinSquared = interfaceEta * interfaceEta
                        * (1.0h - interfaceCos * interfaceCos);
                    criticalReflection = smoothstep(0.9h, 1.0h, transmittedSinSquared);
                }

                half surfaceReflection = hasSurfaceInterface
                    ? saturate(criticalReflection + (1.0h - criticalReflection) * interfaceFresnel)
                    : 0.0h;
                half surfaceTransmission = hasSurfaceInterface
                    ? (1.0h - surfaceReflection)
                    : 1.0h;
                half specularVisibility = hasSurfaceInterface
                    ? saturate(0.18h + interfaceFresnel * 2.5h)
                    : 0.0h;
                half3 surfaceGlint = _FarionOceanSpecularColor.rgb * starRadiance * specular * specularVisibility;

                float nearSampleDistance = segmentLength * 0.25;
                float farSampleDistance = segmentLength * 0.75;
                half3 nearVolumeLight = EvaluateWaterVolumeLight(
                    rayOrigin + rayDirection * nearSampleDistance,
                    centre,
                    oceanRadius,
                    extinctionCoefficients,
                    ambient,
                    starRadiance,
                    starDirection,
                    scatterStrength);
                half3 farVolumeLight = EvaluateWaterVolumeLight(
                    rayOrigin + rayDirection * farSampleDistance,
                    centre,
                    oceanRadius,
                    extinctionCoefficients,
                    ambient,
                    starRadiance,
                    starDirection,
                    scatterStrength);
                half3 volumeColor = lerp(oceanColor, _FarionOceanUnderwaterColor.rgb, volumeDepthBlend);

                float3 reflectedDirection = reflect(rayDirection, interfaceNormal);
                half3 reflectedFallback = volumeColor
                    * max(nearVolumeLight, surfaceReflectionLight * 0.2h);
                half3 reflectedSurface = cameraInsideOcean
                    ? (exitsToAir
                        ? SampleReflectedWater(
                            hitPosition,
                            normalize(reflectedDirection),
                            _FarionOceanCameraSurfaceParams.z,
                            reflectedFallback)
                        : reflectedFallback)
                    : SampleSkyReflection(reflectedDirection, sphereNormal, surfaceReflectionLight);

                half3 transmittedSurface = sourceColor;
                if (cameraInsideOcean && exitsToAir && refractedLengthSquared > 0.000001)
                {
                    if (sceneIsSky)
                    {
                        float3 normalDeltaVS = mul(
                            (float3x3)UNITY_MATRIX_V,
                            waveNormal - sphereNormal);
                        float2 distortion = clamp(normalDeltaVS.xy * 0.06, -0.035, 0.035);
                        float2 refractedUv = saturate(uv + distortion);
                        float refractedDepth = SampleSceneDepth(refractedUv);
                        transmittedSurface = FarionIsSkyDepth(refractedDepth)
                            ? SAMPLE_TEXTURE2D_X(
                                _BlitTexture,
                                sampler_LinearClamp,
                                refractedUv).rgb
                            : sourceColor;
                    }
                    else
                    {
                        transmittedSurface = SampleSkyReflection(
                            normalize(refractedDirection),
                            sphereNormal,
                            surfaceReflectionLight);
                    }
                }

                half3 waterColor;
                if (cameraInsideOcean)
                {
                    half3 interfaceColor = exitsToAir
                        ? transmittedSurface * surfaceTransmission
                            + reflectedSurface * surfaceReflection
                            + surfaceGlint
                        : sourceColor;
                    waterColor = ApplySegmentedWaterVolume(
                        interfaceColor,
                        volumeColor,
                        nearVolumeLight,
                        farVolumeLight,
                        segmentLength,
                        extinctionCoefficients);
                    waterColor += EvaluateUnderwaterSpotLightScattering(
                        rayOrigin,
                        rayDirection,
                        segmentLength,
                        extinctionCoefficients);
                }
                else
                {
                    half3 transmittedWater = ApplySegmentedWaterVolume(
                        sourceColor,
                        volumeColor,
                        nearVolumeLight,
                        farVolumeLight,
                        segmentLength,
                        extinctionCoefficients);
                    waterColor = transmittedWater * surfaceTransmission
                        + reflectedSurface * surfaceReflection
                        + surfaceGlint;
                }

                half foamStrength = saturate(_FarionOceanSwellShape.w);
                if (foamStrength > 0.0h && !cameraInsideOcean)
                {
                    half foamWidth = max(_FarionOceanSwellShape.z, 0.0001h);
                    half shoreFoam = 1.0h - saturate(seafloorDepth / foamWidth);
                    half waveContact = _FarionOceanSwellShape.x > 0.0
                        ? saturate(swellHeight / _FarionOceanSwellShape.x * 0.5h + 0.5h)
                        : 1.0h;
                    half foam = shoreFoam * shoreFoam
                        * lerp(0.65h, 1.0h, waveContact)
                        * foamStrength
                        * (1.0h - volumeDepthBlend);
                    half3 foamLight = ambient + starRadiance * (0.2h + waveDiffuseLighting * 0.8h);
                    waterColor = lerp(waterColor, _FarionOceanFoamColor.rgb * foamLight, foam);
                }

                return half4(lerp(source.rgb, waterColor, waterVisibility), source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
