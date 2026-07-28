using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Spacecraft Landing Profile", fileName = "SO_SpacecraftLandingProfile")]
    public sealed class SpacecraftLandingProfile : ScriptableObject
    {
        const float DefaultMinimumSurfaceFrameAltitude = 100f;
        const float DefaultMaximumSurfaceFrameBodyRadii = 0.5f;

        [Header("Surface Frame")]
        [Min(0f)]
        [SerializeField] float minimumSurfaceFrameAltitude =
            DefaultMinimumSurfaceFrameAltitude;
        [Min(0f)]
        [SerializeField] float maximumSurfaceFrameBodyRadii =
            DefaultMaximumSurfaceFrameBodyRadii;

        [Header("Altitude Bands")]
        [Min(0f)]
        [SerializeField] float highDescentAltitude = 25f;
        [Min(0f)]
        [SerializeField] float lowApproachAltitude = 5f;
        [Min(0f)]
        [SerializeField] float touchdownAltitude = 1.5f;

        [Header("Velocity Limits")]
        [Min(0f)]
        [SerializeField] float orbitTangentialSpeed = 6f;
        [Min(0f)]
        [SerializeField] float safeTouchdownVerticalSpeed = 2f;
        [Min(0f)]
        [SerializeField] float safeTouchdownTangentialSpeed = 3f;
        [Min(0f)]
        [SerializeField] float highDescentVerticalSpeed = 8f;
        [Min(0f)]
        [SerializeField] float highDescentTangentialSpeed = 12f;

        [Header("Phase Thresholds")]
        [Min(0f)]
        [SerializeField] float deorbitDescentSpeed = 0.25f;

        [Header("Surface")]
        [Range(0f, 89f)]
        [SerializeField] float safeTouchdownSlopeAngle = 14f;

        public float HighDescentAltitude => highDescentAltitude;
        public float MinimumSurfaceFrameAltitude =>
            minimumSurfaceFrameAltitude > 0f
                ? minimumSurfaceFrameAltitude
                : DefaultMinimumSurfaceFrameAltitude;
        public float MaximumSurfaceFrameBodyRadii =>
            maximumSurfaceFrameBodyRadii > 0f
                ? maximumSurfaceFrameBodyRadii
                : DefaultMaximumSurfaceFrameBodyRadii;
        public float LowApproachAltitude => lowApproachAltitude;
        public float TouchdownAltitude => touchdownAltitude;
        public float OrbitTangentialSpeed => orbitTangentialSpeed;
        public float SafeTouchdownVerticalSpeed => safeTouchdownVerticalSpeed;
        public float SafeTouchdownTangentialSpeed => safeTouchdownTangentialSpeed;
        public float HighDescentVerticalSpeed => highDescentVerticalSpeed;
        public float HighDescentTangentialSpeed => highDescentTangentialSpeed;
        public float DeorbitDescentSpeed => deorbitDescentSpeed;
        public float SafeTouchdownSlopeAngle => safeTouchdownSlopeAngle;

        void OnValidate()
        {
            highDescentAltitude = Mathf.Max(0f, highDescentAltitude);
            minimumSurfaceFrameAltitude = minimumSurfaceFrameAltitude > 0f
                ? Mathf.Max(highDescentAltitude, minimumSurfaceFrameAltitude)
                : Mathf.Max(
                    highDescentAltitude,
                    DefaultMinimumSurfaceFrameAltitude);
            maximumSurfaceFrameBodyRadii = maximumSurfaceFrameBodyRadii > 0f
                ? maximumSurfaceFrameBodyRadii
                : DefaultMaximumSurfaceFrameBodyRadii;
            lowApproachAltitude = Mathf.Clamp(lowApproachAltitude, 0f, highDescentAltitude);
            touchdownAltitude = Mathf.Clamp(touchdownAltitude, 0f, lowApproachAltitude);
            orbitTangentialSpeed = Mathf.Max(0f, orbitTangentialSpeed);
            safeTouchdownVerticalSpeed = Mathf.Max(0f, safeTouchdownVerticalSpeed);
            safeTouchdownTangentialSpeed = Mathf.Max(0f, safeTouchdownTangentialSpeed);
            highDescentVerticalSpeed = Mathf.Max(safeTouchdownVerticalSpeed, highDescentVerticalSpeed);
            highDescentTangentialSpeed = Mathf.Max(safeTouchdownTangentialSpeed, highDescentTangentialSpeed);
            deorbitDescentSpeed = Mathf.Max(0f, deorbitDescentSpeed);
            safeTouchdownSlopeAngle = Mathf.Clamp(safeTouchdownSlopeAngle, 0f, 89f);
        }

        public SpacecraftLandingAssessment Evaluate(CelestialFrameSample frame)
        {
            if (!IsSurfaceFrameRelevant(frame))
            {
                return SpacecraftLandingAssessment.Empty(frame);
            }

            float descendingSpeed = Mathf.Max(0f, -frame.SurfaceNormalVelocity);
            float verticalLimit = EvaluateVerticalSpeedLimit(frame.SurfaceAltitude);
            float tangentialLimit = EvaluateTangentialSpeedLimit(frame.SurfaceAltitude);

            SpacecraftLandingRiskFlags risks = SpacecraftLandingRiskFlags.None;
            if (frame.IsApproachingSurface)
            {
                risks |= SpacecraftLandingRiskFlags.Descending;
            }

            if (frame.IsInsideAtmosphere)
            {
                risks |= SpacecraftLandingRiskFlags.InsideAtmosphere;
            }

            if (frame.IsBelowOceanLevel)
            {
                risks |= SpacecraftLandingRiskFlags.BelowOceanLevel;
            }

            bool excessiveVerticalSpeed = descendingSpeed > verticalLimit;
            bool excessiveTangentialSpeed = frame.SurfaceTangentialSpeed > tangentialLimit;
            bool excessiveSurfaceSlope = frame.SurfaceSlopeAngleDegrees > safeTouchdownSlopeAngle;
            if (excessiveVerticalSpeed)
            {
                risks |= SpacecraftLandingRiskFlags.ExcessiveVerticalSpeed;
            }

            if (excessiveTangentialSpeed)
            {
                risks |= SpacecraftLandingRiskFlags.ExcessiveTangentialSpeed;
            }

            if (excessiveSurfaceSlope)
            {
                risks |= SpacecraftLandingRiskFlags.ExcessiveSurfaceSlope;
            }

            if (frame.SurfaceAltitude <= touchdownAltitude)
            {
                risks |= SpacecraftLandingRiskFlags.TouchdownCandidate;
            }

            if (frame.SurfaceAltitude <= lowApproachAltitude &&
                (excessiveVerticalSpeed || excessiveTangentialSpeed || excessiveSurfaceSlope))
            {
                risks |= SpacecraftLandingRiskFlags.ImpactRisk;
            }

            SpacecraftApproachPhase phase = EvaluatePhase(frame, risks);
            float stress = EvaluateStress(frame, descendingSpeed, verticalLimit, tangentialLimit, safeTouchdownSlopeAngle);
            return new SpacecraftLandingAssessment(
                frame,
                phase,
                risks,
                verticalLimit,
                tangentialLimit,
                safeTouchdownSlopeAngle,
                stress);
        }

        public bool IsSurfaceFrameRelevant(CelestialFrameSample frame)
        {
            if (!frame.HasBody)
            {
                return false;
            }

            float maximumAltitude = Mathf.Max(
                MinimumSurfaceFrameAltitude,
                frame.BodyRadius * MaximumSurfaceFrameBodyRadii);
            return frame.SurfaceAltitude <= maximumAltitude;
        }

        SpacecraftApproachPhase EvaluatePhase(CelestialFrameSample frame, SpacecraftLandingRiskFlags risks)
        {
            if (frame.IsBelowOceanLevel)
            {
                return SpacecraftApproachPhase.Submerged;
            }

            if (frame.SurfaceAltitude <= touchdownAltitude)
            {
                bool unsafeTouchdown =
                    (risks & SpacecraftLandingRiskFlags.ExcessiveVerticalSpeed) != 0 ||
                    (risks & SpacecraftLandingRiskFlags.ExcessiveTangentialSpeed) != 0 ||
                    (risks & SpacecraftLandingRiskFlags.ExcessiveSurfaceSlope) != 0;
                return unsafeTouchdown
                    ? SpacecraftApproachPhase.UnsafeTouchdown
                    : SpacecraftApproachPhase.TouchdownWindow;
            }

            if (frame.SurfaceAltitude <= lowApproachAltitude)
            {
                return SpacecraftApproachPhase.LowApproach;
            }

            if (frame.IsInsideAtmosphere)
            {
                return frame.IsApproachingSurface
                    ? SpacecraftApproachPhase.AtmosphericDescent
                    : SpacecraftApproachPhase.AtmosphericFlight;
            }

            if (frame.SurfaceAltitude <= highDescentAltitude && frame.IsApproachingSurface)
            {
                return SpacecraftApproachPhase.HighDescent;
            }

            if (frame.RadialVelocity < -deorbitDescentSpeed)
            {
                return SpacecraftApproachPhase.Deorbiting;
            }

            if (frame.TangentialSpeed >= orbitTangentialSpeed)
            {
                return SpacecraftApproachPhase.Orbit;
            }

            return SpacecraftApproachPhase.BodyProximity;
        }

        float EvaluateVerticalSpeedLimit(float altitude)
        {
            if (lowApproachAltitude <= touchdownAltitude)
            {
                return safeTouchdownVerticalSpeed;
            }

            float t = Mathf.InverseLerp(touchdownAltitude, lowApproachAltitude, altitude);
            return Mathf.Lerp(safeTouchdownVerticalSpeed, highDescentVerticalSpeed, t);
        }

        float EvaluateTangentialSpeedLimit(float altitude)
        {
            if (lowApproachAltitude <= touchdownAltitude)
            {
                return safeTouchdownTangentialSpeed;
            }

            float t = Mathf.InverseLerp(touchdownAltitude, lowApproachAltitude, altitude);
            return Mathf.Lerp(safeTouchdownTangentialSpeed, highDescentTangentialSpeed, t);
        }

        static float EvaluateStress(
            CelestialFrameSample frame,
            float descendingSpeed,
            float verticalLimit,
            float tangentialLimit,
            float slopeLimit)
        {
            float verticalStress = verticalLimit > 0f ? descendingSpeed / verticalLimit : 0f;
            float tangentialStress = tangentialLimit > 0f ? frame.SurfaceTangentialSpeed / tangentialLimit : 0f;
            float slopeStress = slopeLimit > 0f ? frame.SurfaceSlopeAngleDegrees / slopeLimit : 0f;
            float altitudeStress = frame.SurfaceAltitude > 0f
                ? Mathf.Clamp01(1f / Mathf.Max(1f, frame.SurfaceAltitude))
                : 1f;
            return Mathf.Clamp01(Mathf.Max(verticalStress, tangentialStress, slopeStress) * 0.8f + altitudeStress * 0.2f);
        }
    }
}
