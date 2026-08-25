Shader "Farion/VFX/Thruster Distortion"
{
    Properties
    {
        _Opacity("Opacity", Range(0, 1)) = 0.7
        _DistortionStrength("Distortion Strength", Range(0, 0.08)) = 0.02
        _DistortionReferenceDistance("Distortion Reference Distance", Range(1, 200)) = 18
        _NoiseScale("Noise Scale", Range(0.25, 24)) = 5
        _FlowSpeed("Flow Speed", Range(0, 10)) = 2.4
        _VacuumStrengthScale("Vacuum Strength Scale", Range(0, 1)) = 0.45
        _SoftFadeDistance("Soft Fade Distance", Range(0, 8)) = 1.6
        [HideInInspector] _Throttle("Throttle", Range(0, 1)) = 0
        [HideInInspector] _Boost("Boost", Range(0, 1)) = 0
        [HideInInspector] _Heat("Heat", Range(0, 1)) = 0
        [HideInInspector] _LayerSeed("Layer Seed", Range(0, 1)) = 0
        [HideInInspector] _AtmosphereDensity("Atmosphere Density", Range(0, 1)) = 1
        [HideInInspector] _SpeedBlend("Speed Blend", Range(0, 1)) = 0
        [HideInInspector] _Flare("Ignition Flare", Range(0, 1)) = 0
        [HideInInspector] _BellExpansion("Bell Expansion", Range(0, 2)) = 0
        [HideInInspector] _GroundSplash("Ground Splash", Range(0, 2)) = 0
        [HideInInspector] _PlumeBend("Plume Bend", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ThrusterDistortion"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "FarionThrusterShaping.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
                half _DistortionStrength;
                half _DistortionReferenceDistance;
                half _NoiseScale;
                half _FlowSpeed;
                half _VacuumStrengthScale;
                half _SoftFadeDistance;
                half _Throttle;
                half _Boost;
                half _Heat;
                half _LayerSeed;
                half _AtmosphereDensity;
                half _SpeedBlend;
                half _Flare;
                half _BellExpansion;
                half _GroundSplash;
                float4 _PlumeBend;
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
                float3 shapedOS = FarionShapeThrusterVertex(
                    input.positionOS.xyz,
                    _BellExpansion,
                    _PlumeBend.xyz,
                    _GroundSplash);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(shapedOS);
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

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionHCS);
                if (_SoftFadeDistance > 0.0)
                {
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                    float fragmentDepth = -TransformWorldToView(input.positionWS).z;
                    envelope *= saturate((sceneDepth - fragmentDepth) / _SoftFadeDistance);
                }

                float strength = _DistortionStrength *
                    lerp(0.35, 1.0, _Throttle) *
                    lerp(1.0, 1.65, saturate(_Boost + _Heat * 0.6)) *
                    lerp(_VacuumStrengthScale, 1.0, _AtmosphereDensity) *
                    (1.0 + _Flare * 0.8) *
                    FarionThrusterViewScale(input.positionWS, _DistortionReferenceDistance);
                float2 sampleUv = saturate(screenUv + noiseVector * strength * envelope);
                half3 distortedScene = SampleSceneColor(sampleUv);
                half alpha = saturate(envelope * _Opacity);
                return half4(distortedScene, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
