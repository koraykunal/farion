#ifndef FARION_FARIONTERRESTRIALINPUT_INCLUDED
#define FARION_FARIONTERRESTRIALINPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
    float4 _FarionMorphRange;
CBUFFER_END

#endif
