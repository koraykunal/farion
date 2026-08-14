using System;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Flight;
using Farion.Simulation.Celestial;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Gameplay.Presentation.Flight
{
    [MovedFrom(true, "Farion.Gameplay.Flight", "Farion.Gameplay.Runtime")]
    [DefaultExecutionOrder(330)]
    [DisallowMultipleComponent]
    public sealed class SpacecraftThrusterVfxController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] CelestialActorProbe celestialProbe;
        [SerializeField] SpacecraftOceanInteractor oceanInteractor;
        [SerializeField] SpacecraftAtmosphereInteractor atmosphereInteractor;

        [Header("Response")]
        [Min(0.01f)]
        [SerializeField] float throttleReferenceAcceleration = 24f;
        [Min(0f)]
        [SerializeField] float throttleResponse = 10f;
        [Min(0f)]
        [SerializeField] float boostResponse = 12f;
        [Min(0f)]
        [SerializeField] float heatRiseResponse = 3.5f;
        [Min(0f)]
        [SerializeField] float heatFallResponse = 1.1f;
        [Min(0f)]
        [SerializeField] float atmosphereResponse = 4f;

        [Header("Load Model")]
        [Range(0f, 1f)]
        [SerializeField] float accelerationLoadWeight = 0.85f;
        [Range(0f, 1f)]
        [SerializeField] float boostLoadFloor = 0.72f;
        [Range(0f, 1f)]
        [SerializeField] float boostHeatContribution = 0.4f;
        [Range(0f, 1f)]
        [SerializeField] float atmosphereHeatContribution = 0.18f;
        [Range(0f, 1f)]
        [SerializeField] float speedHeatContribution = 0.08f;
        [Range(0f, 1f)]
        [SerializeField] float aerodynamicHeatContribution = 1f;
        [Min(1f)]
        [SerializeField] float heatReferenceSpeed = 260f;
        [Min(0.01f)]
        [SerializeField] float groundEffectAltitude = 45f;
        [SerializeField] bool suppressAtmosphereVfxUnderwater = true;

        [Header("Nozzles")]
        [SerializeField] SpacecraftThrusterNozzleVfx[] nozzles = Array.Empty<SpacecraftThrusterNozzleVfx>();

        [Header("Runtime Debug")]
        [SerializeField, Range(0f, 1f)] float debugThrottle;
        [SerializeField, Range(0f, 1f)] float debugBoost;
        [SerializeField, Range(0f, 1f)] float debugHeat;
        [SerializeField, Range(0f, 1f)] float debugAtmosphereDensity;
        [SerializeField, Range(0f, 1f)] float debugGroundProximity;
        [SerializeField] Vector3 debugLocalLinearAcceleration;

        float throttle;
        float boost;
        float heat;
        float atmosphereDensity;

        public SpacecraftThrusterVfxFrame CurrentFrame { get; private set; } = SpacecraftThrusterVfxFrame.Idle;

        void Reset()
        {
            AutoAssignReferences();
            AutoAssignNozzles();
        }

        void Awake()
        {
            AutoAssignReferences();
            if (nozzles == null || nozzles.Length == 0)
            {
                AutoAssignNozzles();
            }

            InitializeNozzles();
        }

        void OnValidate()
        {
            throttleReferenceAcceleration = Mathf.Max(0.01f, throttleReferenceAcceleration);
            throttleResponse = Mathf.Max(0f, throttleResponse);
            boostResponse = Mathf.Max(0f, boostResponse);
            heatRiseResponse = Mathf.Max(0f, heatRiseResponse);
            heatFallResponse = Mathf.Max(0f, heatFallResponse);
            atmosphereResponse = Mathf.Max(0f, atmosphereResponse);
            accelerationLoadWeight = Mathf.Clamp01(accelerationLoadWeight);
            boostLoadFloor = Mathf.Clamp01(boostLoadFloor);
            boostHeatContribution = Mathf.Clamp01(boostHeatContribution);
            atmosphereHeatContribution = Mathf.Clamp01(atmosphereHeatContribution);
            speedHeatContribution = Mathf.Clamp01(speedHeatContribution);
            aerodynamicHeatContribution = Mathf.Clamp01(aerodynamicHeatContribution);
            heatReferenceSpeed = Mathf.Max(1f, heatReferenceSpeed);
            groundEffectAltitude = Mathf.Max(0.01f, groundEffectAltitude);
            AutoAssignReferences();
            InitializeNozzles();
        }

        void Update()
        {
            SpacecraftThrusterVfxFrame target = BuildTargetFrame();
            float deltaTime = Time.deltaTime;

            throttle = Smooth(throttle, target.Throttle, throttleResponse, deltaTime);
            boost = Smooth(boost, target.Boost, boostResponse, deltaTime);
            atmosphereDensity = Smooth(atmosphereDensity, target.AtmosphereDensity, atmosphereResponse, deltaTime);

            float heatResponse = target.Heat >= heat ? heatRiseResponse : heatFallResponse;
            heat = Smooth(heat, target.Heat, heatResponse, deltaTime);

            CurrentFrame = new SpacecraftThrusterVfxFrame(
                throttle,
                boost,
                heat,
                atmosphereDensity,
                target.RelativeSpeed,
                target.LocalTranslation,
                target.LocalRotation,
                target.LocalLinearAcceleration,
                target.LocalAngularAcceleration,
                target.Thrusters,
                target.GroundProximity);

            ApplyNozzles(deltaTime);
            ApplyDebug(CurrentFrame);
        }

        void AutoAssignReferences()
        {
            motor ??= GetComponentInParent<SpacecraftMotor>();
            celestialProbe ??= GetComponentInParent<CelestialActorProbe>();
            oceanInteractor ??= GetComponentInParent<SpacecraftOceanInteractor>();
            atmosphereInteractor ??= GetComponentInParent<SpacecraftAtmosphereInteractor>();
        }

        void AutoAssignNozzles()
        {
            nozzles = GetComponentsInChildren<SpacecraftThrusterNozzleVfx>(true);
        }

        void InitializeNozzles()
        {
            if (nozzles == null)
            {
                return;
            }

            for (int i = 0; i < nozzles.Length; i++)
            {
                nozzles[i]?.Initialize();
            }
        }

        SpacecraftThrusterVfxFrame BuildTargetFrame()
        {
            if (motor == null)
            {
                return SpacecraftThrusterVfxFrame.Idle;
            }

            SpacecraftMovementTelemetry movement = motor.Telemetry;
            SpacecraftThrusterCommand thrusters = movement.Thrusters;
            float mainThrustDemand = CalculateMainThrustDemand(thrusters);
            float weightedAccelerationLoad = CalculateForwardAccelerationLoad(
                movement.LocalLinearAcceleration,
                throttleReferenceAcceleration);

            float targetThrottle = Mathf.Max(
                mainThrustDemand,
                weightedAccelerationLoad * accelerationLoadWeight);

            float targetBoost = Mathf.Clamp01(movement.BoostBlend);
            if (movement.BoostActive)
            {
                targetThrottle = Mathf.Max(targetThrottle, Mathf.Lerp(boostLoadFloor, 1f, targetBoost));
            }

            float targetAtmosphereDensity = SampleAtmosphereDensity();
            float propulsionHeat =
                targetThrottle * 0.25f +
                targetBoost * boostHeatContribution;
            float aerodynamicHeat = SampleAerodynamicHeat(
                movement.RelativeSpeed,
                targetAtmosphereDensity);
            float targetHeat = Mathf.Clamp01(Mathf.Max(propulsionHeat, aerodynamicHeat));
            return new SpacecraftThrusterVfxFrame(
                targetThrottle,
                targetBoost,
                targetHeat,
                targetAtmosphereDensity,
                movement.RelativeSpeed,
                movement.Command.Translation,
                movement.Command.Rotation,
                movement.LocalLinearAcceleration,
                movement.LocalAngularAcceleration,
                thrusters,
                SampleGroundProximity());
        }

        void ApplyNozzles(float deltaTime)
        {
            if (nozzles == null)
            {
                return;
            }

            for (int i = 0; i < nozzles.Length; i++)
            {
                nozzles[i]?.ApplyFrame(CurrentFrame, deltaTime);
            }
        }

        void ApplyDebug(SpacecraftThrusterVfxFrame frame)
        {
            debugThrottle = frame.Throttle;
            debugBoost = frame.Boost;
            debugHeat = frame.Heat;
            debugAtmosphereDensity = frame.AtmosphereDensity;
            debugGroundProximity = frame.GroundProximity;
            debugLocalLinearAcceleration = frame.LocalLinearAcceleration;
        }

        float SampleGroundProximity()
        {
            if (celestialProbe == null || !celestialProbe.HasSample)
            {
                return 0f;
            }

            return CalculateGroundProximity(
                celestialProbe.CurrentSample.SurfaceAltitude,
                groundEffectAltitude);
        }

        internal static float CalculateGroundProximity(float surfaceAltitude, float referenceAltitude)
        {
            if (float.IsNaN(surfaceAltitude) || float.IsInfinity(surfaceAltitude))
            {
                return 0f;
            }

            return 1f - Mathf.Clamp01(surfaceAltitude / Mathf.Max(0.01f, referenceAltitude));
        }

        float SampleAtmosphereDensity()
        {
            if (suppressAtmosphereVfxUnderwater &&
                oceanInteractor != null &&
                oceanInteractor.CurrentInteraction.SubmergedFraction > 0.15f)
            {
                return 0f;
            }

            if (celestialProbe == null || !celestialProbe.HasSample)
            {
                return 0f;
            }

            if (atmosphereInteractor != null)
            {
                return atmosphereInteractor.CurrentInteraction.AtmosphereDensity;
            }

            CelestialFrameSample sample = celestialProbe.CurrentSample;
            return sample.IsInsideAtmosphere ? sample.AtmosphereNormalizedDepth : 0f;
        }

        float SampleAerodynamicHeat(float relativeSpeed, float normalizedDensity)
        {
            if (atmosphereInteractor != null)
            {
                return atmosphereInteractor.CurrentInteraction.HeatLoad *
                    aerodynamicHeatContribution;
            }

            float speedHeat = Mathf.Clamp01(relativeSpeed / heatReferenceSpeed) *
                speedHeatContribution;
            return Mathf.Clamp01(
                normalizedDensity * atmosphereHeatContribution +
                speedHeat);
        }

        internal static float CalculateMainThrustDemand(SpacecraftThrusterCommand thrusters)
        {
            float angularStabilization = thrusters.AngularActivity * 0.18f;
            return Mathf.Clamp01(Mathf.Max(thrusters.Forward, angularStabilization));
        }

        internal static float CalculateForwardAccelerationLoad(
            Vector3 localLinearAcceleration,
            float referenceAcceleration)
        {
            float forwardAcceleration = Mathf.Max(0f, localLinearAcceleration.z);
            return Mathf.Clamp01(forwardAcceleration / Mathf.Max(0.01f, referenceAcceleration));
        }

        static float Smooth(float current, float target, float response, float deltaTime)
        {
            float responseT = response <= 0f ? 1f : 1f - Mathf.Exp(-response * Mathf.Max(0f, deltaTime));
            return Mathf.Lerp(current, target, responseT);
        }
    }
}
