using Farion.Core.Physics;
using Farion.Gameplay.Actors;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SpacecraftMotor : MonoBehaviour
    {
        const float DefaultMaxForwardSpeed = 180f;
        const float DefaultMaxBoostForwardSpeed = 260f;
        const float DefaultMaxReverseSpeed = 70f;
        const float DefaultMaxStrafeSpeed = 45f;
        const float DefaultMaxVerticalSpeed = 40f;
        const float DefaultForwardAcceleration = 20f;
        const float DefaultBoostForwardAcceleration = 34f;
        const float DefaultReverseAcceleration = 14f;
        const float DefaultStrafeAcceleration = 8f;
        const float DefaultVerticalAcceleration = 7f;
        const float DefaultBrakeGain = 3.2f;
        const float DefaultPitchRateRad = 65f * Mathf.Deg2Rad;
        const float DefaultYawRateRad = 42f * Mathf.Deg2Rad;
        const float DefaultRollRateRad = 95f * Mathf.Deg2Rad;
        const float DefaultPitchAccelerationRad = 180f * Mathf.Deg2Rad;
        const float DefaultYawAccelerationRad = 140f * Mathf.Deg2Rad;
        const float DefaultRollAccelerationRad = 240f * Mathf.Deg2Rad;
        const float DefaultTranslationSpoolRate = 7f;
        const float DefaultRotationSpoolRate = 8f;
        const float DefaultBoostSpoolRate = 3.5f;
        const float DefaultInputDeadZone = 0.04f;

        static readonly Vector3 DefaultVelocityGain = new(2.8f, 2.8f, 2.2f);
        static readonly Vector3 DefaultAngularVelocityGain = new(7f, 7f, 9f);

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
        SpacecraftPilotCommand currentCommand = SpacecraftPilotCommand.None;
        SpacecraftThrusterCommand currentThrusterCommand = SpacecraftThrusterCommand.None;
        SpacecraftMovementTelemetry telemetry = SpacecraftMovementTelemetry.Empty;
        Vector3 smoothedTranslation;
        Vector3 smoothedRotationInput;
        float smoothedBoostAuthority;
        bool boostActive;
        Vector3 lastGravityAcceleration;
        Vector3 lastThrustAcceleration;
        Vector3 lastFlightAssistAcceleration;
        Vector3 lastLocalLinearAcceleration;
        Vector3 lastLocalAngularAcceleration;

        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();
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
        public Vector3 LastLocalTranslationInput => smoothedTranslation;
        public Vector3 LastLocalRotationInput => smoothedRotationInput;
        public Vector3 CurrentLocalTranslationInput => currentInput.Translation;
        public bool FlightAssistEnabled => flightAssistEnabled;
        public bool BoostActive => boostActive;
        public float CurrentBoostMultiplier => 1f + smoothedBoostAuthority;
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
            ResolveInputSource();
            ResolveContactProbe();
            ResolveCelestialProbe();
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
            ResolveInputSource();
            ResolveContactProbe();
            ResolveCelestialProbe();

            currentInput = resolvedInput?.CurrentInput ?? SpacecraftInputState.None;
            SpacecraftPilotCommand nextCommand = BuildPilotCommand(currentInput);
            if (nextCommand.ToggleFlightAssist)
            {
                flightAssistEnabled = !flightAssistEnabled;
            }

            currentCommand = new SpacecraftPilotCommand(
                nextCommand.Translation,
                nextCommand.Rotation,
                nextCommand.Boost,
                nextCommand.Brake,
                toggleFlightAssist: false);
        }

        void FixedUpdate()
        {
            float deltaTime = UnityEngine.Time.fixedDeltaTime;
            ApplyGravity();
            ApplyTranslation(deltaTime);
            ApplyRotation(deltaTime);
            RefreshTelemetry();
        }

        public void SetInputSource(ISpacecraftInputSource source)
        {
            resolvedInput = source;
            inputSource = source as MonoBehaviour;
        }

        public void SetFlightAssistEnabled(bool enabled)
        {
            flightAssistEnabled = enabled;
        }

        void ConfigureRigidbody()
        {
            Rigidbody.useGravity = false;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Rigidbody.centerOfMass = Vector3.zero;
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
            if (surfaceContactProbe == null)
            {
                surfaceContactProbe = GetComponent<SpacecraftSurfaceContactProbe>();
            }
        }

        void ResolveCelestialProbe()
        {
            if (celestialProbe == null)
            {
                celestialProbe = GetComponent<CelestialActorProbe>();
            }
        }

        void ApplyGravity()
        {
            lastGravityAcceleration = Vector3.zero;
            if (!applyGravity)
            {
                return;
            }

            GravitySimulation source = simulation != null ? simulation : GravitySimulation.Active;
            if (source == null)
            {
                return;
            }

            lastGravityAcceleration = source.CalculateAcceleration(Rigidbody.position);
            Rigidbody.AddForce(lastGravityAcceleration, ForceMode.Acceleration);
        }

        void ApplyTranslation(float deltaTime)
        {
            Vector3 targetTranslation = DeadZone(currentCommand.Translation, InputDeadZone);
            smoothedTranslation = Vector3.Lerp(
                smoothedTranslation,
                targetTranslation,
                ResponsivenessToLerp(TranslationSpoolRate, deltaTime));
            smoothedTranslation = DeadZone(smoothedTranslation, 0.001f);

            boostActive = currentCommand.Boost && smoothedTranslation.z > InputDeadZone;
            smoothedBoostAuthority = Mathf.Lerp(
                smoothedBoostAuthority,
                boostActive ? 1f : 0f,
                ResponsivenessToLerp(BoostSpoolRate, deltaTime));

            Vector3 localRelativeVelocity = transform.InverseTransformDirection(RelativeVelocity);
            Vector3 localAcceleration = flightAssistEnabled
                ? CalculateAssistedLinearAcceleration(localRelativeVelocity)
                : CalculateManualLinearAcceleration(localRelativeVelocity);

            lastLocalLinearAcceleration = localAcceleration;
            lastThrustAcceleration = transform.TransformDirection(localAcceleration);
            lastFlightAssistAcceleration = flightAssistEnabled ? lastThrustAcceleration : Vector3.zero;

            if (lastThrustAcceleration.sqrMagnitude > 0.000001f)
            {
                Rigidbody.AddForce(lastThrustAcceleration, ForceMode.Acceleration);
            }
        }

        Vector3 CalculateAssistedLinearAcceleration(Vector3 localRelativeVelocity)
        {
            Vector3 speed = AssistedMaxSpeed(boostActive);
            Vector3 desiredLocalVelocity = new(
                smoothedTranslation.x * speed.x,
                smoothedTranslation.y * speed.y,
                smoothedTranslation.z >= 0f
                    ? smoothedTranslation.z * speed.z
                    : smoothedTranslation.z * MaxReverseSpeed);

            if (currentCommand.Brake)
            {
                desiredLocalVelocity = Vector3.zero;
            }

            Vector3 acceleration = Vector3.Scale(
                desiredLocalVelocity - localRelativeVelocity,
                VelocityGain);

            return ClampLinearAcceleration(acceleration, boostActive);
        }

        Vector3 CalculateManualLinearAcceleration(Vector3 localRelativeVelocity)
        {
            if (currentCommand.Brake)
            {
                return ClampLinearAcceleration(-localRelativeVelocity * BrakeGain, boostActive: false);
            }

            Vector3 acceleration = new(
                smoothedTranslation.x * StrafeAcceleration,
                smoothedTranslation.y * VerticalAcceleration,
                smoothedTranslation.z >= 0f
                    ? smoothedTranslation.z * ForwardAcceleration(boostActive)
                    : smoothedTranslation.z * ReverseAcceleration);

            acceleration.z *= ForwardSpeedAuthority(localRelativeVelocity.z, boostActive);
            return ClampLinearAcceleration(acceleration, boostActive);
        }

        void ApplyRotation(float deltaTime)
        {
            Vector3 targetRotation = currentCommand.Rotation;
            smoothedRotationInput = Vector3.Lerp(
                smoothedRotationInput,
                targetRotation,
                ResponsivenessToLerp(RotationSpoolRate, deltaTime));
            smoothedRotationInput = DeadZone(smoothedRotationInput, 0.001f);

            if (RotationSuspendedByContact)
            {
                smoothedRotationInput = Vector3.zero;
                lastLocalAngularAcceleration = Vector3.zero;
                Rigidbody.angularVelocity = Vector3.zero;
                return;
            }

            Vector3 localAngularVelocity = transform.InverseTransformDirection(Rigidbody.angularVelocity);
            Vector3 localAngularAcceleration = flightAssistEnabled
                ? CalculateAssistedAngularAcceleration(localAngularVelocity)
                : CalculateManualAngularAcceleration();

            lastLocalAngularAcceleration = localAngularAcceleration;
            if (localAngularAcceleration.sqrMagnitude > 0.000001f)
            {
                Rigidbody.AddRelativeTorque(localAngularAcceleration, ForceMode.Acceleration);
            }
        }

        Vector3 CalculateAssistedAngularAcceleration(Vector3 localAngularVelocity)
        {
            Vector3 targetAngularVelocity = Vector3.Scale(smoothedRotationInput, MaxAngularRate);
            Vector3 acceleration = Vector3.Scale(
                targetAngularVelocity - localAngularVelocity,
                AngularVelocityGain);
            return ClampAngularAcceleration(acceleration);
        }

        Vector3 CalculateManualAngularAcceleration()
        {
            return Vector3.Scale(smoothedRotationInput, MaxAngularAcceleration);
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

        Vector3 ClampLinearAcceleration(Vector3 acceleration, bool boostActive)
        {
            Vector3 positive = MaxPositiveAcceleration(boostActive);
            Vector3 negative = MaxNegativeAcceleration;

            return new Vector3(
                ClampAsymmetric(acceleration.x, negative.x, positive.x),
                ClampAsymmetric(acceleration.y, negative.y, positive.y),
                ClampAsymmetric(acceleration.z, negative.z, positive.z));
        }

        Vector3 ClampAngularAcceleration(Vector3 acceleration)
        {
            Vector3 limit = MaxAngularAcceleration;
            return new Vector3(
                Mathf.Clamp(acceleration.x, -limit.x, limit.x),
                Mathf.Clamp(acceleration.y, -limit.y, limit.y),
                Mathf.Clamp(acceleration.z, -limit.z, limit.z));
        }

        SpacecraftThrusterCommand BuildThrusterCommand(Vector3 localAcceleration, Vector3 localAngularAcceleration)
        {
            Vector3 positive = MaxPositiveAcceleration(boostActive);
            Vector3 negative = MaxNegativeAcceleration;
            Vector3 angular = MaxAngularAcceleration;

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
            currentThrusterCommand = BuildThrusterCommand(lastLocalLinearAcceleration, lastLocalAngularAcceleration);
            telemetry = new SpacecraftMovementTelemetry(
                AssistMode,
                currentCommand,
                currentThrusterCommand,
                localRelativeVelocity,
                localAngularVelocity,
                lastLocalLinearAcceleration,
                lastLocalAngularAcceleration,
                relativeVelocity,
                boostActive,
                smoothedBoostAuthority);
        }

        Vector3 ResolveFlightReferenceVelocity()
        {
            if (!useBodyRelativeFlightAssist || celestialProbe == null || !celestialProbe.HasSample)
            {
                return Vector3.zero;
            }

            return celestialProbe.CurrentSample.BodyPointVelocity;
        }

        float ForwardSpeedAuthority(float localForwardVelocity, bool boostActive)
        {
            float speedLimit = boostActive ? MaxBoostForwardSpeed : MaxForwardSpeed;
            if (speedLimit <= 0f || localForwardVelocity <= 0f)
            {
                return 1f;
            }

            float ratio = Mathf.Clamp01(localForwardVelocity / speedLimit);
            if (ratio < 0.7f)
            {
                return 1f;
            }

            return Mathf.Clamp01(1f - Mathf.InverseLerp(0.7f, 1f, ratio));
        }

        float ForwardAcceleration(bool boostActive)
        {
            return boostActive
                ? Mathf.Lerp(MaxForwardAcceleration, MaxBoostForwardAcceleration, smoothedBoostAuthority)
                : MaxForwardAcceleration;
        }

        Vector3 AssistedMaxSpeed(bool boostActive)
        {
            if (flightProfile != null)
            {
                return flightProfile.AssistedMaxSpeed(boostActive);
            }

            return new Vector3(
                DefaultMaxStrafeSpeed,
                DefaultMaxVerticalSpeed,
                boostActive ? DefaultMaxBoostForwardSpeed : DefaultMaxForwardSpeed);
        }

        Vector3 MaxPositiveAcceleration(bool boostActive)
        {
            if (flightProfile != null)
            {
                return flightProfile.MaxPositiveAcceleration(boostActive);
            }

            return new Vector3(
                DefaultStrafeAcceleration,
                DefaultVerticalAcceleration,
                boostActive ? DefaultBoostForwardAcceleration : DefaultForwardAcceleration);
        }

        Vector3 MaxNegativeAcceleration => flightProfile != null
            ? flightProfile.MaxNegativeAcceleration()
            : new Vector3(DefaultStrafeAcceleration, DefaultVerticalAcceleration, DefaultReverseAcceleration);

        Vector3 MaxAngularRate => flightProfile != null
            ? flightProfile.MaxAngularRate()
            : new Vector3(DefaultPitchRateRad, DefaultYawRateRad, DefaultRollRateRad);

        Vector3 MaxAngularAcceleration => flightProfile != null
            ? flightProfile.MaxAngularAcceleration()
            : new Vector3(DefaultPitchAccelerationRad, DefaultYawAccelerationRad, DefaultRollAccelerationRad);

        Vector3 VelocityGain => flightProfile != null ? flightProfile.VelocityGain : DefaultVelocityGain;
        Vector3 AngularVelocityGain => flightProfile != null ? flightProfile.AngularVelocityGain : DefaultAngularVelocityGain;
        float MaxForwardSpeed => flightProfile != null ? flightProfile.MaxForwardSpeed : DefaultMaxForwardSpeed;
        float MaxBoostForwardSpeed => flightProfile != null ? flightProfile.MaxBoostForwardSpeed : DefaultMaxBoostForwardSpeed;
        float MaxReverseSpeed => flightProfile != null ? flightProfile.MaxReverseSpeed : DefaultMaxReverseSpeed;
        float MaxForwardAcceleration => flightProfile != null ? flightProfile.ForwardAcceleration : DefaultForwardAcceleration;
        float MaxBoostForwardAcceleration => flightProfile != null ? flightProfile.BoostForwardAcceleration : DefaultBoostForwardAcceleration;
        float ReverseAcceleration => flightProfile != null ? flightProfile.ReverseAcceleration : DefaultReverseAcceleration;
        float StrafeAcceleration => flightProfile != null ? flightProfile.StrafeAcceleration : DefaultStrafeAcceleration;
        float VerticalAcceleration => flightProfile != null ? flightProfile.VerticalAcceleration : DefaultVerticalAcceleration;
        float BrakeGain => flightProfile != null ? flightProfile.BrakeGain : DefaultBrakeGain;
        float TranslationSpoolRate => flightProfile != null ? flightProfile.TranslationSpoolRate : DefaultTranslationSpoolRate;
        float RotationSpoolRate => flightProfile != null ? flightProfile.RotationSpoolRate : DefaultRotationSpoolRate;
        float BoostSpoolRate => flightProfile != null ? flightProfile.BoostSpoolRate : DefaultBoostSpoolRate;
        float InputDeadZone => flightProfile != null ? flightProfile.InputDeadZone : DefaultInputDeadZone;

        static Vector3 DeadZone(Vector3 value, float deadZone)
        {
            return value.sqrMagnitude <= deadZone * deadZone ? Vector3.zero : value;
        }

        static float ClampAsymmetric(float value, float negativeLimit, float positiveLimit)
        {
            return Mathf.Clamp(value, -Mathf.Abs(negativeLimit), Mathf.Abs(positiveLimit));
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
