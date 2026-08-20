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
                if (rule.AllowsBiome(sample.Biome.Biome) &&
                    rule.EvaluateSuitability(sample, sample.Biome.Suitability, hasOcean, oceanRadius) > 0f)
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
