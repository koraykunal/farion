Shader "Farion/VFX/Thruster Distortion"
{
    Properties
    {
        _Opacity("Opacity", Range(0, 1)) = 0.3
        _DistortionStrength("Distortion Strength", Range(0, 0.08)) = 0.018
        _NoiseScale("Noise Scale", Range(0.25, 24)) = 5
        _FlowSpeed("Flow Speed", Range(0, 10)) = 2.4
        [HideInInspector] _Throttle("Throttle", Range(0, 1)) = 0
        [HideInInspector] _Boost("Boost", Range(0, 1)) = 0
        [HideInInspector] _Heat("Heat", Range(0, 1)) = 0
        [HideInInspector] _Damage("Damage", Range(0, 1)) = 0
        [HideInInspector] _LayerSeed("Layer Seed", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ThrusterDistortion"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
                half _DistortionStrength;
                half _NoiseScale;
                half _FlowSpeed;
                half _Throttle;
                half _Boost;
                half _Heat;
                half _Damage;
                half _LayerSeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            float Hash21(float2 position)
            {
                position = frac(position * float2(123.34, 456.21));
                position += dot(position, position + 45.32);
                return frac(position.x * position.y);
            }

            float Noise(float2 position)
            {
                float2 cell = floor(position);
                float2 localPosition = frac(position);
                float2 blend = localPosition * localPosition * (3.0 - 2.0 * localPosition);
                return lerp(
                    lerp(Hash21(cell), Hash21(cell + float2(1, 0)), blend.x),
                    lerp(Hash21(cell + float2(0, 1)), Hash21(cell + 1.0), blend.x),
                    blend.y);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float axial = saturate(input.positionOS.z);
                float radial = length(input.positionOS.xy);
                float viewFresnel = 1.0 - saturate(dot(
                    normalize(input.normalWS),
                    normalize(GetWorldSpaceViewDir(input.positionWS))));
                float envelope =
                    smoothstep(0.0, 0.05, axial) *
                    (1.0 - smoothstep(0.72, 1.0, axial)) *
                    (1.0 - smoothstep(0.55, 1.0, radial)) *
                    smoothstep(0.05, 0.85, viewFresnel);

                float time = _Time.y * _FlowSpeed;
                float2 noiseUv = input.positionOS.xy * _NoiseScale +
                    float2(axial * 3.7 - time, axial * -2.1 + time * 0.71) +
                    _LayerSeed * 19.0;
                float2 noiseVector = float2(
                    Noise(noiseUv),
                    Noise(noiseUv + 7.31)) * 2.0 - 1.0;

                float strength = _DistortionStrength *
                    lerp(0.35, 1.0, _Throttle) *
                    lerp(1.0, 1.65, saturate(_Boost + _Heat * 0.6));
                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionHCS);
                half3 distortedScene = SampleSceneColor(screenUv + noiseVector * strength * envelope);
                half alpha = saturate(envelope * _Opacity);
                return half4(distortedScene, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
