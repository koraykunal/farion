using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Tests.EditMode
{
    public sealed class SurfaceDecorationTests
    {
        const string ProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_SurfaceDecoration_StartingTemperate.asset";

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
        public void StartingTemperateProfile_HasValidInstancedVariantsAndUniqueRuleIds()
        {
            SurfaceDecorationProfile profile =
                AssetDatabase.LoadAssetAtPath<SurfaceDecorationProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.Rules.Count, Is.EqualTo(3));
            Assert.That(profile.MaximumCandidateEvaluationsPerFrame, Is.LessThanOrEqualTo(64));
            Assert.That(profile.CandidateEvaluationBudgetMilliseconds, Is.LessThanOrEqualTo(1f));
            Assert.That(SurfaceDecorationRenderer.ShouldSuspendPlacement(0f, 0f, profile), Is.False);
            Assert.That(
                SurfaceDecorationRenderer.ShouldSuspendPlacement(profile.PlacementPauseSpeed, 0f, profile),
                Is.True);
            Assert.That(
                SurfaceDecorationRenderer.ShouldSuspendPlacement(0f, profile.PlacementPauseAltitude, profile),
                Is.True);

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

                    if (rule.StableId == "surfaceflora.birch")
                    {
                        Assert.That(variant.NearMesh.subMeshCount, Is.EqualTo(2));
                        Assert.That(variant.NearMesh.GetIndexCount(1), Is.GreaterThan(1000));
                        Assert.That(variant.FarMesh.GetIndexCount(1), Is.GreaterThan(1000));
                        Assert.That(variant.Materials.Count, Is.EqualTo(2));
                        Assert.That(variant.Materials[0], Is.Not.SameAs(variant.Materials[1]));
                        Assert.That(
                            variant.Materials[1].shader.name,
                            Is.EqualTo("Universal Render Pipeline/Lit"));
                        Assert.That(variant.Materials[1].GetFloat("_Cull"), Is.EqualTo(2f));
                    }
                }
            }
        }

        [Test]
        public void TemperateBirchSuitability_RejectsOceanCoveredSurface()
        {
            SurfaceDecorationProfile profile =
                AssetDatabase.LoadAssetAtPath<SurfaceDecorationProfile>(ProfilePath);
            SurfaceDecorationRule birch = profile.Rules[0];
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

        [TestCase("Assets/Project/Scenes/SC_WorldZone.unity")]
        public void TargetScene_HasConfiguredVisualOnlySurfaceDecorationRenderer(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                SurfaceDecorationRenderer[] renderers =
                    Object.FindObjectsByType<SurfaceDecorationRenderer>(FindObjectsInactive.Include);
                SurfaceDecorationRenderer renderer = null;
                foreach (SurfaceDecorationRenderer candidate in renderers)
                {
                    if (candidate.gameObject.scene == scene)
                    {
                        renderer = candidate;
                        break;
                    }
                }

                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.Profile, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void StartingWorldLandingArea_SupportsTemperateDecoration()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/Project/Scenes/SC_WorldZone.unity",
                OpenSceneMode.Additive);
            try
            {
                SurfaceDecorationRenderer renderer = null;
                foreach (SurfaceDecorationRenderer candidate in
                    Object.FindObjectsByType<SurfaceDecorationRenderer>(FindObjectsInactive.Include))
                {
                    if (candidate.gameObject.scene == scene && candidate.gameObject.name == "Starting Planet")
                    {
                        renderer = candidate;
                        break;
                    }
                }

                Assert.That(renderer, Is.Not.Null);
                PlanetSurfaceModel surfaceModel = renderer.GetComponent<PlanetSurfaceModel>();
                Assert.That(surfaceModel.TrySamplePlanetSurface(
                    renderer.transform.InverseTransformDirection(Vector3.up),
                    out PlanetSurfaceSample sample), Is.True);
                Assert.That(sample.Biome.Biome.name, Is.EqualTo("SO_Biome_Temperate"));
                Assert.That(sample.Climate.TemperatureCelsius, Is.InRange(-3f, 24f));
                Assert.That(sample.Climate.EffectiveMoisture, Is.GreaterThanOrEqualTo(0.65f));

                bool hasOcean = false;
                float oceanRadius = 0f;
                foreach (MonoBehaviour behaviour in renderer.GetComponents<MonoBehaviour>())
                {
                    if (behaviour is ICelestialEnvironmentProvider provider &&
                        provider.TryGetEnvironment(sample.Surface.Body, out CelestialEnvironmentSample environment))
                    {
                        hasOcean = environment.HasOcean;
                        oceanRadius = environment.OceanRadius;
                        break;
                    }
                }

                foreach (SurfaceDecorationRule rule in renderer.Profile.Rules)
                {
                    Assert.That(
                        rule.EvaluateSuitability(sample, sample.Biome.Suitability, hasOcean, oceanRadius),
                        Is.GreaterThan(0f),
                        rule.DisplayName);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
