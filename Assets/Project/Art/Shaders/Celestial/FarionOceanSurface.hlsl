#ifndef FARION_OCEAN_SURFACE_INCLUDED
#define FARION_OCEAN_SURFACE_INCLUDED

#include "FarionCelestialRaycast.hlsl"
#include "FarionOceanWaves.hlsl"

#define FARION_LOCAL_SURFACE_STEPS 64
#define FARION_LOCAL_SURFACE_REFINEMENTS 6
#define FARION_SWELL_FADE_START_WAVES 16.0
#define FARION_SWELL_FADE_END_WAVES 24.0

// Shared by every pass that crosses the air-water boundary. The wave field is
// mirrored by OceanWaveField.cs, while this file owns the GPU ray intersection.

struct FarionOceanSurfaceField
{
    float3 centre;
    float oceanRadius;
    float waveLength;
    float waveAmplitude;
    float3 wavePhases;
    float4x4 worldToLocal;
};

struct FarionOceanSurfaceIntersection
{
    float entryDistance;
    float exitDistance;
    float surfaceDistance;
    float surfaceHeight;
    float3 surfaceGradient;
    float displacementFade;
    bool wasRefined;
};

FarionOceanSurfaceField FarionBuildOceanSurfaceField(
    float3 centre,
    float oceanRadius,
    float waveLength,
    float waveAmplitude,
    float3 wavePhases,
    float4x4 worldToLocal)
{
    FarionOceanSurfaceField field;
    field.centre = centre;
    field.oceanRadius = oceanRadius;
    field.waveLength = waveLength;
    field.waveAmplitude = waveAmplitude;
    field.wavePhases = wavePhases;
    field.worldToLocal = worldToLocal;
    return field;
}

float FarionSampleOceanSurfaceHeight(
    FarionOceanSurfaceField field,
    float3 relativePosition,
    out float3 worldGradient)
{
    float3 localGradient;
    float height = FarionSampleWaveHeight(
        mul((float3x3)field.worldToLocal, relativePosition),
        field.waveLength,
        field.waveAmplitude,
        field.wavePhases,
        localGradient);
    worldGradient = mul(transpose((float3x3)field.worldToLocal), localGradient);
    return height;
}

float FarionOceanSurfaceRadialError(
    FarionOceanSurfaceField field,
    float3 worldPosition,
    float displacementScale)
{
    float3 relativePosition = worldPosition - field.centre;
    float3 localGradient;
    float height = FarionSampleWaveHeight(
        mul((float3x3)field.worldToLocal, relativePosition),
        field.waveLength,
        field.waveAmplitude,
        field.wavePhases,
        localGradient);
    return length(relativePosition) - (field.oceanRadius + height * displacementScale);
}

float FarionOceanDisplacementFade(FarionOceanSurfaceField field, float surfaceDistance)
{
    float fadeStart = field.waveLength * FARION_SWELL_FADE_START_WAVES;
    float fadeEnd = max(field.waveLength * FARION_SWELL_FADE_END_WAVES, fadeStart + 0.001);
    return 1.0 - smoothstep(fadeStart, fadeEnd, surfaceDistance);
}

bool FarionTryFindFirstOceanSurfaceTransition(
    FarionOceanSurfaceField field,
    float3 rayOrigin,
    float3 rayDirection,
    float displacementScale,
    bool startsInside,
    float maxDistance,
    out float transitionDistance,
    out float surfaceHeight,
    out float3 surfaceGradient)
{
    transitionDistance = -1.0;
    surfaceHeight = 0.0;
    surfaceGradient = 0.0;
    if (maxDistance <= 0.0001)
    {
        return false;
    }

    float previousError = FarionOceanSurfaceRadialError(field, rayOrigin, displacementScale);
    if ((startsInside && previousError > 0.0) || (!startsInside && previousError < 0.0))
    {
        return false;
    }

    float previousDistance = 0.0;
    float stepSize = maxDistance / FARION_LOCAL_SURFACE_STEPS;
    [loop]
    for (int i = 1; i <= FARION_LOCAL_SURFACE_STEPS; i++)
    {
        float distance = stepSize * i;
        float error = FarionOceanSurfaceRadialError(
            field,
            rayOrigin + rayDirection * distance,
            displacementScale);
        bool crossedSurface = startsInside ? error >= 0.0 : error <= 0.0;
        if (crossedSurface)
        {
            float lowerDistance = previousDistance;
            float upperDistance = distance;
            [unroll]
            for (int refinement = 0; refinement < FARION_LOCAL_SURFACE_REFINEMENTS; refinement++)
            {
                float middleDistance = (lowerDistance + upperDistance) * 0.5;
                float middleError = FarionOceanSurfaceRadialError(
                    field,
                    rayOrigin + rayDirection * middleDistance,
                    displacementScale);
                if ((middleError < 0.0) == startsInside)
                {
                    lowerDistance = middleDistance;
                }
                else
                {
                    upperDistance = middleDistance;
                }
            }

            transitionDistance = upperDistance;
            surfaceHeight = FarionSampleOceanSurfaceHeight(
                field,
                rayOrigin + rayDirection * transitionDistance - field.centre,
                surfaceGradient);
            return true;
        }

        previousDistance = distance;
    }

    return false;
}

float FarionRefineOceanSurfaceDistance(
    FarionOceanSurfaceField field,
    float3 rayOrigin,
    float3 rayDirection,
    float distance,
    float expectedSlopeSign,
    float displacementScale,
    out float surfaceHeight,
    out float3 surfaceGradient)
{
    surfaceHeight = 0.0;
    surfaceGradient = 0.0;
    float effectiveAmplitude = field.waveAmplitude * displacementScale;
    if (effectiveAmplitude <= 0.0)
    {
        return distance;
    }

    float maxStep = effectiveAmplitude * 4.0;
    [loop]
    for (int i = 0; i < 3; i++)
    {
        float3 relativePosition = rayOrigin + rayDirection * distance - field.centre;
        float radius = length(relativePosition);
        float3 radialNormal = relativePosition / max(radius, 0.0001);
        float3 gradient;
        float height = FarionSampleOceanSurfaceHeight(field, relativePosition, gradient)
            * displacementScale;
        gradient *= displacementScale;
        float error = radius - (field.oceanRadius + height);
        float slope = dot(rayDirection, radialNormal) - dot(rayDirection, gradient);
        if (abs(slope) < 0.02)
        {
            return -1.0;
        }

        distance -= clamp(error / slope, -maxStep, maxStep);
    }

    distance = max(distance, 0.0);
    float3 finalRelativePosition = rayOrigin + rayDirection * distance - field.centre;
    float finalRadius = length(finalRelativePosition);
    float3 finalGradient;
    float finalHeight = FarionSampleOceanSurfaceHeight(field, finalRelativePosition, finalGradient);
    float finalError = finalRadius - (field.oceanRadius + finalHeight * displacementScale);
    float finalSlope = dot(rayDirection, finalRelativePosition / max(finalRadius, 0.0001))
        - dot(rayDirection, finalGradient * displacementScale);
    if (abs(finalError) > max(0.01, effectiveAmplitude * 0.05)
        || finalSlope * expectedSlopeSign <= 0.0)
    {
        return -1.0;
    }

    surfaceHeight = finalHeight;
    surfaceGradient = finalGradient;
    return distance;
}

bool FarionTryIntersectOceanSurface(
    FarionOceanSurfaceField field,
    float3 rayOrigin,
    float3 rayDirection,
    bool cameraInsideOcean,
    out FarionOceanSurfaceIntersection intersection)
{
    intersection.entryDistance = -1.0;
    intersection.exitDistance = -1.0;
    intersection.surfaceDistance = -1.0;
    intersection.surfaceHeight = 0.0;
    intersection.surfaceGradient = 0.0;
    intersection.displacementFade = 0.0;
    intersection.wasRefined = false;

    float2 meanOceanHit = FarionRaySphere(field.centre, field.oceanRadius, rayOrigin, rayDirection);
    float2 swellEnvelopeHit = FarionRaySphere(
        field.centre,
        field.oceanRadius + field.waveAmplitude,
        rayOrigin,
        rayDirection);
    if (swellEnvelopeHit.x < 0.0)
    {
        return false;
    }

    float displacementFade = cameraInsideOcean
        ? 1.0
        : FarionOceanDisplacementFade(
            field,
            meanOceanHit.x >= 0.0 ? meanOceanHit.x : swellEnvelopeHit.x);
    float effectiveAmplitude = field.waveAmplitude * displacementFade;
    if (effectiveAmplitude <= 0.0001)
    {
        if (meanOceanHit.x < 0.0)
        {
            return false;
        }

        intersection.entryDistance = meanOceanHit.x;
        intersection.exitDistance = meanOceanHit.x + meanOceanHit.y;
        intersection.surfaceDistance = cameraInsideOcean
            ? intersection.exitDistance
            : intersection.entryDistance;
        return true;
    }

    float envelopeExit = swellEnvelopeHit.x + swellEnvelopeHit.y;
    if (cameraInsideOcean)
    {
        // Near the interface the first local exit must win. A mean-sphere exit
        // can be on the far side of the planet and would fill real air with water.
        float localExit;
        float localSurfaceHeight;
        float3 localSurfaceGradient;
        if (FarionTryFindFirstOceanSurfaceTransition(
            field,
            rayOrigin,
            rayDirection,
            displacementFade,
            true,
            min(envelopeExit, field.waveLength * 2.0),
            localExit,
            localSurfaceHeight,
            localSurfaceGradient))
        {
            intersection.entryDistance = 0.0;
            intersection.exitDistance = max(localExit, 0.0001);
            intersection.surfaceDistance = intersection.exitDistance;
            intersection.surfaceHeight = localSurfaceHeight;
            intersection.surfaceGradient = localSurfaceGradient;
            intersection.displacementFade = displacementFade;
            intersection.wasRefined = true;
            return true;
        }
    }

    float2 oceanHit = FarionRaySphere(
        field.centre,
        field.oceanRadius + effectiveAmplitude,
        rayOrigin,
        rayDirection);
    if (oceanHit.x < 0.0)
    {
        return false;
    }

    float entryDistance = oceanHit.x;
    float exitDistance = oceanHit.x + oceanHit.y;
    float refinedHeight;
    float3 refinedGradient;
    if (cameraInsideOcean)
    {
        float refinedDistance = FarionRefineOceanSurfaceDistance(
            field,
            rayOrigin,
            rayDirection,
            exitDistance,
            1.0,
            displacementFade,
            refinedHeight,
            refinedGradient);
        if (refinedDistance >= 0.0)
        {
            exitDistance = refinedDistance;
            intersection.surfaceHeight = refinedHeight;
            intersection.surfaceGradient = refinedGradient;
            intersection.wasRefined = true;
        }
        else if (meanOceanHit.x >= 0.0)
        {
            exitDistance = meanOceanHit.x + meanOceanHit.y;
        }
    }
    else
    {
        float refinedDistance = FarionRefineOceanSurfaceDistance(
            field,
            rayOrigin,
            rayDirection,
            entryDistance,
            -1.0,
            displacementFade,
            refinedHeight,
            refinedGradient);
        if (refinedDistance >= 0.0)
        {
            entryDistance = refinedDistance;
            intersection.surfaceHeight = refinedHeight;
            intersection.surfaceGradient = refinedGradient;
            intersection.wasRefined = true;
        }
        else
        {
            float2 troughEnvelopeHit = FarionRaySphere(
                field.centre,
                max(0.0001, field.oceanRadius - effectiveAmplitude),
                rayOrigin,
                rayDirection);
            float searchEnd = troughEnvelopeHit.x > entryDistance
                ? troughEnvelopeHit.x
                : exitDistance;
            float transitionDistance;
            if (!FarionTryFindFirstOceanSurfaceTransition(
                field,
                rayOrigin + rayDirection * entryDistance,
                rayDirection,
                displacementFade,
                false,
                searchEnd - entryDistance,
                transitionDistance,
                refinedHeight,
                refinedGradient))
            {
                // Mean and envelope spheres are search bounds, never visible water.
                return false;
            }

            entryDistance += transitionDistance;
            intersection.surfaceHeight = refinedHeight;
            intersection.surfaceGradient = refinedGradient;
            intersection.wasRefined = true;
        }
    }

    if (cameraInsideOcean)
    {
        // Crossing the displaced surface can legitimately resolve to zero for
        // one frame. Keep that interface instead of dropping both ocean and air.
        exitDistance = max(exitDistance, 0.0001);
    }

    if (entryDistance < 0.0 || exitDistance <= entryDistance)
    {
        return false;
    }

    intersection.entryDistance = entryDistance;
    intersection.exitDistance = exitDistance;
    intersection.surfaceDistance = cameraInsideOcean ? exitDistance : entryDistance;
    intersection.displacementFade = displacementFade;
    return true;
}

#endif
