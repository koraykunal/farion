using Farion.Gameplay.Actors;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Session;
using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Audio.Spacecraft
{
    [DefaultExecutionOrder(315)]
    [DisallowMultipleComponent]
    public sealed class ShipAudioTelemetryProvider : MonoBehaviour, ILocalPilotContextReceiver
    {
        [Header("Tuning")]
        [SerializeField] SpacecraftAudioTuningProfile tuningProfile;

        [Header("Sources")]
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] CelestialActorProbe celestialProbe;
        [SerializeField] SpacecraftSurfaceContactProbe surfaceContactProbe;
        [SerializeField] SpacecraftOceanInteractor oceanInteractor;
        [SerializeField] SpacecraftAtmosphereInteractor atmosphereInteractor;
        [SerializeField] MonoBehaviour pilotContextSource;

        [Header("Runtime Debug")]
        [SerializeField, Range(0f, 1f)] float debugEngineLoad;
        [SerializeField, Range(0f, 1f)] float debugMainThrustDemand;
        [SerializeField, Range(0f, 1f)] float debugWeightedAccelerationLoad;
        [SerializeField, Range(0f, 1f)] float debugBoost;
        [SerializeField, Range(0f, 1f)] float debugAerodynamicStress;
        [SerializeField] Vector3 debugLocalLinearAcceleration;

        ShipAudioTelemetry telemetry = ShipAudioTelemetry.Silent;
        float lastContactTime = float.NegativeInfinity;
        float impactEnvelope;
        ILocalPilotContext pilotContext;

        public ShipAudioTelemetry Telemetry => telemetry;

        public void SetPilotContext(ILocalPilotContext context)
        {
            pilotContext = context;
        }

        void Reset()
        {
            AutoAssignSources();
        }

        void Awake()
        {
            AutoAssignSources();
            RefreshTelemetry();
        }

        void OnValidate()
        {
            if (pilotContextSource != null && pilotContextSource is not ILocalPilotContext)
            {
                pilotContextSource = null;
            }

            AutoAssignSources();
        }

        public void RefreshTelemetry()
        {
            float deltaTime = Application.isPlaying ? Time.deltaTime : 0f;

            if (motor == null)
            {
                telemetry = ShipAudioTelemetry.Silent;
                return;
            }

            SpacecraftMovementTelemetry movement = motor.Telemetry;
            SpacecraftThrusterCommand thrusters = movement.Thrusters;
            float normalizedSpeed = NormalizeSpeed(movement.RelativeSpeed);
            float normalizedLinearAcceleration = NormalizeLinearAcceleration(movement.LocalLinearAcceleration.magnitude);
            float normalizedAngularAcceleration = NormalizeAngularAcceleration(movement.LocalAngularAcceleration.magnitude);
            float boost = Mathf.Clamp01(movement.BoostBlend);

            float mainThrustDemand = CalculateMainThrustDemand(thrusters);
            float weightedAccelerationLoad = CalculateWeightedAccelerationLoad(movement.LocalLinearAcceleration);
            float engineLoad = Mathf.Max(mainThrustDemand, weightedAccelerationLoad * AccelerationLoadWeight);
            if (movement.BoostActive)
            {
                engineLoad = Mathf.Max(engineLoad, Mathf.Lerp(BoostLoadFloor, 1f, boost));
            }

            debugEngineLoad = Mathf.Clamp01(engineLoad);
            debugMainThrustDemand = mainThrustDemand;
            debugWeightedAccelerationLoad = weightedAccelerationLoad;
            debugBoost = boost;
            debugLocalLinearAcceleration = movement.LocalLinearAcceleration;

            float impact = SampleImpact(deltaTime);
            float atmosphere = SampleAtmosphere();
            float water = oceanInteractor != null ? oceanInteractor.CurrentInteraction.SubmergedFraction : 0f;
            float pressureStress = oceanInteractor != null ? oceanInteractor.CurrentInteraction.PressureStress : 0f;
            float aerodynamicStress = atmosphereInteractor != null
                ? atmosphereInteractor.CurrentInteraction.AerodynamicStress
                : 0f;
            debugAerodynamicStress = aerodynamicStress;
            float hullStress = Mathf.Max(normalizedLinearAcceleration, normalizedAngularAcceleration);
            hullStress = Mathf.Max(hullStress, pressureStress);
            hullStress = Mathf.Max(hullStress, aerodynamicStress);
            hullStress = Mathf.Max(hullStress, impact);

            telemetry = new ShipAudioTelemetry(
                engineLoad,
                thrusters.Forward,
                thrusters.Reverse,
                Mathf.Max(thrusters.StrafeLeft, thrusters.StrafeRight),
                Mathf.Max(thrusters.Ascend, thrusters.Descend),
                thrusters.AngularActivity,
                boost,
                movement.BoostActive,
                normalizedSpeed,
                hullStress,
                impact,
                atmosphere,
                water,
                ResolvePerspective());
        }

        void AutoAssignSources()
        {
            motor ??= GetComponentInParent<SpacecraftMotor>();
            celestialProbe ??= GetComponentInParent<CelestialActorProbe>();
            surfaceContactProbe ??= GetComponentInParent<SpacecraftSurfaceContactProbe>();
            oceanInteractor ??= GetComponentInParent<SpacecraftOceanInteractor>();
            atmosphereInteractor ??= GetComponentInParent<SpacecraftAtmosphereInteractor>();
            pilotContextSource ??= GetComponentInParent<PlayerPossessionController>();
            pilotContext ??= pilotContextSource as ILocalPilotContext;
        }

        float CalculateMainThrustDemand(SpacecraftThrusterCommand thrusters)
        {
            float lateral = Mathf.Max(thrusters.StrafeLeft, thrusters.StrafeRight) * 0.3f;
            float vertical = Mathf.Max(thrusters.Ascend, thrusters.Descend) * 0.25f;
            float reverse = thrusters.Reverse * 0.45f;
            return Mathf.Clamp01(Mathf.Max(thrusters.Forward, reverse, lateral, vertical));
        }

        float CalculateWeightedAccelerationLoad(Vector3 localLinearAcceleration)
        {
            float weightedAcceleration =
                Mathf.Abs(localLinearAcceleration.z) +
                Mathf.Abs(localLinearAcceleration.x) * 0.3f +
                Mathf.Abs(localLinearAcceleration.y) * 0.25f;

            return NormalizeLinearAcceleration(weightedAcceleration);
        }

        float SampleImpact(float deltaTime)
        {
            impactEnvelope = Mathf.MoveTowards(impactEnvelope, 0f, Mathf.Max(0f, deltaTime) * 2f);
            if (surfaceContactProbe == null)
            {
                return impactEnvelope;
            }

            SpacecraftSurfaceContactSample contact = surfaceContactProbe.CurrentContact;
            if (!contact.HasContact || contact.Time <= lastContactTime)
            {
                return impactEnvelope;
            }

            lastContactTime = contact.Time;
            float impactSpeed = Mathf.Max(contact.NormalSpeed, contact.TangentialSpeed * 0.35f);
            impactEnvelope = Mathf.Max(impactEnvelope, NormalizeImpactSpeed(impactSpeed));
            return impactEnvelope;
        }

        float SampleAtmosphere()
        {
            if (atmosphereInteractor != null)
            {
                return atmosphereInteractor.CurrentInteraction.AtmosphereDensity;
            }

            if (celestialProbe == null || !celestialProbe.HasSample)
            {
                return 0f;
            }

            CelestialFrameSample sample = celestialProbe.CurrentSample;
            return sample.IsInsideAtmosphere ? sample.AtmosphereNormalizedDepth : 0f;
        }

        SpacecraftAudioPerspective ResolvePerspective()
        {
            if (pilotContext == null)
            {
                return SpacecraftAudioPerspective.Exterior;
            }

            return pilotContext.CurrentMode switch
            {
                PlayerPossessionMode.Spacecraft => pilotContext.CurrentPilotCameraView == SpacecraftPilotCameraView.Cockpit
                    ? SpacecraftAudioPerspective.Cockpit
                    : SpacecraftAudioPerspective.Exterior,
                PlayerPossessionMode.ShipInterior => SpacecraftAudioPerspective.ShipInterior,
                PlayerPossessionMode.OnFoot => SpacecraftAudioPerspective.OnFootExterior,
                _ => SpacecraftAudioPerspective.Exterior
            };
        }

        float NormalizeSpeed(float value)
        {
            return tuningProfile != null ? tuningProfile.NormalizeSpeed(value) : Mathf.Clamp01(value / 260f);
        }

        float NormalizeLinearAcceleration(float value)
        {
            return tuningProfile != null ? tuningProfile.NormalizeLinearAcceleration(value) : Mathf.Clamp01(value / 24f);
        }

        float NormalizeAngularAcceleration(float value)
        {
            return tuningProfile != null ? tuningProfile.NormalizeAngularAcceleration(value) : Mathf.Clamp01(value / 4f);
        }

        float NormalizeImpactSpeed(float value)
        {
            return tuningProfile != null ? tuningProfile.NormalizeImpactSpeed(value) : Mathf.Clamp01(value / 12f);
        }

        float AccelerationLoadWeight => tuningProfile != null ? tuningProfile.AccelerationLoadWeight : 0.85f;
        float BoostLoadFloor => tuningProfile != null ? tuningProfile.BoostLoadFloor : 0.75f;
    }
}
