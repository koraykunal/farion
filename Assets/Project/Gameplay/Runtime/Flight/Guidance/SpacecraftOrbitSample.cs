namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftOrbitSample
    {
        public SpacecraftOrbitSample(
            SpacecraftOrbitRegime regime,
            float gravitationalParameter,
            float radius,
            float altitude,
            float speed,
            float radialVelocity,
            float tangentialSpeed,
            float circularVelocity,
            float escapeVelocity,
            float specificOrbitalEnergy,
            float eccentricity,
            float semiMajorAxis,
            float periapsisAltitude,
            float apoapsisAltitude,
            float orbitalPeriod,
            float flightPathAngleDegrees)
        {
            Regime = regime;
            GravitationalParameter = gravitationalParameter;
            Radius = radius;
            Altitude = altitude;
            Speed = speed;
            RadialVelocity = radialVelocity;
            TangentialSpeed = tangentialSpeed;
            CircularVelocity = circularVelocity;
            EscapeVelocity = escapeVelocity;
            SpecificOrbitalEnergy = specificOrbitalEnergy;
            Eccentricity = eccentricity;
            SemiMajorAxis = semiMajorAxis;
            PeriapsisAltitude = periapsisAltitude;
            ApoapsisAltitude = apoapsisAltitude;
            OrbitalPeriod = orbitalPeriod;
            FlightPathAngleDegrees = flightPathAngleDegrees;
        }

        public SpacecraftOrbitRegime Regime { get; }
        public bool HasFrame => Regime != SpacecraftOrbitRegime.NoFrame;
        public bool IsBound => Regime == SpacecraftOrbitRegime.Elliptic || Regime == SpacecraftOrbitRegime.NearCircular;
        public float GravitationalParameter { get; }
        public float Radius { get; }
        public float Altitude { get; }
        public float Speed { get; }
        public float RadialVelocity { get; }
        public float TangentialSpeed { get; }
        public float CircularVelocity { get; }
        public float EscapeVelocity { get; }
        public float SpecificOrbitalEnergy { get; }
        public float Eccentricity { get; }
        public float SemiMajorAxis { get; }
        public float PeriapsisAltitude { get; }
        public float ApoapsisAltitude { get; }
        public float OrbitalPeriod { get; }
        public float FlightPathAngleDegrees { get; }

        public static SpacecraftOrbitSample NoFrame => new(
            SpacecraftOrbitRegime.NoFrame,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f);
    }
}
