#ifndef FARION_SURFACE_GEOMORPH_INCLUDED
#define FARION_SURFACE_GEOMORPH_INCLUDED

float4 _FarionSurfaceObserverWS;
float4 _FarionPreviousSurfaceObserverWS;

float FarionResolveMorphWeightWS(float3 positionWS, float3 observerWS)
{
    float morphEnd = _FarionMorphRange.y;
    if (morphEnd <= 0.0)
    {
        return 0.0;
    }

    float morphStart = _FarionMorphRange.x;
    float observerDistance = distance(positionWS, observerWS);
    return saturate((observerDistance - morphStart) / max(morphEnd - morphStart, 0.0001));
}

float FarionResolveMorphWeight(float3 positionOS)
{
    return FarionResolveMorphWeightWS(
        TransformObjectToWorld(positionOS),
        _FarionSurfaceObserverWS.xyz);
}

float3 FarionApplyGeomorph(float3 positionOS, float3 morphOffsetOS)
{
    return positionOS + morphOffsetOS * FarionResolveMorphWeight(positionOS);
}

float3 FarionApplyGeomorphNormal(
    float3 normalOS,
    float3 morphNormalOS,
    float morphWeight)
{
    return normalize(lerp(normalOS, morphNormalOS, morphWeight));
}

#endif
