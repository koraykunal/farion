Shader "Farion/VFX/Space Dust"
{
    Properties
    {
        _Color("Color", Color) = (0.62, 0.72, 0.85, 1)
        _Alpha("Alpha", Range(0, 1)) = 1
        _SoftEdge("Soft Edge", Range(0.01, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SpaceDust"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
                half _SoftEdge;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                half across = 1.0h - saturate(abs(centered.x));
                half along = 1.0h - saturate(abs(centered.y));
                half edge = max(_SoftEdge, 0.01h);
                half falloff = pow(across, 1.0h / edge) * pow(along, 0.65h);
                half alpha = saturate(falloff * _Alpha);
                return half4(_Color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
