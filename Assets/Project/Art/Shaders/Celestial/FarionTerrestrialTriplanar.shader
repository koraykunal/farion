Shader "Farion/Celestial/Terrestrial Triplanar"
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

        [Header(Textures)]
        [NoScaleOffset] _NoiseTex("Terrain Noise", 2D) = "white" {}
        [NoScaleOffset] _RockNormal("Rock Normal", 2D) = "bump" {}
        _NoiseScale("Noise Scale", Float) = 10
        _NoiseScale2("Noise Scale 2", Float) = 50
        _RockNormalScale("Rock Normal Scale", Float) = 21
        _NormalStrength("Normal Strength", Range(0, 1)) = 0.5

        [Header(Blending)]
        _OceanLevel("Ocean Level", Range(0, 1)) = 1
        _HasOcean("Has Ocean", Float) = 1
        _FlatColorBlend("Flat Color Blend", Range(0, 3)) = 1.5
        _FlatColorBlendNoise("Flat Color Blend Noise", Range(0, 1)) = 0.3
        _ShoreHeight("Shore Height", Range(0, 0.25)) = 0.058
        _ShoreBlend("Shore Blend", Range(0, 0.25)) = 0.089
        _OceanEdgeBlend("Ocean Edge Blend", Range(0.001, 0.12)) = 0.035
        _ShoreWetness("Shore Wetness", Range(0, 1)) = 0.38
        _ShoreFoamStrength("Shore Foam Strength", Range(0, 1)) = 0.14
        _MaxFlatHeight("Max Flat Height", Range(0, 1)) = 0.52
        _SteepBands("Steep Bands", Range(1, 20)) = 8
        _SteepBandStrength("Steep Band Strength", Range(-1, 1)) = 0.5
        _SteepnessThreshold("Steepness Threshold", Range(0, 1)) = 0.378
        _FlatToSteepBlend("Flat To Steep Blend", Range(0, 0.3)) = 0.051
        _FlatToSteepNoise("Flat To Steep Noise", Range(0, 0.2)) = 0.026

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
            TEXTURE2D_ARRAY(_BiomeBaseColorArray);
            SAMPLER(sampler_BiomeBaseColorArray);
            TEXTURE2D_ARRAY(_BiomeNormalArray);
            SAMPLER(sampler_BiomeNormalArray);
            TEXTURE2D_ARRAY(_BiomeRoughnessArray);
            SAMPLER(sampler_BiomeRoughnessArray);
            TEXTURE2D_ARRAY(_BiomeAmbientOcclusionArray);
            SAMPLER(sampler_BiomeAmbientOcclusionArray);

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
                float _NoiseScale;
                float _NoiseScale2;
                float _RockNormalScale;
                half _NormalStrength;
                half _OceanLevel;
                half _HasOcean;
                half _FlatColorBlend;
                half _FlatColorBlendNoise;
                half _ShoreHeight;
                half _ShoreBlend;
                half _OceanEdgeBlend;
                half _ShoreWetness;
                half _ShoreFoamStrength;
                half _MaxFlatHeight;
                half _SteepBands;
                half _SteepBandStrength;
                half _SteepnessThreshold;
                half _FlatToSteepBlend;
                half _FlatToSteepNoise;
                half _Metallic;
                half _LandSmoothness;
                half _OceanSmoothness;
                half _BiomeVisualCount;
                half _BiomeVisualBlendStrength;
                half _BiomeTextureCount;
                half _BiomeTextureBlendStrength;
                half4 _BiomeFlatLow[8];
                half4 _BiomeFlatHigh[8];
                half4 _BiomeSteepLow[8];
                half4 _BiomeSteepHigh[8];
                half4 _BiomeParams[8];
                half4 _BiomeTextureParams[8];
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
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalOS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 terrainData : TEXCOORD4;
                float4 biomeWeightsA : TEXCOORD5;
                float4 biomeWeightsB : TEXCOORD6;
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
                output.biomeWeightsA = input.texcoord1;
                output.biomeWeightsB = input.texcoord2;
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

            half FarionGetBiomeWeight(int index, half4 weightsA, half4 weightsB)
            {
                if (index == 0) return weightsA.x;
                if (index == 1) return weightsA.y;
                if (index == 2) return weightsA.z;
                if (index == 3) return weightsA.w;
                if (index == 4) return weightsB.x;
                if (index == 5) return weightsB.y;
                if (index == 6) return weightsB.z;
                return weightsB.w;
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

            half4 FarionSampleBiomeBaseColor(float3 positionOS, float3 normalOS, float scale, int layer)
            {
                float3 normalizedPosition = positionOS / max(_BodyRadius, 0.0001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half4 x = SAMPLE_TEXTURE2D_ARRAY(_BiomeBaseColorArray, sampler_BiomeBaseColorArray, normalizedPosition.zy * scale, layer);
                half4 y = SAMPLE_TEXTURE2D_ARRAY(_BiomeBaseColorArray, sampler_BiomeBaseColorArray, normalizedPosition.xz * scale, layer);
                half4 z = SAMPLE_TEXTURE2D_ARRAY(_BiomeBaseColorArray, sampler_BiomeBaseColorArray, normalizedPosition.xy * scale, layer);
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half FarionSampleBiomeRoughness(float3 positionOS, float3 normalOS, float scale, int layer)
            {
                float3 normalizedPosition = positionOS / max(_BodyRadius, 0.0001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half x = SAMPLE_TEXTURE2D_ARRAY(_BiomeRoughnessArray, sampler_BiomeRoughnessArray, normalizedPosition.zy * scale, layer).r;
                half y = SAMPLE_TEXTURE2D_ARRAY(_BiomeRoughnessArray, sampler_BiomeRoughnessArray, normalizedPosition.xz * scale, layer).r;
                half z = SAMPLE_TEXTURE2D_ARRAY(_BiomeRoughnessArray, sampler_BiomeRoughnessArray, normalizedPosition.xy * scale, layer).r;
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half FarionSampleBiomeAmbientOcclusion(float3 positionOS, float3 normalOS, float scale, int layer)
            {
                float3 normalizedPosition = positionOS / max(_BodyRadius, 0.0001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half x = SAMPLE_TEXTURE2D_ARRAY(_BiomeAmbientOcclusionArray, sampler_BiomeAmbientOcclusionArray, normalizedPosition.zy * scale, layer).r;
                half y = SAMPLE_TEXTURE2D_ARRAY(_BiomeAmbientOcclusionArray, sampler_BiomeAmbientOcclusionArray, normalizedPosition.xz * scale, layer).r;
                half z = SAMPLE_TEXTURE2D_ARRAY(_BiomeAmbientOcclusionArray, sampler_BiomeAmbientOcclusionArray, normalizedPosition.xy * scale, layer).r;
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

            half3 FarionUnpackBiomeNormalOS(
                float3 positionOS,
                float3 normalOS,
                float scale,
                int layer,
                half strength)
            {
                float3 normalizedPosition = positionOS / max(_BodyRadius, 0.0001);
                float3 weights = FarionTriplanarWeights(normalOS);
                float3 axisSign = sign(normalOS);

                half3 normalX = UnpackNormalScale(
                    SAMPLE_TEXTURE2D_ARRAY(_BiomeNormalArray, sampler_BiomeNormalArray, normalizedPosition.zy * scale, layer),
                    strength);
                half3 normalY = UnpackNormalScale(
                    SAMPLE_TEXTURE2D_ARRAY(_BiomeNormalArray, sampler_BiomeNormalArray, normalizedPosition.xz * scale, layer),
                    strength);
                half3 normalZ = UnpackNormalScale(
                    SAMPLE_TEXTURE2D_ARRAY(_BiomeNormalArray, sampler_BiomeNormalArray, normalizedPosition.xy * scale, layer),
                    strength);

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
                half hasOcean = saturate(_HasOcean);
                float oceanRadius = lerp(_RadiusMinMax.x, _BodyRadius, _OceanLevel);
                half aboveOcean01 = FarionRemap01(terrainRadius, oceanRadius, _RadiusMinMax.y);
                half oceanDepth01 = 1.0h - FarionRemap01(terrainRadius, _RadiusMinMax.x, oceanRadius);
                half oceanDistance01 = (terrainRadius - oceanRadius) / heightRange;
                half landWaterBlend = smoothstep(-_OceanEdgeBlend, _OceanEdgeBlend, oceanDistance01);
                half shorelineBand = hasOcean * saturate(1.0h - abs(landWaterBlend * 2.0h - 1.0h));

                half4 texNoise = FarionSampleTriplanarNoise(input.positionOS, normalOS, _NoiseScale);
                half4 texNoise2 = FarionSampleTriplanarNoise(input.positionOS, normalOS, _NoiseScale2);
                half largeNoise = input.terrainData.x;
                half detailNoise = input.terrainData.y;
                half smallNoise = input.terrainData.z;
                half warpedNoise = input.terrainData.w;
                half4 biomeWeightsA = saturate(input.biomeWeightsA);
                half4 biomeWeightsB = saturate(input.biomeWeightsB);

                half biomeMask = 0.0h;
                half biomeTextureMask = 0.0h;
                half biomeNormalStrength = 0.0h;
                half biomeSmoothness = 0.0h;
                half biomeTextureScale = 0.0h;

                half steepness = FarionRemap01(1.0h - dot(normalOS, radialOS), 0.0h, 0.65h);
                half flatHeight01 = FarionRemap01(aboveOcean01, 0.0h, _MaxFlatHeight);

                half flatBlendWeight = FarionBlend(0.0h, _FlatColorBlend, (flatHeight01 - 0.5h) + (texNoise.b - 0.5h) * _FlatColorBlendNoise);
                half3 flatTerrainA = lerp(_FlatLowA.rgb, _FlatHighA.rgb, flatBlendWeight);
                flatTerrainA = lerp(flatTerrainA, (_FlatLowA.rgb + _FlatHighA.rgb) * 0.5h, texNoise.a);
                half3 flatTerrainB = lerp(_FlatLowB.rgb, _FlatHighB.rgb, flatBlendWeight);
                flatTerrainB = lerp(flatTerrainB, (_FlatLowB.rgb + _FlatHighB.rgb) * 0.5h, texNoise.a);

                half biomeWeight = saturate(largeNoise * 0.5h + warpedNoise * 0.35h + texNoise.r * 0.15h);
                half3 flatTerrain = lerp(flatTerrainA, flatTerrainB, biomeWeight);
                half3 biomeFlatTerrain = half3(0.0h, 0.0h, 0.0h);
                half biomeVisualWeight = 0.0h;
                half biomeTextureWeight = 0.0h;

                half shoreBlendWeight = 1.0h - FarionBlend(_ShoreHeight, _ShoreBlend, flatHeight01);
                half3 shoreColor = lerp(_ShoreLow.rgb, _ShoreHigh.rgb, FarionRemap01(aboveOcean01, 0.0h, max(_ShoreHeight, 0.0001h)));
                shoreColor = lerp(shoreColor, (_ShoreLow.rgb + _ShoreHigh.rgb) * 0.5h, texNoise.g);

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
                half3 baseSteepTerrain = steepTerrain;
                half3 biomeSteepTerrain = half3(0.0h, 0.0h, 0.0h);

                [unroll]
                for (int biomeSlot = 0; biomeSlot < 8; biomeSlot++)
                {
                    half weight = FarionGetBiomeWeight(biomeSlot, biomeWeightsA, biomeWeightsB);
                    if (weight <= 0.0001h)
                    {
                        continue;
                    }

                    if (biomeSlot < _BiomeVisualCount)
                    {
                        half3 slotFlatTerrain = lerp(_BiomeFlatLow[biomeSlot].rgb, _BiomeFlatHigh[biomeSlot].rgb, flatBlendWeight);
                        slotFlatTerrain = lerp(slotFlatTerrain, (_BiomeFlatLow[biomeSlot].rgb + _BiomeFlatHigh[biomeSlot].rgb) * 0.5h, texNoise.a);
                        half3 slotSteepTerrain = lerp(_BiomeSteepLow[biomeSlot].rgb, _BiomeSteepHigh[biomeSlot].rgb, saturate(aboveOcean01 + banding));
                        biomeFlatTerrain += slotFlatTerrain * weight;
                        biomeSteepTerrain += slotSteepTerrain * weight;
                        biomeNormalStrength += _BiomeParams[biomeSlot].x * weight;
                        biomeSmoothness += _BiomeParams[biomeSlot].y * weight;
                        biomeVisualWeight += weight;
                    }

                    if (biomeSlot < _BiomeTextureCount)
                    {
                        biomeTextureScale += _BiomeTextureParams[biomeSlot].x * weight;
                        biomeTextureWeight += weight;
                    }
                }

                half shorePreservation = saturate(1.0h - shoreBlendWeight * hasOcean * 0.75h);
                biomeMask = saturate(biomeVisualWeight * _BiomeVisualBlendStrength * shorePreservation);
                biomeTextureMask = saturate(biomeTextureWeight * _BiomeTextureBlendStrength * shorePreservation);
                if (biomeVisualWeight > 0.0001h)
                {
                    biomeFlatTerrain /= biomeVisualWeight;
                    biomeSteepTerrain /= biomeVisualWeight;
                    biomeNormalStrength /= biomeVisualWeight;
                    biomeSmoothness /= biomeVisualWeight;
                    flatTerrain = lerp(flatTerrain, biomeFlatTerrain, biomeMask);
                    steepTerrain = lerp(steepTerrain, biomeSteepTerrain, biomeMask);
                }
                else
                {
                    biomeNormalStrength = _NormalStrength;
                    biomeSmoothness = _LandSmoothness;
                }

                if (biomeTextureWeight > 0.0001h)
                {
                    biomeTextureScale /= biomeTextureWeight;
                }
                else
                {
                    biomeTextureScale = 1.0h;
                }

                half flatBlendNoise = (texNoise2.r - 0.5h) * _FlatToSteepNoise;
                half flatStrength = 1.0h - FarionBlend(_SteepnessThreshold + flatBlendNoise, _FlatToSteepBlend, steepness);
                half flatHeightFalloff = 1.0h - FarionBlend(_MaxFlatHeight + flatBlendNoise, _FlatToSteepBlend, aboveOcean01);
                flatStrength *= flatHeightFalloff;

                half3 landColor = lerp(steepTerrain, flatTerrain, flatStrength);
                if (biomeTextureMask > 0.0h)
                {
                    half3 biomeBaseColor = half3(0.0h, 0.0h, 0.0h);
                    [unroll]
                    for (int biomeSlot = 0; biomeSlot < 8; biomeSlot++)
                    {
                        half weight = FarionGetBiomeWeight(biomeSlot, biomeWeightsA, biomeWeightsB);
                        if (biomeSlot < _BiomeTextureCount)
                        {
                            biomeBaseColor += FarionSampleBiomeBaseColor(input.positionOS, normalOS, biomeTextureScale, biomeSlot).rgb * weight;
                        }
                    }
                    biomeBaseColor /= max(biomeTextureWeight, 0.0001h);
                    half landLuminance = max(dot(landColor, half3(0.2126h, 0.7152h, 0.0722h)), 0.05h);
                    half textureLuminance = max(dot(biomeBaseColor, half3(0.2126h, 0.7152h, 0.0722h)), 0.05h);
                    half3 biomeTexturedColor = saturate(biomeBaseColor * (landLuminance / textureLuminance));
                    landColor = lerp(landColor, biomeTexturedColor, biomeTextureMask * 0.65h);
                }
                landColor *= lerp(0.9h, 1.12h, saturate(smallNoise + detailNoise * 0.2h));
                landColor = lerp(landColor, shoreColor, shoreBlendWeight * hasOcean);
                half wetNoise = saturate(texNoise2.g * 0.65h + texNoise.b * 0.35h);
                half wetShoreMask = shorelineBand * landWaterBlend * _ShoreWetness * lerp(0.72h, 1.0h, wetNoise);
                half3 wetShoreColor = lerp(shoreColor * 0.58h, _OceanHigh.rgb * 0.82h, 0.35h);
                landColor = lerp(landColor, wetShoreColor, wetShoreMask);

                half oceanMask = hasOcean * (1.0h - landWaterBlend);
                half3 seabedColor = lerp(shoreColor * 0.62h, baseSteepTerrain * 0.72h, saturate(oceanDepth01));
                seabedColor *= lerp(0.86h, 1.04h, texNoise.b);
                half3 albedo = lerp(landColor, seabedColor, oceanMask);
                half foamNoise = saturate(texNoise2.r * 0.55h + texNoise2.b * 0.45h);
                half foamMask = shorelineBand * oceanMask * _ShoreFoamStrength * smoothstep(0.42h, 0.9h, foamNoise);
                albedo = lerp(albedo, half3(0.82h, 0.9h, 0.86h), foamMask);

                half3 rockNormalOS = FarionUnpackTriplanarNormalOS(TEXTURE2D_ARGS(_RockNormal, sampler_RockNormal), input.positionOS, normalOS, _RockNormalScale);
                if (biomeTextureMask > 0.0h)
                {
                    half3 biomeNormalOS = half3(0.0h, 0.0h, 0.0h);
                    [unroll]
                    for (int biomeSlot = 0; biomeSlot < 8; biomeSlot++)
                    {
                        half weight = FarionGetBiomeWeight(biomeSlot, biomeWeightsA, biomeWeightsB);
                        if (biomeSlot < _BiomeTextureCount)
                        {
                            biomeNormalOS += FarionUnpackBiomeNormalOS(input.positionOS, normalOS, biomeTextureScale, biomeSlot, biomeNormalStrength) * weight;
                        }
                    }
                    biomeNormalOS = normalize(biomeNormalOS / max(biomeTextureWeight, 0.0001h));
                    rockNormalOS = normalize(lerp(rockNormalOS, biomeNormalOS, biomeTextureMask));
                }
                half3 blendedNormalOS = rockNormalOS;
                blendedNormalOS = normalize(lerp(blendedNormalOS, normalOS, oceanMask * 0.75h));
                half3 normalWS = normalize(TransformObjectToWorldNormal(blendedNormalOS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 starDirectionWS = normalize(_FarionStarPositionWS.xyz - input.positionWS);
                half ndotl = saturate(dot(normalWS, starDirectionWS));
                half3 ambient = max(SampleSH(normalWS), _FarionAmbientColor.rgb);
                half3 starRadiance = _FarionStarColor.rgb * _FarionStarIntensity;
                half shadow = mainLight.shadowAttenuation;
                if (biomeTextureMask > 0.0h)
                {
                    half biomeAmbientOcclusion = 0.0h;
                    [unroll]
                    for (int biomeSlot = 0; biomeSlot < 8; biomeSlot++)
                    {
                        half weight = FarionGetBiomeWeight(biomeSlot, biomeWeightsA, biomeWeightsB);
                        if (biomeSlot < _BiomeTextureCount)
                        {
                            biomeAmbientOcclusion += FarionSampleBiomeAmbientOcclusion(input.positionOS, normalOS, biomeTextureScale, biomeSlot) * weight;
                        }
                    }
                    biomeAmbientOcclusion /= max(biomeTextureWeight, 0.0001h);
                    albedo *= lerp(1.0h, biomeAmbientOcclusion, biomeTextureMask * (1.0h - oceanMask) * 0.5h);
                }
                half3 diffuse = albedo * (ambient + starRadiance * ndotl * shadow);

                half smoothness = lerp(_LandSmoothness, _OceanSmoothness, oceanMask);
                smoothness = lerp(smoothness, biomeSmoothness, biomeMask * (1.0h - oceanMask));
                if (biomeTextureMask > 0.0h)
                {
                    half biomeTextureSmoothness = 0.0h;
                    [unroll]
                    for (int biomeSlot = 0; biomeSlot < 8; biomeSlot++)
                    {
                        half weight = FarionGetBiomeWeight(biomeSlot, biomeWeightsA, biomeWeightsB);
                        if (biomeSlot < _BiomeTextureCount)
                        {
                            biomeTextureSmoothness += (1.0h - FarionSampleBiomeRoughness(input.positionOS, normalOS, biomeTextureScale, biomeSlot)) * weight;
                        }
                    }
                    biomeTextureSmoothness /= max(biomeTextureWeight, 0.0001h);
                    smoothness = lerp(smoothness, biomeTextureSmoothness, biomeTextureMask * (1.0h - oceanMask) * 0.8h);
                }
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
