Shader "Hidden/Farion/Space/Volumetric Nebula"
{
    Properties
    {
        [NoScaleOffset] _FarionNebulaNoise("Noise", 2D) = "gray" {}
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
            Name "FarionVolumetricNebula"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define FARION_NEBULA_MAX_STEPS 192
            #define FARION_SPIRAL_NOISE_ITERATIONS 8
            #define FARION_MAX_FLOAT 3.402823466e+38

            float4 _FarionNebulaSphere;
            half4 _FarionNebulaDeepColor;
            half4 _FarionNebulaMidColor;
            half4 _FarionNebulaHighlightColor;
            float4 _FarionNebulaShape;
            float4 _FarionNebulaDetail;
            float4 _FarionNebulaMotion;
            float4x4 _FarionNebulaWorldToLocalRotation;

            TEXTURE2D(_FarionNebulaNoise);
            SAMPLER(sampler_FarionNebulaNoise);
            float4 _FarionNebulaNoise_TexelSize;

            float Hash13(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 758.5))) * 43758.54);
            }

            float Noise3D(float3 x)
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f *= f * (3.0 - f - f);

                float2 texelSize = max(abs(_FarionNebulaNoise_TexelSize.xy), float2(1.0 / 256.0, 1.0 / 256.0));
                float2 uv = p.xy + float2(37.0, 17.0) * p.z + f.xy + 0.5;
                float a = SAMPLE_TEXTURE2D_LOD(
                    _FarionNebulaNoise,
                    sampler_FarionNebulaNoise,
                    frac(uv * texelSize),
                    0).r;
                float b = SAMPLE_TEXTURE2D_LOD(
                    _FarionNebulaNoise,
                    sampler_FarionNebulaNoise,
                    frac((uv + float2(19.0, 73.0)) * texelSize),
                    0).r;
                return 2.4 * lerp(a, b, f.z) - 1.0;
            }

            float PaletteNoise(float3 position)
            {
                float3 seedOffset = _FarionNebulaDetail.z * float3(0.017, 0.029, 0.043);
                float3 p = position + seedOffset;
                float noise = Noise3D(p * 0.12);
                noise += Noise3D(p * 0.27 + 23.1) * 0.5;
                noise += Noise3D(p * 0.61 - 47.3) * 0.25;
                float phase = dot(p, float3(0.52, -0.37, 0.43)) + noise * 1.5;
                return sin(phase) * 0.5 + 0.5;
            }

            // Spiral-wave density from the supplied Shadertoy reference.
            float SpiralNoise(float3 p)
            {
                const float nudge = 20.0;
                const float normalizer = 0.0499376169;
                float frequency = 2.0;
                float noise = 1.5;

                [unroll]
                for (int i = 0; i < FARION_SPIRAL_NOISE_ITERATIONS; i++)
                {
                    noise -= abs(sin(p.y * frequency) + cos(p.x * frequency)) / frequency;
                    p.xy += float2(p.y, -p.x) * nudge;
                    p.xy *= normalizer;
                    p.xz += float2(p.z, -p.x) * nudge;
                    p.xz *= normalizer;
                    frequency *= 1.133733;
                }

                return noise;
            }

            float NebulaMap(float3 position)
            {
                float spiral = SpiralNoise(position.zxy * 0.4132 + _FarionNebulaDetail.z);
                float detail = Noise3D(position * 8.5) * 0.12;
                return 0.9 * (0.5 + spiral * 3.0 + detail);
            }

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

                float root = sqrt(discriminant);
                float entry = max(-b - root, 0.0);
                float exit = -b + root;
                if (exit < 0.0)
                {
                    return float2(FARION_MAX_FLOAT, 0.0);
                }

                return float2(entry, exit - entry);
            }

            bool IsSkyDepth(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth <= 0.000001;
            #else
                return rawDepth >= 0.999999;
            #endif
            }

            float4 MarchNebula(float3 rayOrigin, float3 rayDirection, float travelDistance, float2 screenUV)
            {
                float structureScale = _FarionNebulaShape.x;
                float densityMultiplier = _FarionNebulaShape.y;
                float edgeFade = _FarionNebulaShape.z;
                float brightness = _FarionNebulaShape.w;
                float extinction = _FarionNebulaDetail.x;

                float3 drift = _FarionNebulaMotion.xyz * (_Time.y * _FarionNebulaMotion.w);
                float3 structureOrigin = rayOrigin * structureScale + drift;
                float structureDistance = travelDistance * structureScale;
                int stepBudget = clamp((int)_FarionNebulaDetail.y, 16, FARION_NEBULA_MAX_STEPS);
                int requiredSteps = max(16, (int)ceil(structureDistance / 0.08));
                int stepCount = min(requiredSteps, stepBudget);
                float stepSize = structureDistance / max(stepCount, 1);
                float jitter = Hash13(float3(screenUV * _ScreenParams.xy, _FarionNebulaDetail.z)) - 0.5;
                float distanceAlongRay = (0.5 + jitter * 0.8) * stepSize;

                float3 accumulatedLight = 0.0;
                float transmittance = 1.0;
                float emissionTransmittance = 1.0;

                [loop]
                for (int i = 0; i < FARION_NEBULA_MAX_STEPS; i++)
                {
                    if (i >= stepCount || distanceAlongRay >= structureDistance || transmittance <= 0.01)
                    {
                        break;
                    }

                    float3 position = structureOrigin + rayDirection * distanceAlongRay;
                    float3 volumePosition = (position - drift) / structureScale;
                    float edge = smoothstep(0.0, edgeFade, 1.0 - length(volumePosition));

                    float distanceToStructure = abs(NebulaMap(position));
                    float localDensity = 1.0 - smoothstep(0.03, 0.22, distanceToStructure);
                    localDensity *= densityMultiplier * edge;
                    float densityBand = saturate(localDensity / max(densityMultiplier, 0.001));
                    half3 bodyColor = lerp(
                        _FarionNebulaDeepColor.rgb,
                        _FarionNebulaMidColor.rgb,
                        smoothstep(0.06, 0.62, densityBand));
                    float highlightMask = smoothstep(0.48, 0.72, PaletteNoise(position));
                    highlightMask *= lerp(0.65, 1.0, densityBand);
                    half3 lightColor = lerp(
                        bodyColor,
                        _FarionNebulaHighlightColor.rgb,
                        highlightMask);
                    lightColor += _FarionNebulaHighlightColor.rgb * highlightMask * 0.35;

                    float scatteringOpacity = 1.0 - exp(-localDensity * stepSize * 1.6);
                    float absorptionOpacity = 1.0 - exp(-localDensity * stepSize * extinction * 1.6);
                    float emission = scatteringOpacity * brightness * (0.35 + densityBand * 0.65);
                    accumulatedLight += emissionTransmittance * lightColor * emission;
                    emissionTransmittance *= 1.0 - scatteringOpacity;
                    transmittance *= 1.0 - absorptionOpacity;
                    distanceAlongRay += stepSize;
                }

                return float4(accumulatedLight, 1.0 - transmittance);
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
                float3 rayOriginWS = _WorldSpaceCameraPos.xyz;
                float3 rayDirectionWS = normalize(scenePositionWS - rayOriginWS);
                float sceneDistance = depthIsSky
                    ? FARION_MAX_FLOAT
                    : length(scenePositionWS - rayOriginWS);

                float2 volumeHit = RaySphere(
                    _FarionNebulaSphere.xyz,
                    _FarionNebulaSphere.w,
                    rayOriginWS,
                    rayDirectionWS);
                float travelDistance = min(volumeHit.y, sceneDistance - volumeHit.x);
                if (travelDistance <= 0.0)
                {
                    return source;
                }

                float3 entryWS = rayOriginWS + rayDirectionWS * volumeHit.x;
                float3 localOrigin = mul(
                    (float3x3)_FarionNebulaWorldToLocalRotation,
                    entryWS - _FarionNebulaSphere.xyz) / _FarionNebulaSphere.w;
                float3 localDirection = normalize(mul(
                    (float3x3)_FarionNebulaWorldToLocalRotation,
                    rayDirectionWS));
                float4 nebula = MarchNebula(
                    localOrigin,
                    localDirection,
                    travelDistance / _FarionNebulaSphere.w,
                    uv);

                half3 color = source.rgb * (1.0 - nebula.a) + nebula.rgb;
                return half4(color, source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
