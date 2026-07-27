using UnityEngine;

namespace Farion.Gameplay.Flight
{
    static class SpacecraftFlightControlLaw
    {
        public static SpacecraftFlightControlOutput Evaluate(
            SpacecraftFlightControlFrame frame,
            SpacecraftFlightControlSettings settings)
        {
            Vector3 positiveAcceleration = settings.MaxPositiveAcceleration(frame.BoostAuthority);
            Vector3 localGravityCompensation =
                frame.FlightAssistEnabled && settings.CompensateGravity
                    ? -frame.LocalGravityAcceleration
                    : Vector3.zero;

            Vector3 localAssistAcceleration = Vector3.zero;
            Vector3 localLinearAcceleration;
            if (frame.FlightAssistEnabled || frame.Command.Brake)
            {
                Vector3 desiredVelocity = frame.Command.Brake
                    ? Vector3.zero
                    : BuildDesiredVelocity(
                        frame.Command.Translation,
                        settings.PositiveSpeed(frame.BoostAuthority),
                        settings.NegativeMaxSpeed);
                Vector3 velocityGain = frame.Command.Brake
                    ? Vector3.one * settings.BrakeGain
                    : settings.VelocityGain;
                localAssistAcceleration = Vector3.Scale(
                    desiredVelocity - frame.LocalRelativeVelocity,
                    velocityGain);
                localLinearAcceleration = ClampLinearAcceleration(
                    localAssistAcceleration + localGravityCompensation,
                    positiveAcceleration,
                    settings.NegativeAcceleration);
            }
            else
            {
                localLinearAcceleration = CalculateManualLinearAcceleration(
                    frame.Command.Translation,
                    frame.LocalRelativeVelocity,
                    positiveAcceleration,
                    settings.NegativeAcceleration,
                    settings.PositiveSpeed(frame.BoostAuthority),
                    settings.NegativeMaxSpeed,
                    settings);
            }

            Vector3 localAngularAcceleration = frame.FlightAssistEnabled
                ? CalculateAssistedAngularAcceleration(frame, settings)
                : CalculateManualAngularAcceleration(frame, settings);

            return new SpacecraftFlightControlOutput(
                localLinearAcceleration,
                localAngularAcceleration,
                frame.FlightAssistEnabled || frame.Command.Brake
                    ? localAssistAcceleration
                    : Vector3.zero,
                localGravityCompensation);
        }

        static Vector3 BuildDesiredVelocity(
            Vector3 command,
            Vector3 positiveMaxSpeed,
            Vector3 negativeMaxSpeed)
        {
            return new Vector3(
                command.x >= 0f
                    ? command.x * positiveMaxSpeed.x
                    : command.x * negativeMaxSpeed.x,
                command.y >= 0f
                    ? command.y * positiveMaxSpeed.y
                    : command.y * negativeMaxSpeed.y,
                command.z >= 0f
                    ? command.z * positiveMaxSpeed.z
                    : command.z * negativeMaxSpeed.z);
        }

        static Vector3 CalculateManualLinearAcceleration(
            Vector3 command,
            Vector3 localVelocity,
            Vector3 positiveAcceleration,
            Vector3 negativeAcceleration,
            Vector3 positiveSpeedLimit,
            Vector3 negativeSpeedLimit,
            SpacecraftFlightControlSettings settings)
        {
            Vector3 acceleration = new(
                command.x >= 0f
                    ? command.x * positiveAcceleration.x
                    : command.x * negativeAcceleration.x,
                command.y >= 0f
                    ? command.y * positiveAcceleration.y
                    : command.y * negativeAcceleration.y,
                command.z >= 0f
                    ? command.z * positiveAcceleration.z
                    : command.z * negativeAcceleration.z);

            if (!settings.LimitManualFlightEnvelope)
            {
                return acceleration;
            }

            return new Vector3(
                ApplyDirectionalEnvelope(
                    acceleration.x,
                    command.x,
                    localVelocity.x,
                    command.x >= 0f ? positiveSpeedLimit.x : negativeSpeedLimit.x,
                    settings.ManualEnvelopeStart),
                ApplyDirectionalEnvelope(
                    acceleration.y,
                    command.y,
                    localVelocity.y,
                    command.y >= 0f ? positiveSpeedLimit.y : negativeSpeedLimit.y,
                    settings.ManualEnvelopeStart),
                ApplyDirectionalEnvelope(
                    acceleration.z,
                    command.z,
                    localVelocity.z,
                    command.z >= 0f ? positiveSpeedLimit.z : negativeSpeedLimit.z,
                    settings.ManualEnvelopeStart));
        }

        static Vector3 CalculateAssistedAngularAcceleration(
            SpacecraftFlightControlFrame frame,
            SpacecraftFlightControlSettings settings)
        {
            Vector3 targetAngularVelocity = Vector3.Scale(
                frame.Command.Rotation,
                settings.MaxAngularRate);
            Vector3 acceleration = Vector3.Scale(
                targetAngularVelocity - frame.LocalAngularVelocity,
                settings.AngularVelocityGain);
            return ClampSymmetric(acceleration, settings.MaxAngularAcceleration);
        }

        static Vector3 CalculateManualAngularAcceleration(
            SpacecraftFlightControlFrame frame,
            SpacecraftFlightControlSettings settings)
        {
            Vector3 acceleration = Vector3.Scale(
                frame.Command.Rotation,
                settings.MaxAngularAcceleration);
            if (!settings.LimitManualFlightEnvelope)
            {
                return acceleration;
            }

            return new Vector3(
                ApplyDirectionalEnvelope(
                    acceleration.x,
                    frame.Command.Rotation.x,
                    frame.LocalAngularVelocity.x,
                    settings.MaxAngularRate.x,
                    settings.ManualEnvelopeStart),
                ApplyDirectionalEnvelope(
                    acceleration.y,
                    frame.Command.Rotation.y,
                    frame.LocalAngularVelocity.y,
                    settings.MaxAngularRate.y,
                    settings.ManualEnvelopeStart),
                ApplyDirectionalEnvelope(
                    acceleration.z,
                    frame.Command.Rotation.z,
                    frame.LocalAngularVelocity.z,
                    settings.MaxAngularRate.z,
                    settings.ManualEnvelopeStart));
        }

        static Vector3 ClampLinearAcceleration(
            Vector3 acceleration,
            Vector3 positiveLimit,
            Vector3 negativeLimit)
        {
            return new Vector3(
                ClampAsymmetric(acceleration.x, negativeLimit.x, positiveLimit.x),
                ClampAsymmetric(acceleration.y, negativeLimit.y, positiveLimit.y),
                ClampAsymmetric(acceleration.z, negativeLimit.z, positiveLimit.z));
        }

        static Vector3 ClampSymmetric(Vector3 value, Vector3 limit)
        {
            return new Vector3(
                Mathf.Clamp(value.x, -limit.x, limit.x),
                Mathf.Clamp(value.y, -limit.y, limit.y),
                Mathf.Clamp(value.z, -limit.z, limit.z));
        }

        static float ApplyDirectionalEnvelope(
            float acceleration,
            float command,
            float velocity,
            float speedLimit,
            float envelopeStart)
        {
            if (Mathf.Abs(command) <= 0.0001f ||
                Mathf.Sign(command) != Mathf.Sign(velocity) ||
                speedLimit <= 0f)
            {
                return acceleration;
            }

            float speedRatio = Mathf.Abs(velocity) / speedLimit;
            if (speedRatio <= envelopeStart)
            {
                return acceleration;
            }

            float authority = 1f - Mathf.InverseLerp(envelopeStart, 1f, speedRatio);
            return acceleration * Mathf.Clamp01(authority);
        }

        static float ClampAsymmetric(float value, float negativeLimit, float positiveLimit)
        {
            return Mathf.Clamp(value, -Mathf.Abs(negativeLimit), Mathf.Abs(positiveLimit));
        }
    }
}
