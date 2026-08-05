using UnityEngine;

namespace Farion.Simulation.Physics
{
    public readonly struct GravitySample
    {
        public GravitySample(
            CelestialBody body,
            Vector3 acceleration,
            float centerDistance,
            float surfaceDistance)
            : this(body, acceleration, centerDistance, surfaceDistance, acceleration.sqrMagnitude > 0.0001f ? -acceleration.normalized : Vector3.up)
        {
        }

        public GravitySample(
            CelestialBody body,
            Vector3 acceleration,
            float centerDistance,
            float surfaceDistance,
            Vector3 surfaceNormal)
        {
            Body = body;
            Acceleration = acceleration;
            CenterDistance = centerDistance;
            SurfaceDistance = surfaceDistance;
            SurfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        }

        public CelestialBody Body { get; }
        public Vector3 Acceleration { get; }
        public float CenterDistance { get; }
        public float SurfaceDistance { get; }
        public Vector3 SurfaceNormal { get; }
        public bool HasBody => Body != null;
        public Vector3 SurfaceUp => HasBody && CenterDistance > 0.0001f
            ? SurfaceNormal
            : Vector3.up;

        public static GravitySample Empty => new(null, Vector3.zero, 0f, 0f);
    }
}
