using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal static class CelestialSurfaceSampling
    {
        public static CelestialShapeSample EvaluateSample(
            float baseRadius,
            Vector3 unitDirection,
            float angularSampleFootprint,
            CelestialShapeProfile shapeProfile,
            CelestialSurfaceProfileBase surfaceProfile)
        {
            unitDirection = NormalizeDirection(unitDirection);
            if (shapeProfile != null)
            {
                return shapeProfile.EvaluateSample(
                    baseRadius,
                    unitDirection,
                    Mathf.Max(0f, angularSampleFootprint));
            }

            float radius = surfaceProfile != null
                ? surfaceProfile.EvaluateRadius(baseRadius, unitDirection)
                : Mathf.Max(0.01f, baseRadius);
            return new CelestialShapeSample(radius, new Vector4(0.5f, 0.5f, 0.5f, 0.5f));
        }

        public static float EvaluateRadius(
            float baseRadius,
            Vector3 unitDirection,
            float angularSampleFootprint,
            CelestialShapeProfile shapeProfile,
            CelestialSurfaceProfileBase surfaceProfile)
        {
            unitDirection = NormalizeDirection(unitDirection);
            if (shapeProfile != null)
            {
                return shapeProfile.EvaluateRadius(
                    baseRadius,
                    unitDirection,
                    Mathf.Max(0f, angularSampleFootprint));
            }

            return surfaceProfile != null
                ? surfaceProfile.EvaluateRadius(baseRadius, unitDirection)
                : Mathf.Max(0.01f, baseRadius);
        }

        static Vector3 NormalizeDirection(Vector3 direction)
        {
            return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.up;
        }
    }
}
