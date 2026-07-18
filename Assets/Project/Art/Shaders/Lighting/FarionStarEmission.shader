Shader "Farion/Lighting/Star Emission"
{
    Properties
    {
        [HDR] _EmissionColor("Emission Color", Color) = (1, 0.78, 0.42, 1)
        _Intensity("Intensity", Range(0, 25)) = 8
        _RimPower("Rim Power", Range(0.25, 8)) = 2
        _RimStrength("Rim Strength", Range(0, 6)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                half _Intensity;
                half _RimPower;
                half _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(saturate(1.0h - dot(normalWS, viewDirectionWS)), _RimPower) * _RimStrength;
                half3 emission = _EmissionColor.rgb * _Intensity * (1.0h + rim);
                return half4(emission, 1);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
