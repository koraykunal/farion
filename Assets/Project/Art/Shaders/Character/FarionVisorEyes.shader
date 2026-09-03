Shader "Farion/Character/Visor Eyes"
{
    Properties
    {
        [HDR] _EyeColor("Eye Color", Color) = (0.35, 2.2, 2.6, 1)
        _EyeOpen("Eye Open", Range(0, 2)) = 1
        _EyeRadius("Eye Radius", Range(0.02, 0.5)) = 0.24
        _EyeSpacing("Eye Spacing", Range(0, 1.5)) = 0.95
        _EdgeSoftness("Edge Softness", Range(0.001, 0.2)) = 0.02
        _CoreOpacity("Core Opacity", Range(0, 1)) = 0.85
        _Aspect("Quad Aspect (w/h)", Float) = 2

        _ScanLines("Scanline Count", Range(4, 96)) = 28
        _ScanStrength("Scanline Strength", Range(0, 1)) = 0.35
        _ScanScroll("Scanline Scroll", Float) = 0.15
        _ChromaOffset("Chroma Offset", Range(0, 0.05)) = 0.008
        _GlowRadius("Glow Radius", Range(1, 4)) = 2.2
        _GlowStrength("Glow Strength", Range(0, 2)) = 0.45
        _Flicker("Flicker", Range(0, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VisorEyes"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _EyeColor;
                float _EyeOpen;
                float _EyeRadius;
                float _EyeSpacing;
                float _EdgeSoftness;
                float _CoreOpacity;
                float _Aspect;
                float _ScanLines;
                float _ScanStrength;
                float _ScanScroll;
                float _ChromaOffset;
                float _GlowRadius;
                float _GlowStrength;
                float _Flicker;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float EyeDistance(float2 p, float open)
            {
                float2 e = float2(abs(p.x) - _EyeSpacing * 0.5, p.y / open);
                return length(e);
            }

            float Core(float d)
            {
                return 1.0 - smoothstep(_EyeRadius - _EdgeSoftness, _EyeRadius + _EdgeSoftness, d);
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * float2(_Aspect, 1.0);
                float open = max(_EyeOpen, 0.05);
                float2 chroma = float2(_ChromaOffset, 0.0);

                float d = EyeDistance(p, open);
                float3 core = float3(
                    Core(EyeDistance(p + chroma, open)),
                    Core(d),
                    Core(EyeDistance(p - chroma, open)));

                float glow = saturate(1.0 - d / (_EyeRadius * _GlowRadius));
                glow = glow * glow * _GlowStrength;

                float scan = sin(input.uv.y * _ScanLines * TWO_PI - _Time.y * _ScanScroll * TWO_PI);
                float scanGain = lerp(1.0 - _ScanStrength, 1.0, scan * 0.5 + 0.5);
                float flicker = 1.0 + sin(_Time.y * 41.0) * _Flicker;

                float3 rgb = _EyeColor.rgb * (core + glow) * scanGain * flicker;
                float alpha = core.g * _CoreOpacity;
                return float4(max(rgb, 0.0), alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
