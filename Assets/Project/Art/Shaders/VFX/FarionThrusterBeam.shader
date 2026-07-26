Shader "Farion/VFX/Thruster Beam"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0.42, 0.12, 1)
        _CoreColor ("Core Color", Color) = (1, 0.92, 0.65, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 0.65
        _RimPower ("Rim Power", Range(0.25, 8)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _CoreColor;
                half _Intensity;
                half _Alpha;
                half _RimPower;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half facing = saturate(1.0h - abs(dot(normalize(input.normalWS), normalize(input.viewDirWS))));
                half rim = pow(max(facing, 0.001h), _RimPower);
                half axial = saturate(input.color.r);
                half alpha = saturate(input.color.a * _Alpha * _Intensity) * lerp(0.45h, 1.0h, rim);
                half3 color = lerp(_BaseColor.rgb, _CoreColor.rgb, axial) * _Intensity;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
