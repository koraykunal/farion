using Farion.Rendering.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Farion.Tests.EditMode
{
    public sealed class SurfaceDecorationTests
    {
        const string ProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_TerrestrialSurfaceDecoration.asset";

        [Test]
        public void SurfaceCellPlacement_IsDeterministicAndSeeded()
        {
            int resolution = SurfaceDecorationPlacement.CalculateResolution(1600f, 11f);
            SurfaceDecorationCell cell = SurfaceDecorationPlacement.CellFromDirection(
                new Vector3(0.63f, 0.71f, -0.31f),
                resolution);

            Vector3 first = SurfaceDecorationPlacement.CandidateDirection(cell, 4065, "surfaceflora.birch");
            Vector3 repeated = SurfaceDecorationPlacement.CandidateDirection(cell, 4065, "surfaceflora.birch");
            Vector3 otherSeed = SurfaceDecorationPlacement.CandidateDirection(cell, 4066, "surfaceflora.birch");

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(Vector3.Distance(first, otherSeed), Is.GreaterThan(0.000001f));
            Assert.That(first.magnitude, Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void PlacementUp_BlendsFromLocalGravityToTerrainNormal()
        {
            Vector3 radialUp = Vector3.right;
            Vector3 surfaceNormal = new Vector3(1f, 1f, 0f).normalized;

            Assert.That(
                SurfaceDecorationPlacement.ResolvePlacementUp(radialUp, surfaceNormal, 0f),
                Is.EqualTo(radialUp).Using(Vector3EqualityComparer.Instance));
            Assert.That(
                SurfaceDecorationPlacement.ResolvePlacementUp(radialUp, surfaceNormal, 1f),
                Is.EqualTo(surfaceNormal).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void PlacementSuspendsAboveSpeedAndAltitudeThresholds()
        {
            SurfaceDecorationProfile profile = LoadProfile();

            Assert.That(SurfaceDecorationRenderer.ShouldSuspendPlacement(0f, 0f, profile), Is.False);
            Assert.That(
                SurfaceDecorationRenderer.ShouldSuspendPlacement(profile.PlacementPauseSpeed, 0f, profile),
                Is.True);
            Assert.That(
                SurfaceDecorationRenderer.ShouldSuspendPlacement(0f, profile.PlacementPauseAltitude, profile),
                Is.True);
        }

        [Test]
        [Category("Content")]
        public void TerrestrialProfile_HasUniqueRuleIdsAndInstancedVariants()
        {
            SurfaceDecorationProfile profile = LoadProfile();
            System.Collections.Generic.HashSet<string> ids = new();

            foreach (SurfaceDecorationRule rule in profile.Rules)
            {
                Assert.That(rule, Is.Not.Null);
                Assert.That(ids.Add(rule.StableId), Is.True, $"Duplicate rule id '{rule.StableId}'.");
                Assert.That(rule.Variants.Count, Is.GreaterThan(0));
                foreach (SurfaceDecorationVariant variant in rule.Variants)
                {
                    Assert.That(variant.NearMesh, Is.Not.Null);
                    Assert.That(variant.FarMesh, Is.Not.Null);
                    Assert.That(variant.Materials.Count, Is.GreaterThan(0));
                    foreach (Material material in variant.Materials)
                    {
                        Assert.That(material, Is.Not.Null);
                        Assert.That(material.enableInstancing, Is.True);
                    }
                }
            }
        }

        [Test]
        public void TemperateBirchSuitability_RejectsOceanCoveredSurface()
        {
            SurfaceDecorationRule birch = LoadProfile().Rules[0];
            GameObject owner = new("Surface Decoration Suitability Test");
            try
            {
                CelestialBody body = owner.AddComponent<CelestialBody>();
                PlanetGenerationContext context = new(
                    4065,
                    1600f,
                    9.81f,
                    PlanetType.Rocky,
                    1f,
                    0.05f);
                PlanetClimateSample climate = new(
                    context,
                    Vector3.up,
                    90f,
                    10f,
                    10f,
                    15f,
                    0.7f,
                    0.7f,
                    0.3f,
                    0.1f);
                CelestialSurfaceSample surface = new(
                    body,
                    Vector3.up * 1610f,
                    Vector3.up,
                    1610f,
                    0f,
                    10f);
                PlanetSurfaceSample sample = new(
                    context,
                    surface,
                    Vector3.up,
                    1610f,
                    climate,
                    new BiomeSample(null, 1f),
                    default,
                    new PlanetSurfaceStateSample(0f, 0f, 0.7f),
                    default);

                Assert.That(birch.EvaluateSuitability(sample, 1f, false, 0f), Is.GreaterThan(0f));
                Assert.That(birch.EvaluateSuitability(sample, 1f, true, 1610f), Is.EqualTo(0f));
                Assert.That(birch.EvaluateSuitability(sample, 0f, false, 0f), Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        static SurfaceDecorationProfile LoadProfile()
        {
            SurfaceDecorationProfile profile =
                AssetDatabase.LoadAssetAtPath<SurfaceDecorationProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null, ProfilePath);
            return profile;
        }
    }
}
