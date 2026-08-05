using Farion.Simulation.Physics;
using Farion.Gameplay.Actors;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SpacecraftMotor : MonoBehaviour
    {
        const float DefaultTranslationSpoolRate = 7f;
        const float DefaultRotationSpoolRate = 8f;
        const float DefaultBoostSpoolRate = 3.5f;
        const float DefaultBoostDrainPerSecond = 0.28f;
        const float DefaultBoostRechargePerSecond = 0.16f;
        const float DefaultBoostRechargeDelay = 1.2f;
        const float DefaultInputDeadZone = 0.04f;

        [Header("Input")]
        [SerializeField] MonoBehaviour inputSource;

        [Header("Flight Model")]
        [SerializeField] SpacecraftFlightProfile flightProfile;
        [FormerlySerializedAs("enableFlightAssist")]
        [SerializeField] bool flightAssistEnabled = true;
        [SerializeField] bool useBodyRelativeFlightAssist = true;

        [Header("Gravity")]
        [SerializeField] GravitySimulation simulation;
        [SerializeField] bool applyGravity = true;
        [SerializeField] CelestialActorProbe celestialProbe;

        [Header("Rotation Input")]
        [Range(0.1f, 4f)]
        [SerializeField] float lookInputLimit = 1f;

        [Header("Surface Contact")]
        [SerializeField] SpacecraftSurfaceContactProbe surfaceContactProbe;
        [SerializeField] bool suspendRotationWhileInSurfaceContact = true;

        Rigidbody cachedRigidbody;
        ISpacecraftInputSource resolvedInput;
        SpacecraftInputState currentInput;
        SpacecraftPilotCommand requestedCommand = SpacecraftPilotCommand.None;
        SpacecraftPilotCommand currentCommand = SpacecraftPilotCommand.None;
        SpacecraftThrusterCommand currentThrusterCommand = SpacecraftThrusterCommand.None;
        SpacecraftMovementTelemetry telemetry = SpacecraftMovementTelemetry.Empty;
        readonly SpacecraftBoostController boostController = new();
        Vector3 smoothedTranslation;
        Vector3 smoothedRotationInput;
        Vector3 lastGravityAcceleration;
        Vector3 lastThrustAcceleration;
        Vector3 lastFlightAssistAcceleration;
        Vector3 lastGravityCompensationAcceleration;
        Vector3 lastLocalLinearAcceleration;
        Vector3 lastLocalAngularAcceleration;

        public Rigidbody Rigidbody =>
            cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();
        public SpacecraftFlightProfile FlightProfile => flightProfile;
        public SpacecraftFlightAssistMode AssistMode => flightAssistEnabled
            ? SpacecraftFlightAssistMode.Assisted
            : SpacecraftFlightAssistMode.Manual;
        public SpacecraftPilotCommand CurrentCommand => currentCommand;
        public SpacecraftThrusterCommand CurrentThrusterCommand => currentThrusterCommand;
        public SpacecraftMovementTelemetry Telemetry => telemetry;
        public Vector3 LastGravityAcceleration => lastGravityAcceleration;
        public Vector3 LastThrustAcceleration => lastThrustAcceleration;
        public Vector3 LastFlightAssistAcceleration => lastFlightAssistAcceleration;
        public Vector3 LastGravityCompensationAcceleration => lastGravityCompensationAcceleration;
        public Vector3 LastLocalTranslationInput => smoothedTranslation;
        public Vector3 LastLocalRotationInput => smoothedRotationInput;
        public Vector3 CurrentLocalTranslationInput => currentInput.Translation;
        public bool FlightAssistEnabled => flightAssistEnabled;
        public bool BoostActive => boostController.IsActive;
        public float BoostAuthority => boostController.Authority;
        public float BoostCharge => boostController.Charge;
        public float CurrentBoostMultiplier => 1f + boostController.Authority;
        public Vector3 Velocity => Rigidbody.linearVelocity;
        public Vector3 RelativeVelocity => Rigidbody.linearVelocity - ResolveFlightReferenceVelocity();
        public float Speed => Velocity.magnitude;
        public float RelativeSpeed => RelativeVelocity.magnitude;
        public bool RotationSuspendedByContact => suspendRotationWhileInSurfaceContact &&
            surfaceContactProbe != null &&
            surfaceContactProbe.HasContact;

        void Awake()
        {
            ConfigureRigidbody();
            ResolveReferences();
            boostController.Reset();
            RefreshTelemetry();
        }

        void OnValidate()
        {
            lookInputLimit = Mathf.Clamp(lookInputLimit, 0.1f, 4f);
            if (inputSource != null && inputSource is not ISpacecraftInputSource)
            {
                inputSource = null;
            }

            ResolveContactProbe();
            ResolveCelestialProbe();
        }

        void Update()
        {
            ResolveReferences();
            currentInput = resolvedInput?.CurrentInput ?? SpacecraftInputState.None;
            SpacecraftPilotCommand nextCommand = BuildPilotCommand(currentInput);
            if (nextCommand.ToggleFlightAssist)
            {
                flightAssistEnabled = !flightAssistEnabled;
            }

            requestedCommand = new SpacecraftPilotCommand(
                nextCommand.Translation,
                nextCommand.Rotation,
                nextCommand.Boost,
                nextCommand.Brake,
                toggleFlightAssist: false);
        }

        void FixedUpdate()
        {
            ResolveReferences();
            float deltaTime = Time.fixedDeltaTime;
            ApplyGravity();
            UpdateSmoothedCommand(deltaTime);
            UpdateBoost(deltaTime);
            ApplyFlightControl();
            RefreshTelemetry();
        }

        public void SetInputSource(ISpacecraftInputSource source)
        {
            resolvedInput = source;
            inputSource = source as MonoBehaviour;
        }

        public void SetSimulation(GravitySimulation source)
        {
            simulation = source;
        }

        public void SetFlightAssistEnabled(bool enabled)
        {
            flightAssistEnabled = enabled;
        }

        void ConfigureRigidbody()
        {
            Rigidbody.useGravity = false;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (flightProfile != null)
            {
                Rigidbody.mass = flightProfile.RigidbodyMass;
                Rigidbody.angularDamping = flightProfile.AngularDamping;
                if (flightProfile.OverrideCenterOfMass)
                {
                    Rigidbody.centerOfMass = flightProfile.CenterOfMass;
                }
                else
                {
                    Rigidbody.ResetCenterOfMass();
                }

                Vector3 maxAngularRate = flightProfile.MaxAngularRate();
                Rigidbody.maxAngularVelocity = Mathf.Max(
                    maxAngularRate.x,
                    maxAngularRate.y,
                    maxAngularRate.z) * 1.1f;
            }
        }

        void ResolveReferences()
        {
            ResolveInputSource();
            ResolveContactProbe();
            ResolveCelestialProbe();
        }

        void ResolveInputSource()
        {
            if (inputSource is ISpacecraftInputSource explicitSource)
            {
                resolvedInput = explicitSource;
                return;
            }

            resolvedInput ??= GetComponent<ISpacecraftInputSource>();
        }

        void ResolveContactProbe()
        {
            surfaceContactProbe ??= GetComponent<SpacecraftSurfaceContactProbe>();
        }

        void ResolveCelestialProbe()
        {
            celestialProbe ??= GetComponent<CelestialActorProbe>();
        }

        void ApplyGravity()
        {
            lastGravityAcceleration = Vector3.zero;
            if (!applyGravity)
            {
                return;
            }

            GravitySimulation source = simulation;
            if (source == null)
            {
                return;
            }

            lastGravityAcceleration =
                source.CalculateReferenceFrameAcceleration(Rigidbody.position);
            Rigidbody.AddForce(lastGravityAcceleration, ForceMode.Acceleration);
        }

        void UpdateSmoothedCommand(float deltaTime)
        {
            Vector3 targetTranslation = DeadZone(requestedCommand.Translation, InputDeadZone);
            smoothedTranslation = Vector3.Lerp(
                smoothedTranslation,
                targetTranslation,
                ResponsivenessToLerp(TranslationSpoolRate, deltaTime));
            smoothedTranslation = DeadZone(smoothedTranslation, 0.001f);

            smoothedRotationInput = Vector3.Lerp(
                smoothedRotationInput,
                requestedCommand.Rotation,
                ResponsivenessToLerp(RotationSpoolRate, deltaTime));
            smoothedRotationInput = DeadZone(smoothedRotationInput, 0.001f);

            currentCommand = new SpacecraftPilotCommand(
                smoothedTranslation,
                smoothedRotationInput,
                requestedCommand.Boost,
                requestedCommand.Brake,
                toggleFlightAssist: false);
        }

        void UpdateBoost(float deltaTime)
        {
            boostController.Step(
                currentCommand.Boost,
                currentCommand.Translation.z > InputDeadZone,
                deltaTime,
                BoostSpoolRate,
                BoostDrainPerSecond,
                BoostRechargePerSecond,
                BoostRechargeDelay);
        }

        void ApplyFlightControl()
        {
            Vector3 localRelativeVelocity = transform.InverseTransformDirection(RelativeVelocity);
            Vector3 localAngularVelocity = transform.InverseTransformDirection(Rigidbody.angularVelocity);
            Vector3 localGravity = transform.InverseTransformDirection(lastGravityAcceleration);
            SpacecraftFlightControlFrame frame = new(
                currentCommand,
                localRelativeVelocity,
                localAngularVelocity,
                localGravity,
                flightAssistEnabled,
                boostController.Authority);
            SpacecraftFlightControlOutput output = SpacecraftFlightControlLaw.Evaluate(
                frame,
                ControlSettings);

            lastLocalLinearAcceleration = output.LocalLinearAcceleration;
            lastThrustAcceleration = transform.TransformDirection(output.LocalLinearAcceleration);
            lastFlightAssistAcceleration = transform.TransformDirection(output.LocalAssistAcceleration);
            lastGravityCompensationAcceleration =
                transform.TransformDirection(output.LocalGravityCompensation);

            if (lastThrustAcceleration.sqrMagnitude > 0.000001f)
            {
                Rigidbody.AddForce(lastThrustAcceleration, ForceMode.Acceleration);
            }

            lastLocalAngularAcceleration = output.LocalAngularAcceleration;
            if (RotationSuspendedByContact)
            {
                lastLocalAngularAcceleration = Vector3.zero;
                return;
            }

            if (lastLocalAngularAcceleration.sqrMagnitude > 0.000001f)
            {
                Rigidbody.AddRelativeTorque(lastLocalAngularAcceleration, ForceMode.Acceleration);
            }
        }

        SpacecraftPilotCommand BuildPilotCommand(SpacecraftInputState input)
        {
            Vector2 clampedLook = Vector2.ClampMagnitude(input.Look, lookInputLimit);
            Vector3 rotation = new(
                -clampedLook.y / lookInputLimit,
                clampedLook.x / lookInputLimit,
                -input.Roll);

            return new SpacecraftPilotCommand(
                input.Translation,
                rotation,
                input.Boost,
                input.Brake,
                input.ToggleFlightAssist);
        }

        SpacecraftThrusterCommand BuildThrusterCommand(
            Vector3 localAcceleration,
            Vector3 localAngularAcceleration)
        {
            SpacecraftFlightControlSettings settings = ControlSettings;
            Vector3 positive = settings.MaxPositiveAcceleration(boostController.Authority);
            Vector3 negative = settings.NegativeAcceleration;
            Vector3 angular = settings.MaxAngularAcceleration;

            return new SpacecraftThrusterCommand(
                NormalizePositive(localAcceleration.z, positive.z),
                NormalizeNegative(localAcceleration.z, negative.z),
                NormalizePositive(localAcceleration.x, positive.x),
                NormalizeNegative(localAcceleration.x, negative.x),
                NormalizePositive(localAcceleration.y, positive.y),
                NormalizeNegative(localAcceleration.y, negative.y),
                NormalizeNegative(localAngularAcceleration.x, angular.x),
                NormalizePositive(localAngularAcceleration.x, angular.x),
                NormalizePositive(localAngularAcceleration.y, angular.y),
                NormalizeNegative(localAngularAcceleration.y, angular.y),
                NormalizeNegative(localAngularAcceleration.z, angular.z),
                NormalizePositive(localAngularAcceleration.z, angular.z));
        }

        void RefreshTelemetry()
        {
            Vector3 relativeVelocity = RelativeVelocity;
            Vector3 localRelativeVelocity = transform.InverseTransformDirection(relativeVelocity);
            Vector3 localAngularVelocity = transform.InverseTransformDirection(Rigidbody.angularVelocity);
            currentThrusterCommand = BuildThrusterCommand(
                lastLocalLinearAcceleration,
                lastLocalAngularAcceleration);
            telemetry = new SpacecraftMovementTelemetry(
                AssistMode,
                currentCommand,
                currentThrusterCommand,
                localRelativeVelocity,
                localAngularVelocity,
                lastLocalLinearAcceleration,
                lastLocalAngularAcceleration,
                relativeVelocity,
                boostController.IsActive,
                boostController.Authority,
                boostController.Charge);
        }

        Vector3 ResolveFlightReferenceVelocity()
        {
            if (!useBodyRelativeFlightAssist)
            {
                return Vector3.zero;
            }

            if (surfaceContactProbe != null && surfaceContactProbe.HasContact)
            {
                SpacecraftSurfaceContactSample contact = surfaceContactProbe.CurrentContact;
                if (contact.HasContact && contact.Body != null)
                {
                    return contact.Body.GetVelocityAtPoint(Rigidbody.worldCenterOfMass);
                }
            }

            return celestialProbe != null && celestialProbe.HasSample
                ? celestialProbe.CurrentSample.BodyPointVelocity
                : Vector3.zero;
        }

        SpacecraftFlightControlSettings ControlSettings => flightProfile != null
            ? flightProfile.BuildControlSettings()
            : DefaultControlSettings;

        float TranslationSpoolRate =>
            flightProfile != null ? flightProfile.TranslationSpoolRate : DefaultTranslationSpoolRate;
        float RotationSpoolRate =>
            flightProfile != null ? flightProfile.RotationSpoolRate : DefaultRotationSpoolRate;
        float BoostSpoolRate =>
            flightProfile != null ? flightProfile.BoostSpoolRate : DefaultBoostSpoolRate;
        float BoostDrainPerSecond =>
            flightProfile != null ? flightProfile.BoostDrainPerSecond : DefaultBoostDrainPerSecond;
        float BoostRechargePerSecond =>
            flightProfile != null ? flightProfile.BoostRechargePerSecond : DefaultBoostRechargePerSecond;
        float BoostRechargeDelay =>
            flightProfile != null ? flightProfile.BoostRechargeDelay : DefaultBoostRechargeDelay;
        float InputDeadZone =>
            flightProfile != null ? flightProfile.InputDeadZone : DefaultInputDeadZone;

        static SpacecraftFlightControlSettings DefaultControlSettings => new(
            new Vector3(45f, 40f, 180f),
            new Vector3(45f, 40f, 260f),
            new Vector3(45f, 40f, 70f),
            new Vector3(12f, 16f, 20f),
            new Vector3(12f, 16f, 34f),
            new Vector3(12f, 16f, 18f),
            new Vector3(65f, 42f, 95f) * Mathf.Deg2Rad,
            new Vector3(180f, 140f, 240f) * Mathf.Deg2Rad,
            new Vector3(2.8f, 2.8f, 2.2f),
            new Vector3(7f, 7f, 9f),
            3.2f,
            compensateGravity: true,
            limitManualFlightEnvelope: true,
            manualEnvelopeStart: 0.85f);

        static Vector3 DeadZone(Vector3 value, float deadZone)
        {
            return value.sqrMagnitude <= deadZone * deadZone ? Vector3.zero : value;
        }

        static float NormalizePositive(float value, float limit)
        {
            return limit <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, value) / limit);
        }

        static float NormalizeNegative(float value, float limit)
        {
            return limit <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, -value) / limit);
        }

        static float ResponsivenessToLerp(float responsiveness, float deltaTime)
        {
            return responsiveness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-responsiveness * deltaTime);
        }
    }
}
