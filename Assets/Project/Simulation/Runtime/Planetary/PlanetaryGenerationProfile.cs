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

        [Header("Environment")]
        [Min(0f)]
        [SerializeField] float atmosphereDensity = 1f;
        [Range(0f, 1f)]
        [SerializeField] float backgroundRadiation = 0.05f;

        [Header("Layers")]
        [SerializeField] CelestialShapeProfile shapeProfile;
        [SerializeField] PlanetClimateProfile climateProfile;
        [SerializeField] PlanetHydrosphereProfile hydrosphereProfile;
        [SerializeField] BiomeDistributionProfile biomeDistribution;
        [SerializeField] SurfaceMaterialDistributionProfile surfaceMaterialDistribution;
        [SerializeField] PlanetSurfaceStateProfile surfaceStateProfile;
        [SerializeField] TerrainFeatureDistributionProfile terrainFeatureDistribution;

        public int PlanetSeed => planetSeed;
        public PlanetType PlanetType => planetType;
        public float AtmosphereDensity => atmosphereDensity;
        public bool HasAtmosphere => atmosphereDensity > 0f;
        public bool SupportsSurfaceWaterClouds =>
            HasAtmosphere &&
            hydrosphereProfile != null &&
            hydrosphereProfile.HasSurfaceOcean &&
            hydrosphereProfile.WaterAvailability > 0f;
        public float BackgroundRadiation => backgroundRadiation;
        public CelestialShapeProfile ShapeProfile => shapeProfile;
        public PlanetClimateProfile ClimateProfile => climateProfile;
        public PlanetHydrosphereProfile HydrosphereProfile => hydrosphereProfile;
        public BiomeDistributionProfile BiomeDistribution => biomeDistribution;
        public SurfaceMaterialDistributionProfile SurfaceMaterialDistribution => surfaceMaterialDistribution;
        public PlanetSurfaceStateProfile SurfaceStateProfile => surfaceStateProfile;
        public TerrainFeatureDistributionProfile TerrainFeatureDistribution => terrainFeatureDistribution;

        public PlanetGenerationContext CreateContext(float radius, float surfaceGravity)
        {
            return new PlanetGenerationContext(
                planetSeed,
                radius,
                surfaceGravity,
                planetType,
                atmosphereDensity,
                backgroundRadiation);
        }

        public void CollectValidationIssues(float radius, float surfaceGravity, List<PlanetGenerationValidationIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            PlanetGenerationContext context = CreateContext(radius, surfaceGravity);
            CollectEnvironmentIssues(context, issues);

            if (climateProfile == null)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(name, "Climate profile is missing."));
            }

            if (biomeDistribution == null)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(name, "Biome distribution profile is missing."));
            }
            else
            {
                List<BiomeDefinition> validatedBiomes = new();
                CollectBiomeIssues(
                    biomeDistribution.FallbackBiome,
                    context,
                    issues,
                    validatedBiomes,
                    "Fallback biome");
                foreach (BiomeDistributionRule rule in biomeDistribution.Rules)
                {
                    if (rule == null)
                    {
                        continue;
                    }

                    CollectBiomeIssues(rule.Biome, context, issues, validatedBiomes, "Biome rule");
                }
            }

            if (surfaceMaterialDistribution == null)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(name, "Surface material distribution profile is missing."));
            }
            else
            {
                if (surfaceMaterialDistribution.FallbackMaterial == null)
                {
                    issues.Add(PlanetGenerationValidationIssue.Error(
                        surfaceMaterialDistribution.name,
                        "Surface material fallback is missing."));
                }

                foreach (SurfaceMaterialDistributionRule rule in surfaceMaterialDistribution.Rules)
                {
                    if (rule != null && rule.HasMissingMaterial())
                    {
                        issues.Add(PlanetGenerationValidationIssue.Error(
                            surfaceMaterialDistribution.name,
                            "Surface material distribution contains an enabled rule without a material reference."));
                    }
                }
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
            atmosphereDensity = Mathf.Max(0f, atmosphereDensity);
            backgroundRadiation = Mathf.Clamp01(backgroundRadiation);
        }

        void CollectEnvironmentIssues(PlanetGenerationContext context, List<PlanetGenerationValidationIssue> issues)
        {
            if (hydrosphereProfile == null || !hydrosphereProfile.HasSurfaceOcean)
            {
                return;
            }

            if (!context.HasAtmosphere || context.AtmosphereDensity <= 0f)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    name,
                    "Stable surface liquid is enabled but the generation environment has no atmosphere."));
            }
            else if (context.SurfaceGravity < 0.2f && hydrosphereProfile.WaterAvailability > 0.1f)
            {
                issues.Add(PlanetGenerationValidationIssue.Warning(
                    name,
                    $"Surface gravity {context.SurfaceGravity:0.###} is very low for water availability {hydrosphereProfile.WaterAvailability:0.##}."));
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
