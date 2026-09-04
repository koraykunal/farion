using System.Collections.Generic;
using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public sealed class PlanetaryGenerationProfile
    {
        readonly string sourceName;
        readonly List<ScriptableObject> ownedVariants;

        public PlanetaryGenerationProfile(
            string sourceName,
            int planetSeed,
            PlanetType planetType,
            float atmosphereDensity,
            float backgroundRadiation,
            float atmosphereScale,
            float atmosphereRadiusOffset,
            CelestialShapeProfile shapeProfile,
            PlanetClimateProfile climateProfile,
            PlanetHydrosphereProfile hydrosphereProfile,
            BiomeDistributionProfile biomeDistribution,
            SurfaceMaterialDistributionProfile surfaceMaterialDistribution,
            PlanetSurfaceStateProfile surfaceStateProfile,
            TerrainFeatureDistributionProfile terrainFeatureDistribution,
            List<ScriptableObject> ownedVariants = null)
        {
            this.sourceName = sourceName;
            PlanetSeed = planetSeed;
            PlanetType = planetType;
            AtmosphereDensity = Mathf.Max(0f, atmosphereDensity);
            BackgroundRadiation = Mathf.Clamp01(backgroundRadiation);
            AtmosphereScale = Mathf.Max(0f, atmosphereScale);
            AtmosphereRadiusOffset = Mathf.Max(0f, atmosphereRadiusOffset);
            ShapeProfile = shapeProfile;
            ClimateProfile = climateProfile;
            HydrosphereProfile = hydrosphereProfile;
            BiomeDistribution = biomeDistribution;
            SurfaceMaterialDistribution = surfaceMaterialDistribution;
            SurfaceStateProfile = surfaceStateProfile;
            TerrainFeatureDistribution = terrainFeatureDistribution;
            this.ownedVariants = ownedVariants ?? new List<ScriptableObject>();
        }

        public int PlanetSeed { get; }
        public PlanetType PlanetType { get; }
        public float AtmosphereDensity { get; }
        public bool HasAtmosphere => AtmosphereDensity > 0f;
        public float AtmosphereScale { get; }
        public float AtmosphereRadiusOffset { get; }
        public float BackgroundRadiation { get; }
        public CelestialShapeProfile ShapeProfile { get; }
        public PlanetClimateProfile ClimateProfile { get; }
        public PlanetHydrosphereProfile HydrosphereProfile { get; }
        public BiomeDistributionProfile BiomeDistribution { get; }
        public SurfaceMaterialDistributionProfile SurfaceMaterialDistribution { get; }
        public PlanetSurfaceStateProfile SurfaceStateProfile { get; }
        public TerrainFeatureDistributionProfile TerrainFeatureDistribution { get; }
        public bool SupportsSurfaceWaterClouds =>
            HasAtmosphere &&
            HydrosphereProfile != null &&
            HydrosphereProfile.HasSurfaceOcean &&
            HydrosphereProfile.WaterAvailability > 0f;

        public PlanetGenerationContext CreateContext(float radius, float surfaceGravity)
        {
            return new PlanetGenerationContext(
                PlanetSeed,
                radius,
                surfaceGravity,
                PlanetType,
                AtmosphereDensity,
                BackgroundRadiation);
        }

        public void Release()
        {
            for (int i = 0; i < ownedVariants.Count; i++)
            {
                ProfileVariants.Destroy(ownedVariants[i]);
            }

            ownedVariants.Clear();
        }

        public void CollectValidationIssues(float radius, float surfaceGravity, List<PlanetGenerationValidationIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            PlanetGenerationContext context = CreateContext(radius, surfaceGravity);
            CollectEnvironmentIssues(context, issues);

            if (ShapeProfile == null)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(sourceName, "Shape profile is missing."));
            }

            if (ClimateProfile == null)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(sourceName, "Climate profile is missing."));
            }

            if (BiomeDistribution == null)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(sourceName, "Biome distribution profile is missing."));
            }
            else
            {
                List<BiomeDefinition> validatedBiomes = new();
                CollectBiomeIssues(
                    BiomeDistribution.FallbackBiome,
                    context,
                    issues,
                    validatedBiomes,
                    "Fallback biome");
                foreach (BiomeDistributionRule rule in BiomeDistribution.Rules)
                {
                    if (rule == null)
                    {
                        continue;
                    }

                    CollectBiomeIssues(rule.Biome, context, issues, validatedBiomes, "Biome rule");
                }
            }

            if (SurfaceMaterialDistribution == null)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(sourceName, "Surface material distribution profile is missing."));
            }
            else
            {
                if (SurfaceMaterialDistribution.FallbackMaterial == null)
                {
                    issues.Add(PlanetGenerationValidationIssue.Error(
                        SurfaceMaterialDistribution.name,
                        "Surface material fallback is missing."));
                }

                foreach (SurfaceMaterialDistributionRule rule in SurfaceMaterialDistribution.Rules)
                {
                    if (rule != null && rule.HasMissingMaterial())
                    {
                        issues.Add(PlanetGenerationValidationIssue.Error(
                            SurfaceMaterialDistribution.name,
                            "Surface material distribution contains an enabled rule without a material reference."));
                    }
                }
            }

            if (TerrainFeatureDistribution == null)
            {
                return;
            }

            foreach (TerrainFeatureDistributionRule rule in TerrainFeatureDistribution.Rules)
            {
                if (rule != null && rule.HasMissingFeature())
                {
                    issues.Add(PlanetGenerationValidationIssue.Error(
                        TerrainFeatureDistribution.name,
                        "Terrain feature distribution contains an enabled rule without a feature reference."));
                }
            }
        }

        void CollectEnvironmentIssues(PlanetGenerationContext context, List<PlanetGenerationValidationIssue> issues)
        {
            if (HydrosphereProfile == null || !HydrosphereProfile.HasSurfaceOcean)
            {
                return;
            }

            if (!context.HasAtmosphere || context.AtmosphereDensity <= 0f)
            {
                issues.Add(PlanetGenerationValidationIssue.Error(
                    sourceName,
                    "Stable surface liquid is enabled but the generation environment has no atmosphere."));
            }
            else if (context.SurfaceGravity < 0.2f && HydrosphereProfile.WaterAvailability > 0.1f)
            {
                issues.Add(PlanetGenerationValidationIssue.Warning(
                    sourceName,
                    $"Surface gravity {context.SurfaceGravity:0.###} is very low for water availability {HydrosphereProfile.WaterAvailability:0.##}."));
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
