#ifndef FARION_CELESTIAL_RAYCAST_INCLUDED
#define FARION_CELESTIAL_RAYCAST_INCLUDED

#define FARION_MAX_FLOAT 3.402823466e+38

// x: distance to the near intersection, y: distance travelled inside the
// sphere. x is negative when the sphere is missed.
float2 FarionRaySphere(
    float3 centre,
    float radius,
    float3 rayOrigin,
    float3 rayDirection)
{
    float3 offset = rayOrigin - centre;
    float b = dot(offset, rayDirection);
    float c = dot(offset, offset) - radius * radius;
    float discriminant = b * b - c;
    if (discriminant < 0.0)
    {
        return float2(-1.0, 0.0);
    }

    float root = sqrt(discriminant);
    float entryDistance = max(-b - root, 0.0);
    float exitDistance = -b + root;
    if (exitDistance < 0.0)
    {
        return float2(-1.0, 0.0);
    }

    return float2(entryDistance, exitDistance - entryDistance);
}

bool FarionIsSkyDepth(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth <= 0.000001;
#else
    return rawDepth >= 0.999999;
#endif
}

#endif
