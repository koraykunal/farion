using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    public readonly struct CelestialFrameSample
    {
        public CelestialFrameSample(
            CelestialBody body,
            Vector3 position,
            Vector3 velocity,
            Vector3 bodyPosition,
            Vector3 bodyVelocity,
            Vector3 bodyAngularVelocity,
            Vector3 surfacePoint,
            Vector3 surfaceNormal,
            Vector3 gravityAcceleration,
            float centerDistance,
            float surfaceAltitude,
            float surfaceSlopeAngleDegrees,
            CelestialEnvironmentSample environment)
        {
            Body = body;
            Position = position;
            Velocity = velocity;
            BodyPosition = bodyPosition;
            BodyVelocity = bodyVelocity;
            BodyAngularVelocity = bodyAngularVelocity;
            SurfacePoint = surfacePoint;
            SurfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
            GravityAcceleration = gravityAcceleration;
            CenterDistance = centerDistance;
            SurfaceAltitude = surfaceAltitude;
            SurfaceSlopeAngleDegrees = Mathf.Max(0f, surfaceSlopeAngleDegrees);
            Environment = environment;

            RelativePosition = position - bodyPosition;
            RelativeVelocity = velocity - bodyVelocity;
            BodyPointVelocity = bodyVelocity + Vector3.Cross(bodyAngularVelocity, position - bodyPosition);
            SurfaceRelativeVelocity = velocity - BodyPointVelocity;
            RadialUp = RelativePosition.sqrMagnitude > 0.0001f
                ? RelativePosition.normalized
                : SurfaceNormal;
            GravityDirection = gravityAcceleration.sqrMagnitude > 0.0001f
                ? gravityAcceleration.normalized
                : -RadialUp;
            GravityUp = -GravityDirection;
            LocalUp = GravityUp.sqrMagnitude > 0.0001f ? GravityUp.normalized : RadialUp;
            SpeedRelativeToBody = RelativeVelocity.magnitude;
            RadialVelocity = Vector3.Dot(RelativeVelocity, RadialUp);
            TangentialSpeed = Vector3.ProjectOnPlane(RelativeVelocity, RadialUp).magnitude;
            SurfaceNormalVelocity = Vector3.Dot(SurfaceRelativeVelocity, SurfaceNormal);
            SurfaceTangentialSpeed = Vector3.ProjectOnPlane(SurfaceRelativeVelocity, SurfaceNormal).magnitude;

            HasOcean = environment.HasOcean;
            OceanAltitude = HasOcean
                ? CenterDistance - environment.GetOceanRadiusAt(RelativePosition)
                : float.PositiveInfinity;
            IsBelowOceanLevel = HasOcean && OceanAltitude < 0f;
            WaterDepth = IsBelowOceanLevel ? -OceanAltitude : 0f;

            HasAtmosphere = environment.HasAtmosphere;
            AtmosphereAltitude = HasAtmosphere ? environment.AtmosphereRadius - CenterDistance : float.NegativeInfinity;
            IsInsideAtmosphere = HasAtmosphere && AtmosphereAltitude > 0f;
            float bodyRadius = body != null ? body.Radius : 0f;
            AtmosphereNormalizedDepth = HasAtmosphere && environment.AtmosphereRadius > bodyRadius
                ? Mathf.Clamp01(AtmosphereAltitude / (environment.AtmosphereRadius - bodyRadius))
                : 0f;
        }

        public CelestialBody Body { get; }
        public bool HasBody => Body != null;
        public float BodyRadius => HasBody ? Body.Radius : 0f;
        public Vector3 WaterPointVelocity => HasBody
            ? BodyPointVelocity + Environment.GetOceanSurfaceVelocityAt(RelativePosition)
            : Vector3.zero;
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public Vector3 BodyPosition { get; }
        public Vector3 BodyVelocity { get; }
        public Vector3 BodyAngularVelocity { get; }
        public Vector3 BodyPointVelocity { get; }
        public Vector3 RelativePosition { get; }
        public Vector3 RelativeVelocity { get; }
        public Vector3 SurfaceRelativeVelocity { get; }
        public Vector3 SurfacePoint { get; }
        public Vector3 SurfaceNormal { get; }
        public Vector3 GravityAcceleration { get; }
        public Vector3 GravityDirection { get; }
        public Vector3 GravityUp { get; }
        public Vector3 LocalUp { get; }
        public Vector3 RadialUp { get; }
        public float CenterDistance { get; }
        public float SurfaceAltitude { get; }
        public float SurfaceSlopeAngleDegrees { get; }
        public float RadialVelocity { get; }
        public float TangentialSpeed { get; }
        public float SurfaceNormalVelocity { get; }
        public float SurfaceTangentialSpeed { get; }
        public float SpeedRelativeToBody { get; }
        public CelestialEnvironmentSample Environment { get; }
        public bool HasOcean { get; }
        public float OceanAltitude { get; }
        public bool IsBelowOceanLevel { get; }
        public float WaterDepth { get; }
        public bool HasAtmosphere { get; }
        public float AtmosphereAltitude { get; }
        public bool IsInsideAtmosphere { get; }
        public float AtmosphereNormalizedDepth { get; }
        public bool IsApproachingSurface => SurfaceNormalVelocity < 0f;

        public static CelestialFrameSample Empty(Vector3 position, Vector3 velocity)
        {
            return new CelestialFrameSample(
                null,
                position,
                velocity,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.up,
                Vector3.zero,
                0f,
                0f,
                0f,
                CelestialEnvironmentSample.Empty(null));
        }

    }
}
