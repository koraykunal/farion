using Farion.Gameplay.Domain.Systems;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Flight/Spacecraft Flight Profile", fileName = "SO_SpacecraftFlightProfile")]
    public sealed class SpacecraftFlightProfile : ScriptableObject
    {
        [Header("Linear Speed")]
        [Min(0f)]
        [SerializeField] float maxForwardSpeed = 180f;
        [Min(0f)]
        [SerializeField] float maxBoostForwardSpeed = 260f;
        [Min(0f)]
        [SerializeField] float maxReverseSpeed = 70f;
        [Min(0f)]
        [SerializeField] float maxStrafeSpeed = 45f;
        [Min(0f)]
        [SerializeField] float maxVerticalSpeed = 40f;

        [Header("Linear Acceleration")]
        [Min(0f)]
        [SerializeField] float forwardAcceleration = 20f;
        [Min(0f)]
        [SerializeField] float boostForwardAcceleration = 34f;
        [Min(0f)]
        [SerializeField] float reverseAcceleration = 18f;
        [Min(0f)]
        [SerializeField] float strafeAcceleration = 12f;
        [Min(0f)]
        [SerializeField] float verticalAcceleration = 16f;
        [Min(0f)]
        [SerializeField] float brakeGain = 3.2f;

        [Header("Angular Rate")]
        [Min(0f)]
        [SerializeField] float pitchRateDeg = 65f;
        [Min(0f)]
        [SerializeField] float yawRateDeg = 42f;
        [Min(0f)]
        [SerializeField] float rollRateDeg = 95f;

        [Header("Angular Acceleration")]
        [Min(0f)]
        [SerializeField] float pitchAccelerationDeg = 180f;
        [Min(0f)]
        [SerializeField] float yawAccelerationDeg = 140f;
        [Min(0f)]
        [SerializeField] float rollAccelerationDeg = 240f;

        [Header("Assist")]
        [SerializeField] Vector3 velocityGain = new(2.8f, 2.8f, 2.2f);
        [SerializeField] Vector3 angularVelocityGain = new(7f, 7f, 9f);
        [SerializeField] bool compensateGravityInAssistedMode = true;
        [SerializeField] bool limitManualFlightEnvelope = true;
        [Range(0.1f, 0.99f)]
        [SerializeField] float manualEnvelopeStart = 0.85f;

        [Header("Response")]
        [Min(0f)]
        [SerializeField] float translationSpoolRate = 7f;
        [Min(0f)]
        [SerializeField] float rotationSpoolRate = 8f;
        [Min(0f)]
        [SerializeField] float boostSpoolRate = 3.5f;
        [Tooltip("Extra forward acceleration multiplier during the short boost onset surge.")]
        [Min(0f)]
        [SerializeField] float boostSurgeStrength = 0.8f;
        [Range(0f, 0.5f)]
        [SerializeField] float inputDeadZone = 0.04f;

        [Header("Fuel")]
        [Min(0f)]
        [SerializeField] float fuelCapacity = 1000f;
        [Tooltip("Fuel burned per unit of commanded linear acceleration per second. Boost burns faster because it commands more acceleration.")]
        [Min(0f)]
        [SerializeField] float fuelPerAccelerationUnit = 0.05f;
        [Tooltip("Fuel burned per second while the drive is powered but not thrusting.")]
        [Min(0f)]
        [SerializeField] float idleFuelPerSecond;

        [Header("Rigidbody")]
        [Min(1f)]
        [SerializeField] float rigidbodyMass = 12000f;
        [SerializeField] bool overrideCenterOfMass;
        [SerializeField] Vector3 centerOfMass;
        [Min(0f)]
        [SerializeField] float angularDamping;

        public float MaxForwardSpeed => maxForwardSpeed;
        public float MaxBoostForwardSpeed => maxBoostForwardSpeed;
        public float MaxReverseSpeed => maxReverseSpeed;
        public float MaxStrafeSpeed => maxStrafeSpeed;
        public float MaxVerticalSpeed => maxVerticalSpeed;
        public float ForwardAcceleration => forwardAcceleration;
        public float BoostForwardAcceleration => boostForwardAcceleration;
        public float ReverseAcceleration => reverseAcceleration;
        public float StrafeAcceleration => strafeAcceleration;
        public float VerticalAcceleration => verticalAcceleration;
        public float BrakeGain => brakeGain;
        public float PitchRateRad => pitchRateDeg * Mathf.Deg2Rad;
        public float YawRateRad => yawRateDeg * Mathf.Deg2Rad;
        public float RollRateRad => rollRateDeg * Mathf.Deg2Rad;
        public float PitchAccelerationRad => pitchAccelerationDeg * Mathf.Deg2Rad;
        public float YawAccelerationRad => yawAccelerationDeg * Mathf.Deg2Rad;
        public float RollAccelerationRad => rollAccelerationDeg * Mathf.Deg2Rad;
        public Vector3 VelocityGain => velocityGain;
        public Vector3 AngularVelocityGain => angularVelocityGain;
        public float TranslationSpoolRate => translationSpoolRate;
        public float RotationSpoolRate => rotationSpoolRate;
        public float BoostSpoolRate => boostSpoolRate;
        public float BoostSurgeStrength => Mathf.Max(0f, boostSurgeStrength);
        public float InputDeadZone => inputDeadZone;
        public bool CompensateGravityInAssistedMode => compensateGravityInAssistedMode;
        public bool LimitManualFlightEnvelope => limitManualFlightEnvelope;
        public float ManualEnvelopeStart => manualEnvelopeStart;
        public float FuelCapacity => fuelCapacity;
        public float FuelPerAccelerationUnit => fuelPerAccelerationUnit;
        public float IdleFuelPerSecond => idleFuelPerSecond;
        public float RigidbodyMass => rigidbodyMass;
        public bool OverrideCenterOfMass => overrideCenterOfMass;
        public Vector3 CenterOfMass => centerOfMass;
        public float AngularDamping => angularDamping;

        public float EvaluateMaxForwardSpeed(in ShipModuleBonuses bonuses) =>
            maxForwardSpeed * bonuses.MaxSpeedMultiplier;

        public float EvaluateMaxBoostForwardSpeed(in ShipModuleBonuses bonuses) =>
            Mathf.Max(
                EvaluateMaxForwardSpeed(bonuses),
                maxBoostForwardSpeed * bonuses.BoostSpeedMultiplier);

        public float EvaluateFuelCapacity(in ShipModuleBonuses bonuses) =>
            fuelCapacity * bonuses.FuelCapacityMultiplier;

        public Vector3 MaxAngularRate()
        {
            return new Vector3(PitchRateRad, YawRateRad, RollRateRad);
        }

        public Vector3 MaxAngularAcceleration()
        {
            return new Vector3(
                PitchAccelerationRad,
                YawAccelerationRad,
                RollAccelerationRad);
        }

        internal SpacecraftFlightControlSettings BuildControlSettings(
            in ShipModuleBonuses bonuses)
        {
            Vector3 positiveMaxSpeed = new(
                maxStrafeSpeed,
                maxVerticalSpeed,
                EvaluateMaxForwardSpeed(bonuses));
            Vector3 boostedPositiveMaxSpeed = new(
                maxStrafeSpeed,
                maxVerticalSpeed,
                EvaluateMaxBoostForwardSpeed(bonuses));
            Vector3 negativeMaxSpeed = new(maxStrafeSpeed, maxVerticalSpeed, maxReverseSpeed);
            Vector3 positiveAcceleration = new(
                strafeAcceleration,
                verticalAcceleration,
                forwardAcceleration);
            Vector3 boostedPositiveAcceleration = new(
                strafeAcceleration,
                verticalAcceleration,
                boostForwardAcceleration);
            Vector3 negativeAcceleration = new(
                strafeAcceleration,
                verticalAcceleration,
                reverseAcceleration);

            return new SpacecraftFlightControlSettings(
                positiveMaxSpeed,
                boostedPositiveMaxSpeed,
                negativeMaxSpeed,
                positiveAcceleration,
                boostedPositiveAcceleration,
                negativeAcceleration,
                MaxAngularRate(),
                MaxAngularAcceleration(),
                velocityGain,
                angularVelocityGain,
                brakeGain,
                compensateGravityInAssistedMode,
                limitManualFlightEnvelope,
                manualEnvelopeStart,
                boostSurgeStrength);
        }

        void OnValidate()
        {
            maxForwardSpeed = Mathf.Max(0f, maxForwardSpeed);
            maxBoostForwardSpeed = Mathf.Max(maxForwardSpeed, maxBoostForwardSpeed);
            maxReverseSpeed = Mathf.Max(0f, maxReverseSpeed);
            maxStrafeSpeed = Mathf.Max(0f, maxStrafeSpeed);
            maxVerticalSpeed = Mathf.Max(0f, maxVerticalSpeed);
            forwardAcceleration = Mathf.Max(0f, forwardAcceleration);
            boostForwardAcceleration = Mathf.Max(forwardAcceleration, boostForwardAcceleration);
            reverseAcceleration = Mathf.Max(0f, reverseAcceleration);
            strafeAcceleration = Mathf.Max(0f, strafeAcceleration);
            verticalAcceleration = Mathf.Max(0f, verticalAcceleration);
            brakeGain = Mathf.Max(0f, brakeGain);
            pitchRateDeg = Mathf.Max(0f, pitchRateDeg);
            yawRateDeg = Mathf.Max(0f, yawRateDeg);
            rollRateDeg = Mathf.Max(0f, rollRateDeg);
            pitchAccelerationDeg = Mathf.Max(0f, pitchAccelerationDeg);
            yawAccelerationDeg = Mathf.Max(0f, yawAccelerationDeg);
            rollAccelerationDeg = Mathf.Max(0f, rollAccelerationDeg);
            velocityGain = Abs(velocityGain);
            angularVelocityGain = Abs(angularVelocityGain);
            translationSpoolRate = Mathf.Max(0f, translationSpoolRate);
            rotationSpoolRate = Mathf.Max(0f, rotationSpoolRate);
            boostSpoolRate = Mathf.Max(0f, boostSpoolRate);
            boostSurgeStrength = Mathf.Max(0f, boostSurgeStrength);
            inputDeadZone = Mathf.Clamp(inputDeadZone, 0f, 0.5f);
            manualEnvelopeStart = Mathf.Clamp(manualEnvelopeStart, 0.1f, 0.99f);
            fuelCapacity = Mathf.Max(0f, fuelCapacity);
            fuelPerAccelerationUnit = Mathf.Max(0f, fuelPerAccelerationUnit);
            idleFuelPerSecond = Mathf.Max(0f, idleFuelPerSecond);
            rigidbodyMass = Mathf.Max(1f, rigidbodyMass);
            angularDamping = Mathf.Max(0f, angularDamping);
        }

        static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
