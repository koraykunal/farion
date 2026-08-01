Shader "Farion/Space/Procedural Star Dome"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.0015, 0.0018, 0.0024, 1)
        _ZenithColor("Zenith Color", Color) = (0.004, 0.005, 0.007, 1)
        _StarColorA("Star Color A", Color) = (0.9, 0.94, 1, 1)
        _StarColorB("Star Color B", Color) = (1, 0.78, 0.55, 1)
        _Exposure("Exposure", Float) = 1
        _Seed("Seed", Float) = 19
        _StarDensity("Star Density", Range(0, 1)) = 0.035
        _StarBrightness("Star Brightness", Float) = 1.8
        _StarScale("Star Scale", Float) = 260
        _StarSize("Star Size", Range(0.001, 0.2)) = 0.045
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "StarDome"

            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ZenithColor;
                half4 _StarColorA;
                half4 _StarColorB;
                float _Exposure;
                float _Seed;
                float _StarDensity;
                float _StarBrightness;
                float _StarScale;
                float _StarSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half3 StarLayer(float3 direction, float scale, float density, float size, float brightness, float seed)
            {
                float3 p = direction * scale + seed;
                float3 cell = floor(p);
                float3 local = frac(p) - 0.5;
                float random = Hash13(cell);
                float mask = step(1.0 - density, random);
                float core = smoothstep(size, 0.0, length(local));
                float colorMix = Hash13(cell + 17.0);
                half3 starColor = lerp(_StarColorA.rgb, _StarColorB.rgb, colorMix);
                return starColor * core * mask * brightness * (0.55 + random);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.directionWS = TransformObjectToWorldDir(input.positionOS.xyz);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 direction = normalize(input.directionWS);
                half vertical = saturate(direction.y * 0.5h + 0.5h);
                half3 color = lerp(_BaseColor.rgb, _ZenithColor.rgb, vertical);

                color += StarLayer(direction, _StarScale, _StarDensity, _StarSize, _StarBrightness, _Seed);
                color += StarLayer(direction, _StarScale * 0.47, _StarDensity * 0.45, _StarSize * 1.6, _StarBrightness * 0.42, _Seed + 71.0);
                color += StarLayer(direction, _StarScale * 1.83, _StarDensity * 0.18, _StarSize * 0.62, _StarBrightness * 1.35, _Seed + 131.0);

                return half4(color * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
