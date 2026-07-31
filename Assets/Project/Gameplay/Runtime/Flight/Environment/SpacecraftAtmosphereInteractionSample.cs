using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftAtmosphereInteractionSample
    {
        public SpacecraftAtmosphereInteractionSample(
            CelestialFrameSample frame,
            float atmosphereDensity,
            float massDensity,
            float dynamicPressure,
            float dynamicPressureLoad,
            float heatingRate,
            float heatLoad,
            Vector3 dragAcceleration)
        {
            Frame = frame;
            AtmosphereDensity = Mathf.Clamp01(atmosphereDensity);
            MassDensity = Mathf.Max(0f, massDensity);
            DynamicPressure = Mathf.Max(0f, dynamicPressure);
            DynamicPressureLoad = Mathf.Clamp01(dynamicPressureLoad);
            HeatingRate = Mathf.Max(0f, heatingRate);
            HeatLoad = Mathf.Clamp01(heatLoad);
            DragAcceleration = dragAcceleration;
        }

        public CelestialFrameSample Frame { get; }
        public bool HasAtmosphere => Frame.HasAtmosphere;
        public bool IsInsideAtmosphere => AtmosphereDensity > 0f;
        public float AtmosphereDensity { get; }
        public float MassDensity { get; }
        public float DynamicPressure { get; }
        public float DynamicPressureLoad { get; }
        public float HeatingRate { get; }
        public float HeatLoad { get; }
        public Vector3 DragAcceleration { get; }
        public float RelativeSpeed => Frame.SurfaceRelativeVelocity.magnitude;
        public float AerodynamicStress => Mathf.Max(DynamicPressureLoad, HeatLoad);

        public static SpacecraftAtmosphereInteractionSample Empty(CelestialFrameSample frame)
        {
            return new SpacecraftAtmosphereInteractionSample(
                frame,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                Vector3.zero);
        }
    }
}
