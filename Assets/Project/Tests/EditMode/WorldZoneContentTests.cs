using System.Collections.Generic;
using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Tests.EditMode
{
    [Category("Content")]
    public sealed class WorldZoneContentTests
    {
        const string WorldZonePath = "Assets/Project/Scenes/SC_WorldZone.unity";

        Scene scene;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.OpenScene(WorldZonePath, OpenSceneMode.Additive);
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [TestCase("Ember")]
        [TestCase("Rime")]
        public void OrbitingPlanetsExposeAUsableTerrainCollider(string planetName)
        {
            GameObject planet = FindNamedObject(planetName);
            Assert.That(planet, Is.Not.Null);
            Assert.That(
                planet.GetComponent<CelestialBody>().MotionMode,
                Is.EqualTo(CelestialBodyMotionMode.KinematicOrbit));

            planet.GetComponent<CelestialBodyVisual>().Rebuild();
            Transform terrain = planet.transform.Find("Terrain Mesh");
            Assert.That(terrain, Is.Not.Null);
            MeshCollider collider = terrain.GetComponent<MeshCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.enabled, Is.True);
            Assert.That(collider.sharedMesh, Is.Not.Null);
        }

        [Test]
        public void EveryDecoratedBodyHasADecorationProfile()
        {
            HashSet<string> decoratedBodies = new();
            foreach (SurfaceDecorationRenderer candidate in
                Object.FindObjectsByType<SurfaceDecorationRenderer>(FindObjectsInactive.Include))
            {
                if (candidate.gameObject.scene != scene)
                {
                    continue;
                }

                Assert.That(candidate.Profile, Is.Not.Null, candidate.gameObject.name);
                decoratedBodies.Add(candidate.gameObject.name);
            }

            Assert.That(decoratedBodies, Is.Not.Empty);
        }

        [Test]
        public void StartingLandingAreaSupportsAtLeastOneDecorationRule()
        {
            SurfaceDecorationRenderer renderer = FindDecorationRenderer("Starting Planet");
            Assert.That(renderer, Is.Not.Null);

            PlanetSurfaceModel surfaceModel = renderer.GetComponent<PlanetSurfaceModel>();
            Assert.That(surfaceModel.TrySamplePlanetSurface(
                renderer.transform.InverseTransformDirection(Vector3.up),
                out PlanetSurfaceSample sample), Is.True);

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

            int suitableRuleCount = 0;
            foreach (SurfaceDecorationRule rule in renderer.Profile.Rules)
            {
                if (rule.Suitability.AllowsBiome(sample.Biome.Biome) &&
                    rule.Suitability.Evaluate(
                        sample,
                        sample.Biome.Suitability,
                        hasOcean,
                        oceanRadius) > 0f)
                {
                    suitableRuleCount++;
                }
            }

            Assert.That(
                suitableRuleCount,
                Is.GreaterThan(0),
                "The starting landing area must support at least one decoration rule. "
                + "Rules tuned for other climates may legitimately score zero here.");
        }

        [Test]
        public void StartingPlanetFormationPiecesSeatBelowTheAnalyticSurface()
        {
            SurfaceDecorationRenderer renderer = FindDecorationRenderer("Starting Planet");
            Assert.That(renderer, Is.Not.Null);
            SurfaceFormationSpawner spawner = renderer.GetComponent<SurfaceFormationSpawner>();
            Assert.That(spawner, Is.Not.Null);
            PlanetSurfaceModel surfaceModel = renderer.GetComponent<PlanetSurfaceModel>();
            CelestialBody body = renderer.GetComponent<CelestialBody>();
            int planetSeed = surfaceModel.CreateContext(body).PlanetSeed;

            List<SurfaceFormationPlacementItem> items = new();
            int seated = 0;
            foreach (SurfaceFormationRule rule in spawner.Profile.Rules)
            {
                if (!rule.Kit.HasOutcrop)
                {
                    continue;
                }

                int resolution = SurfaceScatterPlacement.CalculateResolution(
                    body.Radius,
                    rule.Distribution.SpacingMeters);
                for (int i = 0; i < 24 && seated < 40; i++)
                {
                    Vector3 direction = Quaternion.Euler(i * 37f, i * 91f, 0f) * Vector3.up;
                    SurfaceScatterCell cell = SurfaceScatterPlacement.CellFromDirection(direction, resolution);
                    direction = SurfaceScatterPlacement.CandidateDirection(cell, planetSeed, rule.StableId);
                    surfaceModel.TrySampleGeology(direction, out CelestialGeologySample geology);
                    SurfaceFormationPlacement.BuildComposition(
                        rule,
                        surfaceModel,
                        renderer.transform,
                        direction,
                        geology,
                        cell,
                        planetSeed,
                        items);
                    foreach (SurfaceFormationPlacementItem item in items)
                    {
                        Vector3 itemDirection = item.SurfaceLocalPosition.normalized;
                        Assert.That(surfaceModel.TrySampleLocalRadius(itemDirection, out float analytic), Is.True);
                        Assert.That(item.SurfaceLocalPosition.magnitude, Is.EqualTo(analytic).Within(0.01f));
                        Assert.That(
                            Vector3.Dot(item.LocalPosition - item.SurfaceLocalPosition, item.LocalUp),
                            Is.LessThan(0f));
                        seated++;
                    }
                }
            }

            Assert.That(seated, Is.GreaterThan(0));
        }

        SurfaceDecorationRenderer FindDecorationRenderer(string bodyName)
        {
            foreach (SurfaceDecorationRenderer candidate in
                Object.FindObjectsByType<SurfaceDecorationRenderer>(FindObjectsInactive.Include))
            {
                if (candidate.gameObject.scene == scene && candidate.gameObject.name == bodyName)
                {
                    return candidate;
                }
            }

            return null;
        }

        GameObject FindNamedObject(string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName)
                    {
                        return child.gameObject;
                    }
                }
            }

            return null;
        }
    }
}
