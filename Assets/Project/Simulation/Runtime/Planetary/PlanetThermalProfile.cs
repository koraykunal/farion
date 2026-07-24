using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Thermal Profile", fileName = "SO_PlanetThermal")]
    public sealed class PlanetThermalProfile : ScriptableObject
    {
        [Header("Surface")]
        [SerializeField, Range(0f, 1f)] float bondAlbedo = 0.3f;
        [SerializeField] float greenhouseWarmingCelsius = 28f;
        [SerializeField] float designTemperatureBiasCelsius;

        [Header("Heat Distribution")]
        [SerializeField, Range(0f, 1f)] float baselineHeatRedistribution = 0.35f;
        [SerializeField, Range(0f, 1f)] float atmosphereHeatRedistribution = 0.25f;
        [SerializeField, Min(0f)] float localInsolationSwingCelsius;

        [Header("Radiation")]
        [SerializeField, Range(0f, 1f)] float stellarRadiationContribution = 0.12f;
        [SerializeField, Range(0f, 1f)] float unshieldedSurfaceRadiationBonus = 0.12f;
        [SerializeField, Range(0f, 1f)] float atmosphereRadiationShielding = 0.6f;

        [Header("Limits")]
        [SerializeField] Vector2 temperatureRangeCelsius = new(-220f, 180f);

        public float BondAlbedo => Mathf.Clamp01(bondAlbedo);

        void OnValidate()
        {
            bondAlbedo = Mathf.Clamp01(bondAlbedo);
            baselineHeatRedistribution = Mathf.Clamp01(baselineHeatRedistribution);
            atmosphereHeatRedistribution = Mathf.Clamp01(atmosphereHeatRedistribution);
            localInsolationSwingCelsius = Mathf.Max(0f, localInsolationSwingCelsius);
            stellarRadiationContribution = Mathf.Clamp01(stellarRadiationContribution);
            unshieldedSurfaceRadiationBonus = Mathf.Clamp01(unshieldedSurfaceRadiationBonus);
            atmosphereRadiationShielding = Mathf.Clamp01(atmosphereRadiationShielding);
            temperatureRangeCelsius.y = Mathf.Max(temperatureRangeCelsius.x, temperatureRangeCelsius.y);
        }

        public float EvaluateSurfaceTemperature(
            PlanetGenerationContext context,
            CelestialInsolationSample insolation,
            float climateBiasCelsius,
            float polarFactor,
            float altitude,
            float temperatureNoise,
            float polarTemperatureDrop,
            float altitudeCoolingPerUnit,
            float temperatureNoiseAmplitude)
        {
            float atmosphericDensity = context.HasAtmosphere ? Mathf.Clamp01(context.AtmosphereDensity) : 0f;
            float heatRedistribution = Mathf.Clamp01(baselineHeatRedistribution + atmosphericDensity * atmosphereHeatRedistribution);
            float exposureDeviation = insolation.DirectExposure - 0.25f;
            float localInsolationOffset = exposureDeviation * localInsolationSwingCelsius * (1f - heatRedistribution);
            float greenhouse = context.HasAtmosphere ? greenhouseWarmingCelsius * atmosphericDensity : 0f;

            float temperature = insolation.EquilibriumTemperatureCelsius +
                greenhouse +
                designTemperatureBiasCelsius +
                climateBiasCelsius +
                localInsolationOffset -
                polarTemperatureDrop * polarFactor -
                Mathf.Max(0f, altitude) * altitudeCoolingPerUnit +
                temperatureNoise * temperatureNoiseAmplitude;

            return Mathf.Clamp(temperature, temperatureRangeCelsius.x, temperatureRangeCelsius.y);
        }

        public float EvaluateSurfaceRadiation(
            PlanetGenerationContext context,
            CelestialInsolationSample insolation,
            float altitude)
        {
            float atmosphericDensity = context.HasAtmosphere ? Mathf.Clamp01(context.AtmosphereDensity) : 0f;
            float atmosphereShield = context.HasAtmosphere ? atmosphereRadiationShielding * atmosphericDensity : 0f;
            float transmittedFraction = Mathf.Clamp01(1f - atmosphereShield);
            float stellarRadiation = insolation.NormalizedIrradiance *
                Mathf.Lerp(0.35f, 1f, insolation.DirectExposure) *
                stellarRadiationContribution *
                transmittedFraction;
            float noAtmosphereBonus = context.HasAtmosphere ? 0f : unshieldedSurfaceRadiationBonus;

            return Mathf.Clamp01(context.RadiationLevel + stellarRadiation + noAtmosphereBonus + Mathf.Max(0f, altitude) * 0.002f);
        }
    }
}
