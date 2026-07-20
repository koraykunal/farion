using UnityEngine;

namespace Farion.Core.Physics
{
    public readonly struct CelestialSurfaceSample
    {
        public CelestialSurfaceSample(
            CelestialBody body,
            Vector3 point,
            Vector3 normal,
            float centerDistance,
            float surfaceDistance,
            float slopeAngleDegrees = 0f)
        {
            Body = body;
            Point = point;
            Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            CenterDistance = centerDistance;
            SurfaceDistance = surfaceDistance;
            SlopeAngleDegrees = Mathf.Max(0f, slopeAngleDegrees);
        }

        public CelestialBody Body { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public float CenterDistance { get; }
        public float SurfaceDistance { get; }
        public float SlopeAngleDegrees { get; }
        public bool HasBody => Body != null;

        public static CelestialSurfaceSample Empty => new(null, Vector3.zero, Vector3.up, 0f, 0f);
    }
}
