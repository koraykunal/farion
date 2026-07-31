using UnityEngine;

namespace Farion.Audio.Spacecraft
{
    public readonly struct ShipAudioTelemetry
    {
        public ShipAudioTelemetry(
            float engineLoad,
            float forwardThrust,
            float reverseThrust,
            float lateralThrust,
            float verticalThrust,
            float angularThrust,
            float boost,
            bool boostActive,
            float normalizedSpeed,
            float hullStress,
            float impact,
            float atmosphere,
            float waterSubmersion,
            SpacecraftAudioPerspective perspective)
        {
            EngineLoad = Mathf.Clamp01(engineLoad);
            ForwardThrust = Mathf.Clamp01(forwardThrust);
            ReverseThrust = Mathf.Clamp01(reverseThrust);
            LateralThrust = Mathf.Clamp01(lateralThrust);
            VerticalThrust = Mathf.Clamp01(verticalThrust);
            AngularThrust = Mathf.Clamp01(angularThrust);
            Boost = Mathf.Clamp01(boost);
            BoostActive = boostActive;
            NormalizedSpeed = Mathf.Clamp01(normalizedSpeed);
            HullStress = Mathf.Clamp01(hullStress);
            Impact = Mathf.Clamp01(impact);
            Atmosphere = Mathf.Clamp01(atmosphere);
            WaterSubmersion = Mathf.Clamp01(waterSubmersion);
            Perspective = perspective;
        }

        public float EngineLoad { get; }
        public float ForwardThrust { get; }
        public float ReverseThrust { get; }
        public float LateralThrust { get; }
        public float VerticalThrust { get; }
        public float AngularThrust { get; }
        public float Boost { get; }
        public bool BoostActive { get; }
        public float NormalizedSpeed { get; }
        public float HullStress { get; }
        public float Impact { get; }
        public float Atmosphere { get; }
        public float WaterSubmersion { get; }
        public SpacecraftAudioPerspective Perspective { get; }

        public static ShipAudioTelemetry Silent => new(
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            false,
            0f,
            0f,
            0f,
            0f,
            0f,
            SpacecraftAudioPerspective.Exterior);
    }
}
