using Farion.Core.Identity;
using System.Collections;
using Farion.Gameplay.Character;
using Farion.Multiplayer.World;
using Farion.Simulation.World;
using Farion.Tests.Support;
using FishNet.Managing.Scened;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Farion.Tests.PlayMode
{
    public sealed class NetworkZoneFoundationPlayModeTests
    {
        GameObject driverObject;
        Scene firstScene;
        Scene secondScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (driverObject != null)
            {
                Object.Destroy(driverObject);
            }

            if (firstScene.IsValid() && firstScene.isLoaded)
            {
                yield return UnitySceneManager.UnloadSceneAsync(firstScene);
            }

            if (secondScene.IsValid() && secondScene.isLoaded)
            {
                yield return UnitySceneManager.UnloadSceneAsync(secondScene);
            }
        }

        [UnityTest]
        public IEnumerator TwoZonePhysicsScenesAdvanceIndependently()
        {
            driverObject = new GameObject("ZonePhysicsTickDriverTest");
            driverObject.SetActive(false);
            ZonePhysicsTickDriver driver =
                driverObject.AddComponent<ZonePhysicsTickDriver>();

            firstScene = CreatePhysicsScene("FarionZoneA");
            secondScene = CreatePhysicsScene("FarionZoneB");
            SimulationZoneContext firstContext = CreateZone(
                firstScene,
                101UL,
                Vector3.right,
                out Rigidbody firstBody);
            SimulationZoneContext secondContext = CreateZone(
                secondScene,
                202UL,
                Vector3.forward * 2f,
                out Rigidbody secondBody);

            Assert.That(driver.RegisterZone(firstContext), Is.True);
            Assert.That(driver.RegisterZone(secondContext), Is.True);
            Assert.That(driver.RegisterZone(firstContext), Is.False);
            Assert.That(driver.RegisteredZoneCount, Is.EqualTo(2));

            int simulated = driver.SimulateRegisteredZones(0.1f);

            Assert.That(simulated, Is.EqualTo(2));
            Assert.That(firstBody.position.x, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(firstBody.position.z, Is.Zero.Within(0.001f));
            Assert.That(secondBody.position.x, Is.Zero.Within(0.001f));
            Assert.That(secondBody.position.z, Is.EqualTo(0.2f).Within(0.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefaultPhysicsSceneCannotBecomeAZone()
        {
            driverObject = new GameObject("DefaultPhysicsZoneTest");
            driverObject.SetActive(false);
            ZonePhysicsTickDriver driver =
                driverObject.AddComponent<ZonePhysicsTickDriver>();
            SimulationZoneContext context =
                new GameObject("DefaultZone").AddComponent<SimulationZoneContext>();
            context.Configure(new GeneratedEntityId(303UL));

            Assert.That(driver.RegisterZone(context), Is.False);
            Assert.That(driver.RegisteredZoneCount, Is.Zero);

            Object.Destroy(context.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IsolatedZoneGroundProbeUsesLocalPhysicsScene()
        {
            firstScene = CreatePhysicsScene("FarionGroundProbeZone");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnitySceneManager.MoveGameObjectToScene(floor, firstScene);
            floor.transform.position = Vector3.down * 0.5f;
            floor.transform.localScale = new Vector3(10f, 1f, 10f);

            GameObject actor = new("GroundProbeActor");
            UnitySceneManager.MoveGameObjectToScene(actor, firstScene);
            ExplorerMotor motor = actor.AddComponent<ExplorerMotor>();
            CapsuleCollider capsule = actor.GetComponent<CapsuleCollider>();
            actor.transform.position = Vector3.up *
                Mathf.Max(capsule.height * 0.5f, capsule.radius);
            ExplorerMotorProfile profile =
                ScriptableObject.CreateInstance<ExplorerMotorProfile>();
            TestFieldAccess.SetField(motor, "profile", profile);
            Rigidbody body = actor.GetComponent<Rigidbody>();
            body.useGravity = false;

            yield return null;
            Physics.SyncTransforms();
            motor.Simulate(
                ExplorerMotorInput.None,
                0.01f,
                new RigidbodyExplorerPhysicsBody(body));

            Assert.That(motor.Grounded, Is.True);
            Assert.That(motor.WalkableGround, Is.True);
            Object.Destroy(profile);
        }

        static Scene CreatePhysicsScene(string prefix)
        {
            return UnitySceneManager.CreateScene(
                $"{prefix}_{Time.frameCount}_{Random.Range(1, int.MaxValue)}",
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        }

        static SimulationZoneContext CreateZone(
            Scene scene,
            ulong zoneId,
            Vector3 velocity,
            out Rigidbody body)
        {
            GameObject root = new($"Zone_{zoneId}");
            UnitySceneManager.MoveGameObjectToScene(root, scene);
            SimulationZoneContext context =
                root.AddComponent<SimulationZoneContext>();
            context.Configure(new GeneratedEntityId(zoneId));

            body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = velocity;
            return context;
        }
    }
}
