using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.World;
using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Tests.Support;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Farion.Tests.PlayMode
{
    public sealed class MultiplayerSessionPlayModeTests
    {
        GameObject testRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MultiplayerSessionController.Active?.Stop();
            yield return null;
            if (testRoot != null)
            {
                UnityEngine.Object.Destroy(testRoot);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SessionFactoryReusesTheActiveRootAndIdleStopClearsActive()
        {
            Assert.That(
                MultiplayerSessionLauncher.TryCreateSession(
                    out MultiplayerSessionController first),
                Is.True);
            Assert.That(
                MultiplayerSessionLauncher.TryCreateSession(
                    out MultiplayerSessionController second),
                Is.True);
            Assert.That(second, Is.SameAs(first));

            first.Stop();
            yield return null;

            Assert.That(MultiplayerSessionController.Active, Is.Null);
        }

        [UnityTest]
        public IEnumerator TransportConnectionDoesNotReportReadyBeforeOwnedPlayer()
        {
            Assert.That(
                MultiplayerSessionLauncher.TryCreateSession(
                    out MultiplayerSessionController session),
                Is.True);
            Invoke(
                session,
                "SelectClientTransport",
                MultiplayerTransportKind.Direct);
            Invoke(session, "SetState", MultiplayerSessionState.Starting);

            Invoke(
                session,
                "OnClientConnectionState",
                new ClientConnectionStateArgs(
                    LocalConnectionState.Started,
                    transportIndex: 0));
            Assert.That(
                session.State,
                Is.EqualTo(MultiplayerSessionState.Starting));

            Invoke(session, "NotifyOwnedPlayerReady");
            Assert.That(
                session.State,
                Is.EqualTo(MultiplayerSessionState.Starting));

            Invoke(session, "NotifyAssignedStarterShuttleReady");
            Assert.That(
                session.State,
                Is.EqualTo(MultiplayerSessionState.Connected));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrozenMultiplayerSimulationKeepsReferenceBodyStationary()
        {
            testRoot = new GameObject("MultiplayerReferenceFrameTest");
            testRoot.SetActive(false);

            CelestialBody body = new GameObject("ReferenceBody")
                .AddComponent<CelestialBody>();
            body.transform.SetParent(testRoot.transform);
            TestFieldAccess.SetField(body, "initialVelocity", new Vector3(0f, 0f, 28.46f));
            TestFieldAccess.SetField(body, "motionMode", CelestialBodyMotionMode.KinematicOrbit);

            GravitySimulation simulation =
                testRoot.AddComponent<GravitySimulation>();
            TestFieldAccess.SetField(simulation, "physicsReferenceBody", body);
            TestFieldAccess.SetField(
                simulation,
                "registeredBodies",
                new List<CelestialBody> { body });

            GameplayRuntimeRoot runtimeRoot =
                testRoot.AddComponent<GameplayRuntimeRoot>();
            MultiplayerSceneContext context =
                testRoot.AddComponent<MultiplayerSceneContext>();
            TestFieldAccess.SetField(context, "runtimeRoot", runtimeRoot);
            TestFieldAccess.SetField(context, "gravitySimulation", simulation);

            testRoot.SetActive(true);
            yield return null;

            Assert.That(simulation.IntegrationEnabled, Is.False);
            Assert.That(
                simulation.ReferenceFrameVelocity.z,
                Is.EqualTo(28.46f).Within(0.001f));
            Assert.That(body.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void BoundZoneContextDoesNotRequireGameplayRuntimeRoot()
        {
            testRoot = new GameObject("MultiplayerZoneContextTest");
            GravitySimulation simulation =
                testRoot.AddComponent<GravitySimulation>();
            MultiplayerSceneContext context =
                testRoot.AddComponent<MultiplayerSceneContext>();
            TestFieldAccess.SetField(context, "gravitySimulation", simulation);

            context.BindSession(
                null,
                null,
                MultiplayerZoneCatalog.StartingZoneId);

            Assert.That(simulation.IntegrationEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator NetworkPlayerAttachesToShiftedRootWithoutLargeLocalOffset()
        {
            testRoot = new GameObject("Actors");
            testRoot.transform.position = new Vector3(262666f, 0f, -124825f);

            GameObject contextObject = new("Multiplayer");
            contextObject.transform.SetParent(testRoot.transform, false);
            contextObject.transform.localPosition =
                new Vector3(-262666f, 0f, 124825f);
            MultiplayerSceneContext context =
                contextObject.AddComponent<MultiplayerSceneContext>();

            Transform player = new GameObject("NetworkPlayer").transform;
            player.position = new Vector3(264822f, 5.7f, -124824.9f);
            context.AttachToShiftedWorld(player);

            Assert.That(player.parent, Is.SameAs(testRoot.transform));
            Assert.That(player.localPosition.magnitude, Is.LessThan(3000f));

            UnityEngine.Object.Destroy(player.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SurfacePatchCollisionObserverCanBeRebound()
        {
            testRoot = new GameObject("SurfacePatchObserverTest");
            CelestialSurfacePatchSystem patchSystem =
                testRoot.AddComponent<CelestialSurfacePatchSystem>();
            Rigidbody body = new GameObject("Observer").AddComponent<Rigidbody>();
            body.transform.SetParent(testRoot.transform);
            TestSurfaceCollisionObserver observer =
                body.gameObject.AddComponent<TestSurfaceCollisionObserver>();

            patchSystem.SetCollisionObserverSource(observer);
            yield return null;

            Assert.That(patchSystem.CollisionObserverRigidbody, Is.SameAs(body));
        }

        [UnityTest]
        public IEnumerator MultiplayerSpawnFindsDryLand()
        {
            testRoot = new GameObject("MultiplayerOceanSpawnTest");
            testRoot.SetActive(false);

            CelestialBody body = new GameObject("OceanBody")
                .AddComponent<CelestialBody>();
            body.transform.SetParent(testRoot.transform);
            TestFieldAccess.SetField(body, "radius", 90f);

            TestOceanEnvironment environment =
                body.gameObject.AddComponent<TestOceanEnvironment>();
            environment.Configure(body, 100f);
            TestLandSurface surface =
                body.gameObject.AddComponent<TestLandSurface>();
            surface.Configure(body, 90f, 110f);

            GravitySimulation simulation =
                testRoot.AddComponent<GravitySimulation>();
            TestFieldAccess.SetField(
                simulation,
                "registeredBodies",
                new List<CelestialBody> { body });

            CelestialFrameProvider frameProvider =
                testRoot.AddComponent<CelestialFrameProvider>();
            TestFieldAccess.SetField(frameProvider, "simulation", simulation);
            TestFieldAccess.SetField(
                frameProvider,
                "environmentSources",
                new List<MonoBehaviour> { environment });
            TestFieldAccess.SetField(
                frameProvider,
                "surfaceSources",
                new List<MonoBehaviour> { surface });

            MultiplayerSceneContext context =
                testRoot.AddComponent<MultiplayerSceneContext>();
            Transform spawnPoint = new GameObject("Spawn_1").transform;
            spawnPoint.SetParent(testRoot.transform);
            spawnPoint.position = Vector3.up * 120f;
            TestFieldAccess.SetField(context, "celestialFrameProvider", frameProvider);
            TestFieldAccess.SetField(context, "spawnPoints", new[] { spawnPoint });

            testRoot.SetActive(true);
            yield return null;

            Assert.That(
                context.TryGetSpawnPose(0, out Vector3 position, out _),
                Is.True);
            float clearance = TestFieldAccess.GetField<float>(context, "surfaceClearance");
            Assert.That(position.x, Is.GreaterThan(0f));
            Assert.That(position.magnitude, Is.GreaterThanOrEqualTo(110f + clearance));
        }


        static void Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, args);
        }
    }

    public sealed class TestOceanEnvironment :
        MonoBehaviour,
        ICelestialEnvironmentProvider
    {
        CelestialBody body;
        float oceanRadius;

        public void Configure(CelestialBody value, float radius)
        {
            body = value;
            oceanRadius = radius;
        }

        public bool TryGetEnvironment(
            CelestialBody candidate,
            out CelestialEnvironmentSample sample)
        {
            float bodyRadius = candidate != null ? candidate.Radius : 0f;
            sample = new CelestialEnvironmentSample(
                candidate,
                candidate == body,
                oceanRadius,
                false,
                0f,
                new Vector2(bodyRadius, bodyRadius),
                oceanRadius > 0f ? oceanRadius : bodyRadius);
            return candidate == body;
        }
    }

    public sealed class TestLandSurface :
        MonoBehaviour,
        ICelestialSurfaceProvider
    {
        CelestialBody body;
        float underwaterRadius;
        float landRadius;

        public void Configure(
            CelestialBody value,
            float waterTerrainRadius,
            float dryTerrainRadius)
        {
            body = value;
            underwaterRadius = waterTerrainRadius;
            landRadius = dryTerrainRadius;
        }

        public bool TrySampleSurface(
            CelestialBody candidate,
            Vector3 position,
            out CelestialSurfaceSample sample)
        {
            if (candidate != body)
            {
                sample = CelestialSurfaceSample.Empty;
                return false;
            }

            Vector3 offset = position - body.Position;
            Vector3 normal = offset.sqrMagnitude > 0.0001f
                ? offset.normalized
                : Vector3.up;
            float radius = normal.x > 0f ? landRadius : underwaterRadius;
            sample = new CelestialSurfaceSample(
                body,
                body.Position + normal * radius,
                normal,
                offset.magnitude,
                offset.magnitude - radius);
            return true;
        }
    }

    public sealed class TestSurfaceCollisionObserver :
        MonoBehaviour,
        ICelestialSurfaceCollisionObserver
    {
        public bool TryGetSurfaceCollisionObserver(
            out CelestialSurfaceCollisionObserverState observer)
        {
            observer = new CelestialSurfaceCollisionObserverState(
                GetComponent<Rigidbody>());
            return observer.IsValid;
        }
    }
}
