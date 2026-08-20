using Farion.Gameplay.Actors;
using Farion.Gameplay.Domain.Systems;
using Farion.Simulation.Physics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Gameplay.Flight
{
    public interface ISpacecraftPhysicsBody
    {
        Vector3 Position { get; }
        Vector3 WorldCenterOfMass { get; }
        Vector3 LinearVelocity { get; }
        Vector3 AngularVelocity { get; }
        void SetLinearVelocity(Vector3 velocity);
        void SetAngularVelocity(Vector3 velocity);
        void MovePosition(Vector3 position);
        void AddForce(Vector3 force, ForceMode mode);
        void AddForceAtPosition(Vector3 force, Vector3 worldPosition, ForceMode mode);
        void AddRelativeTorque(Vector3 torque, ForceMode mode);
        void Commit();
    }

    public struct SpacecraftMotorState
    {
        public bool FlightAssistEnabled;
        public Vector3 RequestedTranslation;
        public Vector3 RequestedRotation;
        public bool RequestedBoost;
        public bool RequestedBrake;
        public Vector3 CurrentTranslation;
        public Vector3 CurrentRotation;
        public bool CurrentBoost;
        public bool CurrentBrake;
        public Vector3 SmoothedTranslation;
        public Vector3 SmoothedRotationInput;
        public SpacecraftBoostState Boost;
        public ResourcePool Fuel;
    }

    sealed class RigidbodySpacecraftPhysicsBody : ISpacecraftPhysicsBody
    {
        readonly Rigidbody body;

        public RigidbodySpacecraftPhysicsBody(Rigidbody body) => this.body = body;
        public Vector3 Position => body.position;
        public Vector3 WorldCenterOfMass => body.worldCenterOfMass;
        public Vector3 LinearVelocity => body.linearVelocity;
        public Vector3 AngularVelocity => body.angularVelocity;
        public void SetLinearVelocity(Vector3 velocity) =>
            body.linearVelocity = velocity;
        public void SetAngularVelocity(Vector3 velocity) =>
            body.angularVelocity = velocity;
        public void MovePosition(Vector3 position) => body.MovePosition(position);
        public void AddForce(Vector3 force, ForceMode mode) => body.AddForce(force, mode);
        public void AddForceAtPosition(Vector3 force, Vector3 worldPosition, ForceMode mode) =>
            body.AddForceAtPosition(force, worldPosition, mode);
        public void AddRelativeTorque(Vector3 torque, ForceMode mode) =>
            body.AddRelativeTorque(torque, mode);
        public void Commit()
        {
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SpacecraftMotor : MonoBehaviour
    {
        const float DefaultTranslationSpoolRate = 7f;
        const float DefaultRotationSpoolRate = 8f;
        const float DefaultBoostSpoolRate = 3.5f;
        const float DefaultInputDeadZone = 0.04f;
        const float DefaultFuelCapacity = 1000f;
        const float DefaultFuelPerAccelerationUnit = 0.05f;

        [Header("Input")]
        [SerializeField] KeyboardSpacecraftInput inputSource;

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
        RigidbodySpacecraftPhysicsBody offlinePhysicsBody;
        KeyboardSpacecraftInput resolvedInput;
        SpacecraftInputState currentInput;
        SpacecraftPilotCommand requestedCommand = SpacecraftPilotCommand.None;
        SpacecraftPilotCommand currentCommand = SpacecraftPilotCommand.None;
        SpacecraftThrusterCommand currentThrusterCommand = SpacecraftThrusterCommand.None;
        SpacecraftMovementTelemetry telemetry = SpacecraftMovementTelemetry.Empty;
        readonly SpacecraftBoostController boostController = new();
        ShipModuleBonuses moduleBonuses = ShipModuleBonuses.None;
        ResourcePool fuel;
        Vector3 smoothedTranslation;
        Vector3 smoothedRotationInput;
        Vector3 lastGravityAcceleration;
        Vector3 lastThrustAcceleration;
        Vector3 lastFlightAssistAcceleration;
        Vector3 lastGravityCompensationAcceleration;
        Vector3 lastLocalLinearAcceleration;
        Vector3 lastLocalAngularAcceleration;
        bool externalSimulation;

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
        public float CurrentBoostMultiplier => 1f + boostController.Authority;
        public ShipModuleBonuses ModuleBonuses => moduleBonuses;
        public ResourcePool Fuel => fuel;
        public float FuelNormalized => fuel.Normalized;
        public float MaxForwardSpeed => flightProfile != null
            ? flightProfile.EvaluateMaxForwardSpeed(moduleBonuses)
            : DefaultControlSettings.PositiveMaxSpeed.z;
        public float MaxReverseSpeed => flightProfile != null
            ? flightProfile.MaxReverseSpeed
            : DefaultControlSettings.NegativeMaxSpeed.z;
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
            offlinePhysicsBody = new RigidbodySpacecraftPhysicsBody(Rigidbody);
            ResolveReferences();
            boostController.Reset();
            RefillFuel();
            RefreshTelemetry();
        }

        void OnValidate()
        {
            lookInputLimit = Mathf.Clamp(lookInputLimit, 0.1f, 4f);

            ResolveContactProbe();
            ResolveCelestialProbe();
        }

        void Update()
        {
            if (externalSimulation)
            {
                return;
            }

            ResolveReferences();
            SetInput(resolvedInput?.CurrentInput ?? SpacecraftInputState.None);
        }

        void FixedUpdate()
        {
            if (externalSimulation)
            {
                return;
            }

            ResolveReferences();
            SimulatePreparedInput(Time.fixedDeltaTime, offlinePhysicsBody);
        }

        public void Simulate(
            SpacecraftInputState input,
            float deltaTime,
            ISpacecraftPhysicsBody physicsBody)
        {
            if (physicsBody == null || deltaTime <= 0f)
            {
                return;
            }

            SetInput(input);
            SimulatePreparedInput(deltaTime, physicsBody);
        }

        void SimulatePreparedInput(
            float deltaTime,
            ISpacecraftPhysicsBody physicsBody)
        {
            ApplyGravity(physicsBody);
            UpdateSmoothedCommand(deltaTime);
            UpdateBoost(deltaTime);
            ApplyFlightControl(deltaTime, physicsBody);
            physicsBody.Commit();
            RefreshTelemetry(physicsBody);
        }

        public void SetExternalSimulation(bool enabled)
        {
            externalSimulation = enabled;
            if (enabled)
            {
                currentInput = SpacecraftInputState.None;
                requestedCommand = SpacecraftPilotCommand.None;
            }
        }

        public SpacecraftMotorState CaptureState() => new()
        {
            FlightAssistEnabled = flightAssistEnabled,
            RequestedTranslation = requestedCommand.Translation,
            RequestedRotation = requestedCommand.Rotation,
            RequestedBoost = requestedCommand.Boost,
            RequestedBrake = requestedCommand.Brake,
            CurrentTranslation = currentCommand.Translation,
            CurrentRotation = currentCommand.Rotation,
            CurrentBoost = currentCommand.Boost,
            CurrentBrake = currentCommand.Brake,
            SmoothedTranslation = smoothedTranslation,
            SmoothedRotationInput = smoothedRotationInput,
            Boost = boostController.CaptureState(),
            Fuel = fuel
        };

        public void RestoreState(SpacecraftMotorState state)
        {
            flightAssistEnabled = state.FlightAssistEnabled;
            requestedCommand = new SpacecraftPilotCommand(
                state.RequestedTranslation,
                state.RequestedRotation,
                state.RequestedBoost,
                state.RequestedBrake,
                toggleFlightAssist: false);
            currentCommand = new SpacecraftPilotCommand(
                state.CurrentTranslation,
                state.CurrentRotation,
                state.CurrentBoost,
                state.CurrentBrake,
                toggleFlightAssist: false);
            smoothedTranslation = state.SmoothedTranslation;
            smoothedRotationInput = state.SmoothedRotationInput;
            boostController.RestoreState(state.Boost);
            fuel = state.Fuel;
        }

        public void SetModuleBonuses(in ShipModuleBonuses bonuses)
        {
            moduleBonuses = bonuses;
            fuel.SetCapacity(FuelCapacity);
        }

        public void RefillFuel()
        {
            fuel = ResourcePool.Full(FuelCapacity);
        }

        public void RestoreFuel(ResourcePool saved)
        {
            fuel = new ResourcePool(FuelCapacity, saved.Current);
        }

        public void SetInputSource(KeyboardSpacecraftInput source)
        {
            resolvedInput = source;
            inputSource = source;
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
            if (inputSource != null)
            {
                resolvedInput = inputSource;
                return;
            }

            resolvedInput ??= GetComponent<KeyboardSpacecraftInput>();
        }

        void ResolveContactProbe()
        {
            surfaceContactProbe ??= GetComponent<SpacecraftSurfaceContactProbe>();
        }

        void ResolveCelestialProbe()
        {
            celestialProbe ??= GetComponent<CelestialActorProbe>();
        }

        void SetInput(SpacecraftInputState input)
        {
            currentInput = input;
            SpacecraftPilotCommand nextCommand = BuildPilotCommand(input);
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

        void ApplyGravity(ISpacecraftPhysicsBody physicsBody)
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
                source.CalculateReferenceFrameAcceleration(physicsBody.Position);
            physicsBody.AddForce(lastGravityAcceleration, ForceMode.Acceleration);
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
                currentCommand.Translation.z > InputDeadZone && HasThrust,
                deltaTime,
                BoostSpoolRate);
        }

        float ConsumeFuel(float deltaTime, Vector3 localLinearAcceleration)
        {
            if (fuel.Capacity <= 0f)
            {
                return 1f;
            }

            fuel.Drain(IdleFuelPerSecond * deltaTime);

            float demand = localLinearAcceleration.magnitude *
                FuelPerAccelerationUnit *
                deltaTime;
            if (demand <= 0f)
            {
                return 1f;
            }

            return fuel.Drain(demand) / demand;
        }

        void ApplyFlightControl(float deltaTime, ISpacecraftPhysicsBody physicsBody)
        {
            Vector3 relativeVelocity = physicsBody.LinearVelocity -
                ResolveFlightReferenceVelocity(physicsBody.WorldCenterOfMass);
            Vector3 localRelativeVelocity = transform.InverseTransformDirection(relativeVelocity);
            Vector3 localAngularVelocity = transform.InverseTransformDirection(physicsBody.AngularVelocity);
            Vector3 localGravity = transform.InverseTransformDirection(lastGravityAcceleration);
            SpacecraftFlightControlFrame frame = new(
                currentCommand,
                localRelativeVelocity,
                localAngularVelocity,
                localGravity,
                flightAssistEnabled,
                boostController.Authority,
                boostController.Surge);
            SpacecraftFlightControlOutput output = SpacecraftFlightControlLaw.Evaluate(
                frame,
                ControlSettings);

            float thrustScale = ConsumeFuel(deltaTime, output.LocalLinearAcceleration);
            lastLocalLinearAcceleration = output.LocalLinearAcceleration * thrustScale;
            lastThrustAcceleration = transform.TransformDirection(lastLocalLinearAcceleration);
            lastFlightAssistAcceleration =
                transform.TransformDirection(output.LocalAssistAcceleration * thrustScale);
            lastGravityCompensationAcceleration =
                transform.TransformDirection(output.LocalGravityCompensation * thrustScale);

            if (lastThrustAcceleration.sqrMagnitude > 0.000001f)
            {
                physicsBody.AddForce(lastThrustAcceleration, ForceMode.Acceleration);
            }

            lastLocalAngularAcceleration = output.LocalAngularAcceleration;
            if (RotationSuspendedByContact)
            {
                lastLocalAngularAcceleration = Vector3.zero;
                return;
            }

            if (lastLocalAngularAcceleration.sqrMagnitude > 0.000001f)
            {
                physicsBody.AddRelativeTorque(lastLocalAngularAcceleration, ForceMode.Acceleration);
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
            RefreshTelemetry(offlinePhysicsBody ??= new RigidbodySpacecraftPhysicsBody(Rigidbody));
        }

        void RefreshTelemetry(ISpacecraftPhysicsBody physicsBody)
        {
            Vector3 relativeVelocity = physicsBody.LinearVelocity -
                ResolveFlightReferenceVelocity(physicsBody.WorldCenterOfMass);
            Vector3 localRelativeVelocity = transform.InverseTransformDirection(relativeVelocity);
            Vector3 localAngularVelocity = transform.InverseTransformDirection(physicsBody.AngularVelocity);
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
                boostController.Surge,
                fuel.Normalized);
        }

        Vector3 ResolveFlightReferenceVelocity() =>
            ResolveFlightReferenceVelocity(Rigidbody.worldCenterOfMass);

        Vector3 ResolveFlightReferenceVelocity(Vector3 worldCenterOfMass)
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
                    return contact.Body.GetVelocityAtPoint(worldCenterOfMass);
                }
            }

            return celestialProbe != null && celestialProbe.HasSample
                ? celestialProbe.CurrentSample.BodyPointVelocity
                : Vector3.zero;
        }

        SpacecraftFlightControlSettings ControlSettings => flightProfile != null
            ? flightProfile.BuildControlSettings(moduleBonuses)
            : DefaultControlSettings;

        bool HasThrust => fuel.Capacity <= 0f || !fuel.IsEmpty;

        float TranslationSpoolRate =>
            flightProfile != null ? flightProfile.TranslationSpoolRate : DefaultTranslationSpoolRate;
        float RotationSpoolRate =>
            flightProfile != null ? flightProfile.RotationSpoolRate : DefaultRotationSpoolRate;
        float BoostSpoolRate =>
            flightProfile != null ? flightProfile.BoostSpoolRate : DefaultBoostSpoolRate;
        float InputDeadZone =>
            flightProfile != null ? flightProfile.InputDeadZone : DefaultInputDeadZone;
        float FuelCapacity => flightProfile != null
            ? flightProfile.EvaluateFuelCapacity(moduleBonuses)
            : DefaultFuelCapacity * moduleBonuses.FuelCapacityMultiplier;
        float FuelPerAccelerationUnit => flightProfile != null
            ? flightProfile.FuelPerAccelerationUnit
            : DefaultFuelPerAccelerationUnit;
        float IdleFuelPerSecond =>
            flightProfile != null ? flightProfile.IdleFuelPerSecond : 0f;

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
            manualEnvelopeStart: 0.85f,
            boostSurgeStrength: 0.8f);

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
