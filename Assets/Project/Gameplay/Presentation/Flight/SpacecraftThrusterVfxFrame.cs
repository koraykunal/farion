using Farion.Gameplay.Flight;
using UnityEngine;

namespace Farion.Gameplay.Presentation.Flight
{
    public readonly struct SpacecraftThrusterVfxFrame
    {
        public SpacecraftThrusterVfxFrame(
            float throttle,
            float boost,
            float heat,
            float atmosphereDensity,
            float relativeSpeed,
            Vector3 localTranslation,
            Vector3 localRotation,
            Vector3 localLinearAcceleration,
            Vector3 localAngularAcceleration,
            SpacecraftThrusterCommand thrusters,
            float groundProximity = 0f)
        {
            Throttle = Mathf.Clamp01(throttle);
            Boost = Mathf.Clamp01(boost);
            Heat = Mathf.Clamp01(heat);
            AtmosphereDensity = Mathf.Clamp01(atmosphereDensity);
            RelativeSpeed = Mathf.Max(0f, relativeSpeed);
            LocalTranslation = ClampAxes(localTranslation);
            LocalRotation = ClampAxes(localRotation);
            LocalLinearAcceleration = localLinearAcceleration;
            LocalAngularAcceleration = localAngularAcceleration;
            Thrusters = thrusters;
            GroundProximity = Mathf.Clamp01(groundProximity);
        }

        public float Throttle { get; }
        public float Boost { get; }
        public float Heat { get; }
        public float AtmosphereDensity { get; }
        public float RelativeSpeed { get; }
        public Vector3 LocalTranslation { get; }
        public Vector3 LocalRotation { get; }
        public Vector3 LocalLinearAcceleration { get; }
        public Vector3 LocalAngularAcceleration { get; }
        public SpacecraftThrusterCommand Thrusters { get; }
        public float GroundProximity { get; }

        public bool InAtmosphere => AtmosphereDensity > 0.001f;
        public bool NearGround => GroundProximity > 0.001f;

        public static SpacecraftThrusterVfxFrame Idle => new(
            0f,
            0f,
            0f,
            0f,
            0f,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            SpacecraftThrusterCommand.None);

        static Vector3 ClampAxes(Vector3 value)
        {
            return new Vector3(
                Mathf.Clamp(value.x, -1f, 1f),
                Mathf.Clamp(value.y, -1f, 1f),
                Mathf.Clamp(value.z, -1f, 1f));
        }
    }
}
