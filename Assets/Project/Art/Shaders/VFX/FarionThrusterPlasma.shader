Shader "Farion/VFX/Thruster Plasma"
{
    Properties
    {
        [HDR] _IdleColor("Idle Color", Color) = (0.02, 0.65, 1.6, 1)
        [HDR] _CruiseColor("Cruise Color", Color) = (0.08, 1.8, 4.5, 1)
        [HDR] _BoostColor("Boost Color", Color) = (2.5, 4.5, 7.5, 1)
        [HDR] _OverheatColor("Overheat Color", Color) = (5.5, 1.1, 0.08, 1)
        [HDR] _DamageColor("Damage Color", Color) = (5.5, 0.08, 0.02, 1)
        _Intensity("Intensity", Range(0, 12)) = 3
        _Opacity("Opacity", Range(0, 1)) = 1
        _NoiseScale("Noise Scale", Range(0.25, 24)) = 5
        _NoiseStretch("Noise Stretch", Range(0.25, 16)) = 3
        _FlowSpeed("Flow Speed", Range(0, 12)) = 3
        _EdgeSoftness("Edge Softness", Range(0.01, 1)) = 0.3
        _DiamondFrequency("Diamond Frequency", Range(1, 16)) = 5
        _DiamondSpacingGrowth("Diamond Spacing Growth", Range(0.35, 1)) = 0.68
        _DiamondDamping("Diamond Damping", Range(0, 6)) = 2.4
        _TurbulenceGrowth("Turbulence Growth", Range(0, 1)) = 0.8
        _EddyGrowth("Eddy Growth", Range(0, 4)) = 1.6
        _SoftFadeDistance("Soft Fade Distance", Range(0, 4)) = 0.6
        [Enum(Core Glow,0,Inner Plasma,1,Outer Plasma,2,Shock Diamonds,3)]
        _LayerMode("Layer Mode", Float) = 1
        [HideInInspector] _Throttle("Throttle", Range(0, 1)) = 0
        [HideInInspector] _Boost("Boost", Range(0, 1)) = 0
        [HideInInspector] _Heat("Heat", Range(0, 1)) = 0
        [HideInInspector] _Damage("Damage", Range(0, 1)) = 0
        [HideInInspector] _LayerSeed("Layer Seed", Range(0, 1)) = 0
        [HideInInspector] _AtmosphereDensity("Atmosphere Density", Range(0, 1)) = 1
        [HideInInspector] _SpeedBlend("Speed Blend", Range(0, 1)) = 0
        [HideInInspector] _Flare("Ignition Flare", Range(0, 1)) = 0
        [HideInInspector] _BellExpansion("Bell Expansion", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ThrusterPlasma"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _IdleColor;
                half4 _CruiseColor;
                half4 _BoostColor;
                half4 _OverheatColor;
                half4 _DamageColor;
                half _Intensity;
                half _Opacity;
                half _NoiseScale;
                half _NoiseStretch;
                half _FlowSpeed;
                half _EdgeSoftness;
                half _DiamondFrequency;
                half _DiamondSpacingGrowth;
                half _DiamondDamping;
                half _TurbulenceGrowth;
                half _EddyGrowth;
                half _SoftFadeDistance;
                half _LayerMode;
                half _Throttle;
                half _Boost;
                half _Heat;
                half _Damage;
                half _LayerSeed;
                half _AtmosphereDensity;
                half _SpeedBlend;
                half _Flare;
                half _BellExpansion;
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
                float3 positionOS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 shapedOS = input.positionOS.xyz;
                float bellAxial = saturate(shapedOS.z);
                shapedOS.xy *= 1.0 + _BellExpansion * bellAxial * bellAxial;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(shapedOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                return output;
            }

            float Hash31(float3 position)
            {
                position = frac(position * 0.1031);
                position += dot(position, position.yzx + 33.33);
                return frac((position.x + position.y) * position.z);
            }

            float ValueNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 localPosition = frac(position);
                float3 blend = localPosition * localPosition * (3.0 - 2.0 * localPosition);

                float nearZ = lerp(
                    lerp(Hash31(cell), Hash31(cell + float3(1, 0, 0)), blend.x),
                    lerp(Hash31(cell + float3(0, 1, 0)), Hash31(cell + float3(1, 1, 0)), blend.x),
                    blend.y);
                float farZ = lerp(
                    lerp(Hash31(cell + float3(0, 0, 1)), Hash31(cell + float3(1, 0, 1)), blend.x),
                    lerp(Hash31(cell + float3(0, 1, 1)), Hash31(cell + 1.0), blend.x),
                    blend.y);
                return lerp(nearZ, farZ, blend.z);
            }

            float Fbm(float3 position)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int octave = 0; octave < 3; octave++)
                {
                    value += ValueNoise(position) * amplitude;
                    position = position * 2.07 + float3(13.1, 7.7, 5.3);
                    amplitude *= 0.5;
                }
                return value / 0.875;
            }

            half3 ResolveColor()
            {
                half3 color = lerp(_IdleColor.rgb, _CruiseColor.rgb, saturate(_Throttle * 1.3h));
                color = lerp(color, _BoostColor.rgb, saturate(_Boost));
                color = lerp(color, _OverheatColor.rgb, saturate(_Heat * _Heat));
                color = lerp(color, _DamageColor.rgb, saturate(_Damage * 0.9h));
                return color;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float axial = saturate(input.positionOS.z);
                float radial = length(input.positionOS.xy);
                float time = _Time.y;

                float eddyScale = 1.0 / (1.0 + axial * _EddyGrowth);
                float3 noisePosition = float3(
                    input.positionOS.xy * _NoiseScale * eddyScale,
                    axial * _NoiseStretch - time * _FlowSpeed + _LayerSeed * 17.0);
                float turbulence = lerp(1.0 - _TurbulenceGrowth, 1.0, pow(axial, 0.75));
                float noise = lerp(0.5, Fbm(noisePosition), turbulence);
                float pulse = 0.96 + sin(time * 19.0 + _LayerSeed * 31.0) * 0.04;

                float startFade = smoothstep(0.0, 0.035, axial);
                float tailStart = lerp(0.68, 0.86, _SpeedBlend);
                float endFade = 1.0 - smoothstep(tailStart, 1.0, axial);
                float edge = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, radial);
                float viewFresnel = 1.0 - saturate(dot(
                    normalize(input.normalWS),
                    normalize(GetWorldSpaceViewDir(input.positionWS))));

                float mask;
                if (_LayerMode < 0.5)
                {
                    float coreRadial = length(input.uv - 0.5) * 2.0;
                    mask = 1.0 - smoothstep(0.35, 1.0, coreRadial);
                    mask *= lerp(0.88, 1.15, noise) * pulse;
                }
                else if (_LayerMode < 1.5)
                {
                    mask = edge * startFade * endFade;
                    mask *= lerp(0.68, 1.2, noise);
                    mask *= lerp(0.75, 1.2, viewFresnel);
                }
                else if (_LayerMode < 2.5)
                {
                    float shell = smoothstep(0.05, 0.75, viewFresnel);
                    mask = shell * startFade * endFade;
                    mask *= lerp(0.22, 1.0, noise);
                    mask *= 1.0 - smoothstep(0.72, 1.0, radial);
                }
                else
                {
                    float nodeAxis = pow(axial, _DiamondSpacingGrowth) * _DiamondFrequency;
                    float breathing = sin(time * 21.0 + _LayerSeed * 31.0) * 0.018;
                    float phase = frac(nodeAxis + breathing);
                    float offset = abs(phase - 0.5);
                    float diamond = 1.0 - smoothstep(0.09, 0.34, offset);
                    float diamondShape = 1.0 - smoothstep(
                        0.16,
                        0.7,
                        abs(radial - offset * 1.45));
                    float damping = exp(-axial * _DiamondDamping);
                    mask = diamond * diamondShape * startFade * endFade * damping;
                    mask *= lerp(0.55, 1.1, noise);
                }

                float pressureFalloff = lerp(lerp(1.0, 0.72, axial), 1.0, _AtmosphereDensity);
                mask *= pressureFalloff;

                if (_SoftFadeDistance > 0.0)
                {
                    float2 screenUv = GetNormalizedScreenSpaceUV(input.positionHCS);
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                    float fragmentDepth = -TransformWorldToView(input.positionWS).z;
                    mask *= saturate((sceneDepth - fragmentDepth) / _SoftFadeDistance);
                }

                half intensity = _Intensity *
                    lerp(0.45h, 1.25h, _Throttle) *
                    lerp(1.0h, 1.6h, _Boost) *
                    (1.0h + _Flare * 1.4h);
                half3 emission = ResolveColor() * intensity * mask * _Opacity;
                return half4(emission, mask * _Opacity);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
