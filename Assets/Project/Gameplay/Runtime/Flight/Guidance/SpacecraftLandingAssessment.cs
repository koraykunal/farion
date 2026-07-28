using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftLandingAssessment
    {
        public SpacecraftLandingAssessment(
            CelestialFrameSample frame,
            SpacecraftApproachPhase phase,
            SpacecraftLandingRiskFlags risks,
            float verticalSpeedLimit,
            float tangentialSpeedLimit,
            float surfaceSlopeLimit,
            float normalizedStress)
        {
            Frame = frame;
            Phase = phase;
            Risks = risks;
            VerticalSpeedLimit = Mathf.Max(0f, verticalSpeedLimit);
            TangentialSpeedLimit = Mathf.Max(0f, tangentialSpeedLimit);
            SurfaceSlopeLimit = Mathf.Max(0f, surfaceSlopeLimit);
            NormalizedStress = Mathf.Clamp01(normalizedStress);
        }

        public CelestialFrameSample Frame { get; }
        public SpacecraftApproachPhase Phase { get; }
        public SpacecraftLandingRiskFlags Risks { get; }
        public float VerticalSpeedLimit { get; }
        public float TangentialSpeedLimit { get; }
        public float SurfaceSlopeLimit { get; }
        public float NormalizedStress { get; }
        public bool HasFrame =>
            Frame.HasBody &&
            Phase != SpacecraftApproachPhase.NoFrame;
        public bool IsSafeTouchdownWindow => Phase == SpacecraftApproachPhase.TouchdownWindow;
        public bool HasImpactRisk => (Risks & SpacecraftLandingRiskFlags.ImpactRisk) != 0;

        public static SpacecraftLandingAssessment Empty(CelestialFrameSample frame)
        {
            return new SpacecraftLandingAssessment(
                frame,
                SpacecraftApproachPhase.NoFrame,
                SpacecraftLandingRiskFlags.NoFrame,
                0f,
                0f,
                0f,
                0f);
        }
    }
}
