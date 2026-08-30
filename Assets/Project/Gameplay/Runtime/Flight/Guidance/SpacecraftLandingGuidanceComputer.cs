using Farion.Gameplay.Actors;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpacecraftLandingComputer))]
    public sealed class SpacecraftLandingGuidanceComputer : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] SpacecraftLandingComputer landingComputer;
        [SerializeField] CelestialActorProbe celestialProbe;
        [SerializeField] SpacecraftMotor motor;

        SpacecraftLandingGuidanceSample currentGuidance = SpacecraftLandingGuidanceSample.Offline;

        public SpacecraftLandingGuidanceSample CurrentGuidance => currentGuidance;

        void Awake()
        {
            ResolveComponents();
        }

        void OnValidate()
        {
            ResolveComponents();
        }

        void FixedUpdate()
        {
            RefreshGuidance();
        }

        [ContextMenu("Refresh Landing Guidance")]
        public void RefreshGuidance()
        {
            ResolveComponents();
            if (landingComputer == null ||
                celestialProbe == null ||
                !celestialProbe.HasSample)
            {
                currentGuidance = SpacecraftLandingGuidanceSample.Offline;
                return;
            }

            SpacecraftLandingAssessment assessment = landingComputer.CurrentAssessment;
            if (!assessment.HasFrame)
            {
                currentGuidance = SpacecraftLandingGuidanceSample.Offline;
                return;
            }

            float verticalRatio = assessment.VerticalSpeedLimit > 0f
                ? Mathf.Max(0f, -assessment.Frame.SurfaceNormalVelocity) / assessment.VerticalSpeedLimit
                : 0f;
            float tangentialRatio = assessment.TangentialSpeedLimit > 0f
                ? assessment.Frame.SurfaceTangentialSpeed / assessment.TangentialSpeedLimit
                : 0f;

            SpacecraftLandingGuidanceCommand nextCommand = EvaluateCommand(landingComputer, assessment, verticalRatio, tangentialRatio);
            SpacecraftLandingGuidanceLevel nextLevel = EvaluateLevel(landingComputer, assessment, verticalRatio, tangentialRatio);
            currentGuidance = new SpacecraftLandingGuidanceSample(
                nextLevel,
                nextCommand,
                BuildAdvisory(nextCommand),
                verticalRatio,
                tangentialRatio,
                assessment.NormalizedStress);
        }

        void ResolveComponents()
        {
            if (landingComputer == null)
            {
                landingComputer = GetComponent<SpacecraftLandingComputer>();
            }

            if (celestialProbe == null)
            {
                celestialProbe = GetComponent<CelestialActorProbe>();
            }

            if (motor == null)
            {
                motor = GetComponent<SpacecraftMotor>();
            }
        }

        SpacecraftLandingGuidanceCommand EvaluateCommand(
            SpacecraftLandingComputer computer,
            SpacecraftLandingAssessment assessment,
            float verticalRatio,
            float tangentialRatio)
        {
            if (computer.UnsafeSurfaceContact || assessment.HasImpactRisk)
            {
                return SpacecraftLandingGuidanceCommand.AbortLanding;
            }

            if (motor != null && motor.GravityExceedsThrust)
            {
                return SpacecraftLandingGuidanceCommand.ThrustDeficit;
            }

            if (computer.TouchdownConfirmed)
            {
                return SpacecraftLandingGuidanceCommand.CommitTouchdown;
            }

            if (assessment.IsSafeTouchdownWindow)
            {
                return SpacecraftLandingGuidanceCommand.CommitTouchdown;
            }

            if (verticalRatio > 1f)
            {
                return SpacecraftLandingGuidanceCommand.ReduceVerticalSpeed;
            }

            if (tangentialRatio > 1f)
            {
                return SpacecraftLandingGuidanceCommand.ReduceTangentialSpeed;
            }

            if ((assessment.Risks & SpacecraftLandingRiskFlags.ExcessiveSurfaceSlope) != 0)
            {
                return SpacecraftLandingGuidanceCommand.SeekLevelSurface;
            }

            return assessment.Phase switch
            {
                SpacecraftApproachPhase.Orbit => SpacecraftLandingGuidanceCommand.PlanRetroBurn,
                SpacecraftApproachPhase.Deorbiting => SpacecraftLandingGuidanceCommand.HoldAttitude,
                SpacecraftApproachPhase.AtmosphericFlight => SpacecraftLandingGuidanceCommand.HoldAttitude,
                SpacecraftApproachPhase.AtmosphericDescent => SpacecraftLandingGuidanceCommand.HoldAttitude,
                SpacecraftApproachPhase.HighDescent => SpacecraftLandingGuidanceCommand.ReduceVerticalSpeed,
                SpacecraftApproachPhase.LowApproach => SpacecraftLandingGuidanceCommand.HoldAttitude,
                SpacecraftApproachPhase.BodyProximity => SpacecraftLandingGuidanceCommand.EstablishOrbit,
                _ => SpacecraftLandingGuidanceCommand.None
            };
        }

        SpacecraftLandingGuidanceLevel EvaluateLevel(
            SpacecraftLandingComputer computer,
            SpacecraftLandingAssessment assessment,
            float verticalRatio,
            float tangentialRatio)
        {
            if (computer.UnsafeSurfaceContact || assessment.HasImpactRisk)
            {
                return SpacecraftLandingGuidanceLevel.Critical;
            }

            if (motor != null && motor.GravityExceedsThrust)
            {
                return SpacecraftLandingGuidanceLevel.Warning;
            }

            if (computer.TouchdownConfirmed || assessment.IsSafeTouchdownWindow)
            {
                return SpacecraftLandingGuidanceLevel.Nominal;
            }

            if (verticalRatio > 1f ||
                tangentialRatio > 1f ||
                (assessment.Risks & SpacecraftLandingRiskFlags.ExcessiveSurfaceSlope) != 0)
            {
                return SpacecraftLandingGuidanceLevel.Warning;
            }

            if (assessment.NormalizedStress > 0.65f)
            {
                return SpacecraftLandingGuidanceLevel.Caution;
            }

            return assessment.Phase switch
            {
                SpacecraftApproachPhase.NoFrame => SpacecraftLandingGuidanceLevel.Offline,
                SpacecraftApproachPhase.Orbit => SpacecraftLandingGuidanceLevel.Advisory,
                SpacecraftApproachPhase.Deorbiting => SpacecraftLandingGuidanceLevel.Advisory,
                SpacecraftApproachPhase.AtmosphericDescent => SpacecraftLandingGuidanceLevel.Caution,
                SpacecraftApproachPhase.LowApproach => SpacecraftLandingGuidanceLevel.Caution,
                _ => SpacecraftLandingGuidanceLevel.Nominal
            };
        }

        static string BuildAdvisory(SpacecraftLandingGuidanceCommand nextCommand)
        {
            return nextCommand switch
            {
                SpacecraftLandingGuidanceCommand.EstablishOrbit => "ESTABLISH ORBIT",
                SpacecraftLandingGuidanceCommand.PlanRetroBurn => "PLAN RETRO BURN",
                SpacecraftLandingGuidanceCommand.ReduceVerticalSpeed => "REDUCE VERTICAL SPEED",
                SpacecraftLandingGuidanceCommand.ReduceTangentialSpeed => "REDUCE LATERAL SPEED",
                SpacecraftLandingGuidanceCommand.SeekLevelSurface => "SEEK LEVEL SURFACE",
                SpacecraftLandingGuidanceCommand.HoldAttitude => "HOLD ATTITUDE",
                SpacecraftLandingGuidanceCommand.CommitTouchdown => "TOUCHDOWN WINDOW",
                SpacecraftLandingGuidanceCommand.AbortLanding => "ABORT LANDING",
                SpacecraftLandingGuidanceCommand.ThrustDeficit => "THRUST BELOW GRAVITY",
                _ => "MONITOR"
            };
        }
    }
}

