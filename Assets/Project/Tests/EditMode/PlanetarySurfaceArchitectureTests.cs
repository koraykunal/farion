using System.Collections.Generic;
using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Planetary;
using Farion.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class PlanetarySurfaceArchitectureTests
    {
        readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdObjects.Count; i++)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void CubeProjection_RoundTripsFaceParametersIncludingNeighbourOverhang()
        {
            for (int face = 0; face < 6; face++)
            {
                for (int ui = 0; ui <= 8; ui++)
                {
                    for (int vi = 0; vi <= 8; vi++)
                    {
                        float u = -1f + ui * 0.25f;
                        float v = -1f + vi * 0.25f;
                        Vector3 direction = CelestialCubeProjection.ToDirection(
                            (CelestialCubeFace)face,
                            u,
                            v);
                        CelestialCubeProjection.Project(
                            direction,
                            out CelestialCubeFace resolvedFace,
                            out float resolvedU,
                            out float resolvedV);

                        if (Mathf.Abs(u) < 0.999f && Mathf.Abs(v) < 0.999f)
                        {
                            Assert.That(resolvedFace, Is.EqualTo((CelestialCubeFace)face));
                        }

                        Vector3 resolvedDirection = CelestialCubeProjection.ToDirection(
                            resolvedFace,
                            resolvedU,
                            resolvedV);
                        Assert.That(
                            Vector3.Distance(direction, resolvedDirection),
                            Is.LessThan(0.0005f));
                    }
                }
            }
        }

        [Test]
        public void CubeProjection_TangentWarpKeepsAngularSpacingNearlyUniform()
        {
            Vector3 centerA = CelestialCubeProjection.ToDirection(CelestialCubeFace.PositiveY, 0f, 0f);
            Vector3 centerB = CelestialCubeProjection.ToDirection(CelestialCubeFace.PositiveY, 0.01f, 0f);
            Vector3 cornerA = CelestialCubeProjection.ToDirection(CelestialCubeFace.PositiveY, 0.98f, 0.98f);
            Vector3 cornerB = CelestialCubeProjection.ToDirection(CelestialCubeFace.PositiveY, 0.99f, 0.98f);

            float centerStep = Vector3.Angle(centerA, centerB);
            float cornerStep = Vector3.Angle(cornerA, cornerB);

            Assert.That(centerStep, Is.GreaterThan(0f));
            Assert.That(cornerStep / centerStep, Is.GreaterThan(0.5f));
            Assert.That(cornerStep / centerStep, Is.LessThan(2f));
        }

        [Test]
        public void BandLimitedSampling_DropsOctavesAndFadesFeaturesBelowTheSampleFootprint()
        {
            Vector3 direction = new Vector3(0.31f, 0.72f, -0.62f).normalized;

            float sharp = PlanetarySampling.SampleBandLimitedFractalSigned(
                direction,
                40f,
                6,
                2f,
                0.5f,
                907,
                Vector3.zero,
                0.00001f);
            float blurred = PlanetarySampling.SampleBandLimitedFractalSigned(
                direction,
                40f,
                6,
                2f,
                0.5f,
                907,
                Vector3.zero,
                0.05f);
            float unlimited = PlanetarySampling.SampleFractalSigned(
                direction,
                40f,
                6,
                2f,
                0.5f,
                907,
                Vector3.zero);

            Assert.That(sharp, Is.EqualTo(unlimited).Within(0.000001f));
            Assert.That(blurred, Is.Not.EqualTo(unlimited).Within(0.000001f));

            Assert.That(PlanetarySampling.ResolveFootprintFade(0.01f, 0f), Is.EqualTo(1f));
            Assert.That(PlanetarySampling.ResolveFootprintFade(0.01f, 0.001f), Is.EqualTo(1f));
            Assert.That(PlanetarySampling.ResolveFootprintFade(0.01f, 0.02f), Is.EqualTo(0f));
            Assert.That(
                PlanetarySampling.ResolveUsableOctaves(4f, 2f, 6, 0.5f),
                Is.EqualTo(1f));
            Assert.That(
                PlanetarySampling.ResolveUsableOctaves(4f, 2f, 6, 0.000001f),
                Is.EqualTo(6f));
        }

        [Test]
        public void SphericalNoise_IsDeterministicAndContinuousAcrossNearbyDirections()
        {
            Vector3 directionA = new(0.999999f, 0.001f, -0.0005f);
            Vector3 directionB = new(0.999999f, 0.0011f, -0.0004f);

            float first = PlanetarySampling.SampleFractal01(directionA, 3f, 5, 2f, 0.5f, 401);
            float repeated = PlanetarySampling.SampleFractal01(directionA, 3f, 5, 2f, 0.5f, 401);
            float nearby = PlanetarySampling.SampleFractal01(directionB, 3f, 5, 2f, 0.5f, 401);

            Assert.That(repeated, Is.EqualTo(first).Within(0.000001f));
            Assert.That(Mathf.Abs(nearby - first), Is.LessThan(0.01f));
        }

        [Test]
        public void BiomeDistribution_UsesCompatibleFallbackWhenNoRuleMatches()
        {
            BiomeDefinition fallback = Create<BiomeDefinition>();
            BiomeDistributionProfile distribution = Create<BiomeDistributionProfile>();
            TestFieldAccess.SetField(distribution, "fallbackBiome", fallback);
            PlanetGenerationContext context = PlanetGenerationContext.CreateDefault(17);
            PlanetClimateSample climate = CreateClimate(context);

            BiomeSample sample = distribution.SampleBiome(context, climate, Vector3.up, 0f, 0f);
            List<BiomeWeight> weights = new();
            int count = distribution.SampleBiomeWeights(context, climate, Vector3.up, 0f, 0f, weights);

            Assert.That(sample.Biome, Is.SameAs(fallback));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(weights[0].Biome, Is.SameAs(fallback));
            Assert.That(weights[0].Weight, Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void SurfaceMaterialDistribution_UsesNormalizedFallbackWhenNoRulesMatch()
        {
            SurfaceMaterialDefinition fallback = Create<SurfaceMaterialDefinition>();
            SurfaceMaterialDistributionProfile distribution = Create<SurfaceMaterialDistributionProfile>();
            TestFieldAccess.SetField(distribution, "fallbackMaterial", fallback);
            PlanetGenerationContext context = PlanetGenerationContext.CreateDefault(31);
            PlanetClimateSample climate = CreateClimate(context);
            List<SurfaceMaterialWeight> weights = new();

            int count = distribution.SampleWeights(
                context,
                climate,
                null,
                Vector3.forward,
                0f,
                0f,
                weights);
            SurfaceMaterialSample dominant = distribution.SampleDominant(
                context,
                climate,
                null,
                Vector3.forward,
                0f,
                0f);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(weights[0].Material, Is.SameAs(fallback));
            Assert.That(weights[0].Weight, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(dominant.Material, Is.SameAs(fallback));
        }

        [Test]
        public void SurfaceWaterClouds_RequireAtmosphereOceanAndAvailableWater()
        {
            PlanetHydrosphereProfile hydrosphere = Create<PlanetHydrosphereProfile>();
            PlanetaryGenerationProfile generation = CreateGeneration(1f, hydrosphere);

            Assert.That(generation.SupportsSurfaceWaterClouds, Is.True);

            TestFieldAccess.SetField(hydrosphere, "hasSurfaceOcean", false);
            Assert.That(generation.SupportsSurfaceWaterClouds, Is.False);

            TestFieldAccess.SetField(hydrosphere, "hasSurfaceOcean", true);
            TestFieldAccess.SetField(hydrosphere, "waterAvailability", 0f);
            Assert.That(generation.SupportsSurfaceWaterClouds, Is.False);

            TestFieldAccess.SetField(hydrosphere, "waterAvailability", 0.4f);
            Assert.That(CreateGeneration(0f, hydrosphere).SupportsSurfaceWaterClouds, Is.False);
        }

        [Test]
        public void PlanetArchetype_DerivesTheSamePlanetForTheSameSeedAndDifferentPlanetsAcrossSeeds()
        {
            PlanetArchetype archetype = CreateArchetype(out BiomeDefinition gatedBiome);

            PlanetaryGenerationProfile first = archetype.Derive(11, 64f, 9.81f);
            PlanetaryGenerationProfile repeat = archetype.Derive(11, 64f, 9.81f);

            Assert.That(first.BiomeDistribution.Rules.Count, Is.InRange(2, 3));
            Assert.That(BiomeNames(first), Is.EqualTo(BiomeNames(repeat)));
            Assert.That(first.HydrosphereProfile.OceanLevel, Is.EqualTo(repeat.HydrosphereProfile.OceanLevel));
            Assert.That(
                first.ClimateProfile.ThermalProfile.DesignTemperatureBiasCelsius,
                Is.EqualTo(repeat.ClimateProfile.ThermalProfile.DesignTemperatureBiasCelsius));
            Assert.That(first.ShapeProfile, Is.Not.SameAs(archetype.ShapeTemplate));
            Assert.That(first.HydrosphereProfile.OceanLevel, Is.InRange(0.3f, 0.7f));

            bool hostsGatedBiome = false;
            foreach (BiomeDistributionRule rule in first.BiomeDistribution.Rules)
            {
                hostsGatedBiome |= rule.Biome == gatedBiome;
            }

            bool hasGatedMaterialRule = false;
            foreach (SurfaceMaterialDistributionRule rule in first.SurfaceMaterialDistribution.Rules)
            {
                hasGatedMaterialRule |= rule.Material.name == "gated";
            }

            Assert.That(hasGatedMaterialRule, Is.EqualTo(hostsGatedBiome));

            HashSet<string> biomeSets = new();
            HashSet<float> oceanLevels = new();
            for (int seed = 1; seed <= 24; seed++)
            {
                PlanetaryGenerationProfile derived = archetype.Derive(seed, 64f, 9.81f);
                biomeSets.Add(BiomeNames(derived));
                oceanLevels.Add(derived.HydrosphereProfile.OceanLevel);
                derived.Release();
            }

            Assert.That(biomeSets.Count, Is.GreaterThan(3));
            Assert.That(oceanLevels.Count, Is.GreaterThan(10));

            first.Release();
            repeat.Release();
        }

        [Test]
        public void SurfaceVisualProfile_VariantFollowsMaterialOrderAndPicksOneRulePerMaterial()
        {
            SurfaceMaterialDefinition rock = Create<SurfaceMaterialDefinition>();
            SurfaceMaterialDefinition sand = Create<SurfaceMaterialDefinition>();
            TestFieldAccess.SetField(rock, "materialId", "rock");
            TestFieldAccess.SetField(sand, "materialId", "sand");
            SurfaceVisualProfile library = Create<SurfaceVisualProfile>();
            TestFieldAccess.SetField(library, "rules", new List<SurfaceVisualRule>
            {
                CreateVisualRule(rock, Color.red),
                CreateVisualRule(rock, Color.green),
                CreateVisualRule(sand, Color.yellow)
            });

            SurfaceVisualProfile variant = library.CreateVariant(new[] { sand, rock }, 5, 0f, 0f);
            createdObjects.Add(variant);
            HashSet<Color> rockPicks = new();
            for (int seed = 1; seed <= 16; seed++)
            {
                SurfaceVisualProfile other = library.CreateVariant(new[] { rock }, seed, 0f, 0f);
                rockPicks.Add(other.Rules[0].FlatLow);
                Object.DestroyImmediate(other);
            }

            Assert.That(variant.Rules.Count, Is.EqualTo(2));
            Assert.That(variant.ResolveMaterialIndex(sand), Is.EqualTo(0));
            Assert.That(variant.ResolveMaterialIndex(rock), Is.EqualTo(1));
            Assert.That(rockPicks.Count, Is.EqualTo(2));
        }

        static string BiomeNames(PlanetaryGenerationProfile generation)
        {
            List<string> names = new();
            foreach (BiomeDistributionRule rule in generation.BiomeDistribution.Rules)
            {
                names.Add(rule.Biome.name);
            }

            return string.Join(",", names);
        }

        static SurfaceVisualRule CreateVisualRule(SurfaceMaterialDefinition material, Color color)
        {
            SurfaceVisualRule rule = new();
            TestFieldAccess.SetField(rule, "material", material);
            TestFieldAccess.SetField(rule, "flatLow", color);
            return rule;
        }

        PlanetArchetype CreateArchetype(out BiomeDefinition gatedBiome)
        {
            BiomeDistributionProfile biomes = Create<BiomeDistributionProfile>();
            TestFieldAccess.SetField(biomes, "fallbackBiome", Create<BiomeDefinition>());
            List<BiomeDistributionRule> biomeRules = new();
            BiomeDefinition[] definitions = new BiomeDefinition[5];
            for (int i = 0; i < definitions.Length; i++)
            {
                definitions[i] = Create<BiomeDefinition>();
                definitions[i].name = "biome" + i;
                BiomeDistributionRule rule = new();
                TestFieldAccess.SetField(rule, "biome", definitions[i]);
                biomeRules.Add(rule);
            }

            TestFieldAccess.SetField(biomes, "rules", biomeRules);
            gatedBiome = definitions[4];

            SurfaceMaterialDefinition fallback = Create<SurfaceMaterialDefinition>();
            SurfaceMaterialDefinition gated = Create<SurfaceMaterialDefinition>();
            gated.name = "gated";
            SurfaceMaterialDistributionProfile materials = Create<SurfaceMaterialDistributionProfile>();
            TestFieldAccess.SetField(materials, "fallbackMaterial", fallback);
            SurfaceMaterialDistributionRule open = new();
            TestFieldAccess.SetField(open, "material", fallback);
            SurfaceMaterialDistributionRule gatedRule = new();
            TestFieldAccess.SetField(gatedRule, "material", gated);
            TestFieldAccess.SetField(gatedRule, "allowedBiomes", new List<BiomeDefinition> { gatedBiome });
            TestFieldAccess.SetField(materials, "rules", new List<SurfaceMaterialDistributionRule> { open, gatedRule });

            PlanetClimateProfile climate = Create<PlanetClimateProfile>();
            TestFieldAccess.SetField(climate, "thermalProfile", Create<PlanetThermalProfile>());

            PlanetArchetype archetype = Create<PlanetArchetype>();
            TestFieldAccess.SetField(archetype, "shapeProfile", Create<ContinentRidgeShapeProfile>());
            TestFieldAccess.SetField(archetype, "climateProfile", climate);
            TestFieldAccess.SetField(archetype, "hydrosphereProfile", Create<PlanetHydrosphereProfile>());
            TestFieldAccess.SetField(archetype, "surfaceStateProfile", Create<PlanetSurfaceStateProfile>());
            TestFieldAccess.SetField(archetype, "biomeLibrary", biomes);
            TestFieldAccess.SetField(archetype, "surfaceMaterialLibrary", materials);
            TestFieldAccess.SetField(archetype, "oceanLevelRange", new Vector2(0.3f, 0.7f));
            TestFieldAccess.SetField(archetype, "temperatureBiasRange", new Vector2(-10f, 10f));
            TestFieldAccess.SetField(archetype, "biomeCountRange", new Vector2Int(2, 3));
            return archetype;
        }

        static PlanetaryGenerationProfile CreateGeneration(float atmosphereDensity, PlanetHydrosphereProfile hydrosphere)
        {
            return new PlanetaryGenerationProfile(
                "test",
                1,
                PlanetType.Rocky,
                atmosphereDensity,
                0.05f,
                0.3f,
                0f,
                null,
                null,
                hydrosphere,
                null,
                null,
                null,
                null);
        }

        static PlanetClimateSample CreateClimate(PlanetGenerationContext context)
        {
            return new PlanetClimateSample(
                context,
                Vector3.up,
                90f,
                0f,
                0f,
                15f,
                0.5f,
                0.4f,
                0.6f,
                0.1f);
        }

        [Test]
        public void TerrainSculpt_CratersCarveOnlyInsideTheFeatureNoiseWindow()
        {
            ContinentRidgeShapeProfile template = Create<ContinentRidgeShapeProfile>();
            CelestialShapeProfile plain = template.CreateVariant(41, 1f);
            CelestialShapeProfile carved = template.CreateVariant(41, 1f);
            CelestialShapeProfile windowed = template.CreateVariant(41, 1f);
            createdObjects.Add(plain);
            createdObjects.Add(carved);
            createdObjects.Add(windowed);

            TerrainSculptLayer everywhere = new(
                TerrainSculptStyle.Craters, 20f, 120f, new Vector2(0f, 1f), 0f, 7);
            TerrainSculptLayer nowhere = new(
                TerrainSculptStyle.Craters, 20f, 120f, new Vector2(2f, 3f), 0f, 7);
            carved.ApplyTerrainSculpts(new TerrainSculptSet(9, 2.5f, new[] { everywhere }));
            windowed.ApplyTerrainSculpts(new TerrainSculptSet(9, 2.5f, new[] { nowhere }));

            float largestDifference = 0f;
            float deepest = 0f;
            for (int i = 0; i < 4000; i++)
            {
                Vector3 direction = Quaternion.Euler(i * 7.1f, i * 13.7f, 0f) * Vector3.forward;
                float baseline = plain.EvaluateRadius(1600f, direction);
                float sculpted = carved.EvaluateRadius(1600f, direction);
                Assert.That(windowed.EvaluateRadius(1600f, direction), Is.EqualTo(baseline).Within(0.0001f));
                largestDifference = Mathf.Max(largestDifference, Mathf.Abs(sculpted - baseline));
                deepest = Mathf.Min(deepest, sculpted - baseline);
            }

            Assert.That(largestDifference, Is.GreaterThan(1f));
            Assert.That(deepest, Is.LessThan(-4f));
            Assert.That(largestDifference, Is.LessThanOrEqualTo(everywhere.AmplitudeMeters * 2.5f));
            Assert.That(carved.EstimatePeakElevationMeters(), Is.EqualTo(plain.EstimatePeakElevationMeters() + 20f).Within(0.001f));

            float farLod = carved.EvaluateRadius(1600f, Vector3.right, 1f);
            Assert.That(farLod, Is.EqualTo(plain.EvaluateRadius(1600f, Vector3.right, 1f)).Within(0.0001f));
        }

        [Test]
        public void TerrainSculpt_DistributionBuildsLayersOnlyForSculptedFeaturesHostedByThePlanet()
        {
            BiomeDefinition hosted = Create<BiomeDefinition>();
            BiomeDefinition foreign = Create<BiomeDefinition>();
            TerrainFeatureDefinition craters = Create<TerrainFeatureDefinition>();
            TestFieldAccess.SetField(craters, "sculptStyle", TerrainSculptStyle.Craters);
            TestFieldAccess.SetField(craters, "sculptAmplitudeMeters", 14f);
            TerrainFeatureDefinition label = Create<TerrainFeatureDefinition>();

            TerrainFeatureDistributionRule craterRule = new();
            TestFieldAccess.SetField(craterRule, "feature", craters);
            TestFieldAccess.SetField(craterRule, "allowedBiomes", new List<BiomeDefinition> { hosted });
            TestFieldAccess.SetField(craterRule, "minFeatureNoise", 0.6f);
            TerrainFeatureDistributionRule labelRule = new();
            TestFieldAccess.SetField(labelRule, "feature", label);

            TerrainFeatureDistributionProfile profile = Create<TerrainFeatureDistributionProfile>();
            TestFieldAccess.SetField(profile, "rules", new List<TerrainFeatureDistributionRule> { craterRule, labelRule });

            TerrainSculptSet forForeign = profile.BuildSculpts(5, new[] { foreign });
            Assert.That(forForeign.IsEmpty, Is.True);

            TerrainSculptSet forHosted = profile.BuildSculpts(5, new[] { hosted });
            Assert.That(forHosted.Layers.Count, Is.EqualTo(1));
            Assert.That(forHosted.Layers[0].Style, Is.EqualTo(TerrainSculptStyle.Craters));
            Assert.That(forHosted.Layers[0].AmplitudeMeters, Is.EqualTo(14f));
            Assert.That(forHosted.Layers[0].NoiseRange.x, Is.EqualTo(0.6f));
            Assert.That(forHosted.Seed, Is.EqualTo(SeedUtility.Derive(5, profile.SeedSalt, "terrain.feature")));
            Assert.That(profile.BuildSculpts(5, new[] { hosted }).Layers[0].Seed, Is.EqualTo(forHosted.Layers[0].Seed));
        }

        T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
        }

    }
}
