using Farion.Core.Numerics;
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
        Quaternion Rotation { get; }
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
        public Vector3 ReferenceVelocity;
        public bool HasReferenceVelocity;
        public SpacecraftBoostState Boost;
        public ResourcePool Fuel;
    }

    sealed class RigidbodySpacecraftPhysicsBody : ISpacecraftPhysicsBody
    {
        readonly Rigidbody body;

        public RigidbodySpacecraftPhysicsBody(Rigidbody body) => this.body = body;
        public Vector3 Position => body.position;
        public Quaternion Rotation => body.rotation;
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
        const float ReferenceVelocitySmoothingRate = 1.5f;

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
        [Tooltip("Turn authority retained while the hull rests on a surface. Below one the ship settles instead of skating, but it must stay above zero or the nose can never be raised for takeoff.")]
        [Range(0.1f, 1f)]
        [SerializeField] float surfaceContactAngularScale = 0.6f;

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
        bool driveDisabled;
        Vector3 smoothedTranslation;
        Vector3 smoothedRotationInput;
        Vector3 smoothedReferenceVelocity;
        bool hasSmoothedReferenceVelocity;
        Vector3 lastGravityAcceleration;
        Vector3 lastThrustAcceleration;
        Vector3 lastFlightAssistAcceleration;
        Vector3 lastGravityCompensationAcceleration;
        Vector3 lastLocalLinearAcceleration;
        Vector3 lastLocalAngularAcceleration;
        bool gravityExceedsThrust;
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
        public bool DriveDisabled => driveDisabled;
        public bool GravityExceedsThrust => gravityExceedsThrust;
        public float MaxForwardSpeed => flightProfile != null
            ? flightProfile.EvaluateMaxForwardSpeed(moduleBonuses)
            : 0f;
        public float MaxReverseSpeed => flightProfile != null
            ? flightProfile.MaxReverseSpeed
            : 0f;
        public Vector3 Velocity => Rigidbody.linearVelocity;
        public Vector3 RelativeVelocity => Rigidbody.linearVelocity - ResolveFlightReferenceVelocity();
        public float Speed => Velocity.magnitude;
        public float RelativeSpeed => RelativeVelocity.magnitude;
        public bool InSurfaceContact =>
            surfaceContactProbe != null && surfaceContactProbe.HasContact;
        public float SurfaceContactAngularScale =>
            Mathf.Clamp(surfaceContactAngularScale, 0.1f, 1f);

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
            if (flightProfile == null)
            {
                return;
            }

            ApplyGravity(physicsBody);
            UpdateReferenceVelocity(deltaTime, physicsBody.WorldCenterOfMass);
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
            ReferenceVelocity = smoothedReferenceVelocity,
            HasReferenceVelocity = hasSmoothedReferenceVelocity,
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
            smoothedReferenceVelocity = state.ReferenceVelocity;
            hasSmoothedReferenceVelocity = state.HasReferenceVelocity;
            boostController.RestoreState(state.Boost);
            fuel = state.Fuel;
        }

        public void SetDriveDisabled(bool disabled)
        {
            driveDisabled = disabled;
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
                source.CalculateReferenceFrameAcceleration(
                    physicsBody.Position,
                    physicsBody.LinearVelocity);
            physicsBody.AddForce(lastGravityAcceleration, ForceMode.Acceleration);
        }

        void UpdateSmoothedCommand(float deltaTime)
        {
            Vector3 targetTranslation = DeadZone(requestedCommand.Translation, InputDeadZone);
            smoothedTranslation = Vector3.Lerp(
                smoothedTranslation,
                targetTranslation,
                FarionMath.SmoothFactor(TranslationSpoolRate, deltaTime));
            smoothedTranslation = DeadZone(smoothedTranslation, 0.001f);

            smoothedRotationInput = Vector3.Lerp(
                smoothedRotationInput,
                requestedCommand.Rotation,
                FarionMath.SmoothFactor(RotationSpoolRate, deltaTime));
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
            if (driveDisabled)
            {
                return 0f;
            }

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
            Quaternion bodyRotation = physicsBody.Rotation;
            Quaternion inverseBodyRotation = Quaternion.Inverse(bodyRotation);
            Vector3 relativeVelocity = physicsBody.LinearVelocity -
                ResolveFlightReferenceVelocity(physicsBody.WorldCenterOfMass);
            Vector3 localRelativeVelocity = inverseBodyRotation * relativeVelocity;
            Vector3 localAngularVelocity = inverseBodyRotation * physicsBody.AngularVelocity;
            Vector3 localGravity = inverseBodyRotation * lastGravityAcceleration;
            SpacecraftFlightControlFrame frame = new(
                currentCommand,
                localRelativeVelocity,
                localAngularVelocity,
                localGravity,
                flightAssistEnabled,
                boostController.Authority,
                boostController.Surge);
            SpacecraftFlightControlSettings settings = ControlSettings;
            SpacecraftFlightControlOutput output = SpacecraftFlightControlLaw.Evaluate(
                frame,
                settings);

            Vector3 requiredCompensation = -localGravity;
            Vector3 positiveLimit = settings.MaxPositiveAcceleration(boostController.Authority);
            Vector3 negativeLimit = settings.NegativeAcceleration;
            gravityExceedsThrust = flightAssistEnabled && HasThrust &&
                (requiredCompensation.x > positiveLimit.x || -requiredCompensation.x > negativeLimit.x ||
                 requiredCompensation.y > positiveLimit.y || -requiredCompensation.y > negativeLimit.y ||
                 requiredCompensation.z > positiveLimit.z || -requiredCompensation.z > negativeLimit.z);

            float thrustScale = ConsumeFuel(deltaTime, output.LocalLinearAcceleration);
            lastLocalLinearAcceleration = output.LocalLinearAcceleration * thrustScale;
            lastThrustAcceleration = bodyRotation * lastLocalLinearAcceleration;
            lastFlightAssistAcceleration =
                bodyRotation * (output.LocalAssistAcceleration * thrustScale);
            lastGravityCompensationAcceleration =
                bodyRotation * (output.LocalGravityCompensation * thrustScale);

            if (lastThrustAcceleration.sqrMagnitude > 0.000001f)
            {
                physicsBody.AddForce(lastThrustAcceleration, ForceMode.Acceleration);
            }

            lastLocalAngularAcceleration = InSurfaceContact
                ? output.LocalAngularAcceleration * SurfaceContactAngularScale
                : output.LocalAngularAcceleration;

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
            Quaternion inverseBodyRotation = Quaternion.Inverse(physicsBody.Rotation);
            Vector3 relativeVelocity = physicsBody.LinearVelocity -
                ResolveFlightReferenceVelocity(physicsBody.WorldCenterOfMass);
            Vector3 localRelativeVelocity = inverseBodyRotation * relativeVelocity;
            Vector3 localAngularVelocity = inverseBodyRotation * physicsBody.AngularVelocity;
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
            return hasSmoothedReferenceVelocity
                ? smoothedReferenceVelocity
                : ResolveRawReferenceVelocity(worldCenterOfMass, out _);
        }

        void UpdateReferenceVelocity(float deltaTime, Vector3 worldCenterOfMass)
        {
            Vector3 target = ResolveRawReferenceVelocity(worldCenterOfMass, out bool hardReference);
            if (hardReference || !hasSmoothedReferenceVelocity)
            {
                smoothedReferenceVelocity = target;
                hasSmoothedReferenceVelocity = true;
                return;
            }

            smoothedReferenceVelocity = Vector3.Lerp(
                smoothedReferenceVelocity,
                target,
                FarionMath.SmoothFactor(ReferenceVelocitySmoothingRate, deltaTime));
        }

        Vector3 ResolveRawReferenceVelocity(Vector3 worldCenterOfMass, out bool hardReference)
        {
            hardReference = false;
            if (!useBodyRelativeFlightAssist)
            {
                hardReference = true;
                return Vector3.zero;
            }

            if (surfaceContactProbe != null && surfaceContactProbe.HasContact)
            {
                SpacecraftSurfaceContactSample contact = surfaceContactProbe.CurrentContact;
                if (contact.HasContact && contact.Body != null)
                {
                    hardReference = true;
                    return contact.Body.GetVelocityAtPoint(worldCenterOfMass);
                }
            }

            if (celestialProbe == null || !celestialProbe.HasSample)
            {
                return Vector3.zero;
            }

            var sample = celestialProbe.CurrentSample;
            Vector3 spinVelocity = sample.BodyPointVelocity - sample.BodyVelocity;
            float spinFade = sample.BodyRadius > 0f
                ? 1f - Mathf.Clamp01(sample.SurfaceAltitude / sample.BodyRadius)
                : 0f;
            return sample.BodyVelocity + spinVelocity * spinFade;
        }

        SpacecraftFlightControlSettings ControlSettings =>
            flightProfile.BuildControlSettings(moduleBonuses);

        bool HasThrust => !driveDisabled && (fuel.Capacity <= 0f || !fuel.IsEmpty);

        float TranslationSpoolRate => flightProfile.TranslationSpoolRate;
        float RotationSpoolRate => flightProfile.RotationSpoolRate;
        float BoostSpoolRate => flightProfile.BoostSpoolRate;
        float InputDeadZone => flightProfile.InputDeadZone;
        float FuelCapacity => flightProfile != null
            ? flightProfile.EvaluateFuelCapacity(moduleBonuses)
            : 0f;
        float FuelPerAccelerationUnit => flightProfile.FuelPerAccelerationUnit;
        float IdleFuelPerSecond => flightProfile.IdleFuelPerSecond;

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

    }
}
