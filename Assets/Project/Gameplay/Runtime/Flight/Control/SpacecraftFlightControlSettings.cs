using UnityEngine;

namespace Farion.Gameplay.Flight
{
    readonly struct SpacecraftFlightControlSettings
    {
        public SpacecraftFlightControlSettings(
            Vector3 positiveMaxSpeed,
            Vector3 boostedPositiveMaxSpeed,
            Vector3 negativeMaxSpeed,
            Vector3 positiveAcceleration,
            Vector3 boostedPositiveAcceleration,
            Vector3 negativeAcceleration,
            Vector3 maxAngularRate,
            Vector3 maxAngularAcceleration,
            Vector3 velocityGain,
            Vector3 angularVelocityGain,
            float brakeGain,
            bool compensateGravity,
            bool limitManualFlightEnvelope,
            float manualEnvelopeStart,
            float boostSurgeStrength,
            float optimalSpeedBandEnd,
            float offBandAngularScale)
        {
            PositiveMaxSpeed = Abs(positiveMaxSpeed);
            BoostedPositiveMaxSpeed = Vector3.Max(
                PositiveMaxSpeed,
                Abs(boostedPositiveMaxSpeed));
            NegativeMaxSpeed = Abs(negativeMaxSpeed);
            PositiveAcceleration = Abs(positiveAcceleration);
            BoostedPositiveAcceleration = Vector3.Max(
                PositiveAcceleration,
                Abs(boostedPositiveAcceleration));
            NegativeAcceleration = Abs(negativeAcceleration);
            MaxAngularRate = Abs(maxAngularRate);
            MaxAngularAcceleration = Abs(maxAngularAcceleration);
            VelocityGain = Abs(velocityGain);
            AngularVelocityGain = Abs(angularVelocityGain);
            BrakeGain = Mathf.Max(0f, brakeGain);
            CompensateGravity = compensateGravity;
            LimitManualFlightEnvelope = limitManualFlightEnvelope;
            ManualEnvelopeStart = Mathf.Clamp(manualEnvelopeStart, 0.1f, 0.99f);
            BoostSurgeStrength = Mathf.Max(0f, boostSurgeStrength);
            OptimalSpeedBandEnd = Mathf.Clamp01(optimalSpeedBandEnd);
            OffBandAngularScale = Mathf.Clamp(offBandAngularScale, 0.1f, 1f);
        }

        public float BoostSurgeStrength { get; }
        public Vector3 PositiveMaxSpeed { get; }
        public Vector3 BoostedPositiveMaxSpeed { get; }
        public Vector3 NegativeMaxSpeed { get; }
        public Vector3 PositiveAcceleration { get; }
        public Vector3 BoostedPositiveAcceleration { get; }
        public Vector3 NegativeAcceleration { get; }
        public Vector3 MaxAngularRate { get; }
        public Vector3 MaxAngularAcceleration { get; }
        public Vector3 VelocityGain { get; }
        public Vector3 AngularVelocityGain { get; }
        public float BrakeGain { get; }
        public bool CompensateGravity { get; }
        public bool LimitManualFlightEnvelope { get; }
        public float ManualEnvelopeStart { get; }
        public float OptimalSpeedBandEnd { get; }
        public float OffBandAngularScale { get; }

        public float ManoeuvreAuthority(float forwardSpeed, float maxForwardSpeed)
        {
            if (maxForwardSpeed <= 0.0001f || OptimalSpeedBandEnd >= 0.9999f)
            {
                return 1f;
            }

            float ratio = Mathf.Abs(forwardSpeed) / maxForwardSpeed;
            if (ratio <= OptimalSpeedBandEnd)
            {
                return 1f;
            }

            return Mathf.Lerp(
                1f,
                OffBandAngularScale,
                Mathf.InverseLerp(OptimalSpeedBandEnd, 1f, ratio));
        }

        public Vector3 PositiveSpeed(float boostAuthority)
        {
            return Vector3.Lerp(
                PositiveMaxSpeed,
                BoostedPositiveMaxSpeed,
                Mathf.Clamp01(boostAuthority));
        }

        public Vector3 MaxPositiveAcceleration(float boostAuthority)
        {
            return Vector3.Lerp(
                PositiveAcceleration,
                BoostedPositiveAcceleration,
                Mathf.Clamp01(boostAuthority));
        }

        static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
