using Farion.Core.Identity;
using System.Collections;
using Farion.Core.Persistence;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.ResourceNodes;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.Spacecraft;
using Farion.UI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Farion.Tests.PlayMode
{
    public sealed class PossessionLoopSmokeTests
    {
        const string SmokeSlot = "playmode-solo-smoke";
        const float StepTimeoutSeconds = 30f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SoloSessionTestScope.Stop(SmokeSlot);
        }

        [UnityTest]
        public IEnumerator SoloSessionBoardsPilotsLeavesHarvestsAndSaves()
        {
            yield return SoloSessionTestScope.Start(SmokeSlot);
            MultiplayerSessionController session = MultiplayerSessionController.Active;
            Assert.That(session, Is.Not.Null);
            Assert.That(session.IsPrivate, Is.True);
            Assert.That(session.PlayerCapacity, Is.EqualTo(1));

            NetworkSessionPlayer local = NetworkSessionPlayer.Local;
            Assert.That(local, Is.Not.Null);
            Assert.That(local.PossessionMode, Is.EqualTo(PlayerPossessionMode.OnFoot));
            Assert.That(local.AssignedStarterShuttleId.IsValid, Is.True);

            NetworkExplorerController explorer = FindOwnedExplorer();
            Assert.That(explorer, Is.Not.Null);
            NetworkStarterShuttle ship = FindAssignedShip(local);
            Assert.That(ship, Is.Not.Null);
            VehicleBoardingPoint boardingPoint =
                ship.GetComponentInChildren<VehicleBoardingPoint>(true);
            PilotSeatInteractable pilotSeat =
                ship.GetComponentInChildren<PilotSeatInteractable>(true);
            Assert.That(boardingPoint.IsBound, Is.True);
            Assert.That(pilotSeat.IsBound, Is.True);
            Assert.That(
                Object.FindAnyObjectByType<UiGameplayController>(),
                Is.Not.Null);

            TeleportExplorer(explorer, boardingPoint.transform.position + boardingPoint.transform.up * 0.5f);
            yield return null;
            Assert.That(local.RequestBoardStarterShuttle(ship.EntityId), Is.True);
            yield return WaitUntil(
                () => local.PossessionMode == PlayerPossessionMode.ShipInterior,
                "Explorer did not board the ship.");
            Assert.That(ship.IsClaimed, Is.True);
            Assert.That(explorer.IsInsideShip, Is.True);

            Assert.That(local.RequestPilotStarterShuttle(ship.EntityId), Is.True);
            yield return WaitUntil(
                () => local.PossessionMode == PlayerPossessionMode.Spacecraft && ship.IsPiloted,
                "Explorer did not take the pilot seat.");

            Assert.That(local.RequestLeavePilotSeat(ship.EntityId), Is.True);
            yield return WaitUntil(
                () => local.PossessionMode == PlayerPossessionMode.ShipInterior && !ship.IsPiloted,
                "Explorer did not leave the pilot seat.");

            InventoryContainerComponent inventory =
                explorer.GetComponentInChildren<InventoryContainerComponent>(true);
            NetworkGameplayCommands commands = local.GetComponent<NetworkGameplayCommands>();
            Assert.That(inventory, Is.Not.Null);
            Assert.That(commands, Is.Not.Null);

            ResourceNodeInteractable resource = FindAddressableResource();
            if (resource == null)
            {
                yield return StreamNearestDeposit();
                resource = FindAddressableResource();
            }

            Assert.That(resource, Is.Not.Null, "No streamed resource node was found.");
            Assert.That(
                commands.CanHarvest(
                    new ResourceHarvestRequest(
                        resource.DepositId,
                        PersistentEntityId.New(),
                        inventory.Revision)),
                Is.EqualTo(ResourceHarvestResult.UnauthorizedDestination));

            MultiplayerSaveBridge saveBridge =
                session.GetComponent<MultiplayerSaveBridge>();
            Assert.That(saveBridge.CanSave, Is.True);
            Assert.That(saveBridge.Save(SmokeSlot).Succeeded, Is.True);
            Assert.That(saveBridge.Load(SmokeSlot).Succeeded, Is.True);
        }

        static NetworkExplorerController FindOwnedExplorer()
        {
            foreach (NetworkExplorerController candidate in
                     NetworkExplorerController.ActiveExplorers)
            {
                if (candidate != null && candidate.IsOwner)
                {
                    return candidate;
                }
            }

            return null;
        }

        static NetworkStarterShuttle FindAssignedShip(NetworkSessionPlayer player)
        {
            foreach (NetworkStarterShuttle candidate in
                     Object.FindObjectsByType<NetworkStarterShuttle>(FindObjectsSortMode.None))
            {
                if (candidate.EntityId == player.AssignedStarterShuttleId)
                {
                    return candidate;
                }
            }

            return null;
        }

        static void TeleportExplorer(NetworkExplorerController explorer, Vector3 position)
        {
            Rigidbody body = explorer.GetComponent<Rigidbody>();
            body.position = position;
            explorer.transform.position = position;
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, string failure)
        {
            float deadline = Time.realtimeSinceStartup + StepTimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, failure);
        }

        static IEnumerator StreamNearestDeposit()
        {
            ResourceDepositRuntimeSpawner spawner = null;
            ResourceDepositRuntimeSpawner[] spawners =
                Object.FindObjectsByType<ResourceDepositRuntimeSpawner>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
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
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].DepositId.IsValid)
                {
                    return nodes[i];
                }
            }

            return null;
        }
    }
}
