#ifndef FARION_THRUSTER_SHAPING_INCLUDED
#define FARION_THRUSTER_SHAPING_INCLUDED

float3 FarionShapeThrusterVertex(
    float3 positionOS,
    float bellExpansion,
    float3 plumeBend,
    float groundSplash)
{
    float axial = saturate(positionOS.z);
    float3 shaped = positionOS;
    shaped.xy *= 1.0 + bellExpansion * axial * axial;
    shaped.xy *= 1.0 + groundSplash * smoothstep(0.45, 1.0, axial);
    shaped.xy += plumeBend.xy * axial * axial;
    return shaped;
}

float FarionThrusterViewScale(float3 positionWS, float referenceDistance)
{
    float viewDistance = max(0.01, length(GetWorldSpaceViewDir(positionWS)));
    return saturate(referenceDistance / viewDistance);
}

#endif
