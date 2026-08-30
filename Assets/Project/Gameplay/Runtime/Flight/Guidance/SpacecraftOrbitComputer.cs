using Farion.Gameplay.Actors;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(48)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialActorProbe))]
    public sealed class SpacecraftOrbitComputer : MonoBehaviour
    {
        const float Epsilon = 0.0001f;
        const float NearCircularEccentricity = 0.05f;

        [Header("Source")]
        [SerializeField] GravitySimulation simulation;
        [SerializeField] CelestialActorProbe celestialProbe;

        SpacecraftOrbitSample currentOrbit = SpacecraftOrbitSample.NoFrame;

        public SpacecraftOrbitSample CurrentOrbit => currentOrbit;

        GravitySimulation Simulation => simulation;

        void Awake()
        {
            ResolveComponents();
        }

        void OnValidate()
        {
            ResolveComponents();
        }

        void FixedUpdate()
        {
            RefreshOrbit();
        }

        public void SetSimulation(GravitySimulation source)
        {
            simulation = source;
        }

        [ContextMenu("Refresh Orbit")]
        public void RefreshOrbit()
        {
            ResolveComponents();

            currentOrbit = celestialProbe != null && celestialProbe.HasSample
                ? EvaluateOrbit(celestialProbe.CurrentSample, Simulation)
                : SpacecraftOrbitSample.NoFrame;
        }

        void ResolveComponents()
        {
            if (celestialProbe == null)
            {
                celestialProbe = GetComponent<CelestialActorProbe>();
            }
        }

        static SpacecraftOrbitSample EvaluateOrbit(CelestialFrameSample frame, GravitySimulation simulation)
        {
            if (!frame.HasBody || simulation == null)
            {
                return SpacecraftOrbitSample.NoFrame;
            }

            Vector3 relativePosition = frame.RelativePosition;
            Vector3 relativeVelocity = frame.RelativeVelocity;
            float radiusMagnitude = relativePosition.magnitude;
            float bodyRadius = frame.Body.Radius;
            float mu = simulation.GravitationalConstant * frame.Body.Mass;
            if (radiusMagnitude <= Epsilon || mu <= Epsilon)
            {
                return SpacecraftOrbitSample.NoFrame;
            }

            float speedMagnitude = relativeVelocity.magnitude;
            float circularSpeed = Mathf.Sqrt(mu / radiusMagnitude);
            float escapeSpeed = Mathf.Sqrt(2f * mu / radiusMagnitude);
            float energy = speedMagnitude * speedMagnitude * 0.5f - mu / radiusMagnitude;
            Vector3 angularMomentum = Vector3.Cross(relativePosition, relativeVelocity);
            float angularMomentumSqr = angularMomentum.sqrMagnitude;
            Vector3 eccentricityVector = angularMomentumSqr > Epsilon
                ? Vector3.Cross(relativeVelocity, angularMomentum) / mu - relativePosition / radiusMagnitude
                : -relativePosition / radiusMagnitude;
            float eccentricityMagnitude = eccentricityVector.magnitude;

            bool isBound = energy < 0f && eccentricityMagnitude < 1f;
            float semiMajor = isBound ? -mu / (2f * energy) : 0f;
            float periapsisRadius = angularMomentumSqr > Epsilon
                ? angularMomentumSqr / (mu * (1f + eccentricityMagnitude))
                : radiusMagnitude;
            float apoapsisRadius = isBound ? semiMajor * (1f + eccentricityMagnitude) : float.PositiveInfinity;
            float period = isBound && semiMajor > 0f
                ? 2f * Mathf.PI * Mathf.Sqrt(semiMajor * semiMajor * semiMajor / mu)
                : 0f;
            float flightPathAngle = Mathf.Atan2(frame.RadialVelocity, frame.TangentialSpeed) * Mathf.Rad2Deg;

            SpacecraftOrbitRegime nextRegime = EvaluateRegime(
                frame.SurfaceAltitude,
                bodyRadius,
                isBound,
                energy,
                eccentricityMagnitude,
                periapsisRadius);

            return new SpacecraftOrbitSample(
                nextRegime,
                mu,
                radiusMagnitude,
                frame.SurfaceAltitude,
                speedMagnitude,
                frame.RadialVelocity,
                frame.TangentialSpeed,
                circularSpeed,
                escapeSpeed,
                energy,
                eccentricityMagnitude,
                semiMajor,
                periapsisRadius - bodyRadius,
                isBound ? apoapsisRadius - bodyRadius : float.PositiveInfinity,
                period,
                flightPathAngle);
        }

        static SpacecraftOrbitRegime EvaluateRegime(
            float altitude,
            float bodyRadius,
            bool isBound,
            float energy,
            float eccentricity,
            float periapsisRadius)
        {
            if (altitude <= Mathf.Max(1f, bodyRadius * 0.02f))
            {
                return SpacecraftOrbitRegime.SurfaceProximity;
            }

            if (!isBound)
            {
                return energy < 0f
                    ? SpacecraftOrbitRegime.Suborbital
                    : SpacecraftOrbitRegime.Escape;
            }

            if (periapsisRadius <= bodyRadius)
            {
                return SpacecraftOrbitRegime.Suborbital;
            }

            return eccentricity <= NearCircularEccentricity
                ? SpacecraftOrbitRegime.NearCircular
                : SpacecraftOrbitRegime.Elliptic;
        }
    }
}

