Shader "Hidden/Farion/Celestial/Cloud Post Process"
{
    Properties
    {
        [NoScaleOffset] _FarionCloudShapeNoise("Shape Noise", 3D) = "white" {}
        [NoScaleOffset] _FarionCloudDetailNoise("Detail Noise", 3D) = "white" {}
        [NoScaleOffset] _FarionCloudBlueNoise("Blue Noise", 2D) = "white" {}
        [NoScaleOffset] _FarionCloudAtmosphereOpticalDepth("Atmosphere Optical Depth", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "FarionAtmosphereLighting.hlsl"
        #include "FarionCelestialRaycast.hlsl"

        #define FARION_CLOUD_MAX_VIEW_STEPS 96
        #define FARION_CLOUD_MAX_LIGHT_STEPS 12

        float4 _FarionCloudSphere;
        float4 _FarionCloudRadii;
        float4x4 _FarionCloudWorldToLocal;
        float4 _FarionCloudSeedOffset;
        float4 _FarionCloudShapeParams;
        float4 _FarionCloudStructureParams;
        float4 _FarionCloudShapeWeights;
        float4 _FarionCloudDetailWeights;
        float4 _FarionCloudAbsorptionParams;
        float4 _FarionCloudPhaseParams;
        float4 _FarionCloudLightingParams;
        float4 _FarionCloudAtmosphereParams;
        float4 _FarionCloudAtmosphereRayleigh;
        float4 _FarionCloudAtmosphereOzone;
        float4 _FarionCloudWindAxis;
        float4 _FarionCloudSamplingParams;
        float4 _FarionCloudMotion;
        float4 _FarionCloudWeatherMotion;
        float4 _FarionCloudLayerParams;
        float4 _FarionCloudTexture_TexelSize;

        float4 _FarionStarDirectionWS;
        half4 _FarionStarColor;
        float _FarionStarIntensity;
        half4 _FarionAmbientColor;
        half4 _FarionCloudAmbientColor;

        TEXTURE3D(_FarionCloudShapeNoise);
        SAMPLER(sampler_FarionCloudShapeNoise);
        TEXTURE3D(_FarionCloudDetailNoise);
        SAMPLER(sampler_FarionCloudDetailNoise);
        TEXTURE2D(_FarionCloudBlueNoise);
        SAMPLER(sampler_FarionCloudBlueNoise);
        TEXTURE2D(_FarionCloudAtmosphereOpticalDepth);
        SAMPLER(sampler_FarionCloudAtmosphereOpticalDepth);
        TEXTURE2D_X(_FarionCloudTexture);

        float3 GetWorldRay(float2 uv)
        {
        #if UNITY_REVERSED_Z
            const float farDepth = 0.0;
        #else
            const float farDepth = 1.0;
        #endif
            float3 farPosition = ComputeWorldSpacePosition(uv, farDepth, UNITY_MATRIX_I_VP);
            return normalize(farPosition - _WorldSpaceCameraPos);
        }

        float GetSceneDistance(float2 uv, float3 rayDirection)
        {
            float rawDepth = SampleSceneDepth(uv);
            if (FarionIsSkyDepth(rawDepth))
            {
                return FARION_MAX_FLOAT;
            }

            float3 scenePosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            return max(0.0, dot(scenePosition - _WorldSpaceCameraPos, rayDirection));
        }

        float3 RotateAroundAxis(float3 value, float3 axis, float sine, float cosine)
        {
            return value * cosine + cross(axis, value) * sine + axis * dot(axis, value) * (1.0 - cosine);
        }

        float SampleWorldNoise(float3 worldPosition)
        {
            float shellThickness = max(_FarionCloudRadii.z - _FarionCloudRadii.y, 0.0001);
            float3 localPosition = mul(
                (float3x3)_FarionCloudWorldToLocal,
                worldPosition - _FarionCloudSphere.xyz) / shellThickness;
            float3 blend = pow(abs(normalize(localPosition)), 4.0);
            blend /= max(dot(blend, 1.0), 0.0001);
            float3 coordinates = localPosition * 8.0 + _FarionCloudSeedOffset.xyz * 31.0;
            float x = SAMPLE_TEXTURE2D_LOD(
                _FarionCloudBlueNoise,
                sampler_FarionCloudBlueNoise,
                coordinates.yz,
                0).r;
            float y = SAMPLE_TEXTURE2D_LOD(
                _FarionCloudBlueNoise,
                sampler_FarionCloudBlueNoise,
                coordinates.xz,
                0).r;
            float z = SAMPLE_TEXTURE2D_LOD(
                _FarionCloudBlueNoise,
                sampler_FarionCloudBlueNoise,
                coordinates.xy,
                0).r;
            return dot(float3(x, y, z), blend);
        }

        float3 AtmosphereSunTransmittance(float3 worldPosition, float3 directionToStar)
        {
            return FarionAtmosphereSunTransmittance(
                worldPosition,
                _FarionCloudSphere.xyz,
                _FarionCloudRadii.x,
                _FarionCloudAtmosphereParams.x,
                _FarionCloudAtmosphereParams.y,
                _FarionCloudAtmosphereParams.z,
                _FarionCloudAtmosphereRayleigh.rgb,
                _FarionCloudAtmosphereOzone.rgb,
                _FarionCloudAtmosphereParams.w,
                directionToStar,
                TEXTURE2D_ARGS(
                    _FarionCloudAtmosphereOpticalDepth,
                    sampler_FarionCloudAtmosphereOpticalDepth));
        }

        float SampleDensity(float3 worldPosition)
        {
            float3 center = _FarionCloudSphere.xyz;
            float innerRadius = _FarionCloudRadii.y;
            float outerRadius = _FarionCloudRadii.z;
            float3 offset = worldPosition - center;
            float radius = length(offset);
            float height01 = saturate((radius - innerRadius) / max(outerRadius - innerRadius, 0.0001));

            float3 localDirection = mul(
                (float3x3)_FarionCloudWorldToLocal,
                offset / max(radius, 0.0001));
            float3 windAxis = _FarionCloudWindAxis.xyz;
            float3 weatherDirection = RotateAroundAxis(
                localDirection,
                windAxis,
                _FarionCloudWeatherMotion.x,
                _FarionCloudWeatherMotion.y);
            float3 weatherPosition = weatherDirection * _FarionCloudStructureParams.x
                + _FarionCloudSeedOffset.xyz;
            float4 weather = SAMPLE_TEXTURE3D_LOD(
                _FarionCloudShapeNoise,
                sampler_FarionCloudShapeNoise,
                weatherPosition,
                0);
            float coverage = saturate(_FarionCloudShapeParams.z);
            float weatherMask = saturate(
                (weather.r - (1.0 - coverage)) / max(coverage, 0.0001));
            if (weatherMask <= 0.0)
            {
                return 0.0;
            }

            float cloudType = saturate(weather.g);
            float heightVariation = _FarionCloudStructureParams.z;
            float localBottom = saturate(
                (weather.b - 0.5) * heightVariation * 0.2
                + (1.0 - cloudType) * heightVariation * 0.08);
            float localTop = saturate(
                1.0
                - (1.0 - cloudType) * heightVariation * 0.65
                + (weather.a - 0.5) * heightVariation * 0.15);
            localTop = max(localTop, localBottom + 0.12);
            float localHeight = saturate(
                (height01 - localBottom) / max(localTop - localBottom, 0.0001));
            float gradientBottom = lerp(
                _FarionCloudLayerParams.x * 0.75,
                _FarionCloudLayerParams.x * 1.2,
                cloudType);
            float gradientTop = lerp(
                _FarionCloudLayerParams.y * 0.82,
                min(0.96, _FarionCloudLayerParams.y * 1.18),
                cloudType);
            float heightGradient = smoothstep(0.0, gradientBottom, localHeight)
                * (1.0 - smoothstep(gradientTop, 1.0, localHeight));
            if (heightGradient <= 0.0)
            {
                return 0.0;
            }

            float3 warp = (weather.gba * 2.0 - 1.0) * _FarionCloudStructureParams.w;
            float verticalScale = _FarionCloudStructureParams.y;
            float3 shapeDirection = RotateAroundAxis(
                localDirection,
                windAxis,
                _FarionCloudMotion.x,
                _FarionCloudMotion.y);
            float3 detailDirection = RotateAroundAxis(
                localDirection,
                windAxis,
                _FarionCloudMotion.z,
                _FarionCloudMotion.w);
            float3 shapePosition = shapeDirection
                * (_FarionCloudShapeParams.x + localHeight * verticalScale)
                + warp
                + _FarionCloudSeedOffset.xyz;
            float3 detailPosition = detailDirection
                * (_FarionCloudShapeParams.y + localHeight * verticalScale * 1.8)
                + warp.zxy * 1.7
                + _FarionCloudSeedOffset.zxy;

            float4 shapeNoise = SAMPLE_TEXTURE3D_LOD(
                _FarionCloudShapeNoise,
                sampler_FarionCloudShapeNoise,
                shapePosition,
                0);
            float shapeFbm = dot(shapeNoise, _FarionCloudShapeWeights);
            float shapeThreshold = lerp(0.58, 0.34, weatherMask);
            float shapeDensity = saturate(
                (shapeFbm - shapeThreshold) / max(1.0 - shapeThreshold, 0.0001));
            float baseDensity = shapeDensity * heightGradient * weatherMask;
            if (baseDensity <= 0.0)
            {
                return 0.0;
            }

            float3 detailNoise = SAMPLE_TEXTURE3D_LOD(
                _FarionCloudDetailNoise,
                sampler_FarionCloudDetailNoise,
                detailPosition,
                0).rgb;
            float detailFbm = dot(detailNoise, _FarionCloudDetailWeights.xyz);
            float erosion = pow(saturate(1.0 - shapeDensity), 2.0)
                * (1.0 - detailFbm)
                * _FarionCloudAbsorptionParams.w
                * heightGradient
                * weatherMask
                * 0.35;
            return saturate(baseDensity - erosion) * _FarionCloudShapeParams.w;
        }

        float HenyeyGreenstein(float cosineAngle, float eccentricity)
        {
            float eccentricitySquared = eccentricity * eccentricity;
            return (1.0 - eccentricitySquared)
                / (12.5663706 * pow(max(1.0 + eccentricitySquared - 2.0 * eccentricity * cosineAngle, 0.001), 1.5));
        }

        float Phase(float cosineAngle, float eccentricityScale)
        {
            float forward = HenyeyGreenstein(
                cosineAngle,
                _FarionCloudPhaseParams.x * eccentricityScale);
            float backward = HenyeyGreenstein(
                cosineAngle,
                -_FarionCloudPhaseParams.y * eccentricityScale);
            return _FarionCloudPhaseParams.z
                + lerp(forward, backward, 0.5) * _FarionCloudPhaseParams.w;
        }

        float PlanetShadow(float3 position, float3 directionToStar)
        {
            float3 toSample = position - _FarionCloudSphere.xyz;
            float along = dot(toSample, directionToStar);
            if (along >= 0.0)
            {
                return 1.0;
            }

            float planetRadius = _FarionCloudRadii.x;
            float perpendicular = length(toSample - directionToStar * along);
            float penumbra = max(_FarionCloudRadii.z - _FarionCloudRadii.y, 0.0001) * 0.25;
            return smoothstep(planetRadius - penumbra, planetRadius + penumbra, perpendicular);
        }

        void AccumulateLightDensity(
            float3 rayOrigin,
            float3 rayDirection,
            float startDistance,
            float endDistance,
            int stepCount,
            float coneRotation,
            inout float totalDensity)
        {
            float segmentLength = endDistance - startDistance;
            if (segmentLength <= 0.0 || stepCount <= 0)
            {
                return;
            }

            float shellThickness = max(_FarionCloudRadii.z - _FarionCloudRadii.y, 0.0001);
            float stepSize = segmentLength / stepCount;
            float3 referenceAxis = abs(rayDirection.y) < 0.99
                ? float3(0.0, 1.0, 0.0)
                : float3(1.0, 0.0, 0.0);
            float3 tangent = normalize(cross(referenceAxis, rayDirection));
            float3 bitangent = cross(rayDirection, tangent);

            [loop]
            for (int i = 0; i < FARION_CLOUD_MAX_LIGHT_STEPS; i++)
            {
                if (i >= stepCount || totalDensity * _FarionCloudAbsorptionParams.y > 7.0)
                {
                    break;
                }

                float normalizedStep = stepSize / shellThickness;
                float sampleDistance = startDistance + (i + 0.5) * stepSize;
                float coneAngle = coneRotation + i * 2.39996323;
                float coneRadius = sampleDistance
                    * _FarionCloudLightingParams.y
                    * sqrt((i + 0.5) / stepCount);
                float sine;
                float cosine;
                sincos(coneAngle, sine, cosine);
                float3 samplePosition = rayOrigin
                    + rayDirection * sampleDistance
                    + (tangent * cosine + bitangent * sine) * coneRadius;
                totalDensity += SampleDensity(samplePosition) * normalizedStep;
            }
        }

        float CloudLightEnergy(
            float3 position,
            float3 directionToStar,
            float cosineAngle,
            float coneRotation)
        {
            float shadow = PlanetShadow(position, directionToStar);
            if (shadow <= 0.0)
            {
                return 0.0;
            }

            float2 outerHit = FarionRaySphere(_FarionCloudSphere.xyz, _FarionCloudRadii.z, position, directionToStar);
            int stepCount = max(1, (int)_FarionCloudSamplingParams.y);
            float outerStart = outerHit.x;
            float outerEnd = outerHit.x + outerHit.y;
            float firstStart = outerStart;
            float firstEnd = outerEnd;
            float secondStart = outerEnd;
            float secondEnd = outerEnd;
            float2 innerHit = FarionRaySphere(_FarionCloudSphere.xyz, _FarionCloudRadii.y, position, directionToStar);
            if (innerHit.y > 0.0)
            {
                float innerStart = innerHit.x;
                float innerEnd = innerHit.x + innerHit.y;
                firstEnd = min(outerEnd, innerStart);
                secondStart = max(outerStart, innerEnd);
            }

            float firstLength = max(0.0, firstEnd - firstStart);
            float secondLength = max(0.0, secondEnd - secondStart);
            int firstSteps = firstLength > 0.0 ? stepCount : 0;
            int secondSteps = 0;
            if (firstLength > 0.0 && secondLength > 0.0 && stepCount > 1)
            {
                firstSteps = clamp(
                    (int)round(stepCount * firstLength / (firstLength + secondLength)),
                    1,
                    stepCount - 1);
                secondSteps = stepCount - firstSteps;
            }
            else if (firstLength <= 0.0 && secondLength > 0.0)
            {
                secondSteps = stepCount;
            }

            float totalDensity = 0.0;
            AccumulateLightDensity(
                position,
                directionToStar,
                firstStart,
                firstEnd,
                firstSteps,
                coneRotation,
                totalDensity);
            AccumulateLightDensity(
                position,
                directionToStar,
                secondStart,
                secondEnd,
                secondSteps,
                coneRotation + 1.0471976,
                totalDensity);
            float darkness = _FarionCloudAbsorptionParams.z;
            float opticalDepth = totalDensity * _FarionCloudAbsorptionParams.y;
            float primaryTransmittance = (
                darkness + exp(-opticalDepth) * (1.0 - darkness)) * shadow;
            float secondaryTransmittance = (
                darkness + exp(-opticalDepth * 0.45) * (1.0 - darkness)) * sqrt(shadow);
            float tertiaryTransmittance = (
                darkness + exp(-opticalDepth * 0.2) * (1.0 - darkness)) * pow(shadow, 0.25);
            float primary = primaryTransmittance * Phase(cosineAngle, 1.0);
            float multiple = (
                primary
                + secondaryTransmittance * Phase(cosineAngle, 0.5) * 0.5
                + tertiaryTransmittance * Phase(cosineAngle, 0.25) * 0.25) / 1.75;
            return lerp(primary, multiple, _FarionCloudLightingParams.x);
        }

        void MarchCloudSegment(
            float3 rayOrigin,
            float3 rayDirection,
            float startDistance,
            float endDistance,
            int stepCount,
            float jitter,
            float3 directionToStar,
            float cosineAngle,
            inout float transmittance,
            inout float3 lightEnergy)
        {
            float marchDistance = endDistance - startDistance;
            if (marchDistance <= 0.0 || stepCount <= 0 || transmittance < 0.01)
            {
                return;
            }

            float stepSize = marchDistance / stepCount;
            float shellThickness = max(_FarionCloudRadii.z - _FarionCloudRadii.y, 0.0001);
            float distanceTravelled = stepSize * saturate(
                0.5 + (jitter - 0.5) * _FarionCloudSamplingParams.z);
            float cachedLight = 0.0;
            int cachedLightStep = -2;
            float3 atmosphereTransmittance = 1.0;
            bool hasAtmosphereTransmittance = false;

            [loop]
            for (int i = 0; i < FARION_CLOUD_MAX_VIEW_STEPS; i++)
            {
                if (i >= stepCount || distanceTravelled >= marchDistance)
                {
                    break;
                }

                float normalizedStep = stepSize / shellThickness;
                float sampleDistance = startDistance + distanceTravelled;
                float3 samplePosition = rayOrigin + rayDirection * sampleDistance;
                float density = SampleDensity(samplePosition);
                if (density > 0.0)
                {
                    if (i - cachedLightStep >= 2)
                    {
                        float coneRotation = frac(jitter + i * 0.61803398875) * 6.2831853;
                        cachedLight = CloudLightEnergy(
                            samplePosition,
                            directionToStar,
                            cosineAngle,
                            coneRotation);
                        cachedLightStep = i;
                    }

                    if (!hasAtmosphereTransmittance)
                    {
                        atmosphereTransmittance = AtmosphereSunTransmittance(samplePosition, directionToStar);
                        hasAtmosphereTransmittance = true;
                    }

                    float weight = density * normalizedStep * transmittance;
                    lightEnergy += weight
                        * cachedLight
                        * atmosphereTransmittance;
                    transmittance *= exp(
                        -density * normalizedStep * _FarionCloudAbsorptionParams.x);
                    if (transmittance < 0.01)
                    {
                        break;
                    }
                }

                distanceTravelled += stepSize;
            }
        }

        void RaymarchCloud(float2 uv, out half4 cloud)
        {
            cloud = half4(0.0, 0.0, 0.0, 1.0);

            float3 rayOrigin = _WorldSpaceCameraPos;
            float3 rayDirection = GetWorldRay(uv);
            float3 center = _FarionCloudSphere.xyz;
            float innerRadius = _FarionCloudRadii.y;
            float outerRadius = _FarionCloudRadii.z;
            float2 outerHit = FarionRaySphere(center, outerRadius, rayOrigin, rayDirection);
            if (outerHit.y <= 0.0)
            {
                return;
            }

            float outerStart = outerHit.x;
            float outerEnd = outerHit.x + outerHit.y;
            float2 innerHit = FarionRaySphere(center, innerRadius, rayOrigin, rayDirection);
            float visibilityLimit = GetSceneDistance(uv, rayDirection);
            float2 surfaceHit = FarionRaySphere(center, _FarionCloudRadii.x, rayOrigin, rayDirection);
            if (surfaceHit.y > 0.0 && surfaceHit.x > 0.0)
            {
                visibilityLimit = min(visibilityLimit, surfaceHit.x);
            }

            outerEnd = min(outerEnd, visibilityLimit);
            float firstStart = outerStart;
            float firstEnd = outerEnd;
            float secondStart = outerEnd;
            float secondEnd = outerEnd;
            if (innerHit.y > 0.0)
            {
                float innerStart = innerHit.x;
                float innerEnd = innerHit.x + innerHit.y;
                firstEnd = min(outerEnd, innerStart);
                secondStart = max(outerStart, innerEnd);
                secondEnd = outerEnd;
            }

            float firstLength = max(0.0, firstEnd - firstStart);
            float secondLength = max(0.0, secondEnd - secondStart);
            float totalLength = firstLength + secondLength;
            if (totalLength <= 0.0)
            {
                return;
            }

            float entryDistance = firstLength > 0.0 ? firstStart : secondStart;
            float blueNoise = SampleWorldNoise(rayOrigin + rayDirection * entryDistance);
            float3 directionToStar = normalize(_FarionStarDirectionWS.xyz);
            float cosineAngle = dot(rayDirection, directionToStar);
            float transmittance = 1.0;
            float3 lightEnergy = 0.0;
            int totalSteps = max(8, (int)_FarionCloudSamplingParams.x);
            int firstSteps = 0;
            int secondSteps = 0;

            if (firstLength > 0.0 && secondLength > 0.0)
            {
                firstSteps = clamp((int)round(totalSteps * firstLength / totalLength), 4, totalSteps - 4);
                secondSteps = totalSteps - firstSteps;
            }
            else if (firstLength > 0.0)
            {
                firstSteps = totalSteps;
            }
            else
            {
                secondSteps = totalSteps;
            }

            MarchCloudSegment(
                rayOrigin,
                rayDirection,
                firstStart,
                firstEnd,
                firstSteps,
                blueNoise,
                directionToStar,
                cosineAngle,
                transmittance,
                lightEnergy);
            MarchCloudSegment(
                rayOrigin,
                rayDirection,
                secondStart,
                secondEnd,
                secondSteps,
                frac(blueNoise + 0.37),
                directionToStar,
                cosineAngle,
                transmittance,
                lightEnergy);

            float3 directLight = _FarionStarColor.rgb * max(_FarionStarIntensity, 0.0);
            float3 ambientLight = max(_FarionAmbientColor.rgb, _FarionCloudAmbientColor.rgb);
            float3 cloudLight = lightEnergy * directLight + (1.0 - transmittance) * ambientLight;
            cloud = half4(cloudLight, transmittance);
        }

        float DepthWeight(float centerDepth, float sampleDepth)
        {
            bool centerSky = FarionIsSkyDepth(centerDepth);
            bool sampleSky = FarionIsSkyDepth(sampleDepth);
            if (centerSky || sampleSky)
            {
                return centerSky == sampleSky ? 1.0 : 0.001;
            }

            float centerEye = LinearEyeDepth(centerDepth, _ZBufferParams);
            float sampleEye = LinearEyeDepth(sampleDepth, _ZBufferParams);
            return exp(-abs(centerEye - sampleEye) / max(centerEye * 0.02, 0.01));
        }
        ENDHLSL

        Pass
        {
            Name "FarionCloudRaymarch"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentRaymarch

            half4 FragmentRaymarch(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 cloud;
                RaymarchCloud(input.texcoord, cloud);
                return cloud;
            }
            ENDHLSL
        }

        Pass
        {
            Name "FarionCloudComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentComposite

            half4 FragmentComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
                float4 cloud = SAMPLE_TEXTURE2D_X(_FarionCloudTexture, sampler_LinearClamp, uv);
                if (FarionIsSkyDepth(rawDepth))
                {
                    half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                    return half4(source.rgb * cloud.a + cloud.rgb, source.a);
                }

                float2 halfTexel = _FarionCloudTexture_TexelSize.xy * 0.5;
                const float2 offsets[4] =
                {
                    float2(-1.0, -1.0),
                    float2(1.0, -1.0),
                    float2(-1.0, 1.0),
                    float2(1.0, 1.0)
                };

                cloud = 0.0;
                float totalWeight = 0.0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 sampleUv = saturate(uv + offsets[i] * halfTexel);
                    float weight = DepthWeight(rawDepth, SampleSceneDepth(sampleUv));
                    cloud += SAMPLE_TEXTURE2D_X(_FarionCloudTexture, sampler_LinearClamp, sampleUv) * weight;
                    totalWeight += weight;
                }

                cloud /= max(totalWeight, 0.0001);
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                return half4(source.rgb * cloud.a + cloud.rgb, source.a);
            }
            ENDHLSL
        }
    }
}
