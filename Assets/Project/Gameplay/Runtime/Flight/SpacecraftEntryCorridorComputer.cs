using Farion.Gameplay.Actors;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(62)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialActorProbe))]
    [RequireComponent(typeof(SpacecraftOrbitComputer))]
    public sealed class SpacecraftEntryCorridorComputer : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] SpacecraftEntryCorridorProfile profile;

        [Header("Source")]
        [SerializeField] CelestialActorProbe celestialProbe;
        [SerializeField] SpacecraftOrbitComputer orbitComputer;

        [Header("Runtime Entry Corridor")]
        [SerializeField] SpacecraftEntryCorridorState state = SpacecraftEntryCorridorState.NoFrame;
        [SerializeField] string advisory = "NO FRAME";
        [SerializeField] float entryAngleDegrees;
        [SerializeField] float speedToEscapeRatio;
        [SerializeField] float speedToCircularRatio;
        [SerializeField] float periapsisAltitude;
        [SerializeField] float atmosphereDepth;
        [SerializeField] float normalizedRisk;

        SpacecraftEntryCorridorSample currentCorridor = SpacecraftEntryCorridorSample.NoFrame;

        public SpacecraftEntryCorridorSample CurrentCorridor => currentCorridor;
        public SpacecraftEntryCorridorState State => state;
        public string Advisory => advisory;

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
            RefreshEntryCorridor();
        }

        [ContextMenu("Refresh Entry Corridor")]
        public void RefreshEntryCorridor()
        {
            ResolveComponents();

            if (profile == null ||
                celestialProbe == null ||
                orbitComputer == null ||
                !celestialProbe.HasSample ||
                !orbitComputer.HasOrbit)
            {
                currentCorridor = SpacecraftEntryCorridorSample.NoFrame;
                ApplyDebugFields();
                return;
            }

            currentCorridor = EvaluateCorridor(
                profile,
                celestialProbe.CurrentSample,
                orbitComputer.CurrentOrbit);
            ApplyDebugFields();
        }

        void ResolveComponents()
        {
            if (celestialProbe == null)
            {
                celestialProbe = GetComponent<CelestialActorProbe>();
            }

            if (orbitComputer == null)
            {
                orbitComputer = GetComponent<SpacecraftOrbitComputer>();
            }
        }

        static SpacecraftEntryCorridorSample EvaluateCorridor(
            SpacecraftEntryCorridorProfile sourceProfile,
            Farion.Simulation.Celestial.CelestialFrameSample frame,
            SpacecraftOrbitSample orbit)
        {
            if (!frame.HasBody || !orbit.HasFrame)
            {
                return SpacecraftEntryCorridorSample.NoFrame;
            }

            if (!frame.HasAtmosphere)
            {
                return Build(
                    SpacecraftEntryCorridorState.NoAtmosphere,
                    0f,
                    0f,
                    0f,
                    orbit.PeriapsisAltitude,
                    0f,
                    "NO ATMOSPHERE");
            }

            float entryAngle = Mathf.Max(0f, -orbit.FlightPathAngleDegrees);
            float escapeRatio = orbit.EscapeVelocity > 0f ? orbit.Speed / orbit.EscapeVelocity : 0f;
            float circularRatio = orbit.CircularVelocity > 0f ? orbit.Speed / orbit.CircularVelocity : 0f;
            float atmosphereDepth = frame.AtmosphereNormalizedDepth;

            SpacecraftEntryCorridorState nextState;
            if (orbit.Regime == SpacecraftOrbitRegime.Escape)
            {
                nextState = SpacecraftEntryCorridorState.Escaping;
            }
            else if (orbit.PeriapsisAltitude <= sourceProfile.ImpactPeriapsisAltitude)
            {
                nextState = SpacecraftEntryCorridorState.Impacting;
            }
            else if (escapeRatio > sourceProfile.MaximumEscapeSpeedRatio ||
                     circularRatio > sourceProfile.MaximumCircularSpeedRatio)
            {
                nextState = SpacecraftEntryCorridorState.Overspeed;
            }
            else if (!frame.IsInsideAtmosphere && orbit.PeriapsisAltitude > sourceProfile.SafePeriapsisAltitude)
            {
                nextState = SpacecraftEntryCorridorState.OutsideAtmosphere;
            }
            else if (entryAngle < sourceProfile.MinimumEntryAngle)
            {
                nextState = SpacecraftEntryCorridorState.ShallowEntry;
            }
            else if (entryAngle > sourceProfile.MaximumEntryAngle)
            {
                nextState = SpacecraftEntryCorridorState.SteepEntry;
            }
            else
            {
                nextState = SpacecraftEntryCorridorState.SafeEntry;
            }

            float angleRisk = EvaluateBandRisk(entryAngle, sourceProfile.MinimumEntryAngle, sourceProfile.MaximumEntryAngle);
            float speedRisk = Mathf.Max(
                RatioRisk(escapeRatio, sourceProfile.MaximumEscapeSpeedRatio),
                RatioRisk(circularRatio, sourceProfile.MaximumCircularSpeedRatio));
            float periapsisRisk = orbit.PeriapsisAltitude <= sourceProfile.SafePeriapsisAltitude
                ? Mathf.InverseLerp(sourceProfile.SafePeriapsisAltitude, sourceProfile.ImpactPeriapsisAltitude, orbit.PeriapsisAltitude)
                : 0f;
            float risk = Mathf.Clamp01(Mathf.Max(angleRisk, speedRisk, periapsisRisk));

            return Build(
                nextState,
                entryAngle,
                escapeRatio,
                circularRatio,
                orbit.PeriapsisAltitude,
                atmosphereDepth,
                risk,
                BuildAdvisory(nextState));
        }

        static SpacecraftEntryCorridorSample Build(
            SpacecraftEntryCorridorState nextState,
            float entryAngle,
            float escapeRatio,
            float circularRatio,
            float periapsis,
            float atmosphereDepth,
            string nextAdvisory)
        {
            return Build(nextState, entryAngle, escapeRatio, circularRatio, periapsis, atmosphereDepth, 0f, nextAdvisory);
        }

        static SpacecraftEntryCorridorSample Build(
            SpacecraftEntryCorridorState nextState,
            float entryAngle,
            float escapeRatio,
            float circularRatio,
            float periapsis,
            float atmosphereDepth,
            float risk,
            string nextAdvisory)
        {
            return new SpacecraftEntryCorridorSample(
                nextState,
                entryAngle,
                escapeRatio,
                circularRatio,
                periapsis,
                atmosphereDepth,
                risk,
                nextAdvisory);
        }

        static float EvaluateBandRisk(float value, float minimum, float maximum)
        {
            if (maximum <= minimum)
            {
                return 0f;
            }

            if (value < minimum)
            {
                return Mathf.InverseLerp(minimum, 0f, value);
            }

            if (value > maximum)
            {
                return Mathf.InverseLerp(maximum, maximum * 1.5f, value);
            }

            return 0f;
        }

        static float RatioRisk(float value, float maximum)
        {
            if (maximum <= 0f || value <= maximum)
            {
                return 0f;
            }

            return Mathf.InverseLerp(maximum, maximum * 1.35f, value);
        }

        static string BuildAdvisory(SpacecraftEntryCorridorState nextState)
        {
            return nextState switch
            {
                SpacecraftEntryCorridorState.NoAtmosphere => "NO ENTRY CORRIDOR",
                SpacecraftEntryCorridorState.OutsideAtmosphere => "LOWER PERIAPSIS",
                SpacecraftEntryCorridorState.SafeEntry => "ENTRY CORRIDOR",
                SpacecraftEntryCorridorState.ShallowEntry => "STEEPEN ENTRY",
                SpacecraftEntryCorridorState.SteepEntry => "RAISE PERIAPSIS",
                SpacecraftEntryCorridorState.Overspeed => "RETRO BURN",
                SpacecraftEntryCorridorState.Impacting => "ABORT IMPACT",
                SpacecraftEntryCorridorState.Escaping => "CAPTURE BURN",
                _ => "NO FRAME"
            };
        }

        void ApplyDebugFields()
        {
            state = currentCorridor.State;
            advisory = currentCorridor.Advisory;
            entryAngleDegrees = currentCorridor.EntryAngleDegrees;
            speedToEscapeRatio = currentCorridor.SpeedToEscapeRatio;
            speedToCircularRatio = currentCorridor.SpeedToCircularRatio;
            periapsisAltitude = currentCorridor.PeriapsisAltitude;
            atmosphereDepth = currentCorridor.AtmosphereDepth;
            normalizedRisk = currentCorridor.NormalizedRisk;
        }
    }
}
