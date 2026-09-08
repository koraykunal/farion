using Farion.Core.Numerics;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    public static class CraterShape
    {
        public static float InfluenceScale(float smoothness, float rimWidth)
        {
            return Mathf.Max(1f + rimWidth, Mathf.Sqrt(1f + smoothness));
        }

        public static float Evaluate(
            float normalizedDistance,
            float floorHeight,
            float smoothness,
            float rimWidth,
            float rimSteepness)
        {
            float cavity = normalizedDistance * normalizedDistance - 1f;
            float rimX = Mathf.Min(normalizedDistance - 1f - rimWidth, 0f);
            float rim = rimSteepness * rimX * rimX;
            float shape = FarionMath.SmoothMax(cavity, floorHeight, smoothness);
            return FarionMath.SmoothMin(shape, rim, smoothness);
        }
    }
}
