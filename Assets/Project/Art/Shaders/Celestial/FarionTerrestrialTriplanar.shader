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
        _RockNormalTileSize("Rock Normal Tile Size", Float) = 10
        _NormalStrength("Normal Strength", Range(0, 1)) = 0.5

        [Header(Detiling)]
        [ToggleUI] _StochasticTiling("Stochastic Tiling", Float) = 1
        _MacroVariation("Macro Variation", Range(0, 0.6)) = 0.15
        _SurfaceTextureLevelMatch("Surface Texture Level Match", Range(0, 1)) = 0.65
        _SurfaceWeightWarp("Surface Weight Warp", Range(0, 0.1)) = 0.03

        [Header(Blending)]
        _OceanLevel("Ocean Level", Range(0, 1)) = 1
        _HasOcean("Has Ocean", Float) = 1
        _FlatColorBlend("Flat Color Blend", Range(0, 3)) = 1.5
        _FlatColorBlendNoise("Flat Color Blend Noise", Range(0, 1)) = 0.3
        _ShoreHeight("Shore Height", Range(0, 0.25)) = 0.058
        _ShoreBlend("Shore Blend", Range(0, 0.25)) = 0.089
        _OceanEdgeBlend("Ocean Edge Blend", Range(0.001, 0.12)) = 0.035
        _ShoreWetness("Shore Wetness", Range(0, 1)) = 0.38
        _MaxFlatHeight("Max Flat Height", Range(0, 1)) = 0.52
        _SteepBands("Steep Bands", Range(1, 20)) = 8
        _SteepBandStrength("Steep Band Strength", Range(-1, 1)) = 0.5
        _SteepnessThreshold("Steepness Threshold", Range(0, 1)) = 0.378
        _FlatToSteepBlend("Flat To Steep Blend", Range(0, 0.3)) = 0.051
        _FlatToSteepNoise("Flat To Steep Noise", Range(0, 0.2)) = 0.026

        [Header(Surface)]
        _SpecularAntialiasing("Specular Antialiasing", Range(0, 1)) = 0.6
        _AmbientHemisphere("Ambient Hemisphere", Range(0, 1)) = 0.65
        _Metallic("Metallic", Range(0, 1)) = 0
        _LandSmoothness("Land Smoothness", Range(0, 1)) = 0.2
        _BodyRadius("Body Radius", Float) = 1
        _RadiusMinMax("Radius Min Max", Vector) = (1, 1, 0, 0)
        _FarNormalFadeStart("Far Normal Fade Start", Float) = 10000000
        _FarNormalFadeEnd("Far Normal Fade End", Float) = 20000000
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

            #define FARION_STOCHASTIC_SHARPNESS 7.0

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_RockNormal);
            SAMPLER(sampler_RockNormal);
            TEXTURE2D_ARRAY(_SurfaceBaseColorArray);
            SAMPLER(sampler_SurfaceBaseColorArray);
            TEXTURE2D_ARRAY(_SurfaceNormalArray);
            TEXTURE2D_ARRAY(_SurfaceRoughnessArray);
            TEXTURE2D_ARRAY(_SurfaceAmbientOcclusionArray);
            TEXTURE2D_ARRAY(_SurfaceHeightArray);
            TEXTURE2D_ARRAY(_SurfaceEmissionArray);
            TEXTURECUBE(_SurfaceNormalMap);
            TEXTURECUBE(_SurfaceWeightsA);
            TEXTURECUBE(_SurfaceWeightsB);
            TEXTURECUBE(_SurfaceStateMap);
            TEXTURE2D(_LavaBaseColor);
            TEXTURE2D(_LavaNormal);
            TEXTURE2D(_LavaRoughness);
            TEXTURE2D(_LavaEmission);
            TEXTURE2D(_SnowBaseColor);
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
                float _RockNormalTileSize;
                half _NormalStrength;
                half _OceanLevel;
                half _HasOcean;
                half _FlatColorBlend;
                half _FlatColorBlendNoise;
                half _ShoreHeight;
                half _ShoreBlend;
                half _OceanEdgeBlend;
                half _ShoreWetness;
                half _MaxFlatHeight;
                half _SteepBands;
                half _SteepBandStrength;
                half _SteepnessThreshold;
                half _FlatToSteepBlend;
                half _FlatToSteepNoise;
                half _StochasticTiling;
                half _MacroVariation;
                half _SurfaceTextureLevelMatch;
                half _SurfaceWeightWarp;
                half _SpecularAntialiasing;
                half _AmbientHemisphere;
                half _Metallic;
                half _LandSmoothness;
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
                half _SurfaceNormalMapEnabled;
                float _FarNormalFadeStart;
                float _FarNormalFadeEnd;
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

            #include "FarionSurfaceGeomorph.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 texcoord : TEXCOORD0;
                float4 morphOffsetOS : TEXCOORD1;
                float4 morphNormalOS : TEXCOORD2;
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
                float morphWeight = FarionResolveMorphWeight(input.positionOS.xyz);
                float3 positionOS = input.positionOS.xyz +
                    input.morphOffsetOS.xyz * morphWeight;
                float3 normalOS = FarionApplyGeomorphNormal(
                    input.normalOS,
                    input.morphNormalOS.xyz,
                    morphWeight);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = positionOS;
                output.normalOS = normalOS;
                output.normalWS = TransformObjectToWorldNormal(normalOS);
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

            struct FarionTriplanarFrame
            {
                float3 position;
                float3 positionDdx;
                float3 positionDdy;
                float3 weights;
                float3 axisSign;
            };

            struct FarionAxisUV
            {
                float2 uv;
                float2 ddxUV;
                float2 ddyUV;
                float3 cellWeights;
                float2 cellOffset0;
                float2 cellOffset1;
                float2 cellOffset2;
                bool stochastic;
            };

            float2 FarionStochasticHash(float2 cell)
            {
                return frac(sin(float2(
                    dot(cell, float2(127.1, 311.7)),
                    dot(cell, float2(269.5, 183.3)))) * 43758.5453);
            }

            void FarionResolveStochasticCells(
                float2 uv,
                out float3 weights,
                out float2 offset0,
                out float2 offset1,
                out float2 offset2)
            {
                const float2x2 toSkewed = float2x2(1.0, -0.57735027, 0.0, 1.15470054);
                float2 skewed = mul(toSkewed, uv);
                float2 baseCell = floor(skewed);
                float3 barycentric = float3(frac(skewed), 0.0);
                barycentric.z = 1.0 - barycentric.x - barycentric.y;

                float2 cell0;
                float2 cell1;
                float2 cell2;
                if (barycentric.z > 0.0)
                {
                    weights = float3(barycentric.z, barycentric.y, barycentric.x);
                    cell0 = baseCell;
                    cell1 = baseCell + float2(0.0, 1.0);
                    cell2 = baseCell + float2(1.0, 0.0);
                }
                else
                {
                    weights = float3(-barycentric.z, 1.0 - barycentric.y, 1.0 - barycentric.x);
                    cell0 = baseCell + float2(1.0, 1.0);
                    cell1 = baseCell + float2(1.0, 0.0);
                    cell2 = baseCell + float2(0.0, 1.0);
                }

                weights = pow(weights, FARION_STOCHASTIC_SHARPNESS);
                weights /= max(weights.x + weights.y + weights.z, 0.0001);

                offset0 = FarionStochasticHash(cell0);
                offset1 = FarionStochasticHash(cell1);
                offset2 = FarionStochasticHash(cell2);
            }

            FarionTriplanarFrame FarionBuildTriplanarFrame(float3 positionOS, float3 normalOS)
            {
                FarionTriplanarFrame frame;
                frame.position = positionOS;
                frame.positionDdx = ddx(positionOS);
                frame.positionDdy = ddy(positionOS);
                frame.weights = FarionTriplanarWeights(normalOS);
                frame.axisSign = sign(normalOS);
                return frame;
            }

            FarionAxisUV FarionBuildAxisUV(
                float2 position,
                float2 positionDdx,
                float2 positionDdy,
                float inverseTileSize,
                bool stochastic)
            {
                FarionAxisUV axis;
                axis.uv = position * inverseTileSize;
                axis.ddxUV = positionDdx * inverseTileSize;
                axis.ddyUV = positionDdy * inverseTileSize;
                axis.cellWeights = float3(1.0, 0.0, 0.0);
                axis.cellOffset0 = float2(0.0, 0.0);
                axis.cellOffset1 = float2(0.0, 0.0);
                axis.cellOffset2 = float2(0.0, 0.0);
                axis.stochastic = stochastic && _StochasticTiling > 0.5h;
                if (axis.stochastic)
                {
                    FarionResolveStochasticCells(
                        axis.uv,
                        axis.cellWeights,
                        axis.cellOffset0,
                        axis.cellOffset1,
                        axis.cellOffset2);
                }

                return axis;
            }

            #define FARION_AXIS_X(frame, invTile, stoch) FarionBuildAxisUV( \
                frame.position.zy, frame.positionDdx.zy, frame.positionDdy.zy, invTile, stoch)
            #define FARION_AXIS_Y(frame, invTile, stoch) FarionBuildAxisUV( \
                frame.position.xz, frame.positionDdx.xz, frame.positionDdy.xz, invTile, stoch)
            #define FARION_AXIS_Z(frame, invTile, stoch) FarionBuildAxisUV( \
                frame.position.xy, frame.positionDdx.xy, frame.positionDdy.xy, invTile, stoch)

            #define FARION_SAMPLE_2D(t, s, axis) ( \
                axis.stochastic \
                    ? SAMPLE_TEXTURE2D_GRAD(t, s, axis.uv + axis.cellOffset0, axis.ddxUV, axis.ddyUV) * axis.cellWeights.x \
                    + SAMPLE_TEXTURE2D_GRAD(t, s, axis.uv + axis.cellOffset1, axis.ddxUV, axis.ddyUV) * axis.cellWeights.y \
                    + SAMPLE_TEXTURE2D_GRAD(t, s, axis.uv + axis.cellOffset2, axis.ddxUV, axis.ddyUV) * axis.cellWeights.z \
                    : SAMPLE_TEXTURE2D_GRAD(t, s, axis.uv, axis.ddxUV, axis.ddyUV))

            #define FARION_SAMPLE_ARRAY(t, s, axis, layer) ( \
                axis.stochastic \
                    ? SAMPLE_TEXTURE2D_ARRAY_GRAD(t, s, axis.uv + axis.cellOffset0, layer, axis.ddxUV, axis.ddyUV) * axis.cellWeights.x \
                    + SAMPLE_TEXTURE2D_ARRAY_GRAD(t, s, axis.uv + axis.cellOffset1, layer, axis.ddxUV, axis.ddyUV) * axis.cellWeights.y \
                    + SAMPLE_TEXTURE2D_ARRAY_GRAD(t, s, axis.uv + axis.cellOffset2, layer, axis.ddxUV, axis.ddyUV) * axis.cellWeights.z \
                    : SAMPLE_TEXTURE2D_ARRAY_GRAD(t, s, axis.uv, layer, axis.ddxUV, axis.ddyUV))

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

            half3 FarionBlendTriplanarNormal(
                FarionTriplanarFrame frame,
                half3 normalX,
                half3 normalY,
                half3 normalZ)
            {
                normalX = half3(normalX.z * frame.axisSign.x, normalX.y, normalX.x);
                normalY = half3(normalY.x, normalY.z * frame.axisSign.y, normalY.y);
                normalZ = half3(normalZ.x, normalZ.y, normalZ.z * frame.axisSign.z);
                return normalize(
                    normalX * frame.weights.x + normalY * frame.weights.y + normalZ * frame.weights.z);
            }

            half4 FarionSampleTriplanarNoise(FarionTriplanarFrame frame, float scale)
            {
                float inverseTileSize = scale / max(_BodyRadius, 0.0001);
                FarionAxisUV axisX = FARION_AXIS_X(frame, inverseTileSize, false);
                FarionAxisUV axisY = FARION_AXIS_Y(frame, inverseTileSize, false);
                FarionAxisUV axisZ = FARION_AXIS_Z(frame, inverseTileSize, false);
                half4 x = FARION_SAMPLE_2D(_NoiseTex, sampler_NoiseTex, axisX);
                half4 y = FARION_SAMPLE_2D(_NoiseTex, sampler_NoiseTex, axisY);
                half4 z = FARION_SAMPLE_2D(_NoiseTex, sampler_NoiseTex, axisZ);
                return x * frame.weights.x + y * frame.weights.y + z * frame.weights.z;
            }

            half4 FarionSampleOverlay(
                TEXTURE2D_PARAM(overlayTexture, overlaySampler),
                FarionTriplanarFrame frame,
                float worldTileSize)
            {
                float inverseTileSize = 1.0 / max(worldTileSize, 0.001);
                FarionAxisUV axisX = FARION_AXIS_X(frame, inverseTileSize, true);
                FarionAxisUV axisY = FARION_AXIS_Y(frame, inverseTileSize, true);
                FarionAxisUV axisZ = FARION_AXIS_Z(frame, inverseTileSize, true);
                half4 x = FARION_SAMPLE_2D(overlayTexture, overlaySampler, axisX);
                half4 y = FARION_SAMPLE_2D(overlayTexture, overlaySampler, axisY);
                half4 z = FARION_SAMPLE_2D(overlayTexture, overlaySampler, axisZ);
                return x * frame.weights.x + y * frame.weights.y + z * frame.weights.z;
            }

            half3 FarionUnpackOverlayNormalOS(
                TEXTURE2D_PARAM(normalTexture, normalSampler),
                FarionTriplanarFrame frame,
                float worldTileSize,
                half strength)
            {
                float inverseTileSize = 1.0 / max(worldTileSize, 0.001);
                FarionAxisUV axisX = FARION_AXIS_X(frame, inverseTileSize, true);
                FarionAxisUV axisY = FARION_AXIS_Y(frame, inverseTileSize, true);
                FarionAxisUV axisZ = FARION_AXIS_Z(frame, inverseTileSize, true);
                return FarionBlendTriplanarNormal(
                    frame,
                    UnpackNormalScale(FARION_SAMPLE_2D(normalTexture, normalSampler, axisX), strength),
                    UnpackNormalScale(FARION_SAMPLE_2D(normalTexture, normalSampler, axisY), strength),
                    UnpackNormalScale(FARION_SAMPLE_2D(normalTexture, normalSampler, axisZ), strength));
            }

            half4 FarionSampleSurfaceArray(
                TEXTURE2D_ARRAY_PARAM(surfaceArray, surfaceSampler),
                FarionTriplanarFrame frame,
                float worldTileSize,
                int layer)
            {
                float inverseTileSize = 1.0 / max(worldTileSize, 0.001);
                FarionAxisUV axisX = FARION_AXIS_X(frame, inverseTileSize, true);
                FarionAxisUV axisY = FARION_AXIS_Y(frame, inverseTileSize, true);
                FarionAxisUV axisZ = FARION_AXIS_Z(frame, inverseTileSize, true);
                half4 x = FARION_SAMPLE_ARRAY(surfaceArray, surfaceSampler, axisX, layer);
                half4 y = FARION_SAMPLE_ARRAY(surfaceArray, surfaceSampler, axisY, layer);
                half4 z = FARION_SAMPLE_ARRAY(surfaceArray, surfaceSampler, axisZ, layer);
                return x * frame.weights.x + y * frame.weights.y + z * frame.weights.z;
            }

            half3 FarionUnpackSurfaceNormalOS(
                FarionTriplanarFrame frame,
                float worldTileSize,
                int layer,
                half strength)
            {
                float inverseTileSize = 1.0 / max(worldTileSize, 0.001);
                FarionAxisUV axisX = FARION_AXIS_X(frame, inverseTileSize, true);
                FarionAxisUV axisY = FARION_AXIS_Y(frame, inverseTileSize, true);
                FarionAxisUV axisZ = FARION_AXIS_Z(frame, inverseTileSize, true);
                return FarionBlendTriplanarNormal(
                    frame,
                    UnpackNormalScale(FARION_SAMPLE_ARRAY(_SurfaceNormalArray, sampler_SurfaceBaseColorArray, axisX, layer), strength),
                    UnpackNormalScale(FARION_SAMPLE_ARRAY(_SurfaceNormalArray, sampler_SurfaceBaseColorArray, axisY, layer), strength),
                    UnpackNormalScale(FARION_SAMPLE_ARRAY(_SurfaceNormalArray, sampler_SurfaceBaseColorArray, axisZ, layer), strength));
            }

            #define FARION_SURFACE_ARRAY(tex) TEXTURE2D_ARRAY_ARGS(tex, sampler_SurfaceBaseColorArray)

            InputData FarionBuildPbrInputData(Varyings input, half3 normalWS, half3 upWS)
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
                half3 ambient = max(SampleSH(inputData.normalWS), _FarionAmbientColor.rgb);
                half hemisphere = saturate(dot(inputData.normalWS, upWS) * 0.5h + 0.5h);
                inputData.bakedGI = ambient * lerp(1.0h, lerp(0.35h, 1.25h, hemisphere), _AmbientHemisphere);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(float2(0.0, 0.0));
                return inputData;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalOS = normalize(input.normalOS);
                if (_SurfaceNormalMapEnabled > 0.5h)
                {
                    float3 bakedNormalOS = SAMPLE_TEXTURECUBE(
                        _SurfaceNormalMap,
                        sampler_NoiseTex,
                        normalize(input.positionOS)).rgb * 2.0 - 1.0;
                    float farWeight = smoothstep(
                        _FarNormalFadeStart,
                        _FarNormalFadeEnd,
                        distance(_WorldSpaceCameraPos, input.positionWS));
                    normalOS = normalize(lerp(normalOS, normalize(bakedNormalOS), farWeight));
                }

                FarionTriplanarFrame frame = FarionBuildTriplanarFrame(input.positionOS, normalOS);

                half largeNoise = input.terrainData.x;
                half detailNoise = input.terrainData.y;
                half smallNoise = input.terrainData.z;
                half warpedNoise = input.terrainData.w;

                half surfaceMacro = saturate(largeNoise * 0.7h + smallNoise * 0.3h);

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

                half4 texNoise = FarionSampleTriplanarNoise(frame, _NoiseScale);
                half4 texNoise2 = FarionSampleTriplanarNoise(frame, _NoiseScale2);
                half4 surfaceWeightsA = half4(0.0h, 0.0h, 0.0h, 0.0h);
                half4 surfaceWeightsB = half4(0.0h, 0.0h, 0.0h, 0.0h);
                half3 surfaceState = half3(0.0h, 0.0h, 0.0h);
                if (_SurfaceWeightMapEnabled > 0.5h)
                {
                    float3 weightDirection = normalize(radialOS + float3(
                        largeNoise - 0.5h,
                        warpedNoise - 0.5h,
                        detailNoise - 0.5h) * _SurfaceWeightWarp);
                    surfaceWeightsA = saturate(SAMPLE_TEXTURECUBE(
                        _SurfaceWeightsA,
                        sampler_NoiseTex,
                        weightDirection));
                    surfaceWeightsB = saturate(SAMPLE_TEXTURECUBE(
                        _SurfaceWeightsB,
                        sampler_NoiseTex,
                        weightDirection));
                    surfaceState = saturate(SAMPLE_TEXTURECUBE(
                        _SurfaceStateMap,
                        sampler_NoiseTex,
                        weightDirection).rgb);
                }

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

                half shoreBlendWeight = 1.0h - FarionBlend(_ShoreHeight, _ShoreBlend, flatHeight01);
                half3 shoreColor = lerp(_ShoreLow.rgb, _ShoreHigh.rgb, FarionRemap01(aboveOcean01, 0.0h, max(_ShoreHeight, 0.0001h)));
                shoreColor = lerp(shoreColor, (_ShoreLow.rgb + _ShoreHigh.rgb) * 0.5h, texNoise.g);

                half banding = 0.0h;
                if (abs(_SteepBandStrength) > 0.001h)
                {
                    float3 sphereTangent = float3(-radialOS.z, 0, radialOS.x);
                    sphereTangent = dot(sphereTangent, sphereTangent) < 0.0001
                        ? float3(1, 0, 0)
                        : normalize(sphereTangent);

                    float3 normalTangent = normalize(normalOS - radialOS * dot(normalOS, radialOS));
                    half bandCoord = dot(sphereTangent, normalTangent) * 0.5h + 0.5h;
                    half bandScaled = bandCoord * (_SteepBands + 1.0h);
                    half bandStep = floor(bandScaled);
                    bandCoord = (bandStep + smoothstep(0.35h, 0.65h, bandScaled - bandStep)) /
                        max(_SteepBands, 1.0h);
                    banding = (abs(bandCoord - 0.5h) * 2.0h - 0.5h) * _SteepBandStrength;
                }

                half3 steepTerrain = lerp(_SteepLow.rgb, _SteepHigh.rgb, saturate(aboveOcean01 + banding));
                half3 baseSteepTerrain = steepTerrain;

                half3 surfaceFlatTerrain = half3(0.0h, 0.0h, 0.0h);
                half3 surfaceSteepTerrain = half3(0.0h, 0.0h, 0.0h);
                half3 surfaceBaseColor = half3(0.0h, 0.0h, 0.0h);
                half3 surfaceNormalOS = half3(0.0h, 0.0h, 0.0h);
                half3 surfaceEmission = half3(0.0h, 0.0h, 0.0h);
                half surfaceSmoothness = 0.0h;
                half surfaceTextureSmoothness = 0.0h;
                half surfaceHeight = 0.0h;
                half surfaceHeightStrength = 0.0h;
                half surfaceAmbientOcclusion = 0.0h;
                half surfaceVisualWeight = 0.0h;
                half surfaceTextureWeight = 0.0h;
                half surfaceHeightWeight = 0.0h;
                half surfaceAmbientOcclusionWeight = 0.0h;
                half surfaceEmissionWeight = 0.0h;
                bool sampleSurfaceTextures = _SurfaceTextureBlendStrength > 0.0h
                    && !(hasOcean > 0.5h && landWaterBlend <= 0.0h);

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

                    float slotTileSize = _SurfaceTextureParams[surfaceSlot].x;
                    int textureLayer;
                    bool hasSurfaceTexture = FarionTryGetSurfaceTextureLayer(surfaceSlot, textureLayer);
                    if (sampleSurfaceTextures && hasSurfaceTexture)
                    {
                        surfaceBaseColor += FarionSampleSurfaceArray(
                            FARION_SURFACE_ARRAY(_SurfaceBaseColorArray),
                            frame,
                            slotTileSize,
                            textureLayer).rgb * weight;
                        surfaceNormalOS += FarionUnpackSurfaceNormalOS(
                            frame,
                            slotTileSize,
                            textureLayer,
                            _SurfaceParams[surfaceSlot].x) * weight;
                        surfaceTextureSmoothness += (1.0h - FarionSampleSurfaceArray(
                            FARION_SURFACE_ARRAY(_SurfaceRoughnessArray),
                            frame,
                            slotTileSize,
                            textureLayer).r) * weight;
                        surfaceTextureWeight += weight;
                    }

                    int auxLayer;
                    if (FarionTryGetSurfaceAuxTextureLayer(
                        _SurfaceAuxTextureParams[surfaceSlot].y,
                        _SurfaceHeightTextureCount,
                        auxLayer))
                    {
                        surfaceHeight += FarionSampleSurfaceArray(
                            FARION_SURFACE_ARRAY(_SurfaceHeightArray),
                            frame,
                            slotTileSize,
                            auxLayer).r * weight;
                        surfaceHeightStrength += _SurfaceTextureParams[surfaceSlot].y * weight;
                        surfaceHeightWeight += weight;
                    }

                    if (FarionTryGetSurfaceAuxTextureLayer(
                        _SurfaceAuxTextureParams[surfaceSlot].x,
                        _SurfaceAmbientOcclusionTextureCount,
                        auxLayer))
                    {
                        surfaceAmbientOcclusion += FarionSampleSurfaceArray(
                            FARION_SURFACE_ARRAY(_SurfaceAmbientOcclusionArray),
                            frame,
                            slotTileSize,
                            auxLayer).r * weight;
                        surfaceAmbientOcclusionWeight += weight;
                    }

                    if (FarionTryGetSurfaceAuxTextureLayer(
                        _SurfaceAuxTextureParams[surfaceSlot].z,
                        _SurfaceEmissionTextureCount,
                        auxLayer))
                    {
                        surfaceEmission += FarionSampleSurfaceArray(
                            FARION_SURFACE_ARRAY(_SurfaceEmissionArray),
                            frame,
                            slotTileSize,
                            auxLayer).rgb
                            * _SurfaceEmissionTints[surfaceSlot].rgb
                            * _SurfaceAuxTextureParams[surfaceSlot].w
                            * weight;
                        surfaceEmissionWeight += weight;
                    }
                }

                half shorePreservation = saturate(1.0h - shoreBlendWeight * hasOcean * 0.75h);
                half surfaceMask = saturate(surfaceVisualWeight * _SurfaceVisualBlendStrength * shorePreservation);
                half surfaceTextureMask = saturate(surfaceTextureWeight * _SurfaceTextureBlendStrength * shorePreservation);
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
                    surfaceBaseColor /= max(surfaceTextureWeight, 0.0001h);
                    half landLuminance = max(dot(landColor, half3(0.2126h, 0.7152h, 0.0722h)), 0.05h);
                    half textureLuminance = max(dot(surfaceBaseColor, half3(0.2126h, 0.7152h, 0.0722h)), 0.05h);
                    half levelMatch = pow(landLuminance / textureLuminance, _SurfaceTextureLevelMatch);
                    half3 surfaceTexturedColor = saturate(surfaceBaseColor * levelMatch);
                    landColor = lerp(landColor, surfaceTexturedColor, surfaceTextureMask * 0.92h);
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

                landColor *= lerp(0.9h, 1.12h, saturate(smallNoise * 0.8h + detailNoise * 0.2h));
                landColor *= lerp(1.0h - _MacroVariation, 1.0h + _MacroVariation, surfaceMacro);
                landColor = lerp(landColor, shoreColor, shoreBlendWeight * hasOcean);
                half wetNoise = saturate(texNoise2.g * 0.65h + texNoise.b * 0.35h);
                half landShoreMask = shorelineBand * saturate(landWaterBlend * 2.0h - 1.0h);
                half wetShoreMask = landShoreMask * _ShoreWetness * lerp(0.72h, 1.0h, wetNoise);
                half3 wetShoreColor = lerp(shoreColor * 0.58h, _OceanHigh.rgb * 0.82h, 0.35h);
                landColor = lerp(landColor, wetShoreColor, wetShoreMask);

                half oceanMask = hasOcean * (1.0h - landWaterBlend);
                half3 seabedColor = lerp(shoreColor * 0.62h, baseSteepTerrain * 0.72h, saturate(oceanDepth01));
                seabedColor *= lerp(0.86h, 1.04h, texNoise.b);
                half3 albedo = lerp(landColor, seabedColor, oceanMask);
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
                        TEXTURE2D_ARGS(_LavaBaseColor, sampler_NoiseTex),
                        frame,
                        _LavaWorldTileSize).rgb;
                    lavaRoughness = FarionSampleOverlay(
                        TEXTURE2D_ARGS(_LavaRoughness, sampler_NoiseTex),
                        frame,
                        _LavaWorldTileSize).r;
                    albedo = lerp(albedo, lavaColor, lavaMask);
                }

                if (snowMask > 0.0001h)
                {
                    half3 snowColor = FarionSampleOverlay(
                        TEXTURE2D_ARGS(_SnowBaseColor, sampler_NoiseTex),
                        frame,
                        _SnowWorldTileSize).rgb;
                    snowRoughness = FarionSampleOverlay(
                        TEXTURE2D_ARGS(_SnowRoughness, sampler_NoiseTex),
                        frame,
                        _SnowWorldTileSize).r;
                    albedo = lerp(albedo, snowColor, snowMask);
                }

                albedo *= lerp(1.0h, 0.68h, wetnessMask * 0.55h);

                half3 rockNormalOS = FarionUnpackOverlayNormalOS(
                    TEXTURE2D_ARGS(_RockNormal, sampler_RockNormal),
                    frame,
                    _RockNormalTileSize,
                    _NormalStrength);
                if (surfaceTextureMask > 0.0h)
                {
                    half3 blendedSurfaceNormalOS = normalize(surfaceNormalOS / max(surfaceTextureWeight, 0.0001h));
                    rockNormalOS = normalize(lerp(rockNormalOS, blendedSurfaceNormalOS, surfaceTextureMask));
                }
                if (lavaMask > 0.0001h)
                {
                    half3 lavaNormalOS = FarionUnpackOverlayNormalOS(
                        TEXTURE2D_ARGS(_LavaNormal, sampler_NoiseTex),
                        frame,
                        _LavaWorldTileSize,
                        _LavaNormalStrength);
                    rockNormalOS = normalize(lerp(rockNormalOS, lavaNormalOS, lavaMask));
                }
                if (snowMask > 0.0001h)
                {
                    half3 snowNormalOS = FarionUnpackOverlayNormalOS(
                        TEXTURE2D_ARGS(_SnowNormal, sampler_NoiseTex),
                        frame,
                        _SnowWorldTileSize,
                        _SnowNormalStrength);
                    rockNormalOS = normalize(lerp(rockNormalOS, snowNormalOS, snowMask));
                }
                half3 blendedNormalOS = normalize(lerp(rockNormalOS, normalOS, oceanMask * 0.75h));
                half3 normalWS = normalize(TransformObjectToWorldNormal(blendedNormalOS));
                half3 upWS = normalize(TransformObjectToWorldNormal(radialOS));

                half surfaceOcclusion = 1.0h;
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

                half smoothness = lerp(_LandSmoothness, 0.0h, oceanMask);
                smoothness = lerp(smoothness, surfaceSmoothness, surfaceMask * (1.0h - oceanMask));
                if (surfaceTextureMask > 0.0h)
                {
                    surfaceTextureSmoothness /= max(surfaceTextureWeight, 0.0001h);
                    smoothness = lerp(smoothness, surfaceTextureSmoothness, surfaceTextureMask * (1.0h - oceanMask) * 0.8h);
                }
                smoothness = lerp(smoothness, 1.0h - lavaRoughness, lavaMask);
                smoothness = lerp(smoothness, 1.0h - snowRoughness, snowMask);
                half combinedWetness = saturate(wetnessMask * 0.65h + wetShoreMask);
                smoothness = lerp(smoothness, max(smoothness, 0.48h), combinedWetness);
                if (_SpecularAntialiasing > 0.0h)
                {
                    smoothness = GeometricNormalFiltering(
                        smoothness,
                        normalWS,
                        _SpecularAntialiasing * 0.5h,
                        0.25h);
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
                if (lavaMask > 0.0001h && _LavaEmissionStrength > 0.0h)
                {
                    half3 lavaEmission = FarionSampleOverlay(
                        TEXTURE2D_ARGS(_LavaEmission, sampler_NoiseTex),
                        frame,
                        _LavaWorldTileSize).rgb;
                    surfaceEmission += lavaEmission
                        * _LavaEmissionTint.rgb
                        * _LavaEmissionStrength
                        * lavaMask;
                }

                InputData inputData = FarionBuildPbrInputData(input, normalWS, upWS);
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = saturate(albedo);
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.metallic = saturate(_Metallic) * (1.0h - oceanMask);
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
            #pragma vertex FarionGeomorphDepthOnlyVertex
            #pragma fragment FarionGeomorphDepthOnlyFragment

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "FarionSurfaceGeomorphPasses.hlsl"
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
            #pragma vertex FarionGeomorphDepthNormalsVertex
            #pragma fragment FarionGeomorphDepthNormalsFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "FarionSurfaceGeomorphPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            ColorMask RG

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FarionGeomorphMotionVertex
            #pragma fragment FarionGeomorphMotionFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "FarionSurfaceGeomorphPasses.hlsl"
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
            #pragma vertex FarionGeomorphShadowVertex
            #pragma fragment FarionGeomorphShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "FarionSurfaceGeomorphPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
