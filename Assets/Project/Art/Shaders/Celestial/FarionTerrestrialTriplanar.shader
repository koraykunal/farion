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
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_RockNormal);
            SAMPLER(sampler_RockNormal);
            TEXTURE2D_ARRAY(_SurfaceBaseColorArray);
            SAMPLER(sampler_SurfaceBaseColorArray);
            TEXTURE2D_ARRAY(_SurfaceNormalArray);
            SAMPLER(sampler_SurfaceNormalArray);
            TEXTURE2D_ARRAY(_SurfaceRoughnessArray);
            SAMPLER(sampler_SurfaceRoughnessArray);
            TEXTURE2D_ARRAY(_SurfaceAmbientOcclusionArray);
            SAMPLER(sampler_SurfaceAmbientOcclusionArray);
            TEXTURE2D_ARRAY(_SurfaceHeightArray);
            SAMPLER(sampler_SurfaceHeightArray);
            TEXTURE2D_ARRAY(_SurfaceEmissionArray);
            SAMPLER(sampler_SurfaceEmissionArray);
            TEXTURECUBE(_SurfaceWeightsA);
            SAMPLER(sampler_SurfaceWeightsA);
            TEXTURECUBE(_SurfaceWeightsB);
            SAMPLER(sampler_SurfaceWeightsB);
            TEXTURECUBE(_SurfaceStateMap);
            SAMPLER(sampler_SurfaceStateMap);
            TEXTURE2D(_LavaBaseColor);
            SAMPLER(sampler_LavaBaseColor);
            TEXTURE2D(_LavaNormal);
            TEXTURE2D(_LavaRoughness);
            TEXTURE2D(_LavaEmission);
            TEXTURE2D(_SnowBaseColor);
            SAMPLER(sampler_SnowBaseColor);
            TEXTURE2D(_SnowNormal);
            TEXTURE2D(_SnowRoughness);

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
                half _SurfaceVisualCount;
                half _SurfaceVisualBlendStrength;
                half _SurfaceTextureCount;
                half _SurfaceTextureBlendStrength;
                half _SurfaceAmbientOcclusionTextureCount;
                half _SurfaceHeightTextureCount;
                half _SurfaceEmissionTextureCount;
                half4 _SurfaceFlatLow[8];
                half4 _SurfaceFlatHigh[8];
                half4 _SurfaceSteepLow[8];
                half4 _SurfaceSteepHigh[8];
                half4 _SurfaceParams[8];
                half4 _SurfaceTextureParams[8];
                half4 _SurfaceAuxTextureParams[8];
                half4 _SurfaceEmissionTints[8];
                half _SurfaceWeightMapEnabled;
                half _LavaOverlayEnabled;
                float _LavaWorldTileSize;
                half _LavaNormalStrength;
                half4 _LavaEmissionTint;
                half _LavaEmissionStrength;
                half _SnowOverlayEnabled;
                float _SnowWorldTileSize;
                half _SnowNormalStrength;
                float _BodyRadius;
                float4 _RadiusMinMax;
            CBUFFER_END

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
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
                half3 vertexLighting : TEXCOORD5;
#endif
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
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
                output.vertexLighting = VertexLighting(positionInputs.positionWS, output.normalWS);
#endif
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

            half FarionGetSurfaceWeight(int index, half4 weightsA, half4 weightsB)
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

            bool FarionTryGetSurfaceTextureLayer(int surfaceSlot, out int textureLayer)
            {
                half4 textureParams = _SurfaceTextureParams[surfaceSlot];
                textureLayer = (int)round(textureParams.z);
                return textureParams.w > 0.5h
                    && textureLayer >= 0
                    && textureLayer < (int)_SurfaceTextureCount;
            }

            bool FarionTryGetSurfaceAuxTextureLayer(float layerValue, float textureCount, out int textureLayer)
            {
                textureLayer = (int)round(layerValue);
                return textureLayer >= 0
                    && textureLayer < (int)textureCount;
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

            half4 FarionSampleOverlay(
                TEXTURE2D_PARAM(overlayTexture, overlaySampler),
                float3 positionOS,
                float3 normalOS,
                float worldTileSize)
            {
                float3 samplePosition = positionOS / max(worldTileSize, 0.001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half4 x = SAMPLE_TEXTURE2D(overlayTexture, overlaySampler, samplePosition.zy);
                half4 y = SAMPLE_TEXTURE2D(overlayTexture, overlaySampler, samplePosition.xz);
                half4 z = SAMPLE_TEXTURE2D(overlayTexture, overlaySampler, samplePosition.xy);
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half3 FarionUnpackOverlayNormalOS(
                TEXTURE2D_PARAM(normalTexture, normalSampler),
                float3 positionOS,
                float3 normalOS,
                float worldTileSize,
                half strength)
            {
                float3 samplePosition = positionOS / max(worldTileSize, 0.001);
                float3 weights = FarionTriplanarWeights(normalOS);
                float3 axisSign = sign(normalOS);
                half3 normalX = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(normalTexture, normalSampler, samplePosition.zy),
                    strength);
                half3 normalY = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(normalTexture, normalSampler, samplePosition.xz),
                    strength);
                half3 normalZ = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(normalTexture, normalSampler, samplePosition.xy),
                    strength);
                normalX = half3(normalX.z * axisSign.x, normalX.y, normalX.x);
                normalY = half3(normalY.x, normalY.z * axisSign.y, normalY.y);
                normalZ = half3(normalZ.x, normalZ.y, normalZ.z * axisSign.z);
                return normalize(normalX * weights.x + normalY * weights.y + normalZ * weights.z);
            }

            half4 FarionSampleSurfaceBaseColor(float3 positionOS, float3 normalOS, float worldTileSize, int layer)
            {
                float3 samplePosition = positionOS / max(worldTileSize, 0.001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half4 x = SAMPLE_TEXTURE2D_ARRAY(_SurfaceBaseColorArray, sampler_SurfaceBaseColorArray, samplePosition.zy, layer);
                half4 y = SAMPLE_TEXTURE2D_ARRAY(_SurfaceBaseColorArray, sampler_SurfaceBaseColorArray, samplePosition.xz, layer);
                half4 z = SAMPLE_TEXTURE2D_ARRAY(_SurfaceBaseColorArray, sampler_SurfaceBaseColorArray, samplePosition.xy, layer);
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half FarionSampleSurfaceRoughness(float3 positionOS, float3 normalOS, float worldTileSize, int layer)
            {
                float3 samplePosition = positionOS / max(worldTileSize, 0.001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half x = SAMPLE_TEXTURE2D_ARRAY(_SurfaceRoughnessArray, sampler_SurfaceRoughnessArray, samplePosition.zy, layer).r;
                half y = SAMPLE_TEXTURE2D_ARRAY(_SurfaceRoughnessArray, sampler_SurfaceRoughnessArray, samplePosition.xz, layer).r;
                half z = SAMPLE_TEXTURE2D_ARRAY(_SurfaceRoughnessArray, sampler_SurfaceRoughnessArray, samplePosition.xy, layer).r;
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half FarionSampleSurfaceAmbientOcclusion(float3 positionOS, float3 normalOS, float worldTileSize, int layer)
            {
                float3 samplePosition = positionOS / max(worldTileSize, 0.001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half x = SAMPLE_TEXTURE2D_ARRAY(_SurfaceAmbientOcclusionArray, sampler_SurfaceAmbientOcclusionArray, samplePosition.zy, layer).r;
                half y = SAMPLE_TEXTURE2D_ARRAY(_SurfaceAmbientOcclusionArray, sampler_SurfaceAmbientOcclusionArray, samplePosition.xz, layer).r;
                half z = SAMPLE_TEXTURE2D_ARRAY(_SurfaceAmbientOcclusionArray, sampler_SurfaceAmbientOcclusionArray, samplePosition.xy, layer).r;
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half FarionSampleSurfaceHeight(float3 positionOS, float3 normalOS, float worldTileSize, int layer)
            {
                float3 samplePosition = positionOS / max(worldTileSize, 0.001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half x = SAMPLE_TEXTURE2D_ARRAY(_SurfaceHeightArray, sampler_SurfaceHeightArray, samplePosition.zy, layer).r;
                half y = SAMPLE_TEXTURE2D_ARRAY(_SurfaceHeightArray, sampler_SurfaceHeightArray, samplePosition.xz, layer).r;
                half z = SAMPLE_TEXTURE2D_ARRAY(_SurfaceHeightArray, sampler_SurfaceHeightArray, samplePosition.xy, layer).r;
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half3 FarionSampleSurfaceEmission(float3 positionOS, float3 normalOS, float worldTileSize, int layer)
            {
                float3 samplePosition = positionOS / max(worldTileSize, 0.001);
                float3 weights = FarionTriplanarWeights(normalOS);
                half3 x = SAMPLE_TEXTURE2D_ARRAY(_SurfaceEmissionArray, sampler_SurfaceEmissionArray, samplePosition.zy, layer).rgb;
                half3 y = SAMPLE_TEXTURE2D_ARRAY(_SurfaceEmissionArray, sampler_SurfaceEmissionArray, samplePosition.xz, layer).rgb;
                half3 z = SAMPLE_TEXTURE2D_ARRAY(_SurfaceEmissionArray, sampler_SurfaceEmissionArray, samplePosition.xy, layer).rgb;
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

            half3 FarionUnpackSurfaceNormalOS(
                float3 positionOS,
                float3 normalOS,
                float worldTileSize,
                int layer,
                half strength)
            {
                float3 samplePosition = positionOS / max(worldTileSize, 0.001);
                float3 weights = FarionTriplanarWeights(normalOS);
                float3 axisSign = sign(normalOS);

                half3 normalX = UnpackNormalScale(
                    SAMPLE_TEXTURE2D_ARRAY(_SurfaceNormalArray, sampler_SurfaceNormalArray, samplePosition.zy, layer),
                    strength);
                half3 normalY = UnpackNormalScale(
                    SAMPLE_TEXTURE2D_ARRAY(_SurfaceNormalArray, sampler_SurfaceNormalArray, samplePosition.xz, layer),
                    strength);
                half3 normalZ = UnpackNormalScale(
                    SAMPLE_TEXTURE2D_ARRAY(_SurfaceNormalArray, sampler_SurfaceNormalArray, samplePosition.xy, layer),
                    strength);

                normalX = half3(normalX.z * axisSign.x, normalX.y, normalX.x);
                normalY = half3(normalY.x, normalY.z * axisSign.y, normalY.y);
                normalZ = half3(normalZ.x, normalZ.y, normalZ.z * axisSign.z);

                return normalize(normalX * weights.x + normalY * weights.y + normalZ * weights.z);
            }

            InputData FarionBuildPbrInputData(Varyings input, half3 normalWS)
            {
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionHCS;
                inputData.normalWS = NormalizeNormalPerPixel(normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
                inputData.vertexLighting = input.vertexLighting;
#endif
                inputData.bakedGI = max(SampleSH(inputData.normalWS), _FarionAmbientColor.rgb);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(float2(0.0, 0.0));
                return inputData;
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
                half4 surfaceWeightsA = half4(0.0h, 0.0h, 0.0h, 0.0h);
                half4 surfaceWeightsB = half4(0.0h, 0.0h, 0.0h, 0.0h);
                half3 surfaceState = half3(0.0h, 0.0h, 0.0h);
                if (_SurfaceWeightMapEnabled > 0.5h)
                {
                    surfaceWeightsA = saturate(SAMPLE_TEXTURECUBE(
                        _SurfaceWeightsA,
                        sampler_SurfaceWeightsA,
                        radialOS));
                    surfaceWeightsB = saturate(SAMPLE_TEXTURECUBE(
                        _SurfaceWeightsB,
                        sampler_SurfaceWeightsB,
                        radialOS));
                    surfaceState = saturate(SAMPLE_TEXTURECUBE(
                        _SurfaceStateMap,
                        sampler_SurfaceStateMap,
                        radialOS).rgb);
                }

                half surfaceMask = 0.0h;
                half surfaceTextureMask = 0.0h;
                half surfaceSmoothness = 0.0h;
                half lavaMask = 0.0h;
                half snowMask = 0.0h;
                half wetnessMask = 0.0h;
                half lavaRoughness = 1.0h;
                half snowRoughness = 1.0h;

                half steepness = FarionRemap01(1.0h - dot(normalOS, radialOS), 0.0h, 0.65h);
                half flatHeight01 = FarionRemap01(aboveOcean01, 0.0h, _MaxFlatHeight);

                half flatBlendWeight = FarionBlend(0.0h, _FlatColorBlend, (flatHeight01 - 0.5h) + (texNoise.b - 0.5h) * _FlatColorBlendNoise);
                half3 flatTerrainA = lerp(_FlatLowA.rgb, _FlatHighA.rgb, flatBlendWeight);
                flatTerrainA = lerp(flatTerrainA, (_FlatLowA.rgb + _FlatHighA.rgb) * 0.5h, texNoise.a);
                half3 flatTerrainB = lerp(_FlatLowB.rgb, _FlatHighB.rgb, flatBlendWeight);
                flatTerrainB = lerp(flatTerrainB, (_FlatLowB.rgb + _FlatHighB.rgb) * 0.5h, texNoise.a);

                half surfaceWeight = saturate(largeNoise * 0.5h + warpedNoise * 0.35h + texNoise.r * 0.15h);
                half3 flatTerrain = lerp(flatTerrainA, flatTerrainB, surfaceWeight);
                half3 surfaceFlatTerrain = half3(0.0h, 0.0h, 0.0h);
                half surfaceVisualWeight = 0.0h;
                half surfaceTextureWeight = 0.0h;

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
                half3 surfaceSteepTerrain = half3(0.0h, 0.0h, 0.0h);

                [unroll]
                for (int surfaceSlot = 0; surfaceSlot < 8; surfaceSlot++)
                {
                    half weight = FarionGetSurfaceWeight(surfaceSlot, surfaceWeightsA, surfaceWeightsB);
                    if (weight <= 0.0001h)
                    {
                        continue;
                    }

                    if (surfaceSlot < _SurfaceVisualCount)
                    {
                        half3 slotFlatTerrain = lerp(_SurfaceFlatLow[surfaceSlot].rgb, _SurfaceFlatHigh[surfaceSlot].rgb, flatBlendWeight);
                        slotFlatTerrain = lerp(slotFlatTerrain, (_SurfaceFlatLow[surfaceSlot].rgb + _SurfaceFlatHigh[surfaceSlot].rgb) * 0.5h, texNoise.a);
                        half3 slotSteepTerrain = lerp(_SurfaceSteepLow[surfaceSlot].rgb, _SurfaceSteepHigh[surfaceSlot].rgb, saturate(aboveOcean01 + banding));
                        surfaceFlatTerrain += slotFlatTerrain * weight;
                        surfaceSteepTerrain += slotSteepTerrain * weight;
                        surfaceSmoothness += _SurfaceParams[surfaceSlot].y * weight;
                        surfaceVisualWeight += weight;
                    }

                    int textureLayer;
                    if (FarionTryGetSurfaceTextureLayer(surfaceSlot, textureLayer))
                    {
                        surfaceTextureWeight += weight;
                    }
                }

                half shorePreservation = saturate(1.0h - shoreBlendWeight * hasOcean * 0.75h);
                surfaceMask = saturate(surfaceVisualWeight * _SurfaceVisualBlendStrength * shorePreservation);
                surfaceTextureMask = saturate(surfaceTextureWeight * _SurfaceTextureBlendStrength * shorePreservation);
                if (surfaceVisualWeight > 0.0001h)
                {
                    surfaceFlatTerrain /= surfaceVisualWeight;
                    surfaceSteepTerrain /= surfaceVisualWeight;
                    surfaceSmoothness /= surfaceVisualWeight;
                    flatTerrain = lerp(flatTerrain, surfaceFlatTerrain, surfaceMask);
                    steepTerrain = lerp(steepTerrain, surfaceSteepTerrain, surfaceMask);
                }
                else
                {
                    surfaceSmoothness = _LandSmoothness;
                }

                half flatBlendNoise = (texNoise2.r - 0.5h) * _FlatToSteepNoise;
                half flatStrength = 1.0h - FarionBlend(_SteepnessThreshold + flatBlendNoise, _FlatToSteepBlend, steepness);
                half flatHeightFalloff = 1.0h - FarionBlend(_MaxFlatHeight + flatBlendNoise, _FlatToSteepBlend, aboveOcean01);
                flatStrength *= flatHeightFalloff;

                half3 landColor = lerp(steepTerrain, flatTerrain, flatStrength);
                if (surfaceTextureMask > 0.0h)
                {
                    half3 surfaceBaseColor = half3(0.0h, 0.0h, 0.0h);
                    [unroll]
                    for (int surfaceSlot = 0; surfaceSlot < 8; surfaceSlot++)
                    {
                        half weight = FarionGetSurfaceWeight(surfaceSlot, surfaceWeightsA, surfaceWeightsB);
                        if (weight <= 0.0001h)
                        {
                            continue;
                        }

                        int textureLayer;
                        if (FarionTryGetSurfaceTextureLayer(surfaceSlot, textureLayer))
                        {
                            surfaceBaseColor += FarionSampleSurfaceBaseColor(
                                input.positionOS,
                                normalOS,
                                _SurfaceTextureParams[surfaceSlot].x,
                                textureLayer).rgb * weight;
                        }
                    }
                    surfaceBaseColor /= max(surfaceTextureWeight, 0.0001h);
                    half landLuminance = max(dot(landColor, half3(0.2126h, 0.7152h, 0.0722h)), 0.05h);
                    half textureLuminance = max(dot(surfaceBaseColor, half3(0.2126h, 0.7152h, 0.0722h)), 0.05h);
                    half3 surfaceTexturedColor = saturate(surfaceBaseColor * (landLuminance / textureLuminance));
                    landColor = lerp(landColor, surfaceTexturedColor, surfaceTextureMask * 0.65h);
                }

                if (_SurfaceHeightTextureCount > 0.5h)
                {
                    half surfaceHeight = 0.0h;
                    half surfaceHeightStrength = 0.0h;
                    half surfaceHeightWeight = 0.0h;
                    [unroll]
                    for (int surfaceSlot = 0; surfaceSlot < 8; surfaceSlot++)
                    {
                        half weight = FarionGetSurfaceWeight(surfaceSlot, surfaceWeightsA, surfaceWeightsB);
                        if (weight <= 0.0001h)
                        {
                            continue;
                        }

                        int textureLayer;
                        if (FarionTryGetSurfaceAuxTextureLayer(
                            _SurfaceAuxTextureParams[surfaceSlot].y,
                            _SurfaceHeightTextureCount,
                            textureLayer))
                        {
                            surfaceHeight += FarionSampleSurfaceHeight(
                                input.positionOS,
                                normalOS,
                                _SurfaceTextureParams[surfaceSlot].x,
                                textureLayer) * weight;
                            surfaceHeightStrength += _SurfaceTextureParams[surfaceSlot].y * weight;
                            surfaceHeightWeight += weight;
                        }
                    }

                    if (surfaceHeightWeight > 0.0001h)
                    {
                        surfaceHeight /= surfaceHeightWeight;
                        surfaceHeightStrength /= surfaceHeightWeight;
                        half heightMask = saturate(
                            surfaceHeightWeight
                            * _SurfaceVisualBlendStrength
                            * shorePreservation);
                        half microRelief = (surfaceHeight - 0.5h)
                            * saturate(surfaceHeightStrength * 8.0h)
                            * heightMask;
                        landColor *= max(0.0h, 1.0h + microRelief * 0.35h);
                    }
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

                half landOverlayMask = 1.0h - oceanMask;
                lavaMask = surfaceState.r * landOverlayMask * saturate(_LavaOverlayEnabled);
                snowMask = surfaceState.g
                    * landOverlayMask
                    * saturate(_SnowOverlayEnabled)
                    * (1.0h - lavaMask);
                wetnessMask = surfaceState.b * landOverlayMask * (1.0h - snowMask) * (1.0h - lavaMask);

                if (lavaMask > 0.0001h)
                {
                    half3 lavaColor = FarionSampleOverlay(
                        TEXTURE2D_ARGS(_LavaBaseColor, sampler_LavaBaseColor),
                        input.positionOS,
                        normalOS,
                        _LavaWorldTileSize).rgb;
                    lavaRoughness = FarionSampleOverlay(
                        TEXTURE2D_ARGS(_LavaRoughness, sampler_LavaBaseColor),
                        input.positionOS,
                        normalOS,
                        _LavaWorldTileSize).r;
                    albedo = lerp(albedo, lavaColor, lavaMask);
                }

                if (snowMask > 0.0001h)
                {
                    half3 snowColor = FarionSampleOverlay(
                        TEXTURE2D_ARGS(_SnowBaseColor, sampler_SnowBaseColor),
                        input.positionOS,
                        normalOS,
                        _SnowWorldTileSize).rgb;
                    snowRoughness = FarionSampleOverlay(
                        TEXTURE2D_ARGS(_SnowRoughness, sampler_SnowBaseColor),
                        input.positionOS,
                        normalOS,
                        _SnowWorldTileSize).r;
                    albedo = lerp(albedo, snowColor, snowMask);
                }

                albedo *= lerp(1.0h, 0.68h, wetnessMask * 0.55h);

                half3 rockNormalOS = FarionUnpackTriplanarNormalOS(TEXTURE2D_ARGS(_RockNormal, sampler_RockNormal), input.positionOS, normalOS, _RockNormalScale);
                if (surfaceTextureMask > 0.0h)
                {
                    half3 surfaceNormalOS = half3(0.0h, 0.0h, 0.0h);
                    [unroll]
                    for (int surfaceSlot = 0; surfaceSlot < 8; surfaceSlot++)
                    {
                        half weight = FarionGetSurfaceWeight(surfaceSlot, surfaceWeightsA, surfaceWeightsB);
                        if (weight <= 0.0001h)
                        {
                            continue;
                        }

                        int textureLayer;
                        if (FarionTryGetSurfaceTextureLayer(surfaceSlot, textureLayer))
                        {
                            surfaceNormalOS += FarionUnpackSurfaceNormalOS(
                                input.positionOS,
                                normalOS,
                                _SurfaceTextureParams[surfaceSlot].x,
                                textureLayer,
                                _SurfaceParams[surfaceSlot].x) * weight;
                        }
                    }
                    surfaceNormalOS = normalize(surfaceNormalOS / max(surfaceTextureWeight, 0.0001h));
                    rockNormalOS = normalize(lerp(rockNormalOS, surfaceNormalOS, surfaceTextureMask));
                }
                if (lavaMask > 0.0001h)
                {
                    half3 lavaNormalOS = FarionUnpackOverlayNormalOS(
                        TEXTURE2D_ARGS(_LavaNormal, sampler_LavaBaseColor),
                        input.positionOS,
                        normalOS,
                        _LavaWorldTileSize,
                        _LavaNormalStrength);
                    rockNormalOS = normalize(lerp(rockNormalOS, lavaNormalOS, lavaMask));
                }
                if (snowMask > 0.0001h)
                {
                    half3 snowNormalOS = FarionUnpackOverlayNormalOS(
                        TEXTURE2D_ARGS(_SnowNormal, sampler_SnowBaseColor),
                        input.positionOS,
                        normalOS,
                        _SnowWorldTileSize,
                        _SnowNormalStrength);
                    rockNormalOS = normalize(lerp(rockNormalOS, snowNormalOS, snowMask));
                }
                half3 blendedNormalOS = rockNormalOS;
                blendedNormalOS = normalize(lerp(blendedNormalOS, normalOS, oceanMask * 0.75h));
                half3 normalWS = normalize(TransformObjectToWorldNormal(blendedNormalOS));

                half surfaceOcclusion = 1.0h;
                if (_SurfaceAmbientOcclusionTextureCount > 0.5h)
                {
                    half surfaceAmbientOcclusion = 0.0h;
                    half surfaceAmbientOcclusionWeight = 0.0h;
                    [unroll]
                    for (int surfaceSlot = 0; surfaceSlot < 8; surfaceSlot++)
                    {
                        half weight = FarionGetSurfaceWeight(surfaceSlot, surfaceWeightsA, surfaceWeightsB);
                        if (weight <= 0.0001h)
                        {
                            continue;
                        }

                        int textureLayer;
                        if (FarionTryGetSurfaceAuxTextureLayer(
                            _SurfaceAuxTextureParams[surfaceSlot].x,
                            _SurfaceAmbientOcclusionTextureCount,
                            textureLayer))
                        {
                            surfaceAmbientOcclusion += FarionSampleSurfaceAmbientOcclusion(
                                input.positionOS,
                                normalOS,
                                _SurfaceTextureParams[surfaceSlot].x,
                                textureLayer) * weight;
                            surfaceAmbientOcclusionWeight += weight;
                        }
                    }

                    if (surfaceAmbientOcclusionWeight > 0.0001h)
                    {
                        surfaceAmbientOcclusion /= surfaceAmbientOcclusionWeight;
                        half ambientOcclusionMask = saturate(
                            surfaceAmbientOcclusionWeight
                            * _SurfaceVisualBlendStrength
                            * shorePreservation);
                        surfaceOcclusion = lerp(
                            1.0h,
                            surfaceAmbientOcclusion,
                            ambientOcclusionMask * (1.0h - oceanMask) * 0.5h);
                    }
                }

                half smoothness = lerp(_LandSmoothness, _OceanSmoothness, oceanMask);
                smoothness = lerp(smoothness, surfaceSmoothness, surfaceMask * (1.0h - oceanMask));
                if (surfaceTextureMask > 0.0h)
                {
                    half surfaceTextureSmoothness = 0.0h;
                    [unroll]
                    for (int surfaceSlot = 0; surfaceSlot < 8; surfaceSlot++)
                    {
                        half weight = FarionGetSurfaceWeight(surfaceSlot, surfaceWeightsA, surfaceWeightsB);
                        if (weight <= 0.0001h)
                        {
                            continue;
                        }

                        int textureLayer;
                        if (FarionTryGetSurfaceTextureLayer(surfaceSlot, textureLayer))
                        {
                            surfaceTextureSmoothness += (1.0h - FarionSampleSurfaceRoughness(
                                input.positionOS,
                                normalOS,
                                _SurfaceTextureParams[surfaceSlot].x,
                                textureLayer)) * weight;
                        }
                    }
                    surfaceTextureSmoothness /= max(surfaceTextureWeight, 0.0001h);
                    smoothness = lerp(smoothness, surfaceTextureSmoothness, surfaceTextureMask * (1.0h - oceanMask) * 0.8h);
                }
                smoothness = lerp(smoothness, 1.0h - lavaRoughness, lavaMask);
                smoothness = lerp(smoothness, 1.0h - snowRoughness, snowMask);
                smoothness = lerp(smoothness, max(smoothness, 0.72h), wetnessMask * 0.65h);

                half3 surfaceEmission = half3(0.0h, 0.0h, 0.0h);
                if (_SurfaceEmissionTextureCount > 0.5h)
                {
                    half surfaceEmissionWeight = 0.0h;
                    [unroll]
                    for (int surfaceSlot = 0; surfaceSlot < 8; surfaceSlot++)
                    {
                        half weight = FarionGetSurfaceWeight(surfaceSlot, surfaceWeightsA, surfaceWeightsB);
                        if (weight <= 0.0001h)
                        {
                            continue;
                        }

                        int textureLayer;
                        if (FarionTryGetSurfaceAuxTextureLayer(
                            _SurfaceAuxTextureParams[surfaceSlot].z,
                            _SurfaceEmissionTextureCount,
                            textureLayer))
                        {
                            surfaceEmission += FarionSampleSurfaceEmission(
                                input.positionOS,
                                normalOS,
                                _SurfaceTextureParams[surfaceSlot].x,
                                textureLayer)
                                * _SurfaceEmissionTints[surfaceSlot].rgb
                                * _SurfaceAuxTextureParams[surfaceSlot].w
                                * weight;
                            surfaceEmissionWeight += weight;
                        }
                    }

                    if (surfaceEmissionWeight > 0.0001h)
                    {
                        surfaceEmission /= surfaceEmissionWeight;
                        half emissionMask = saturate(
                            surfaceEmissionWeight
                            * _SurfaceVisualBlendStrength
                            * shorePreservation);
                        surfaceEmission *= emissionMask * (1.0h - oceanMask);
                    }
                }
                if (lavaMask > 0.0001h && _LavaEmissionStrength > 0.0h)
                {
                    half3 lavaEmission = FarionSampleOverlay(
                        TEXTURE2D_ARGS(_LavaEmission, sampler_LavaBaseColor),
                        input.positionOS,
                        normalOS,
                        _LavaWorldTileSize).rgb;
                    surfaceEmission += lavaEmission
                        * _LavaEmissionTint.rgb
                        * _LavaEmissionStrength
                        * lavaMask;
                }

                InputData inputData = FarionBuildPbrInputData(input, normalWS);
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = saturate(albedo);
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.metallic = saturate(_Metallic);
                surfaceData.smoothness = saturate(smoothness);
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
                surfaceData.emission = surfaceEmission;
                surfaceData.occlusion = saturate(surfaceOcclusion);
                surfaceData.alpha = 1.0h;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 1.0h;

                return UniversalFragmentPBR(inputData, surfaceData);
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
