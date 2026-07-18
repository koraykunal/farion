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
            float4 _FarionOceanOpticalParams[FARION_MAX_OCEAN_EFFECTS];
            float4 _FarionOceanWaveParams[FARION_MAX_OCEAN_EFFECTS];
            float4 _FarionOceanLightingParams[FARION_MAX_OCEAN_EFFECTS];

            float4 _FarionStarPositionWS;
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

                float2 waveOffsetA = float2(_Time.x * speed, _Time.x * speed * 0.8);
                float2 waveOffsetB = float2(_Time.x * speed * -0.8, _Time.x * speed * -0.3);

                half3 waveNormal = SampleTriplanarNormal(
                    TEXTURE2D_ARGS(_FarionOceanWaveNormalA, sampler_FarionOceanWaveNormalA),
                    localOceanPosition,
                    sphereNormal,
                    scale,
                    waveOffsetA);
                waveNormal = SampleTriplanarNormal(
                    TEXTURE2D_ARGS(_FarionOceanWaveNormalB, sampler_FarionOceanWaveNormalB),
                    localOceanPosition,
                    waveNormal,
                    scale,
                    waveOffsetB);

                return normalize(lerp(sphereNormal, waveNormal, strength));
            }

            half3 ApplyOcean(
                half3 sourceColor,
                int index,
                float3 rayOrigin,
                float3 rayDirection,
                float sceneDistance)
            {
                float oceanRadius = _FarionOceanSpheres[index].w;
                if (oceanRadius <= 0.0)
                {
                    return sourceColor;
                }

                float3 centre = _FarionOceanSpheres[index].xyz;
                float bodyRadius = max(_FarionPlanetSpheres[index].w, 0.001);
                float2 oceanHit = RaySphere(centre, oceanRadius, rayOrigin, rayDirection);
                if (oceanHit.x < 0.0)
                {
                    return sourceColor;
                }

                float oceanViewDepth = min(oceanHit.y, sceneDistance - oceanHit.x);
                if (oceanViewDepth <= 0.0)
                {
                    return sourceColor;
                }

                float3 hitPosition = rayOrigin + rayDirection * oceanHit.x;
                float3 localOceanPosition = hitPosition - centre;
                float3 sphereNormal = normalize(localOceanPosition);
                half3 waveNormal = SampleWaveNormal(index, localOceanPosition, sphereNormal, bodyRadius);

                float depthMultiplier = _FarionOceanOpticalParams[index].x;
                float alphaMultiplier = _FarionOceanOpticalParams[index].y;
                half smoothness = saturate(_FarionOceanLightingParams[index].x);
                half specularStrength = _FarionOceanLightingParams[index].y;
                half fresnelPower = max(_FarionOceanLightingParams[index].z, 0.5h);
                half fresnelStrength = saturate(_FarionOceanLightingParams[index].w);

                half depth01 = saturate(1.0h - exp(-oceanViewDepth / bodyRadius * depthMultiplier));
                half alpha = saturate(1.0h - exp(-oceanViewDepth / bodyRadius * alphaMultiplier));

                half3 starDirection = normalize(_FarionStarPositionWS.xyz - hitPosition);
                half3 viewDirection = -rayDirection;
                half diffuseLighting = saturate(dot(sphereNormal, starDirection));
                half fresnel = pow(saturate(1.0h - dot(sphereNormal, viewDirection)), fresnelPower);

                half3 oceanColor = lerp(_FarionOceanShallowColors[index].rgb, _FarionOceanDeepColors[index].rgb, depth01);
                oceanColor = lerp(oceanColor, _FarionOceanFresnelColors[index].rgb, fresnel * fresnelStrength);

                half3 ambient = max(_FarionAmbientColor.rgb, 0.025h);
                half3 starRadiance = _FarionStarColor.rgb * max(_FarionStarIntensity, 0.0);
                half3 halfDirection = normalize(starDirection + viewDirection);
                half specularPower = lerp(64.0h, 512.0h, smoothness);
                half specular = pow(saturate(dot(waveNormal, halfDirection)), specularPower) * specularStrength;
                half3 litOcean = oceanColor * (ambient + starRadiance * diffuseLighting * 0.62h)
                    + _FarionOceanSpecularColors[index].rgb * starRadiance * specular;

                return lerp(sourceColor, litOcean, alpha);
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
                for (int i = 0; i < _FarionOceanEffectCount; i++)
                {
                    color = ApplyOcean(color, i, rayOrigin, rayDirection, sceneDistance);
                }

                return half4(color, source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
