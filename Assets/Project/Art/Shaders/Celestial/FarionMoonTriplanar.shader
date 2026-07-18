Shader "Farion/Celestial/Moon Triplanar"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor("Base Color", Color) = (0.55, 0.55, 0.55, 1)
        _SecondaryColor("Secondary Color", Color) = (0.32, 0.32, 0.32, 1)
        _SteepColor("Steep Color", Color) = (0.16, 0.16, 0.16, 1)
        _EjectaColor("Ejecta Color", Color) = (0.85, 0.82, 0.75, 1)
        _SteepColorStrength("Steep Color Strength", Range(0, 1)) = 0.75

        [Header(Triplanar)]
        [NoScaleOffset] _AlbedoTex("Albedo Noise", 2D) = "white" {}
        [NoScaleOffset] _EjectaRayTex("Ejecta Ray", 2D) = "black" {}
        [NoScaleOffset] _NormalMapFlat("Normal Flat", 2D) = "bump" {}
        [NoScaleOffset] _NormalMapSteep("Normal Steep", 2D) = "bump" {}
        _AlbedoScale("Albedo Scale", Float) = 12
        _NormalFlatScale("Normal Flat Scale", Float) = 18
        _NormalSteepScale("Normal Steep Scale", Float) = 12
        _NormalStrength("Normal Strength", Range(0, 1)) = 0.35
        _BiomeBlendStrength("Biome Blend Strength", Range(0, 2)) = 0.8
        _EjectaStrength("Ejecta Strength", Range(0, 2)) = 0.65
        _EjectaRayFrequency("Ejecta Ray Frequency", Float) = 36
        _UseEjectaRayTex("Use Ejecta Ray Texture", Float) = 0

        [Header(Surface)]
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.35
        _BodyRadius("Body Radius", Float) = 1
        _RadiusMinMax("Radius Min Max", Vector) = (1, 1, 0, 0)
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            TEXTURE2D(_AlbedoTex);
            SAMPLER(sampler_AlbedoTex);
            TEXTURE2D(_EjectaRayTex);
            SAMPLER(sampler_EjectaRayTex);
            TEXTURE2D(_NormalMapFlat);
            SAMPLER(sampler_NormalMapFlat);
            TEXTURE2D(_NormalMapSteep);
            SAMPLER(sampler_NormalMapSteep);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SecondaryColor;
                half4 _SteepColor;
                half4 _EjectaColor;
                half _SteepColorStrength;
                half _Metallic;
                half _Smoothness;
                float _BodyRadius;
                float4 _RadiusMinMax;
                float _AlbedoScale;
                float _NormalFlatScale;
                float _NormalSteepScale;
                half _NormalStrength;
                half _BiomeBlendStrength;
                half _EjectaStrength;
                float _EjectaRayFrequency;
                half _UseEjectaRayTex;
            CBUFFER_END

            float4 _FarionStarPositionWS;
            half4 _FarionStarColor;
            float _FarionStarIntensity;
            half4 _FarionAmbientColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalOS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 terrainData : TEXCOORD4;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = normalize(input.normalOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.terrainData = input.texcoord;
                return output;
            }

            float3 TriplanarWeights(float3 normalDirection)
            {
                float3 weights = pow(abs(normalDirection), 4.0);
                return weights / max(dot(weights, 1.0), 0.0001);
            }

            half4 SampleTriplanarColor(float3 positionOS, float3 normalOS, float scale)
            {
                float3 normalizedPosition = positionOS / max(_BodyRadius, 0.0001);
                float3 weights = TriplanarWeights(normalOS);

                half4 x = SAMPLE_TEXTURE2D(_AlbedoTex, sampler_AlbedoTex, normalizedPosition.zy * scale);
                half4 y = SAMPLE_TEXTURE2D(_AlbedoTex, sampler_AlbedoTex, normalizedPosition.xz * scale);
                half4 z = SAMPLE_TEXTURE2D(_AlbedoTex, sampler_AlbedoTex, normalizedPosition.xy * scale);

                return x * weights.x + y * weights.y + z * weights.z;
            }

            half3 UnpackTriplanarNormalOS(
                TEXTURE2D_PARAM(normalTexture, normalSampler),
                float3 positionOS,
                float3 normalOS,
                float scale)
            {
                float3 normalizedPosition = positionOS / max(_BodyRadius, 0.0001);
                float3 weights = TriplanarWeights(normalOS);
                float3 axisSign = sign(normalOS);

                half3 normalX = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(normalTexture, normalSampler, normalizedPosition.zy * scale),
                    _NormalStrength);
                half3 normalY = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(normalTexture, normalSampler, normalizedPosition.xz * scale),
                    _NormalStrength);
                half3 normalZ = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(normalTexture, normalSampler, normalizedPosition.xy * scale),
                    _NormalStrength);

                normalX = half3(normalX.z * axisSign.x, normalX.y, normalX.x);
                normalY = half3(normalY.x, normalY.z * axisSign.y, normalY.y);
                normalZ = half3(normalZ.x, normalZ.y, normalZ.z * axisSign.z);

                return normalize(normalX * weights.x + normalY * weights.y + normalZ * weights.z);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalOS = normalize(input.normalOS);
                float3 radialOS = normalize(input.positionOS);

                float heightRange = max(_RadiusMinMax.y - _RadiusMinMax.x, 0.0001);
                float height01 = saturate((length(input.positionOS) - _RadiusMinMax.x) / heightRange);

                float steepness = saturate((1.0 - dot(normalOS, radialOS)) / 0.3);
                half4 triplanarNoise = SampleTriplanarColor(input.positionOS, normalOS, _AlbedoScale);
                float4 terrainData = input.terrainData;
                half detailNoise = terrainData.z;
                half biomeNoise = saturate(terrainData.w / max(_BiomeBlendStrength * 3.0h, 0.0001h));
                half biomeMask = 1.0h - biomeNoise;

                half colorNoise = (triplanarNoise.r - 0.5h) * 0.4h + (triplanarNoise.g - 0.5h) * 0.25h + detailNoise * 0.18h;
                half heightBlend = smoothstep(0.25h, 0.85h, height01 + colorNoise);
                half3 baseBlend = lerp(_SecondaryColor.rgb, _BaseColor.rgb, heightBlend);
                baseBlend = lerp(baseBlend, _SecondaryColor.rgb, biomeMask * 0.35h);
                half3 albedo = lerp(baseBlend, _SteepColor.rgb, steepness * _SteepColorStrength);
                albedo *= lerp(0.82h, 1.18h, triplanarNoise.b);

                half ejectaDistance = terrainData.y;
                half ejectaFade = saturate(1.0h - ejectaDistance);
                half rayPattern = pow(saturate(sin(terrainData.x * _EjectaRayFrequency + detailNoise * 8.0h) * 0.5h + 0.5h), 6.0h);
                float2 ejectaUv = 0.5 + float2(cos(terrainData.x), sin(terrainData.x)) * ejectaDistance;
                half ejectaTexture = SAMPLE_TEXTURE2D(_EjectaRayTex, sampler_EjectaRayTex, ejectaUv).r;
                half ejectaPattern = lerp(rayPattern, ejectaTexture, saturate(_UseEjectaRayTex));
                half ejectaMask = saturate(ejectaPattern * ejectaFade * ejectaFade * _EjectaStrength);
                albedo = lerp(albedo, _EjectaColor.rgb, ejectaMask);

                half3 flatNormalOS = UnpackTriplanarNormalOS(
                    TEXTURE2D_ARGS(_NormalMapFlat, sampler_NormalMapFlat),
                    input.positionOS,
                    normalOS,
                    _NormalFlatScale);

                half3 steepNormalOS = UnpackTriplanarNormalOS(
                    TEXTURE2D_ARGS(_NormalMapSteep, sampler_NormalMapSteep),
                    input.positionOS,
                    normalOS,
                    _NormalSteepScale);

                half3 blendedNormalOS = normalize(lerp(flatNormalOS, steepNormalOS, steepness));
                half3 normalWS = normalize(TransformObjectToWorldNormal(blendedNormalOS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 starDirectionWS = normalize(_FarionStarPositionWS.xyz - input.positionWS);
                half ndotl = saturate(dot(normalWS, starDirectionWS));
                half3 ambient = max(SampleSH(normalWS), _FarionAmbientColor.rgb);
                half3 starRadiance = _FarionStarColor.rgb * _FarionStarIntensity;
                half3 diffuse = albedo * (ambient + starRadiance * ndotl * mainLight.shadowAttenuation);

                half3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = normalize(starDirectionWS + viewDirectionWS);
                half specularPower = lerp(8.0h, 96.0h, _Smoothness);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower) * _Smoothness;
                half3 specularColor = starRadiance * specular * lerp(0.04h, 0.35h, _Metallic);

                return half4(diffuse + specularColor, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
