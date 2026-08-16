Shader "Farion/Space/Procedural Star Dome"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.0015, 0.0018, 0.0024, 1)
        _ZenithColor("Zenith Color", Color) = (0.004, 0.005, 0.007, 1)
        _Exposure("Exposure", Float) = 1
        _Seed("Seed", Float) = 19

        [Header(Stars)]
        _StarDensity("Star Density", Range(0, 1)) = 0.035
        _StarBrightness("Star Brightness", Float) = 1.8
        _StarScale("Star Scale", Float) = 260
        _StarSize("Star Size", Range(0.001, 0.2)) = 0.045
        _StarMagnitudeFalloff("Magnitude Falloff", Range(1, 8)) = 3.5
        _StarTemperatureMin("Temperature Min (K)", Range(1500, 10000)) = 3200
        _StarTemperatureMax("Temperature Max (K)", Range(3000, 40000)) = 15000
        _StarTemperatureBias("Temperature Bias", Range(0.25, 4)) = 1.6
        _StarColorSaturation("Color Saturation", Range(0, 1)) = 0.65

        [Header(Milky Way)]
        _GalacticNormal("Galactic Plane Normal", Vector) = (0.34, 0.87, 0.36, 0)
        _MilkyWayColor("Band Color", Color) = (0.42, 0.46, 0.62, 1)
        _MilkyWayIntensity("Band Intensity", Range(0, 4)) = 0.85
        _MilkyWayWidth("Band Width", Range(0.02, 1)) = 0.19
        _MilkyWayScale("Band Detail Scale", Float) = 5.5
        _MilkyWayDustStrength("Dust Lane Strength", Range(0, 1)) = 0.7
        _MilkyWayStarBoost("Star Density Boost", Range(0, 8)) = 2.6
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
                float _Exposure;
                float _Seed;
                float _StarDensity;
                float _StarBrightness;
                float _StarScale;
                float _StarSize;
                float _StarMagnitudeFalloff;
                float _StarTemperatureMin;
                float _StarTemperatureMax;
                float _StarTemperatureBias;
                float _StarColorSaturation;
                float4 _GalacticNormal;
                half4 _MilkyWayColor;
                float _MilkyWayIntensity;
                float _MilkyWayWidth;
                float _MilkyWayScale;
                float _MilkyWayDustStrength;
                float _MilkyWayStarBoost;
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

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float ValueNoise(float3 x)
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f *= f * (3.0 - f - f);

                float lower = lerp(
                    lerp(Hash13(p), Hash13(p + float3(1, 0, 0)), f.x),
                    lerp(Hash13(p + float3(0, 1, 0)), Hash13(p + float3(1, 1, 0)), f.x),
                    f.y);
                float upper = lerp(
                    lerp(Hash13(p + float3(0, 0, 1)), Hash13(p + float3(1, 0, 1)), f.x),
                    lerp(Hash13(p + float3(0, 1, 1)), Hash13(p + 1.0), f.x),
                    f.y);
                return lerp(lower, upper, f.z);
            }

            float Fbm(float3 position)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int octave = 0; octave < 4; octave++)
                {
                    value += ValueNoise(position) * amplitude;
                    position = position * 2.07 + float3(19.3, 7.7, 11.1);
                    amplitude *= 0.5;
                }

                return value / 0.9375;
            }

            float3 BlackbodyColor(float kelvin)
            {
                float t = clamp(kelvin, 1500.0, 40000.0) * 0.01;
                float3 color;

                if (t <= 66.0)
                {
                    color.r = 1.0;
                    color.g = saturate(0.39008157 * log(t) - 0.63184144);
                }
                else
                {
                    color.r = saturate(1.29293618 * pow(t - 60.0, -0.1332047592));
                    color.g = saturate(1.12989086 * pow(t - 60.0, -0.0755148492));
                }

                if (t >= 66.0)
                {
                    color.b = 1.0;
                }
                else if (t <= 19.0)
                {
                    color.b = 0.0;
                }
                else
                {
                    color.b = saturate(0.54320679 * log(t - 10.0) - 1.19625408);
                }

                return color;
            }

            float GalacticBand(float3 direction)
            {
                float3 planeNormal = normalize(_GalacticNormal.xyz + float3(0.0, 0.0001, 0.0));
                float distanceFromPlane = dot(direction, planeNormal);
                float width = max(_MilkyWayWidth, 0.02);
                float normalized = distanceFromPlane / width;
                return exp(-normalized * normalized);
            }

            half3 MilkyWay(float3 direction, float band)
            {
                if (band <= 0.001 || _MilkyWayIntensity <= 0.0)
                {
                    return 0.0;
                }

                float3 samplePosition = direction * _MilkyWayScale + _Seed * 0.37;
                float clouds = saturate(Fbm(samplePosition) * 1.35);
                float dust = saturate(Fbm(samplePosition * 2.7 + 41.0));
                float occlusion = 1.0 - dust * _MilkyWayDustStrength;

                float glow = band * clouds * saturate(occlusion) * _MilkyWayIntensity;
                return _MilkyWayColor.rgb * glow;
            }

            half3 StarLayer(
                float3 direction,
                float scale,
                float density,
                float sizeScale,
                float brightness,
                float seedOffset,
                float footprint)
            {
                float3 p = direction * scale + seedOffset;
                float3 cell = floor(p);
                float3 local = frac(p);

                float3 cellHash = Hash33(cell);
                if (cellHash.x > density)
                {
                    return 0.0;
                }

                float3 jitter = Hash33(cell + 5.17) * 0.7 + 0.15;
                float magnitudeRaw = pow(cellHash.y, _StarMagnitudeFalloff);
                float magnitude = magnitudeRaw * (_StarMagnitudeFalloff + 1.0);
                float radius = sizeScale * (0.55 + magnitudeRaw * 0.9);

                float cellFootprint = footprint * scale;
                float effectiveRadius = max(radius, cellFootprint);
                float energy = (radius * radius) / max(effectiveRadius * effectiveRadius, 1e-8);

                float distanceToStar = length(local - jitter);
                float core = smoothstep(effectiveRadius, 0.0, distanceToStar);
                if (core <= 0.0)
                {
                    return 0.0;
                }

                float kelvin = lerp(
                    _StarTemperatureMin,
                    _StarTemperatureMax,
                    pow(cellHash.z, _StarTemperatureBias));
                half3 tint = lerp(1.0, BlackbodyColor(kelvin), _StarColorSaturation);

                return tint * core * energy * magnitude * brightness;
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
                float footprint = max(length(ddx(direction)), length(ddy(direction)));

                half vertical = saturate(direction.y * 0.5h + 0.5h);
                half3 color = lerp(_BaseColor.rgb, _ZenithColor.rgb, vertical);

                float band = GalacticBand(direction);
                color += MilkyWay(direction, band);

                float density = saturate(_StarDensity * (1.0 + band * _MilkyWayStarBoost));

                color += StarLayer(
                    direction, _StarScale, density,
                    _StarSize, _StarBrightness, _Seed, footprint);
                color += StarLayer(
                    direction, _StarScale * 0.47, density * 0.45,
                    _StarSize * 1.6, _StarBrightness * 0.42, _Seed + 71.0, footprint);
                color += StarLayer(
                    direction, _StarScale * 1.83, density * 0.62,
                    _StarSize * 0.62, _StarBrightness * 1.35, _Seed + 131.0, footprint);

                return half4(color * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
