using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal readonly struct CelestialSurfaceSampler
    {
        readonly CelestialShapeProfile shapeProfile;
        readonly CelestialSurfaceProfileBase surfaceProfile;
        readonly float baseRadius;
        readonly float angularFootprint;

        CelestialSurfaceSampler(
            CelestialShapeProfile shapeProfile,
            CelestialSurfaceProfileBase surfaceProfile,
            float baseRadius,
            float angularFootprint)
        {
            this.shapeProfile = shapeProfile;
            this.surfaceProfile = surfaceProfile;
            this.baseRadius = baseRadius;
            this.angularFootprint = angularFootprint;
        }

        public static CelestialSurfaceSampler Create(
            float baseRadius,
            float angularFootprint,
            CelestialShapeProfile shapeProfile,
            CelestialSurfaceProfileBase surfaceProfile)
        {
            CelestialShapeProfile resolvedShape = shapeProfile != null ? shapeProfile : null;
            CelestialSurfaceProfileBase resolvedSurface = surfaceProfile != null ? surfaceProfile : null;
            resolvedShape?.PrepareSampling();
            return new CelestialSurfaceSampler(
                resolvedShape,
                resolvedSurface,
                Mathf.Max(0.01f, baseRadius),
                Mathf.Max(0f, angularFootprint));
        }

        public CelestialShapeSample EvaluateSample(Vector3 unitDirection)
        {
            unitDirection = NormalizeDirection(unitDirection);
            if (shapeProfile is not null)
            {
                return shapeProfile.EvaluateSample(baseRadius, unitDirection, angularFootprint);
            }

            float radius = surfaceProfile is not null
                ? surfaceProfile.EvaluateRadius(baseRadius, unitDirection)
                : baseRadius;
            return new CelestialShapeSample(radius, new Vector4(0.5f, 0.5f, 0.5f, 0.5f));
        }

        public float EvaluateRadius(Vector3 unitDirection)
        {
            unitDirection = NormalizeDirection(unitDirection);
            if (shapeProfile is not null)
            {
                return shapeProfile.EvaluateRadius(baseRadius, unitDirection, angularFootprint);
            }

            return surfaceProfile is not null
                ? surfaceProfile.EvaluateRadius(baseRadius, unitDirection)
                : baseRadius;
        }

        static Vector3 NormalizeDirection(Vector3 direction)
        {
            return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.up;
        }
    }
}
