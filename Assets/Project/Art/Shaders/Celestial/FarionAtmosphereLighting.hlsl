#ifndef FARION_ATMOSPHERE_LIGHTING_INCLUDED
#define FARION_ATMOSPHERE_LIGHTING_INCLUDED

float3 FarionAtmosphereSunTransmittance(
    float3 worldPosition,
    float3 centre,
    float surfaceRadius,
    float atmosphereRadius,
    float intensity,
    float mieExtinction,
    float3 rayleighExtinction,
    float3 ozoneExtinction,
    float enabled,
    float3 directionToStar,
    TEXTURE2D_PARAM(opticalDepthTexture, opticalDepthSampler))
{
    if (enabled <= 0.0)
    {
        return 1.0;
    }

    float3 offset = worldPosition - centre;
    float atmosphereThickness = max(atmosphereRadius - surfaceRadius, 0.0001);
    float height01 = saturate((length(offset) - surfaceRadius) / atmosphereThickness);
    float uvX = 1.0 - (dot(normalize(offset), directionToStar) * 0.5 + 0.5);
    float3 opticalDepth = SAMPLE_TEXTURE2D_LOD(
        opticalDepthTexture,
        opticalDepthSampler,
        float2(uvX, height01),
        0).rgb;
    float3 extinction = rayleighExtinction * opticalDepth.x
        + mieExtinction * opticalDepth.y
        + ozoneExtinction * opticalDepth.z;
    return exp(-extinction * intensity);
}

#endif
