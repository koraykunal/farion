#ifndef FARION_FARIONMOONINPUT_INCLUDED
#define FARION_FARIONMOONINPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    half4 _SecondaryColor;
    half4 _SteepColor;
    half4 _EjectaColor;
    half _SteepColorStrength;
    half _Metallic;
    half _Smoothness;
    half _SpecularAntialiasing;
    half _EjectaSmoothness;
    float _BodyRadius;
    float4 _RadiusMinMax;
    float _SurfaceNoiseWorldTileSize;
    float _NormalFlatWorldTileSize;
    float _NormalSteepWorldTileSize;
    half _NormalStrength;
    half _AmbientHemisphere;
    half _BiomeBlendStrength;
    half _EjectaStrength;
    float _EjectaRayFrequency;
    half _UseEjectaRayTex;
    float4 _FarionMorphRange;
CBUFFER_END

#endif
