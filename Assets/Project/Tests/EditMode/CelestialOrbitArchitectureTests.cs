using Farion.Rendering.Celestial;
using Farion.Simulation.Physics;
using Farion.Tests.Support;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Tests.EditMode
{
    public sealed class CelestialOrbitArchitectureTests
    {
        const string StartingZonePath = "Assets/Project/Scenes/SC_WorldZone.unity";

        [Test]
        public void KinematicOrbitUsesOnlyItsAssignedAttractor()
        {
            GameObject root = new("Orbit Test");
            try
            {
                CelestialBody star = CreateBody(root.transform, "Star", Vector3.zero, 1200f, 350f);
                CelestialBody planet = CreateBody(root.transform, "Planet", Vector3.right * 60000f, 1600f, 9.81f);
                CelestialBody perturbingBody = CreateBody(root.transform, "Perturber", Vector3.right * 65000f, 800f, 5f);
                TestFieldAccess.SetField(planet, "motionMode", CelestialBodyMotionMode.KinematicOrbit);
                planet.SetOrbitAttractor(star);

                GravitySimulation simulation = root.AddComponent<GravitySimulation>();
                Vector3 expected = simulation.CalculateAccelerationFromBody(planet.Position, star);
                Vector3 fullNBody = expected +
                    simulation.CalculateAccelerationFromBody(planet.Position, perturbingBody);
                Vector3 actual = simulation.CalculateOrbitalAcceleration(planet);

                Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f));
                Assert.That(Vector3.Distance(actual, fullNBody), Is.GreaterThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("Ember")]
        [TestCase("Rime")]
        public void StartingPlanetHasUsableTerrainCollider(string planetName)
        {
            Scene scene = EditorSceneManager.OpenScene(StartingZonePath, OpenSceneMode.Additive);
            try
            {
                GameObject planet = FindNamedObject(scene, planetName);
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
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        static CelestialBody CreateBody(
            Transform parent,
            string objectName,
            Vector3 position,
            float radius,
            float surfaceGravity)
        {
            GameObject owner = new(objectName);
            owner.transform.SetParent(parent);
            owner.transform.position = position;
            CelestialBody body = owner.AddComponent<CelestialBody>();
            TestFieldAccess.SetField(body, "radius", radius);
            TestFieldAccess.SetField(body, "surfaceGravity", surfaceGravity);
            body.RecalculateMass(GravitySimulation.DefaultGravitationalConstant);
            body.Rigidbody.position = position;
            return body;
        }


        static GameObject FindNamedObject(Scene scene, string objectName)
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
