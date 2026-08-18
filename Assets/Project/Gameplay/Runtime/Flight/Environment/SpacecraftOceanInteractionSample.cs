using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftOceanInteractionSample
    {
        public SpacecraftOceanInteractionSample(
            CelestialFrameSample frame,
            float submergedFraction,
            float waterDepth,
            float waterEntrySpeed,
            Vector3 buoyancyAcceleration,
            Vector3 dragAcceleration,
            float pressureStress,
            bool crushingDepth,
            bool unsafeWaterEntry)
        {
            Frame = frame;
            SubmergedFraction = Mathf.Clamp01(submergedFraction);
            WaterDepth = Mathf.Max(0f, waterDepth);
            WaterEntrySpeed = Mathf.Max(0f, waterEntrySpeed);
            BuoyancyAcceleration = buoyancyAcceleration;
            DragAcceleration = dragAcceleration;
            PressureStress = Mathf.Clamp01(pressureStress);
            CrushingDepth = crushingDepth;
            UnsafeWaterEntry = unsafeWaterEntry;
        }

        public CelestialFrameSample Frame { get; }
        public bool HasFrame => Frame.HasBody;
        public bool HasOcean => Frame.HasOcean;
        public bool IsTouchingWater => SubmergedFraction > 0f;
        public bool IsCenterBelowWater => Frame.IsBelowOceanLevel;
        public float SubmergedFraction { get; }
        public float WaterDepth { get; }
        public float WaterEntrySpeed { get; }
        public Vector3 BuoyancyAcceleration { get; }
        public Vector3 DragAcceleration { get; }
        public float PressureStress { get; }
        public bool CrushingDepth { get; }
        public bool UnsafeWaterEntry { get; }

        public static SpacecraftOceanInteractionSample Empty(CelestialFrameSample frame)
        {
            return new SpacecraftOceanInteractionSample(
                frame,
                0f,
                0f,
                0f,
                Vector3.zero,
                Vector3.zero,
                0f,
                false,
                false);
        }
    }
}
