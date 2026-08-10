using System.Collections.Generic;
using Farion.Simulation.Planetary;
using Farion.Tests;
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

            BiomeSample sample = distribution.SampleBiome(context, climate, 0f, 0f);
            List<BiomeWeight> weights = new();
            int count = distribution.SampleBiomeWeights(context, climate, 0f, 0f, weights);

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
            PlanetaryGenerationProfile generation = Create<PlanetaryGenerationProfile>();
            PlanetHydrosphereProfile hydrosphere = Create<PlanetHydrosphereProfile>();
            TestFieldAccess.SetField(generation, "hydrosphereProfile", hydrosphere);

            Assert.That(generation.SupportsSurfaceWaterClouds, Is.True);

            TestFieldAccess.SetField(hydrosphere, "hasSurfaceOcean", false);
            Assert.That(generation.SupportsSurfaceWaterClouds, Is.False);

            TestFieldAccess.SetField(hydrosphere, "hasSurfaceOcean", true);
            TestFieldAccess.SetField(hydrosphere, "waterAvailability", 0f);
            Assert.That(generation.SupportsSurfaceWaterClouds, Is.False);

            TestFieldAccess.SetField(hydrosphere, "waterAvailability", 0.4f);
            TestFieldAccess.SetField(generation, "atmosphereDensity", 0f);
            Assert.That(generation.SupportsSurfaceWaterClouds, Is.False);
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

        T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
        }

    }
}
