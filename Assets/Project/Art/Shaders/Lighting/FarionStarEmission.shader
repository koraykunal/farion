Shader "Farion/Lighting/Star Emission"
{
    Properties
    {
        [HDR] _PhotosphereColor("Photosphere Color", Color) = (1, 0.58, 0.16, 1)
        [HDR] _LimbColor("Limb Color", Color) = (1, 0.18, 0.015, 1)
        _Intensity("Intensity", Range(0, 25)) = 5.5
        _LimbDarkening("Limb Darkening", Range(0.1, 4)) = 0.72
        _GranulationScale("Granulation Scale", Range(1, 96)) = 46
        _GranulationStrength("Granulation Strength", Range(0, 1)) = 0.22
        _GranulationSpeed("Granulation Speed", Range(0, 0.2)) = 0.018
        _CellScale("Cell Scale", Range(0.5, 24)) = 8
        _CellStrength("Cell Strength", Range(0, 1)) = 0.12
        _CellSpeed("Cell Speed", Range(0, 0.2)) = 0.035
        _SunspotScale("Sunspot Scale", Range(0.5, 16)) = 5.5
        _SunspotStrength("Sunspot Strength", Range(0, 1)) = 0.42
        _SunspotThreshold("Sunspot Threshold", Range(0, 1)) = 0.7
        _SurfaceRotationSpeed("Surface Rotation Speed", Range(0, 5)) = 0.35
        _PulseAmplitude("Pulse Amplitude", Range(0, 0.2)) = 0.012
        _PulseSpeed("Pulse Speed", Range(0, 4)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background+10"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _PhotosphereColor;
                half4 _LimbColor;
                half _Intensity;
                half _LimbDarkening;
                half _GranulationScale;
                half _GranulationStrength;
                half _GranulationSpeed;
                half _CellScale;
                half _CellStrength;
                half _CellSpeed;
                half _SunspotScale;
                half _SunspotStrength;
                half _SunspotThreshold;
                half _SurfaceRotationSpeed;
                half _PulseAmplitude;
                half _PulseSpeed;
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
                float3 positionOS : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
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

                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));

                float nearZ = lerp(
                    lerp(n000, n100, blend.x),
                    lerp(n010, n110, blend.x),
                    blend.y);
                float farZ = lerp(
                    lerp(n001, n101, blend.x),
                    lerp(n011, n111, blend.x),
                    blend.y);
                return lerp(nearZ, farZ, blend.z);
            }

            float Fbm(float3 position)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int octave = 0; octave < 4; octave++)
                {
                    value += ValueNoise(position) * amplitude;
                    position = position * 2.03 + float3(11.7, 7.3, 5.1);
                    amplitude *= 0.5;
                }

                return value / 0.9375;
            }

            float3 RotateAroundY(float3 position, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float3(
                    position.x * cosine - position.z * sine,
                    position.y,
                    position.x * sine + position.z * cosine);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float time = _Time.y;
                float rotationAngle = radians(_SurfaceRotationSpeed) * time;
                float3 spherePosition = RotateAroundY(normalize(input.positionOS), rotationAngle);

                float3 granulationFlow = float3(
                    time * _GranulationSpeed,
                    -time * _GranulationSpeed * 0.61,
                    time * _GranulationSpeed * 0.37);
                float3 cellFlow = float3(
                    -time * _CellSpeed * 0.43,
                    time * _CellSpeed,
                    time * _CellSpeed * 0.29);

                float3 granulationPosition =
                    spherePosition * _GranulationScale
                    + granulationFlow;
                float granulationFootprint = max(
                    length(ddx(granulationPosition)),
                    length(ddy(granulationPosition)));
                float granulationVisibility =
                    1.0 - smoothstep(0.35, 1.25, granulationFootprint);
                float granulation = lerp(
                    0.5,
                    Fbm(granulationPosition),
                    granulationVisibility);
                float cells = Fbm(spherePosition * _CellScale + cellFlow);
                float sunspotNoise = Fbm(
                    spherePosition * _SunspotScale
                    + float3(time * 0.002, 0.0, -time * 0.0015));
                float sunspots =
                    smoothstep(_SunspotThreshold, 1.0, sunspotNoise)
                    * _SunspotStrength;

                half facing = saturate(dot(normalWS, viewDirectionWS));
                half limbFactor = pow(max(facing, 0.0001h), _LimbDarkening);
                half3 photosphereColor = lerp(_LimbColor.rgb, _PhotosphereColor.rgb, limbFactor);

                float surfaceVariation =
                    1.0
                    + (granulation - 0.5) * 2.0 * _GranulationStrength
                    + (cells - 0.5) * 2.0 * _CellStrength;
                float pulse = 1.0 + sin(time * _PulseSpeed) * _PulseAmplitude;
                half3 emission =
                    photosphereColor
                    * _Intensity
                    * max(surfaceVariation, 0.1)
                    * (1.0 - sunspots)
                    * pulse;
                return half4(emission, 1);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
