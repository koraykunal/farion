using UnityEngine;

namespace Farion.Simulation.Physics
{
    public readonly struct OrbitalElements
    {
        const int KeplerIterations = 8;
        const double KeplerTolerance = 1e-12d;

        OrbitalElements(
            double semiMajorAxis,
            double eccentricity,
            double meanMotion,
            double meanAnomalyAtEpoch,
            Vector3 periapsisAxis,
            Vector3 semiMinorAxis)
        {
            SemiMajorAxis = semiMajorAxis;
            Eccentricity = eccentricity;
            MeanMotion = meanMotion;
            MeanAnomalyAtEpoch = meanAnomalyAtEpoch;
            PeriapsisAxis = periapsisAxis;
            SemiMinorAxis = semiMinorAxis;
        }

        public double SemiMajorAxis { get; }
        public double Eccentricity { get; }
        public double MeanMotion { get; }
        public double MeanAnomalyAtEpoch { get; }
        public Vector3 PeriapsisAxis { get; }
        public Vector3 SemiMinorAxis { get; }
        public bool IsValid => MeanMotion > 0d && SemiMajorAxis > 0d && Eccentricity < 0.999d;
        public double PeriodSeconds => MeanMotion > 0d ? 2d * System.Math.PI / MeanMotion : 0d;

        public static bool TryCreate(
            Vector3 relativePosition,
            Vector3 relativeVelocity,
            double gravitationalParameter,
            out OrbitalElements elements)
        {
            elements = default;
            double mu = gravitationalParameter;
            double r = relativePosition.magnitude;
            double v2 = relativeVelocity.sqrMagnitude;
            if (mu <= 0d || r <= 0d || v2 <= 0d)
            {
                return false;
            }

            double energy = v2 * 0.5d - mu / r;
            if (energy >= 0d)
            {
                return false;
            }

            double semiMajorAxis = -mu / (2d * energy);
            Vector3 angularMomentum = Vector3.Cross(relativePosition, relativeVelocity);
            if (angularMomentum.sqrMagnitude <= 0d)
            {
                return false;
            }

            Vector3 eccentricityVector =
                ((float)(v2 - mu / r) * relativePosition -
                 Vector3.Dot(relativePosition, relativeVelocity) * relativeVelocity) /
                (float)mu;
            double eccentricity = eccentricityVector.magnitude;
            if (eccentricity >= 0.999d)
            {
                return false;
            }

            Vector3 orbitNormal = angularMomentum.normalized;
            Vector3 periapsisAxis = eccentricity > 1e-6d
                ? eccentricityVector.normalized
                : relativePosition.normalized;
            Vector3 semiMinorAxis = Vector3.Cross(orbitNormal, periapsisAxis).normalized;

            double x = Vector3.Dot(relativePosition, periapsisAxis);
            double y = Vector3.Dot(relativePosition, semiMinorAxis);
            double semiMinorLength = semiMajorAxis * System.Math.Sqrt(1d - eccentricity * eccentricity);
            double cosE = x / semiMajorAxis + eccentricity;
            double sinE = semiMinorLength > 0d ? y / semiMinorLength : 0d;
            double eccentricAnomaly = System.Math.Atan2(sinE, cosE);
            double meanAnomaly = eccentricAnomaly - eccentricity * System.Math.Sin(eccentricAnomaly);
            double meanMotion = System.Math.Sqrt(mu / (semiMajorAxis * semiMajorAxis * semiMajorAxis));

            elements = new OrbitalElements(
                semiMajorAxis,
                eccentricity,
                meanMotion,
                meanAnomaly,
                periapsisAxis,
                semiMinorAxis);
            return true;
        }

        public void Evaluate(double timeSeconds, out Vector3 position, out Vector3 velocity)
        {
            double meanAnomaly = MeanAnomalyAtEpoch + MeanMotion * timeSeconds;
            double eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, Eccentricity);
            double cosE = System.Math.Cos(eccentricAnomaly);
            double sinE = System.Math.Sin(eccentricAnomaly);
            double semiMinorLength = SemiMajorAxis * System.Math.Sqrt(1d - Eccentricity * Eccentricity);

            double x = SemiMajorAxis * (cosE - Eccentricity);
            double y = semiMinorLength * sinE;

            double radiusFactor = 1d - Eccentricity * cosE;
            double angularRate = radiusFactor > 1e-12d ? MeanMotion / radiusFactor : 0d;
            double vx = -SemiMajorAxis * angularRate * sinE;
            double vy = semiMinorLength * angularRate * cosE;

            position = PeriapsisAxis * (float)x + SemiMinorAxis * (float)y;
            velocity = PeriapsisAxis * (float)vx + SemiMinorAxis * (float)vy;
        }

        static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
        {
            double twoPi = 2d * System.Math.PI;
            meanAnomaly -= twoPi * System.Math.Floor(meanAnomaly / twoPi);
            double eccentricAnomaly = eccentricity < 0.8d ? meanAnomaly : System.Math.PI;
            for (int i = 0; i < KeplerIterations; i++)
            {
                double f = eccentricAnomaly - eccentricity * System.Math.Sin(eccentricAnomaly) - meanAnomaly;
                if (System.Math.Abs(f) < KeplerTolerance)
                {
                    break;
                }

                double derivative = 1d - eccentricity * System.Math.Cos(eccentricAnomaly);
                if (System.Math.Abs(derivative) < 1e-12d)
                {
                    break;
                }

                eccentricAnomaly -= f / derivative;
            }

            return eccentricAnomaly;
        }
    }
}
