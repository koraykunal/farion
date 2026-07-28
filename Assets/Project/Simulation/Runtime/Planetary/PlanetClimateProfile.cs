using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public enum PlanetClimateMode
    {
        Rotating,
        TidallyLocked
    }

    [CreateAssetMenu(
        menuName = "Farion/Simulation/Planetary/Climate Profile",
        fileName = "SO_PlanetClimate")]
    public sealed class PlanetClimateProfile : ScriptableObject
    {
        [Header("Thermal Model")]
        [SerializeField] PlanetThermalProfile thermalProfile;
        [SerializeField] PlanetClimateMode climateMode = PlanetClimateMode.Rotating;
        [SerializeField] float fallbackMeanTemperatureCelsius = 12f;
        [Min(0f)]
        [SerializeField] float polarTemperatureDrop = 35f;
        [Min(0f)]
        [SerializeField] float altitudeCoolingPerUnit = 0.045f;
        [Min(0f)]
        [SerializeField] float temperatureNoiseAmplitude = 8f;
        [Min(0.01f)]
        [SerializeField] float temperatureNoiseScale = 2.25f;

        [Header("Hydrology")]
        [Range(0f, 1f)]
        [SerializeField] float baselinePrecipitation = 0.22f;
        [Range(0f, 1f)]
        [SerializeField] float atmosphereMoistureRetention = 0.22f;
        [Range(0f, 1f)]
        [SerializeField] float oceanMoistureContribution = 0.28f;
        [Range(0f, 1f)]
        [SerializeField] float precipitationNoiseAmplitude = 0.38f;
        [Min(0.01f)]
        [SerializeField] float precipitationNoiseScale = 2.7f;
        [SerializeField] float evaporationStartCelsius = 18f;
        [SerializeField] float evaporationFullCelsius = 75f;
        [Range(0f, 1f)]
        [SerializeField] float evaporationStrength = 0.72f;
        [Min(0f)]
        [SerializeField] float altitudeDryingPerUnit = 0.004f;

        public PlanetThermalProfile ThermalProfile => thermalProfile;
        public PlanetClimateMode ClimateMode => climateMode;

        public PlanetClimateSample Evaluate(
            PlanetGenerationContext context,
            PlanetHydrosphereProfile hydrosphere,
            CelestialInsolationSample insolation,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            Vector3 direction = localDirection.sqrMagnitude > 0.0001f
                ? localDirection.normalized
                : Vector3.up;
            float latitudeDegrees = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            float polarFactor = Mathf.Pow(Mathf.Abs(direction.y), 1.5f);
            float temperatureNoise = PlanetarySampling.SampleFractalSigned(
                direction,
                temperatureNoiseScale,
                4,
                2f,
                0.5f,
                SeedUtility.Derive(context.PlanetSeed, "climate.temperature"));

            bool hasInsolation = insolation.HasSource && thermalProfile != null;
            float temperature = hasInsolation
                ? thermalProfile.EvaluateLongTermSurfaceTemperature(
                    context,
                    insolation,
                    climateMode,
                    polarFactor,
                    altitude,
                    temperatureNoise,
                    polarTemperatureDrop,
                    altitudeCoolingPerUnit,
                    temperatureNoiseAmplitude)
                : fallbackMeanTemperatureCelsius
                    - polarTemperatureDrop * polarFactor
                    - Mathf.Max(0f, altitude) * altitudeCoolingPerUnit
                    + temperatureNoise * temperatureNoiseAmplitude;

            float precipitationNoise = PlanetarySampling.SampleFractalSigned(
                direction,
                precipitationNoiseScale,
                4,
                2f,
                0.52f,
                SeedUtility.Derive(context.PlanetSeed, "climate.precipitation"));
            float atmosphericMoisture = context.HasAtmosphere
                ? Mathf.Clamp01(context.AtmosphereDensity) * atmosphereMoistureRetention
                : 0f;
            float oceanMoisture = hydrosphere != null
                ? hydrosphere.WaterAvailability * oceanMoistureContribution
                : 0f;
            float precipitation = baselinePrecipitation
                + atmosphericMoisture
                + oceanMoisture
                + precipitationNoise * precipitationNoiseAmplitude
                - Mathf.Max(0f, altitude) * altitudeDryingPerUnit;
            precipitation = Mathf.Clamp01(precipitation);

            float evaporation = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    evaporationStartCelsius,
                    Mathf.Max(evaporationStartCelsius + 0.01f, evaporationFullCelsius),
                    temperature));
            float effectiveMoisture = Mathf.Clamp01(precipitation - evaporation * evaporationStrength);
            float radiation = hasInsolation
                ? thermalProfile.EvaluateLongTermSurfaceRadiation(context, insolation, polarFactor, altitude)
                : Mathf.Clamp01(
                    context.BackgroundRadiation
                    + Mathf.Max(0f, altitude) * 0.002f
                    + (context.HasAtmosphere ? 0f : 0.12f));

            return new PlanetClimateSample(
                context,
                direction,
                latitudeDegrees,
                altitude,
                slopeDegrees,
                temperature,
                precipitation,
                effectiveMoisture,
                1f - effectiveMoisture,
                radiation);
        }

        void OnValidate()
        {
            polarTemperatureDrop = Mathf.Max(0f, polarTemperatureDrop);
            altitudeCoolingPerUnit = Mathf.Max(0f, altitudeCoolingPerUnit);
            temperatureNoiseAmplitude = Mathf.Max(0f, temperatureNoiseAmplitude);
            temperatureNoiseScale = Mathf.Max(0.01f, temperatureNoiseScale);
            baselinePrecipitation = Mathf.Clamp01(baselinePrecipitation);
            atmosphereMoistureRetention = Mathf.Clamp01(atmosphereMoistureRetention);
            oceanMoistureContribution = Mathf.Clamp01(oceanMoistureContribution);
            precipitationNoiseAmplitude = Mathf.Clamp01(precipitationNoiseAmplitude);
            precipitationNoiseScale = Mathf.Max(0.01f, precipitationNoiseScale);
            evaporationFullCelsius = Mathf.Max(evaporationStartCelsius + 0.01f, evaporationFullCelsius);
            evaporationStrength = Mathf.Clamp01(evaporationStrength);
            altitudeDryingPerUnit = Mathf.Max(0f, altitudeDryingPerUnit);
        }
    }
}
