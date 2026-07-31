Shader "Farion/VFX/Spacecraft Reentry"
{
    Properties
    {
        [HDR] _StagnationColor("Stagnation Color", Color) = (8, 1.2, 0.08, 1)
        [HDR] _PlasmaColor("Plasma Color", Color) = (3.5, 0.18, 0.02, 1)
        [HDR] _ShockColor("Shock Color", Color) = (0.35, 1.4, 5, 1)
        _Intensity("Intensity", Range(0, 8)) = 2.4
        _Opacity("Opacity", Range(0, 1)) = 0.8
        _NoiseScale("Noise Scale", Range(0.25, 16)) = 4
        _FlowSpeed("Flow Speed", Range(0, 8)) = 1.8
        _RimPower("Rim Power", Range(0.25, 8)) = 2.2
        _BandFrequency("Shock Band Frequency", Range(1, 16)) = 6
        [HideInInspector] _EffectStrength("Effect Strength", Range(0, 1)) = 0
        [HideInInspector] _LayerMode("Layer Mode", Float) = 0
        [HideInInspector] _LayerSeed("Layer Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+15"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SpacecraftReentry"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _StagnationColor;
                half4 _PlasmaColor;
                half4 _ShockColor;
                half _Intensity;
                half _Opacity;
                half _NoiseScale;
                half _FlowSpeed;
                half _RimPower;
                half _BandFrequency;
                half _EffectStrength;
                half _LayerMode;
                half _LayerSeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 position)
            {
                position = frac(position * float2(123.34, 456.21));
                position += dot(position, position + 45.32);
                return frac(position.x * position.y);
            }

            float ValueNoise(float2 position)
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
                float axial = saturate(input.uv.y);
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(
                    1.0 - saturate(dot(normalize(input.normalWS), viewDirection)),
                    _RimPower);
                float tailFade = 1.0 - smoothstep(0.68, 1.0, axial);
                float tipFade = smoothstep(0.0, 0.025, axial);
                float noise = ValueNoise(float2(
                    input.uv.x * _NoiseScale * 8.0 + _LayerSeed,
                    axial * _NoiseScale - _Time.y * _FlowSpeed));
                float stagnation = 1.0 - smoothstep(0.06, 0.72, axial);

                float mask;
                half3 color;
                if (_LayerMode < 0.5h)
                {
                    mask = lerp(0.3, 1.0, fresnel);
                    mask *= tailFade * tipFade * lerp(0.62, 1.18, noise);
                    color = lerp(_PlasmaColor.rgb, _StagnationColor.rgb, stagnation);
                }
                else
                {
                    float band = sin(
                        (axial * _BandFrequency - _Time.y * _FlowSpeed * 0.18) *
                        TWO_PI) * 0.5 + 0.5;
                    band = smoothstep(0.42, 0.92, band);
                    mask = fresnel * fresnel;
                    mask *= tailFade * tipFade * lerp(0.24, 0.72, band);
                    mask *= lerp(0.72, 1.12, noise);
                    color = lerp(_ShockColor.rgb, _StagnationColor.rgb, stagnation * 0.35);
                }

                half strength = saturate(_EffectStrength);
                half emissionStrength = _Intensity * strength * strength * mask * _Opacity;
                return half4(color * emissionStrength, emissionStrength);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
