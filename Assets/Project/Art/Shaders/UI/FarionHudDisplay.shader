Shader "Farion/UI/Hud Display"
{
    Properties
    {
        [PerRendererData] _MainTex("Hud Render Texture", 2D) = "black" {}
        _Color("Tint", Color) = (1, 1, 1, 1)
        _GrainTex("Grain Noise", 2D) = "gray" {}

        _EffectStrength("Effect Strength", Range(0, 1)) = 0.85

        _ScanPeriod("Scanline Period (px)", Range(2, 12)) = 3
        _ScanStrength("Scanline Strength", Range(0, 1)) = 0.35
        _ScanScroll("Scanline Scroll", Float) = 0.15

        _ChromaOffset("Chroma Offset (px)", Range(0, 4)) = 0.9

        _GlowRadius("Phosphor Glow Radius (px)", Range(0, 8)) = 2.2
        _GlowStrength("Phosphor Glow", Range(0, 2)) = 0.55

        _GrainStrength("Grain Strength", Range(0, 0.5)) = 0.07
        _GrainScale("Grain Scale", Float) = 3
        _Flicker("Flicker", Range(0, 0.2)) = 0.025
        _Gain("Gain", Range(0.5, 2)) = 1.08

        _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "HudDisplay"

            // The HUD render texture holds premultiplied color, because the canvas
            // blended it onto a transparent black clear.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            Cull Off
            Lighting Off
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_GrainTex);
            SAMPLER(sampler_GrainTex);

            // Written by HudDisplayRenderer. The canvas binds the render texture straight to
            // the renderer, which leaves the built in _MainTex_TexelSize stale.
            float4 _HudTexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _EffectStrength;
                float _ScanPeriod;
                float _ScanStrength;
                float _ScanScroll;
                float _ChromaOffset;
                float _GlowRadius;
                float _GlowStrength;
                float _GrainStrength;
                float _GrainScale;
                float _Flicker;
                float _Gain;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float4 SampleHud(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 texel = _HudTexelSize.xy;

                float4 source = SampleHud(uv);
                float4 hud = source;

                // Channel separation, the way a shadow mask misconverges.
                float2 chroma = float2(_ChromaOffset * texel.x, 0.0);
                hud.r = SampleHud(uv + chroma).r;
                hud.b = SampleHud(uv - chroma).b;

                // Phosphor bleed. The texture is premultiplied, so alpha blooms
                // together with color and the glow spills past the element edge.
                float2 glow = _GlowRadius * texel;
                float4 bleed =
                    SampleHud(uv + float2(glow.x, glow.y)) +
                    SampleHud(uv + float2(-glow.x, glow.y)) +
                    SampleHud(uv + float2(glow.x, -glow.y)) +
                    SampleHud(uv + float2(-glow.x, -glow.y));
                hud += bleed * 0.25 * _GlowStrength;

                // Scanlines run in render texture pixels so they stay put on screen.
                float lineCount = _HudTexelSize.w / max(_ScanPeriod, 1.0);
                float scan = sin(uv.y * lineCount * TWO_PI - _Time.y * _ScanScroll * TWO_PI);
                hud *= lerp(1.0 - _ScanStrength, 1.0, scan * 0.5 + 0.5);

                // 256px noise tile, _GrainScale is how many screen pixels one noise texel covers.
                float2 grainUv = uv * _HudTexelSize.zw / (256.0 * max(_GrainScale, 0.01)) +
                    float2(frac(_Time.y * 1.7), frac(_Time.y * 1.3));
                float grain = SAMPLE_TEXTURE2D(_GrainTex, sampler_GrainTex, grainUv).r - 0.5;
                hud += grain * _GrainStrength * hud.a;

                hud *= _Gain * (1.0 + sin(_Time.y * 41.0) * _Flicker);
                hud = lerp(source, hud, _EffectStrength);
                hud *= _Color * input.color.a;

                return max(hud, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
