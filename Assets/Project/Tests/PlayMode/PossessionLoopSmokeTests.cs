using Farion.Core.Identity;
using System.Reflection;
using System.Collections;
using Farion.App.Flow;
using Farion.Core.Persistence;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
using Farion.UI.Feedback;
using Farion.UI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Farion.Tests.PlayMode
{
    public sealed class PossessionLoopSmokeTests
    {
        const string UnloadWorkflowSlot = "playmode-unload-workflow";

        [UnityTest]
        public IEnumerator GameplayShellSupportsExitAndReenterPilotLoop()
        {
            yield return LoadGameplayShell();

            PlayerPossessionController controller =
                Object.FindAnyObjectByType<PlayerPossessionController>();
            Assert.That(controller, Is.Not.Null);

            SpacecraftMotor motor = controller.SpacecraftMotor;
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.FlightProfile, Is.Not.Null);
            Assert.That(
                motor.Rigidbody.mass,
                Is.EqualTo(motor.FlightProfile.RigidbodyMass).Within(0.01f));
            Assert.That(motor.Telemetry.BoostCharge, Is.InRange(0f, 1f));

            SpacecraftLandingGearAnimator landingGear =
                motor.GetComponent<SpacecraftLandingGearAnimator>();
            Assert.That(landingGear, Is.Not.Null);
            Assert.That(landingGear.IsDeployed, Is.True);

            AssertLandingContact(motor, "COL_Landing_Front");
            AssertLandingContact(motor, "COL_Landing_Left");
            AssertLandingContact(motor, "COL_Landing_Right");

            SpacecraftCameraRig cameraRig = Object.FindAnyObjectByType<SpacecraftCameraRig>();
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(
                Object.FindAnyObjectByType<UiSpacecraftFlightHudPresenter>(),
                Is.Not.Null);

            PilotSeatInteractable pilotSeat =
                Object.FindAnyObjectByType<PilotSeatInteractable>(
                    FindObjectsInactive.Include);
            VehicleBoardingPoint boardingPoint =
                Object.FindAnyObjectByType<VehicleBoardingPoint>(
                    FindObjectsInactive.Include);
            Assert.That(pilotSeat, Is.Not.Null);
            Assert.That(boardingPoint, Is.Not.Null);
            Assert.That(pilotSeat.IsBound, Is.True);
            Assert.That(boardingPoint.IsBound, Is.True);

            Assert.That(controller.IsPilotingSpacecraft, Is.True);
            Assert.That(controller.ExitPilotSeat(), Is.True);
            Assert.That(controller.IsInShipInterior, Is.True);
            Assert.That(controller.EnterPilotSeat(), Is.True);
            Assert.That(controller.IsPilotingSpacecraft, Is.True);
        }

        [UnityTest]
        public IEnumerator GameplayShellCompletesHarvestUnloadAndSaveLoadWorkflow()
        {
            SaveGameFileService.DeleteSlot(UnloadWorkflowSlot);
            SaveGameStartupRequest.RequestNewGame();

            try
            {
                yield return LoadGameplayShell();

                GameplaySessionController controller =
                    Object.FindAnyObjectByType<GameplaySessionController>();
                Assert.That(controller, Is.Not.Null);
                GameplaySessionRuntime runtime = controller.Runtime;
                Assert.That(runtime, Is.Not.Null);
                Assert.That(controller.Commands, Is.Not.Null);

                ResourceNodeInteractable resource = FindAddressableResource();
                if (resource == null)
                {
                    yield return StreamNearestDeposit();
                    resource = FindAddressableResource();
                }

                Assert.That(resource, Is.Not.Null);
                Assert.That(resource.DepositId.IsValid, Is.True);
                Assert.That(resource.Definition, Is.Not.Null);
                Assert.That(resource.Definition.YieldedItem, Is.Not.Null);
                var item = resource.Definition.YieldedItem;

                Assert.That(
                    controller.Commands.CanHarvest(
                        new ResourceHarvestRequest(
                            resource.DepositId,
                            PersistentEntityId.New(),
                            runtime.LocalInventory.Revision)),
                    Is.EqualTo(ResourceHarvestResult.UnauthorizedDestination));

                Assert.That(
                    controller.Commands.CanHarvest(
                        new ResourceHarvestRequest(
                            resource.DepositId,
                            runtime.LocalInventory.ContainerId,
                            runtime.LocalInventory.Revision + 1)),
                    Is.EqualTo(ResourceHarvestResult.StaleDestination));

                Assert.That(
                    controller.Commands.CanHarvest(
                        new ResourceHarvestRequest(
                            GeneratedEntityId.None,
                            runtime.LocalInventory.ContainerId,
                            runtime.LocalInventory.Revision)),
                    Is.EqualTo(ResourceHarvestResult.MissingSource));

                Assert.That(
                    controller.TryLoadAssignedShuttleCargo(
                        new CargoTransferRequest(
                            PersistentEntityId.New(),
                            runtime.ShuttleBinding.Cargo.Revision)),
                    Is.EqualTo(CargoTransferResult.UnauthorizedDestination));

                Assert.That(
                    controller.Commands.TryHarvest(
                        new ResourceHarvestRequest(
                            resource.DepositId,
                            runtime.LocalInventory.ContainerId,
                            runtime.LocalInventory.Revision)),
                    Is.EqualTo(ResourceHarvestResult.Succeeded));
                int harvested = runtime.LocalInventory.Count(item);
                Assert.That(harvested, Is.GreaterThan(0));

                Assert.That(
                    controller.TryLoadAssignedShuttleCargo(
                        new CargoTransferRequest(
                            runtime.ShuttleBinding.Cargo.ContainerId,
                            runtime.ShuttleBinding.Cargo.Revision)),
                    Is.EqualTo(CargoTransferResult.Succeeded));
                Assert.That(runtime.LocalInventory.Count(item), Is.Zero);
                Assert.That(runtime.ShuttleBinding.Cargo.Count(item), Is.EqualTo(harvested));

                ShuttleDockingBoundary dockingBoundary =
                    Object.FindAnyObjectByType<ShuttleDockingBoundary>();
                FleetCargoUnloadInteractable unload =
                    Object.FindAnyObjectByType<FleetCargoUnloadInteractable>();
                Collider shuttleCollider =
                    runtime.ShuttleBinding.GetComponentInChildren<Collider>();
                Assert.That(dockingBoundary, Is.Not.Null);
                Assert.That(unload, Is.Not.Null);
                Assert.That(shuttleCollider, Is.Not.Null);

                MethodInfo enterBoundary = typeof(ShuttleDockingBoundary)
                    .GetMethod(
                        "OnTriggerEnter",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(enterBoundary, Is.Not.Null);
                enterBoundary.Invoke(dockingBoundary, new object[] { shuttleCollider });

                UiFeedbackService feedback =
                    Object.FindAnyObjectByType<UiFeedbackService>();
                Assert.That(feedback, Is.Not.Null);
                feedback.Clear();
                string unloadMessage = null;
                feedback.MessageShown += CaptureUnloadMessage;

                InteractionContext context = new(
                    runtime.Possession.gameObject,
                    runtime.Possession.transform,
                    default,
                    controller.Commands);
                Assert.That(unload.CanInteract(context), Is.True);
                unload.Interact(context);

                for (int frame = 0; frame < 10 && unloadMessage == null; frame++)
                {
                    yield return null;
                }

                feedback.MessageShown -= CaptureUnloadMessage;
                Assert.That(runtime.ShuttleBinding.Cargo.Count(item), Is.Zero);
                Assert.That(runtime.FleetStorage.Count(item), Is.EqualTo(harvested));
                Assert.That(unloadMessage, Does.Contain("UNLOAD COMPLETE"));
                Assert.That(unloadMessage, Does.Contain("FLEET STORAGE"));

                GameplaySaveCoordinator saves =
                    controller.GetComponent<GameplaySaveCoordinator>();
                Assert.That(saves, Is.Not.Null);
                Assert.That(saves.Save(UnloadWorkflowSlot).Succeeded, Is.True);
                Assert.That(
                    runtime.FleetStorage.TryRemove(item, harvested),
                    Is.EqualTo(harvested));
                Assert.That(runtime.FleetStorage.Count(item), Is.Zero);
                Assert.That(saves.Load(UnloadWorkflowSlot).Succeeded, Is.True);
                Assert.That(runtime.FleetStorage.Count(item), Is.EqualTo(harvested));

                void CaptureUnloadMessage(
                    string message,
                    UiFeedbackSeverity _)
                {
                    if (message.Contains("UNLOAD COMPLETE"))
                    {
                        unloadMessage = message;
                    }
                }
            }
            finally
            {
                SaveGameFileService.DeleteSlot(UnloadWorkflowSlot);
            }
        }

        static Collider FindCollider(SpacecraftMotor motor, string colliderName)
        {
            Collider[] colliders = motor.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (candidate != null && candidate.name == colliderName)
                {
                    return candidate;
                }
            }

            return null;
        }

        static IEnumerator LoadGameplayShell()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "SC_GameplayShell",
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
            {
                yield return null;
            }

            float zoneLoadDeadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < zoneLoadDeadline &&
                   !SceneManager.GetSceneByName("SC_WorldZone").isLoaded)
            {
                yield return null;
            }

            Assert.That(
                SceneManager.GetSceneByName("SC_WorldZone").isLoaded,
                Is.True,
                "Gameplay shell did not load SC_WorldZone.");
            yield return null;
        }

        static IEnumerator StreamNearestDeposit()
        {
            ResourceDepositRuntimeSpawner spawner = null;
            ResourceDepositRuntimeSpawner[] spawners =
                Object.FindObjectsByType<ResourceDepositRuntimeSpawner>(
                    FindObjectsInactive.Exclude);
            for (int i = 0; i < spawners.Length; i++)
            {
                spawners[i].Regenerate();
                if (spawners[i].Body != null && spawners[i].GeneratedDeposits.Count > 0)
                {
                    spawner = spawners[i];
                    break;
                }
            }

            Assert.That(spawner, Is.Not.Null, "No world-zone resource spawner generated deposits.");

            Transform probe = new GameObject("DepositStreamingProbe").transform;
            int attempts = Mathf.Min(6, spawner.GeneratedDeposits.Count);
            for (int i = 0; i < attempts; i++)
            {
                ResourceDepositData deposit = spawner.GeneratedDeposits[i];
                probe.position = spawner.Body.Position +
                    spawner.Body.transform.TransformDirection(deposit.LocalDirection) *
                    (spawner.Body.Radius + deposit.Altitude + 2f);
                spawner.SetTrackingTarget(probe);

                for (int wait = 0; wait < 4; wait++)
                {
                    yield return new WaitForSeconds(0.2f);
                    if (FindAddressableResource() != null)
                    {
                        yield break;
                    }
                }
            }
        }

        static ResourceNodeInteractable FindAddressableResource()
        {
            ResourceNodeInteractable[] nodes = Object.FindObjectsByType<ResourceNodeInteractable>(
                FindObjectsInactive.Exclude);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].DepositId.IsValid)
                {
                    return nodes[i];
                }
            }

            return null;
        }

        static void AssertLandingContact(
            SpacecraftMotor motor,
            string colliderName)
        {
            Collider contact = FindCollider(motor, colliderName);
            Assert.That(contact, Is.Not.Null, $"{colliderName} is missing.");
            Assert.That(contact.enabled, Is.True, $"{colliderName} is disabled.");
        }
    }
}
