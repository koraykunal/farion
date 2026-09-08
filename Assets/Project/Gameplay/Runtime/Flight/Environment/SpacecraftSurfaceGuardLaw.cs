using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftSurfaceGuardFrame
    {
        public SpacecraftSurfaceGuardFrame(
            float currentAltitude,
            float predictedAltitude,
            Vector3 surfaceNormal,
            Vector3 radialUp,
            Vector3 surfaceRelativeVelocity,
            bool engaged)
        {
            CurrentAltitude = currentAltitude;
            PredictedAltitude = predictedAltitude;
            SurfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
            RadialUp = radialUp.sqrMagnitude > 0.0001f ? radialUp.normalized : SurfaceNormal;
            SurfaceRelativeVelocity = surfaceRelativeVelocity;
            Engaged = engaged;
        }

        public float CurrentAltitude { get; }
        public float PredictedAltitude { get; }
        public Vector3 SurfaceNormal { get; }
        public Vector3 RadialUp { get; }
        public Vector3 SurfaceRelativeVelocity { get; }
        public bool Engaged { get; }
    }

    public readonly struct SpacecraftSurfaceGuardSettings
    {
        public SpacecraftSurfaceGuardSettings(float clearanceMeters, float toleranceMeters)
        {
            ClearanceMeters = Mathf.Max(0f, clearanceMeters);
            ToleranceMeters = Mathf.Max(0.01f, toleranceMeters);
        }

        public float ClearanceMeters { get; }
        public float ToleranceMeters { get; }
        public float EngageAltitude => ClearanceMeters - ToleranceMeters;
        public float ReleaseAltitude => ClearanceMeters + ToleranceMeters;
    }

    public readonly struct SpacecraftSurfaceGuardOutput
    {
        public SpacecraftSurfaceGuardOutput(
            bool engaged,
            Vector3 surfaceRelativeVelocity,
            float removedClosingSpeed,
            Vector3 positionCorrection,
            float impactSpeed)
        {
            Engaged = engaged;
            SurfaceRelativeVelocity = surfaceRelativeVelocity;
            RemovedClosingSpeed = removedClosingSpeed;
            PositionCorrection = positionCorrection;
            ImpactSpeed = impactSpeed;
        }

        public bool Engaged { get; }
        public Vector3 SurfaceRelativeVelocity { get; }
        public float RemovedClosingSpeed { get; }
        public Vector3 PositionCorrection { get; }
        public float ImpactSpeed { get; }
        public bool CorrectsVelocity => RemovedClosingSpeed > 0f;
        public bool CorrectsPosition => PositionCorrection.sqrMagnitude > 0f;

        public static SpacecraftSurfaceGuardOutput Passthrough(in SpacecraftSurfaceGuardFrame frame) =>
            new(false, frame.SurfaceRelativeVelocity, 0f, Vector3.zero, 0f);
    }

    public static class SpacecraftSurfaceGuardLaw
    {
        public static SpacecraftSurfaceGuardOutput Evaluate(
            in SpacecraftSurfaceGuardFrame frame,
            in SpacecraftSurfaceGuardSettings settings)
        {
            bool engaged = frame.Engaged
                ? frame.CurrentAltitude <= settings.ReleaseAltitude
                : frame.CurrentAltitude < settings.EngageAltitude ||
                    frame.PredictedAltitude < settings.EngageAltitude;
            if (!engaged)
            {
                return SpacecraftSurfaceGuardOutput.Passthrough(frame);
            }

            float closingSpeed = Mathf.Max(0f, -Vector3.Dot(frame.SurfaceRelativeVelocity, frame.SurfaceNormal));
            Vector3 velocity = frame.SurfaceRelativeVelocity + frame.SurfaceNormal * closingSpeed;
            Vector3 correction = frame.CurrentAltitude < settings.ClearanceMeters
                ? frame.RadialUp * (settings.ClearanceMeters - frame.CurrentAltitude)
                : Vector3.zero;
            float impactSpeed = frame.Engaged ? 0f : closingSpeed;
            return new SpacecraftSurfaceGuardOutput(true, velocity, closingSpeed, correction, impactSpeed);
        }
    }
}
