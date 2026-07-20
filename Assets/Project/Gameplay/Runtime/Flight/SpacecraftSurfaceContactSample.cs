using Farion.Core.Physics;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftSurfaceContactSample
    {
        public SpacecraftSurfaceContactSample(
            CelestialBody body,
            Vector3 point,
            Vector3 normal,
            Vector3 relativeVelocity,
            float separation,
            int contactCount,
            float time)
        {
            Body = body;
            Point = point;
            Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            RelativeVelocity = relativeVelocity;
            Separation = separation;
            ContactCount = Mathf.Max(0, contactCount);
            Time = time;
            NormalSpeed = Mathf.Max(0f, Vector3.Dot(relativeVelocity, -Normal));
            TangentialSpeed = Vector3.ProjectOnPlane(relativeVelocity, Normal).magnitude;
        }

        public CelestialBody Body { get; }
        public bool HasContact => Body != null && ContactCount > 0;
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public Vector3 RelativeVelocity { get; }
        public float NormalSpeed { get; }
        public float TangentialSpeed { get; }
        public float Separation { get; }
        public int ContactCount { get; }
        public float Time { get; }

        public static SpacecraftSurfaceContactSample Empty => new(
            null,
            Vector3.zero,
            Vector3.up,
            Vector3.zero,
            0f,
            0,
            0f);
    }
}
