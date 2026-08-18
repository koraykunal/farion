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
            #include "FarionOceanWaves.hlsl"

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
            // x: depth multiplier, y: alpha multiplier, z: unused, w: reference light intensity.
            float4 _FarionOceanOpticalParams;
            // xyz: extinction per world unit, w: specular strength.
            float4 _FarionOceanUnderwaterOptics;
            // x: detail normal scale, y: detail speed, z: detail strength.
            float4 _FarionOceanWaveParams;
            // xyz: wrapped wave phases.
            float4 _FarionOceanSwellPhases;
            // x: amplitude, y: wave length, z: foam width, w: foam strength.
            float4 _FarionOceanSwellShape;
            float4x4 _FarionOceanWorldToLocal;
            float _FarionOceanCameraInside;
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

            float SampleSwellHeight(float3 relativePosition, out float3 gradient)
            {
                float3 localGradient;
                float height = FarionSampleWaveHeight(
                    mul((float3x3)_FarionOceanWorldToLocal, relativePosition),
                    _FarionOceanSwellShape.y,
                    _FarionOceanSwellShape.x,
                    _FarionOceanSwellPhases.xyz,
                    localGradient);
                gradient = mul(transpose((float3x3)_FarionOceanWorldToLocal), localGradient);
                return height;
            }

            // The analytic intersection is against the maximum swell envelope.
            // Newton refinement pulls the relevant entry or exit onto the displaced
            // surface, including when the camera starts underwater.
            float RefineSurfaceDistance(
                float3 rayOrigin,
                float3 rayDirection,
                float3 centre,
                float distance,
                float expectedSlopeSign,
                float displacementScale,
                out float surfaceHeight,
                out float3 surfaceGradient)
            {
                surfaceHeight = 0.0;
                surfaceGradient = 0.0;
                float amplitude = _FarionOceanSwellShape.x * displacementScale;
                if (amplitude <= 0.0)
                {
                    return distance;
                }

                float oceanRadius = _FarionOceanSphere.w;
                float maxStep = amplitude * 4.0;

                [loop]
                for (int i = 0; i < 3; i++)
                {
                    float3 relative = rayOrigin + rayDirection * distance - centre;
                    float radius = length(relative);
                    float3 normal = relative / max(radius, 0.0001);
                    float3 gradient;
                    float height = SampleSwellHeight(relative, gradient) * displacementScale;
                    gradient *= displacementScale;
                    float error = radius - (oceanRadius + height);
                    float slope = dot(rayDirection, normal) - dot(rayDirection, gradient);
                    if (abs(slope) < 0.02)
                    {
                        return -1.0;
                    }

                    distance -= clamp(error / slope, -maxStep, maxStep);
                }

                distance = max(distance, 0.0);

                float3 finalRelative = rayOrigin + rayDirection * distance - centre;
                float finalRadius = length(finalRelative);
                float3 finalGradient;
                float rawFinalHeight = SampleSwellHeight(finalRelative, finalGradient);
                float finalHeight = rawFinalHeight * displacementScale;
                float3 scaledFinalGradient = finalGradient * displacementScale;
                float finalError = finalRadius - (oceanRadius + finalHeight);
                float finalSlope = dot(rayDirection, finalRelative / max(finalRadius, 0.0001))
                    - dot(rayDirection, scaledFinalGradient);
                if (abs(finalError) > max(0.01, amplitude * 0.05)
                    || finalSlope * expectedSlopeSign <= 0.0)
                {
                    return -1.0;
                }

                surfaceHeight = rawFinalHeight;
                surfaceGradient = finalGradient;
                return distance;
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
                    scale,
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
                half3 waterVolumeLight,
                float segmentLength,
                float3 extinctionCoefficients)
            {
                half3 transmittance = saturate(exp(-segmentLength * extinctionCoefficients));
                half3 transmittedScene = sourceColor * transmittance;
                half3 inScatteredWater = waterColor * waterVolumeLight * (1.0h - transmittance);
                return transmittedScene + inScatteredWater;
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
                bool sceneIsSky = IsSkyDepth(rawDepth);
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
                bool cameraInsideOcean = _FarionOceanCameraInside > 0.5;

                float amplitude = _FarionOceanSwellShape.x;
                float2 meanOceanHit = RaySphere(centre, oceanRadius, rayOrigin, rayDirection);
                float2 swellEnvelopeHit = RaySphere(centre, oceanRadius + amplitude, rayOrigin, rayDirection);
                if (swellEnvelopeHit.x < 0.0)
                {
                    return half4(sourceColor, source.a);
                }

                float envelopeEnd = swellEnvelopeHit.x + swellEnvelopeHit.y;
                float meanSurfaceDistance = meanOceanHit.x >= 0.0
                    ? (cameraInsideOcean ? meanOceanHit.x + meanOceanHit.y : meanOceanHit.x)
                    : (cameraInsideOcean ? envelopeEnd : swellEnvelopeHit.x);
                float fadeStart = _FarionOceanSwellShape.y * 16.0;
                float fadeEnd = _FarionOceanSwellShape.y * 24.0;
                float surfaceDisplacementFade = 1.0 - smoothstep(
                    fadeStart,
                    max(fadeEnd, fadeStart + 0.001),
                    meanSurfaceDistance);
                float effectiveAmplitude = amplitude * surfaceDisplacementFade;
                float2 oceanHit = effectiveAmplitude > 0.0001
                    ? RaySphere(centre, oceanRadius + effectiveAmplitude, rayOrigin, rayDirection)
                    : meanOceanHit;
                if (oceanHit.x < 0.0)
                {
                    return half4(sourceColor, source.a);
                }

                float hitStart = oceanHit.x;
                float hitEnd = oceanHit.x + oceanHit.y;
                float refinedSwellHeight = 0.0;
                float3 refinedSwellGradient = 0.0;
                bool hasRefinedSwell = false;
                if (effectiveAmplitude > 0.0001)
                {
                    float refinedDistance;
                    float candidateSwellHeight;
                    float3 candidateSwellGradient;
                    if (cameraInsideOcean)
                    {
                        refinedDistance = RefineSurfaceDistance(
                            rayOrigin,
                            rayDirection,
                            centre,
                            hitEnd,
                            1.0,
                            surfaceDisplacementFade,
                            candidateSwellHeight,
                            candidateSwellGradient);
                        if (refinedDistance >= 0.0)
                        {
                            hitEnd = refinedDistance;
                            refinedSwellHeight = candidateSwellHeight;
                            refinedSwellGradient = candidateSwellGradient;
                            hasRefinedSwell = true;
                        }
                        else if (meanOceanHit.x >= 0.0)
                        {
                            hitEnd = meanOceanHit.x + meanOceanHit.y;
                        }
                    }
                    else
                    {
                        refinedDistance = RefineSurfaceDistance(
                            rayOrigin,
                            rayDirection,
                            centre,
                            hitStart,
                            -1.0,
                            surfaceDisplacementFade,
                            candidateSwellHeight,
                            candidateSwellGradient);
                        if (refinedDistance >= 0.0)
                        {
                            hitStart = refinedDistance;
                            refinedSwellHeight = candidateSwellHeight;
                            refinedSwellGradient = candidateSwellGradient;
                            hasRefinedSwell = true;
                        }
                        else if (meanOceanHit.x >= 0.0)
                        {
                            hitStart = meanOceanHit.x;
                        }
                    }

                    if (hitStart < 0.0 || hitEnd <= hitStart)
                    {
                        return half4(sourceColor, source.a);
                    }
                }

                oceanHit = float2(hitStart, hitEnd - hitStart);

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
                if (hasRefinedSwell)
                {
                    swellHeight = refinedSwellHeight;
                    swellGradient = refinedSwellGradient;
                }
                else
                {
                    swellHeight = SampleSwellHeight(relativeOceanPosition, swellGradient);
                }
                float3 swellNormal = swellFade > 0.001h
                    ? normalize(lerp(sphereNormal, FarionApplyWaveNormal(sphereNormal, swellGradient), swellFade))
                    : sphereNormal;
                half3 localSwellNormal = mul((float3x3)_FarionOceanWorldToLocal, swellNormal);
                half3 localWaveNormal = localSwellNormal;
                if (!cameraInsideOcean)
                {
                    localWaveNormal = SampleDetailNormal(
                        localOceanPosition,
                        localSwellNormal,
                        detailFade);
                }
                half3 waveNormal = normalize(mul(
                    transpose((float3x3)_FarionOceanWorldToLocal),
                    localWaveNormal));

                float depthMultiplier = _FarionOceanOpticalParams.x;
                float alphaMultiplier = _FarionOceanOpticalParams.y;
                float referenceLightIntensity = max(_FarionOceanOpticalParams.w, 0.001);
                float3 underwaterExtinction = max(_FarionOceanUnderwaterOptics.xyz, 0.0);
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

                float minimumUnderwaterExtinction = max(
                    min(underwaterExtinction.x, min(underwaterExtinction.y, underwaterExtinction.z)),
                    0.0001);
                float3 exteriorExtinction = alphaMultiplier / bodyRadius
                    * underwaterExtinction / minimumUnderwaterExtinction;
                float3 extinctionCoefficients = cameraInsideOcean
                    ? underwaterExtinction
                    : exteriorExtinction;
                half depth01 = saturate(1.0h - exp(-seafloorDepth / bodyRadius * depthMultiplier));

                half3 starDirection = normalize(_FarionStarDirectionWS.xyz);
                half3 viewDirection = -rayDirection;
                half diffuseLighting = saturate(dot(sphereNormal, starDirection));
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

                float volumeSampleDistance = segmentStart + segmentLength * 0.5;
                float3 volumeSamplePosition = rayOrigin + rayDirection * volumeSampleDistance;
                float volumeWaterDepth = max(0.0, oceanRadius - length(volumeSamplePosition - centre));
                half3 subsurfaceLightTransmission = exp(-underwaterExtinction * volumeWaterDepth);
                half3 submergedAmbient = ambient * subsurfaceLightTransmission;
                half3 submergedStarRadiance = starRadiance * subsurfaceLightTransmission;
                half3 waterVolumeLight = submergedAmbient
                    + submergedStarRadiance * scatterStrength * (0.22h * daylight + diffuseLighting);
                half3 surfaceReflectionLight = ambient + starRadiance * scatterStrength * (0.25h * daylight + waveDiffuseLighting * 0.76h);
                half3 volumeColor = cameraInsideOcean
                    ? _FarionOceanUnderwaterColor.rgb
                    : oceanColor;
                half3 volumeLight = cameraInsideOcean
                    ? submergedAmbient
                        + submergedStarRadiance * scatterStrength * (0.07h * daylight + diffuseLighting * 0.29h)
                    : waterVolumeLight;
                float3 reflectionNormal = cameraInsideOcean ? swellNormal : waveNormal;
                float3 reflectDirection = reflect(rayDirection, reflectionNormal);
                half3 reflectedSurface = cameraInsideOcean
                    ? volumeColor * volumeLight
                    : SampleSkyReflection(reflectDirection, sphereNormal, surfaceReflectionLight);

                bool hasSurfaceInterface = entersFromAir || exitsToAir;
                half interfaceCos = entersFromAir
                    ? saturate(dot(swellNormal, viewDirection))
                    : saturate(dot(swellNormal, rayDirection));
                half interfaceFresnel = SchlickFresnel(interfaceCos, indexOfRefraction);
                half totalInternalReflection = 0.0h;

                if (exitsToAir)
                {
                    half etaWaterToAir = indexOfRefraction;
                    half sin2Transmitted = etaWaterToAir * etaWaterToAir * (1.0h - interfaceCos * interfaceCos);
                    totalInternalReflection = step(1.0h, sin2Transmitted);
                }

                half surfaceReflection = hasSurfaceInterface
                    ? saturate(totalInternalReflection + (1.0h - totalInternalReflection) * interfaceFresnel)
                    : 0.0h;
                half surfaceTransmission = hasSurfaceInterface
                    ? (1.0h - surfaceReflection)
                    : 1.0h;
                half specularVisibility = hasSurfaceInterface
                    ? saturate(0.18h + interfaceFresnel * 2.5h)
                    : 0.0h;
                half3 surfaceGlint = _FarionOceanSpecularColor.rgb * starRadiance * specular * specularVisibility;

                half3 waterColor;
                if (cameraInsideOcean)
                {
                    half3 interfaceColor = exitsToAir
                        ? sourceColor * surfaceTransmission
                            + reflectedSurface * surfaceReflection
                            + surfaceGlint
                        : sourceColor;
                    waterColor = ApplyWaterVolume(
                        interfaceColor,
                        volumeColor,
                        volumeLight,
                        segmentLength,
                        extinctionCoefficients);
                }
                else
                {
                    half3 transmittedWater = ApplyWaterVolume(
                        sourceColor,
                        volumeColor,
                        volumeLight,
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
                    half foam = shoreFoam * shoreFoam * lerp(0.65h, 1.0h, waveContact) * foamStrength;
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
