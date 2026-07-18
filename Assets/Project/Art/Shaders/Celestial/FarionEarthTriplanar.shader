Shader "Farion/Celestial/Earth Triplanar"
{
    Properties
    {
        [Header(Colors)]
        _OceanLow("Ocean Low", Color) = (0.015, 0.08, 0.16, 1)
        _OceanHigh("Ocean High", Color) = (0.05, 0.32, 0.48, 1)
        _ShoreLow("Shore Low", Color) = (0.98, 1, 0.67, 1)
        _ShoreHigh("Shore High", Color) = (0.95, 0.91, 0.38, 1)
        _FlatLowA("Flat Low A", Color) = (0.79, 0.86, 0, 1)
        _FlatHighA("Flat High A", Color) = (0.19, 0.46, 0, 1)
        _FlatLowB("Flat Low B", Color) = (0.58, 0.86, 0, 1)
        _FlatHighB("Flat High B", Color) = (0.19, 0.46, 0, 1)
        _SteepLow("Steep Low", Color) = (0.53, 0.49, 0.19, 1)
        _SteepHigh("Steep High", Color) = (0.15, 0.07, 0, 1)
        _SnowColor("Snow Color", Color) = (1, 1, 1, 1)

        [Header(Textures)]
        [NoScaleOffset] _NoiseTex("Earth Noise", 2D) = "white" {}
        [NoScaleOffset] _RockNormal("Rock Normal", 2D) = "bump" {}
        [NoScaleOffset] _SnowNormal("Snow Normal", 2D) = "bump" {}
        _NoiseScale("Noise Scale", Float) = 10
        _NoiseScale2("Noise Scale 2", Float) = 50
        _RockNormalScale("Rock Normal Scale", Float) = 21
        _SnowNormalScale("Snow Normal Scale", Float) = 21
        _NormalStrength("Normal Strength", Range(0, 1)) = 0.5

        [Header(Blending)]
        _OceanLevel("Ocean Level", Range(0, 1)) = 1
        _FlatColorBlend("Flat Color Blend", Range(0, 3)) = 1.5
        _FlatColorBlendNoise("Flat Color Blend Noise", Range(0, 1)) = 0.3
        _ShoreHeight("Shore Height", Range(0, 0.25)) = 0.058
        _ShoreBlend("Shore Blend", Range(0, 0.25)) = 0.089
        _MaxFlatHeight("Max Flat Height", Range(0, 1)) = 0.52
        _SteepBands("Steep Bands", Range(1, 20)) = 8
        _SteepBandStrength("Steep Band Strength", Range(-1, 1)) = 0.5
        _SteepnessThreshold("Steepness Threshold", Range(0, 1)) = 0.378
        _FlatToSteepBlend("Flat To Steep Blend", Range(0, 0.3)) = 0.051
        _FlatToSteepNoise("Flat To Steep Noise", Range(0, 0.2)) = 0.026

        [Header(Snow)]
        _UseSnowyPoles("Use Snowy Poles", Float) = 0
        _SnowLongitude("Snow Longitude", Range(0, 1)) = 0.94
        _SnowBlend("Snow Blend", Range(0, 0.2)) = 0.03
        _SnowSpecular("Snow Specular", Range(0, 1)) = 0.7
        _SnowHighlight("Snow Highlight", Range(1, 2)) = 1.2
        _SnowNoiseA("Snow Noise A", Range(0, 10)) = 3
        _SnowNoiseB("Snow Noise B", Range(0, 10)) = 2.87

        [Header(Surface)]
        _Metallic("Metallic", Range(0, 1)) = 0
        _LandSmoothness("Land Smoothness", Range(0, 1)) = 0.2
        _OceanSmoothness("Ocean Smoothness", Range(0, 1)) = 0.75
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

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_RockNormal);
            SAMPLER(sampler_RockNormal);
            TEXTURE2D(_SnowNormal);
            SAMPLER(sampler_SnowNormal);

            CBUFFER_START(UnityPerMaterial)
                half4 _OceanLow;
                half4 _OceanHigh;
                half4 _ShoreLow;
                half4 _ShoreHigh;
                half4 _FlatLowA;
                half4 _FlatHighA;
                half4 _FlatLowB;
                half4 _FlatHighB;
                half4 _SteepLow;
                half4 _SteepHigh;
                half4 _SnowColor;
                float _NoiseScale;
                float _NoiseScale2;
                float _RockNormalScale;
                float _SnowNormalScale;
                half _NormalStrength;
                half _OceanLevel;
                half _FlatColorBlend;
                half _FlatColorBlendNoise;
                half _ShoreHeight;
                half _ShoreBlend;
                half _MaxFlatHeight;
                half _SteepBands;
                half _SteepBandStrength;
                half _SteepnessThreshold;
                half _FlatToSteepBlend;
                half _FlatToSteepNoise;
                half _UseSnowyPoles;
                half _SnowLongitude;
                half _SnowBlend;
                half _SnowSpecular;
                half _SnowHighlight;
                half _SnowNoiseA;
                half _SnowNoiseB;
                half _Metallic;
                half _LandSmoothness;
                half _OceanSmoothness;
                float _BodyRadius;
                float4 _RadiusMinMax;
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

            half FarionRemap01(float value, float minValue, float maxValue)
            {
                return saturate((value - minValue) / max(maxValue - minValue, 0.0001));
            }

            half FarionBlend(float startHeight, float blendDistance, float height)
            {
                return smoothstep(startHeight - blendDistance * 0.5h, startHeight + blendDistance * 0.5h, height);
            }

            float3 FarionTriplanarWeights(float3 normalDirection)
            {
                float3 weights = pow(abs(normalDirection), 4.0);
                return weights / max(dot(weights, 1.0), 0.0001);
            }

            half4 FarionSampleTriplanarNoise(float3 positionOS, float3 normalOS, float scale)
            {
                float3 normalizedPosition = positionOS / max(_BodyRadius, 0.0001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half4 x = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, normalizedPosition.zy * scale);
                half4 y = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, normalizedPosition.xz * scale);
                half4 z = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, normalizedPosition.xy * scale);
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half3 FarionUnpackTriplanarNormalOS(
                TEXTURE2D_PARAM(normalTexture, normalSampler),
                float3 positionOS,
                float3 normalOS,
                float scale)
            {
                float3 normalizedPosition = positionOS / max(_BodyRadius, 0.0001);
                float3 weights = FarionTriplanarWeights(normalOS);
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
                float terrainRadius = length(input.positionOS);
                float heightRange = max(_RadiusMinMax.y - _RadiusMinMax.x, 0.0001);
                float oceanRadius = lerp(_RadiusMinMax.x, _BodyRadius, _OceanLevel);
                half aboveOcean01 = FarionRemap01(terrainRadius, oceanRadius, _RadiusMinMax.y);
                half oceanDepth01 = 1.0h - FarionRemap01(terrainRadius, _RadiusMinMax.x, oceanRadius);

                half4 texNoise = FarionSampleTriplanarNoise(input.positionOS, normalOS, _NoiseScale);
                half4 texNoise2 = FarionSampleTriplanarNoise(input.positionOS, normalOS, _NoiseScale2);
                half largeNoise = input.terrainData.x;
                half detailNoise = input.terrainData.y;
                half smallNoise = input.terrainData.z;
                half warpedNoise = input.terrainData.w;

                half steepness = FarionRemap01(1.0h - dot(normalOS, radialOS), 0.0h, 0.65h);
                half flatHeight01 = FarionRemap01(aboveOcean01, 0.0h, _MaxFlatHeight);

                half flatBlendWeight = FarionBlend(0.0h, _FlatColorBlend, (flatHeight01 - 0.5h) + (texNoise.b - 0.5h) * _FlatColorBlendNoise);
                half3 flatTerrainA = lerp(_FlatLowA.rgb, _FlatHighA.rgb, flatBlendWeight);
                flatTerrainA = lerp(flatTerrainA, (_FlatLowA.rgb + _FlatHighA.rgb) * 0.5h, texNoise.a);
                half3 flatTerrainB = lerp(_FlatLowB.rgb, _FlatHighB.rgb, flatBlendWeight);
                flatTerrainB = lerp(flatTerrainB, (_FlatLowB.rgb + _FlatHighB.rgb) * 0.5h, texNoise.a);

                half biomeWeight = saturate(largeNoise * 0.5h + warpedNoise * 0.35h + texNoise.r * 0.15h);
                half3 flatTerrain = lerp(flatTerrainA, flatTerrainB, biomeWeight);

                half shoreBlendWeight = 1.0h - FarionBlend(_ShoreHeight, _ShoreBlend, flatHeight01);
                half3 shoreColor = lerp(_ShoreLow.rgb, _ShoreHigh.rgb, FarionRemap01(aboveOcean01, 0.0h, max(_ShoreHeight, 0.0001h)));
                shoreColor = lerp(shoreColor, (_ShoreLow.rgb + _ShoreHigh.rgb) * 0.5h, texNoise.g);
                flatTerrain = lerp(flatTerrain, shoreColor, shoreBlendWeight);

                float3 sphereTangent = float3(-radialOS.z, 0, radialOS.x);
                if (dot(sphereTangent, sphereTangent) < 0.0001)
                {
                    sphereTangent = float3(1, 0, 0);
                }
                else
                {
                    sphereTangent = normalize(sphereTangent);
                }

                float3 normalTangent = normalize(normalOS - radialOS * dot(normalOS, radialOS));
                half banding = dot(sphereTangent, normalTangent) * 0.5h + 0.5h;
                banding = floor(banding * (_SteepBands + 1.0h)) / max(_SteepBands, 1.0h);
                banding = (abs(banding - 0.5h) * 2.0h - 0.5h) * _SteepBandStrength;
                half3 steepTerrain = lerp(_SteepLow.rgb, _SteepHigh.rgb, saturate(aboveOcean01 + banding));

                half flatBlendNoise = (texNoise2.r - 0.5h) * _FlatToSteepNoise;
                half flatStrength = 1.0h - FarionBlend(_SteepnessThreshold + flatBlendNoise, _FlatToSteepBlend, steepness);
                half flatHeightFalloff = 1.0h - FarionBlend(_MaxFlatHeight + flatBlendNoise, _FlatToSteepBlend, aboveOcean01);
                flatStrength *= flatHeightFalloff;

                half snowLineNoise = detailNoise * _SnowNoiseA * 0.01h + (texNoise.b - 0.5h) * _SnowNoiseB * 0.01h;
                half snowWeight = FarionBlend(_SnowLongitude, _SnowBlend, abs(radialOS.y + snowLineNoise)) * saturate(_UseSnowyPoles);
                half snowSpeckle = 1.0h - texNoise2.g * 0.05h;
                half3 snow = _SnowColor.rgb * lerp(1.0h, _SnowHighlight, saturate(aboveOcean01 + banding)) * snowSpeckle;

                half3 landColor = lerp(steepTerrain, flatTerrain, flatStrength);
                landColor = lerp(landColor, snow, snowWeight);
                landColor *= lerp(0.9h, 1.12h, saturate(smallNoise + detailNoise * 0.2h));

                half oceanMask = 1.0h - smoothstep(oceanRadius - heightRange * 0.01, oceanRadius + heightRange * 0.01, terrainRadius);
                half3 oceanColor = lerp(_OceanHigh.rgb, _OceanLow.rgb, saturate(oceanDepth01));
                oceanColor *= lerp(0.88h, 1.1h, texNoise.b);
                half3 albedo = lerp(landColor, oceanColor, oceanMask);

                half3 rockNormalOS = FarionUnpackTriplanarNormalOS(TEXTURE2D_ARGS(_RockNormal, sampler_RockNormal), input.positionOS, normalOS, _RockNormalScale);
                half3 snowNormalOS = FarionUnpackTriplanarNormalOS(TEXTURE2D_ARGS(_SnowNormal, sampler_SnowNormal), input.positionOS, normalOS, _SnowNormalScale);
                half3 blendedNormalOS = normalize(lerp(rockNormalOS, snowNormalOS, snowWeight));
                blendedNormalOS = normalize(lerp(blendedNormalOS, normalOS, oceanMask * 0.75h));
                half3 normalWS = normalize(TransformObjectToWorldNormal(blendedNormalOS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 starDirectionWS = normalize(_FarionStarPositionWS.xyz - input.positionWS);
                half ndotl = saturate(dot(normalWS, starDirectionWS));
                half3 ambient = max(SampleSH(normalWS), _FarionAmbientColor.rgb);
                half3 starRadiance = _FarionStarColor.rgb * _FarionStarIntensity;
                half shadow = mainLight.shadowAttenuation;
                half3 diffuse = albedo * (ambient + starRadiance * ndotl * shadow);

                half smoothness = lerp(_LandSmoothness, _OceanSmoothness, oceanMask);
                smoothness = max(smoothness, snowWeight * _SnowSpecular);
                half3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = normalize(starDirectionWS + viewDirectionWS);
                half specularPower = lerp(8.0h, 128.0h, smoothness);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower) * smoothness;
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
