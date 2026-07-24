using System.Collections.Generic;
using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Generation Profile", fileName = "SO_PlanetaryGeneration")]
    public sealed class PlanetaryGenerationProfile : ScriptableObject
    {
        [SerializeField] int planetSeed = 1001;
        [SerializeField] PlanetType planetType = PlanetType.Rocky;
        [SerializeField] ClimateType climate = ClimateType.Temperate;
        [Header("Environment")]
        [SerializeField] bool hasAtmosphere = true;
        [Min(0f)]
        [SerializeField] float atmosphereDensity = 1f;
        [SerializeField] bool hasStableLiquidSurface;
        [Range(0f, 1f)]
        [SerializeField] float liquidCoverage;
        [SerializeField] float meanTemperatureCelsius = 15f;
        [Range(0f, 1f)]
        [SerializeField] float radiationLevel = 0.05f;

        [Header("Layers")]
        [SerializeField] CelestialShapeProfile shapeProfile;
        [SerializeField] BiomeDistributionProfile biomeDistribution;
        [SerializeField] TerrainFeatureDistributionProfile terrainFeatureDistribution;

        public int PlanetSeed => planetSeed;
        public PlanetType PlanetType => planetType;
        public ClimateType Climate => climate;
        public bool HasAtmosphere => hasAtmosphere;
        public float AtmosphereDensity => atmosphereDensity;
        public bool HasStableLiquidSurface => hasStableLiquidSurface;
        public float LiquidCoverage => liquidCoverage;
        public float MeanTemperatureCelsius => meanTemperatureCelsius;
        public float RadiationLevel => radiationLevel;
        public CelestialShapeProfile ShapeProfile => shapeProfile;
        public BiomeDistributionProfile BiomeDistribution => biomeDistribution;
        public TerrainFeatureDistributionProfile TerrainFeatureDistribution => terrainFeatureDistribution;

        public PlanetGenerationContext CreateContext(float radius, float surfaceGravity)
        {
            return new PlanetGenerationContext(
                planetSeed,
                radius,
                surfaceGravity,
                planetType,
                climate,
                hasAtmosphere,
                atmosphereDensity,
                hasStableLiquidSurface,
                liquidCoverage,
                meanTemperatureCelsius,
                radiationLevel);
        }

        public void CollectValidationIssues(float radius, float surfaceGravity, List<PlanetGenerationValidationIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            PlanetGenerationContext context = CreateContext(radius, surfaceGravity);
            CollectEnvironmentIssues(context, issues);

            if (biomeDistribution == null)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(name, "Biome distribution profile is missing."));
                return;
            }

            List<BiomeDefinition> validatedBiomes = new();
            CollectBiomeIssues(biomeDistribution.FallbackBiome, context, issues, validatedBiomes, "Fallback biome");
            foreach (BiomeDistributionRule rule in biomeDistribution.Rules)
            {
                if (rule == null)
                {
                    continue;
                }

                CollectBiomeIssues(rule.Biome, context, issues, validatedBiomes, "Biome rule");
            }

            if (terrainFeatureDistribution == null)
            {
                return;
            }

            foreach (TerrainFeatureDistributionRule rule in terrainFeatureDistribution.Rules)
            {
                if (rule != null && rule.HasMissingFeature())
                {
                    issues.Add(PlanetGenerationValidationIssue.Error(
                        terrainFeatureDistribution.name,
                        "Terrain feature distribution contains an enabled rule without a feature reference."));
                }
            }
        }

        void OnValidate()
        {
            atmosphereDensity = hasAtmosphere ? Mathf.Max(0f, atmosphereDensity) : 0f;
            liquidCoverage = hasStableLiquidSurface ? Mathf.Clamp01(liquidCoverage) : 0f;
            radiationLevel = Mathf.Clamp01(radiationLevel);
        }

        void CollectEnvironmentIssues(PlanetGenerationContext context, List<PlanetGenerationValidationIssue> issues)
        {
            if (!context.HasStableLiquidSurface)
            {
                return;
            }

            if (!context.HasAtmosphere || context.AtmosphereDensity <= 0f)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    name,
                    "Stable surface liquid is enabled but the generation environment has no atmosphere."));
            }

            if (context.SurfaceGravity < 0.2f && context.LiquidCoverage > 0.1f)
            {
                issues.Add(PlanetGenerationValidationIssue.Warning(
                    name,
                    $"Surface gravity {context.SurfaceGravity:0.###} is very low for stable liquid coverage {context.LiquidCoverage:0.##}."));
            }

            if (context.MeanTemperatureCelsius < -80f || context.MeanTemperatureCelsius > 120f)
            {
                issues.Add(PlanetGenerationValidationIssue.Warning(
                    name,
                    $"Mean temperature {context.MeanTemperatureCelsius:0.###}C is extreme for stable surface liquid."));
            }
        }

        static void CollectBiomeIssues(
            BiomeDefinition biome,
            PlanetGenerationContext context,
            List<PlanetGenerationValidationIssue> issues,
            List<BiomeDefinition> validatedBiomes,
            string source)
        {
            if (biome == null)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(source, "Biome reference is missing."));
                return;
            }

            if (validatedBiomes.Contains(biome))
            {
                return;
            }

            validatedBiomes.Add(biome);
            biome.CollectValidationIssues(context, issues, source);
        }
    }
}
