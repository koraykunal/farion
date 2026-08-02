Shader "Hidden/Farion/Celestial/Cloud Post Process"
{
    Properties
    {
        [NoScaleOffset] _FarionCloudShapeNoise("Shape Noise", 3D) = "white" {}
        [NoScaleOffset] _FarionCloudDetailNoise("Detail Noise", 3D) = "white" {}
        [NoScaleOffset] _FarionCloudBlueNoise("Blue Noise", 2D) = "white" {}
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

        #define FARION_CLOUD_MAX_VIEW_STEPS 96
        #define FARION_CLOUD_MAX_LIGHT_STEPS 12
        #define FARION_MAX_FLOAT 3.402823466e+38

        float4 _FarionCloudSphere;
        float4 _FarionCloudRadii;
        float4x4 _FarionCloudWorldToLocal;
        float4 _FarionCloudSeedOffset;
        float4 _FarionCloudShapeParams;
        float4 _FarionCloudShapeWeights;
        float4 _FarionCloudDetailWeights;
        float4 _FarionCloudAbsorptionParams;
        float4 _FarionCloudPhaseParams;
        float4 _FarionCloudWindAxis;
        float4 _FarionCloudSamplingParams;
        float4 _FarionCloudTexture_TexelSize;

        float4 _FarionStarDirectionWS;
        half4 _FarionStarColor;
        float _FarionStarIntensity;
        half4 _FarionAmbientColor;

        TEXTURE3D(_FarionCloudShapeNoise);
        SAMPLER(sampler_FarionCloudShapeNoise);
        TEXTURE3D(_FarionCloudDetailNoise);
        SAMPLER(sampler_FarionCloudDetailNoise);
        TEXTURE2D(_FarionCloudBlueNoise);
        SAMPLER(sampler_FarionCloudBlueNoise);
        TEXTURE2D_X(_FarionCloudTexture);

        float2 RaySphere(float3 center, float radius, float3 rayOrigin, float3 rayDirection)
        {
            float3 offset = rayOrigin - center;
            float b = dot(offset, rayDirection);
            float c = dot(offset, offset) - radius * radius;
            float discriminant = b * b - c;
            if (discriminant < 0.0)
            {
                return float2(FARION_MAX_FLOAT, 0.0);
            }

            float root = sqrt(discriminant);
            float nearDistance = max(-b - root, 0.0);
            float farDistance = -b + root;
            return farDistance > 0.0
                ? float2(nearDistance, farDistance - nearDistance)
                : float2(FARION_MAX_FLOAT, 0.0);
        }

        bool IsSkyDepth(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return rawDepth <= 0.000001;
        #else
            return rawDepth >= 0.999999;
        #endif
        }

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
            if (IsSkyDepth(rawDepth))
            {
                return FARION_MAX_FLOAT;
            }

            float3 scenePosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            return max(0.0, dot(scenePosition - _WorldSpaceCameraPos, rayDirection));
        }

        float3 RotateAroundAxis(float3 value, float3 axis, float angle)
        {
            float sine;
            float cosine;
            sincos(angle, sine, cosine);
            return value * cosine + cross(axis, value) * sine + axis * dot(axis, value) * (1.0 - cosine);
        }

        float SampleDensity(float3 worldPosition)
        {
            float3 center = _FarionCloudSphere.xyz;
            float innerRadius = _FarionCloudRadii.y;
            float outerRadius = _FarionCloudRadii.z;
            float radius = length(worldPosition - center);
            float height01 = saturate((radius - innerRadius) / max(outerRadius - innerRadius, 0.0001));
            float heightGradient = smoothstep(0.0, 0.18, height01) * (1.0 - smoothstep(0.72, 1.0, height01));
            if (heightGradient <= 0.0)
            {
                return 0.0;
            }

            float3 localPosition = mul((float3x3)_FarionCloudWorldToLocal, worldPosition - center) / outerRadius;
            float3 windAxis = normalize(_FarionCloudWindAxis.xyz);
            float shapeAngle = radians(_FarionCloudWindAxis.w * _Time.y);
            float detailAngle = radians(_FarionCloudSamplingParams.z * _Time.y);
            float3 shapePosition = RotateAroundAxis(localPosition, windAxis, shapeAngle)
                * _FarionCloudShapeParams.x + _FarionCloudSeedOffset.xyz;
            float3 detailPosition = RotateAroundAxis(localPosition, windAxis, detailAngle)
                * _FarionCloudShapeParams.y + _FarionCloudSeedOffset.zxy;

            float4 shapeNoise = SAMPLE_TEXTURE3D_LOD(
                _FarionCloudShapeNoise,
                sampler_FarionCloudShapeNoise,
                shapePosition,
                0);
            float4 shapeWeights = max(_FarionCloudShapeWeights, 0.0);
            if (dot(shapeWeights, 1.0) <= 0.0001)
            {
                shapeWeights = float4(1.0, 0.0, 0.0, 0.0);
            }

            float shapeWeight = dot(shapeWeights, 1.0);
            float shapeFbm = dot(shapeNoise, shapeWeights / shapeWeight);
            float baseDensity = shapeFbm * heightGradient - (1.0 - _FarionCloudShapeParams.z);
            if (baseDensity <= 0.0)
            {
                return 0.0;
            }

            float3 detailNoise = SAMPLE_TEXTURE3D_LOD(
                _FarionCloudDetailNoise,
                sampler_FarionCloudDetailNoise,
                detailPosition,
                0).rgb;
            float3 detailWeights = max(_FarionCloudDetailWeights.xyz, 0.0);
            if (dot(detailWeights, 1.0) <= 0.0001)
            {
                detailWeights = float3(1.0, 0.0, 0.0);
            }

            float detailWeight = dot(detailWeights, 1.0);
            float detailFbm = dot(detailNoise, detailWeights / detailWeight);
            float erosion = pow(saturate(1.0 - shapeFbm), 3.0)
                * (1.0 - detailFbm)
                * _FarionCloudAbsorptionParams.w;
            return saturate(baseDensity - erosion) * _FarionCloudShapeParams.w;
        }

        float HenyeyGreenstein(float cosineAngle, float eccentricity)
        {
            float eccentricitySquared = eccentricity * eccentricity;
            return (1.0 - eccentricitySquared)
                / (12.5663706 * pow(max(1.0 + eccentricitySquared - 2.0 * eccentricity * cosineAngle, 0.001), 1.5));
        }

        float Phase(float cosineAngle)
        {
            float forward = HenyeyGreenstein(cosineAngle, _FarionCloudPhaseParams.x);
            float backward = HenyeyGreenstein(cosineAngle, -_FarionCloudPhaseParams.y);
            return _FarionCloudPhaseParams.z
                + lerp(forward, backward, 0.5) * _FarionCloudPhaseParams.w;
        }

        float LightTransmittance(float3 position, float3 directionToStar)
        {
            float2 planetHit = RaySphere(
                _FarionCloudSphere.xyz,
                _FarionCloudRadii.x,
                position + directionToStar * 0.001,
                directionToStar);
            if (planetHit.y > 0.0)
            {
                return _FarionCloudAbsorptionParams.z;
            }

            float2 outerHit = RaySphere(_FarionCloudSphere.xyz, _FarionCloudRadii.z, position, directionToStar);
            int stepCount = max(1, (int)_FarionCloudSamplingParams.y);
            float stepSize = outerHit.y / stepCount;
            float normalizedStep = stepSize / max(_FarionCloudRadii.z - _FarionCloudRadii.y, 0.0001);
            float totalDensity = 0.0;
            float3 samplePosition = position + directionToStar * (stepSize * 0.5);

            [loop]
            for (int i = 0; i < FARION_CLOUD_MAX_LIGHT_STEPS; i++)
            {
                if (i >= stepCount)
                {
                    break;
                }

                totalDensity += SampleDensity(samplePosition) * normalizedStep;
                samplePosition += directionToStar * stepSize;
            }

            float transmittance = exp(-totalDensity * _FarionCloudAbsorptionParams.y);
            return _FarionCloudAbsorptionParams.z
                + transmittance * (1.0 - _FarionCloudAbsorptionParams.z);
        }

        void MarchCloudSegment(
            float3 rayOrigin,
            float3 rayDirection,
            float startDistance,
            float endDistance,
            int stepCount,
            float jitter,
            float3 directionToStar,
            float phase,
            inout float transmittance,
            inout float3 lightEnergy)
        {
            float marchDistance = endDistance - startDistance;
            if (marchDistance <= 0.0 || stepCount <= 0 || transmittance < 0.01)
            {
                return;
            }

            float stepSize = marchDistance / stepCount;
            float normalizedStep = stepSize / max(_FarionCloudRadii.z - _FarionCloudRadii.y, 0.0001);
            float distanceTravelled = stepSize * saturate(0.5 + (jitter - 0.5) * _FarionCloudSamplingParams.w);

            [loop]
            for (int i = 0; i < FARION_CLOUD_MAX_VIEW_STEPS; i++)
            {
                if (i >= stepCount || distanceTravelled >= marchDistance)
                {
                    break;
                }

                float3 samplePosition = rayOrigin + rayDirection * (startDistance + distanceTravelled);
                float density = SampleDensity(samplePosition);
                if (density > 0.0)
                {
                    float light = LightTransmittance(samplePosition, directionToStar);
                    lightEnergy += density * normalizedStep * transmittance * light * phase;
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

        half4 RaymarchCloud(float2 uv)
        {
            float3 rayOrigin = _WorldSpaceCameraPos;
            float3 rayDirection = GetWorldRay(uv);
            float3 center = _FarionCloudSphere.xyz;
            float innerRadius = _FarionCloudRadii.y;
            float outerRadius = _FarionCloudRadii.z;
            float2 outerHit = RaySphere(center, outerRadius, rayOrigin, rayDirection);
            if (outerHit.y <= 0.0)
            {
                return half4(0.0, 0.0, 0.0, 1.0);
            }

            float outerStart = outerHit.x;
            float outerEnd = outerHit.x + outerHit.y;
            float2 innerHit = RaySphere(center, innerRadius, rayOrigin, rayDirection);
            float visibilityLimit = GetSceneDistance(uv, rayDirection);
            float2 surfaceHit = RaySphere(center, _FarionCloudRadii.x, rayOrigin, rayDirection);
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
                return half4(0.0, 0.0, 0.0, 1.0);
            }

            float blueNoise = SAMPLE_TEXTURE2D_LOD(
                _FarionCloudBlueNoise,
                sampler_FarionCloudBlueNoise,
                uv * _ScreenParams.xy / 256.0,
                0).r;
            float3 directionToStar = normalize(_FarionStarDirectionWS.xyz);
            float phase = Phase(dot(rayDirection, directionToStar));
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
                phase,
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
                phase,
                transmittance,
                lightEnergy);

            float3 directLight = _FarionStarColor.rgb * max(_FarionStarIntensity, 0.0);
            float3 cloudLight = lightEnergy * directLight + (1.0 - transmittance) * _FarionAmbientColor.rgb;
            return half4(cloudLight, transmittance);
        }

        float DepthWeight(float centerDepth, float sampleDepth)
        {
            bool centerSky = IsSkyDepth(centerDepth);
            bool sampleSky = IsSkyDepth(sampleDepth);
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
                return RaymarchCloud(input.texcoord);
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
                float2 halfTexel = _FarionCloudTexture_TexelSize.xy * 0.5;
                float rawDepth = SampleSceneDepth(uv);
                const float2 offsets[4] =
                {
                    float2(-1.0, -1.0),
                    float2(1.0, -1.0),
                    float2(-1.0, 1.0),
                    float2(1.0, 1.0)
                };

                float4 cloud = 0.0;
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
