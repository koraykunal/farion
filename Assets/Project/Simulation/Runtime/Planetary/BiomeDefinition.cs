using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Biome Definition", fileName = "SO_Biome")]
    public sealed class BiomeDefinition : ScriptableObject
    {
        [SerializeField] string biomeId = "biome.temperate";
        [SerializeField] string displayName = "Temperate";
        [SerializeField] Color previewColor = new(0.25f, 0.55f, 0.28f, 1f);

        [Header("Physical Compatibility")]
        [SerializeField] List<PlanetType> allowedPlanetTypes = new();
        [SerializeField] List<ClimateType> allowedClimates = new();
        [SerializeField] Vector2 radiusRange = new(0f, 1000000f);
        [SerializeField] Vector2 surfaceGravityRange = new(0f, 1000f);
        [SerializeField] bool requiresAtmosphere;
        [SerializeField] Vector2 temperatureRange = new(-1000000f, 1000000f);
        [SerializeField] Vector2 radiationRange = new(0f, 1f);

        public string BiomeId => string.IsNullOrWhiteSpace(biomeId) ? name : biomeId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? BiomeId : displayName.Trim();
        public Color PreviewColor => previewColor;

        public bool IsCompatibleWith(PlanetGenerationContext context)
        {
            return AllowsPlanetType(context.PlanetType) &&
                AllowsClimate(context.Climate) &&
                Contains(radiusRange, context.Radius) &&
                Contains(surfaceGravityRange, context.SurfaceGravity) &&
                (!requiresAtmosphere || context.HasAtmosphere) &&
                Contains(temperatureRange, context.MeanTemperatureCelsius) &&
                Contains(radiationRange, context.RadiationLevel);
        }

        public bool IsCompatibleWith(PlanetGenerationContext context, PlanetClimateSample climate)
        {
            return AllowsPlanetType(context.PlanetType) &&
                AllowsClimate(context.Climate) &&
                Contains(radiusRange, context.Radius) &&
                Contains(surfaceGravityRange, context.SurfaceGravity) &&
                (!requiresAtmosphere || context.HasAtmosphere) &&
                Contains(temperatureRange, climate.TemperatureCelsius) &&
                Contains(radiationRange, climate.Radiation);
        }

        public void CollectValidationIssues(
            PlanetGenerationContext context,
            List<PlanetGenerationValidationIssue> issues,
            string source)
        {
            if (issues == null)
            {
                return;
            }

            string issueSource = $"{source}: {DisplayName}";
            if (!AllowsPlanetType(context.PlanetType))
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Planet type {context.PlanetType} is not allowed for biome {BiomeId}."));
            }

            if (!AllowsClimate(context.Climate))
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Climate {context.Climate} is not allowed for biome {BiomeId}."));
            }

            if (!Contains(radiusRange, context.Radius))
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Body radius {context.Radius:0.###} is outside biome range {radiusRange.x:0.###}-{radiusRange.y:0.###}."));
            }

            if (!Contains(surfaceGravityRange, context.SurfaceGravity))
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Surface gravity {context.SurfaceGravity:0.###} is outside biome range {surfaceGravityRange.x:0.###}-{surfaceGravityRange.y:0.###}."));
            }

            if (requiresAtmosphere && !context.HasAtmosphere)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Biome {BiomeId} requires an atmosphere."));
            }

            if (!Contains(temperatureRange, context.MeanTemperatureCelsius))
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Mean temperature {context.MeanTemperatureCelsius:0.###}C is outside biome range {temperatureRange.x:0.###}-{temperatureRange.y:0.###}C."));
            }

            if (!Contains(radiationRange, context.RadiationLevel))
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    issueSource,
                    $"Radiation level {context.RadiationLevel:0.###} is outside biome range {radiationRange.x:0.###}-{radiationRange.y:0.###}."));
            }
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(biomeId))
            {
                biomeId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = biomeId;
            }

            NormalizeRange(ref radiusRange, 0f, 1000000f);
            NormalizeRange(ref surfaceGravityRange, 0f, 1000f);
            NormalizeRange(ref temperatureRange, -1000000f, 1000000f);
            NormalizeRange(ref radiationRange, 0f, 1f);
            allowedPlanetTypes ??= new List<PlanetType>();
            allowedClimates ??= new List<ClimateType>();
        }

        bool AllowsPlanetType(PlanetType planetType)
        {
            return allowedPlanetTypes == null || allowedPlanetTypes.Count == 0 || allowedPlanetTypes.Contains(planetType);
        }

        bool AllowsClimate(ClimateType climate)
        {
            return allowedClimates == null || allowedClimates.Count == 0 || allowedClimates.Contains(climate);
        }

        static bool Contains(Vector2 range, float value)
        {
            return value >= range.x && value <= range.y;
        }

        static void NormalizeRange(ref Vector2 range, float min, float max)
        {
            range.x = Mathf.Clamp(range.x, min, max);
            range.y = Mathf.Clamp(range.y, min, max);
            if (range.y < range.x)
            {
                range.y = range.x;
            }
        }
    }
}
