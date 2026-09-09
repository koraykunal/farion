using System.Collections;
using Farion.Core.Identity;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Processing;
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.Spacecraft;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Farion.Tests.PlayMode
{
    public sealed class SoloSessionLoopTests
    {
        const string BoardingSlot = "playmode-solo-boarding";
        const string LoopSlot = "playmode-solo-loop";
        const string OreItemId = "item.iron_ore";
        const string IngotItemId = "item.iron_ingot";
        const string RecipeId = "process.iron_ore";
        const float StepTimeoutSeconds = 30f;
        const int MaximumHarvestAttempts = 12;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SoloSessionTestScope.Stop(BoardingSlot);
            yield return SoloSessionTestScope.Stop(LoopSlot);
        }

        [UnityTest]
        public IEnumerator SoloSessionBoardsPilotsAndLeavesTheSeat()
        {
            yield return SoloSessionTestScope.Start(BoardingSlot);
            MultiplayerSessionController session = MultiplayerSessionController.Active;
            Assert.That(session.IsPrivate, Is.True);
            Assert.That(session.PlayerCapacity, Is.EqualTo(1));

            NetworkSessionPlayer local = NetworkSessionPlayer.Local;
            Assert.That(local.PossessionMode, Is.EqualTo(PlayerPossessionMode.OnFoot));
            NetworkExplorerController explorer = FindOwnedExplorer();
            NetworkStarterShuttle ship = FindAssignedShip(local);
            Assert.That(explorer, Is.Not.Null);
            Assert.That(ship, Is.Not.Null);
            VehicleBoardingPoint boardingPoint =
                ship.GetComponentInChildren<VehicleBoardingPoint>(true);
            Assert.That(boardingPoint.IsBound, Is.True);

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
        }

        [UnityTest]
        public IEnumerator SoloLoopHarvestsLoadsUnloadsProcessesSavesAndSurvivesRestart()
        {
            yield return SoloSessionTestScope.Start(LoopSlot);
            MultiplayerSessionController session = MultiplayerSessionController.Active;
            NetworkSessionPlayer local = NetworkSessionPlayer.Local;
            NetworkExplorerController explorer = FindOwnedExplorer();
            NetworkStarterShuttle ship = FindAssignedShip(local);
            Assert.That(explorer, Is.Not.Null);
            Assert.That(ship, Is.Not.Null);
            yield return WaitUntil(
                () => !string.IsNullOrEmpty(local.PersistentPlayerId),
                "The host profile was not accepted.");
            string persistentId = local.PersistentPlayerId;

            GameplayRuntimeBindings bindings = MultiplayerSceneContext.Active.RuntimeRoot.Bindings;
            FleetStorageInventory storage = bindings.FleetStorage;
            GameplayDefinitionRegistry registry = bindings.Definitions;
            Assert.That(storage, Is.Not.Null);
            Assert.That(registry.TryGetInventoryItem(OreItemId, out InventoryItemDefinition ore), Is.True);
            Assert.That(registry.TryGetInventoryItem(IngotItemId, out InventoryItemDefinition ingot), Is.True);
            Assert.That(DefinitionId.TryCreate(RecipeId, out DefinitionId recipeId), Is.True);
            Assert.That(registry.TryGetProcessingRecipe(recipeId, out ProcessingRecipeDefinition recipe), Is.True);
            int requiredOre = 0;
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                if (recipe.Inputs[i].Item == ore)
                {
                    requiredOre += recipe.Inputs[i].Amount;
                }
            }

            Assert.That(requiredOre, Is.GreaterThan(0), "The iron recipe no longer consumes iron ore.");

            InventoryContainerComponent inventory =
                explorer.GetComponentInChildren<InventoryContainerComponent>(true);
            NetworkGameplayCommands commands = local.GetComponent<NetworkGameplayCommands>();
            ResourceHarvestResult? lastHarvest = null;
            CargoTransferReceipt? lastCargo = null;
            FleetProcessingResult? lastProcessing = null;
            commands.HarvestCompleted += result => lastHarvest = result;
            commands.CargoTransferCompleted += receipt => lastCargo = receipt;
            commands.FleetProcessingCompleted += result => lastProcessing = result;

            int oreBefore = inventory.Count(ore);
            int storageOreBefore = storage.Count(ore);
            int storageIngotBefore = storage.Count(ingot);
            for (int attempt = 0; attempt < MaximumHarvestAttempts && inventory.Count(ore) - oreBefore < requiredOre; attempt++)
            {
                ResourceNodeInteractable node = FindHarvestableNode(ore);
                if (node == null)
                {
                    yield return StreamNearestDeposit(ore);
                    node = FindHarvestableNode(ore);
                }

                Assert.That(node, Is.Not.Null, "No iron ore deposit could be streamed in.");
                TeleportExplorer(explorer, node.transform.position + node.transform.up * 1.5f);
                yield return null;
                yield return null;

                int before = inventory.Count(ore);
                lastHarvest = null;
                ResourceHarvestResult sent = commands.TryHarvest(
                    new ResourceHarvestRequest(node.DepositId, inventory.ContainerId, inventory.Revision));
                Assert.That(sent, Is.EqualTo(ResourceHarvestResult.Pending), $"Harvest was rejected locally: {sent}.");
                yield return WaitUntil(() => lastHarvest.HasValue, "Harvest never completed.");
                Assert.That(lastHarvest.Value, Is.EqualTo(ResourceHarvestResult.Succeeded));
                Assert.That(inventory.Count(ore), Is.GreaterThan(before));
            }

            int harvested = inventory.Count(ore) - oreBefore;
            Assert.That(harvested, Is.GreaterThanOrEqualTo(requiredOre));

            TeleportExplorer(explorer, ship.transform.position + ship.transform.up * 2f + ship.transform.right * 4f);
            yield return null;
            yield return null;
            lastCargo = null;
            Assert.That(
                commands.TryLoadAssignedShuttleCargo(
                    new CargoTransferRequest(ship.Cargo.ContainerId, ship.Cargo.Revision)),
                Is.EqualTo(CargoTransferResult.Pending));
            yield return WaitUntil(() => lastCargo.HasValue, "Cargo load never completed.");
            Assert.That(lastCargo.Value.Result, Is.EqualTo(CargoTransferResult.Succeeded));
            Assert.That(inventory.Count(ore), Is.EqualTo(oreBefore));
            Assert.That(ship.Cargo.Count(ore), Is.EqualTo(harvested));

            Transform fleet = bindings.Fleet.transform;
            Assert.That(
                Vector3.Distance(ship.transform.position, fleet.position),
                Is.LessThanOrEqualTo(60f),
                "The formation slot is outside the docking range of the fleet.");
            lastCargo = null;
            Assert.That(
                commands.TryUnloadAssignedShuttleCargo(
                    new CargoTransferRequest(ship.Cargo.ContainerId, ship.Cargo.Revision)),
                Is.EqualTo(CargoTransferResult.Pending));
            yield return WaitUntil(() => lastCargo.HasValue, "Cargo unload never completed.");
            Assert.That(lastCargo.Value.Result, Is.EqualTo(CargoTransferResult.Succeeded));
            Assert.That(ship.Cargo.Count(ore), Is.Zero);
            Assert.That(storage.Count(ore), Is.EqualTo(storageOreBefore + harvested));

            TeleportExplorer(explorer, fleet.position + fleet.up * 3f);
            yield return null;
            yield return null;
            lastProcessing = null;
            Assert.That(
                commands.TryProcessFleetRecipe(
                    new FleetProcessingRequest(recipeId, storage.ContainerId, storage.Revision)),
                Is.EqualTo(FleetProcessingResult.Pending));
            yield return WaitUntil(() => lastProcessing.HasValue, "Processing never completed.");
            Assert.That(lastProcessing.Value, Is.EqualTo(FleetProcessingResult.Succeeded));
            int expectedOre = storageOreBefore + harvested - requiredOre;
            int expectedIngots = storageIngotBefore + 1;
            Assert.That(storage.Count(ore), Is.EqualTo(expectedOre));
            Assert.That(storage.Count(ingot), Is.EqualTo(expectedIngots));

            MultiplayerSaveBridge saveBridge = session.GetComponent<MultiplayerSaveBridge>();
            Assert.That(saveBridge.CanSave, Is.True);
            Assert.That(saveBridge.Save(LoopSlot).Succeeded, Is.True);

            yield return SoloSessionTestScope.Stop(LoopSlot, deleteSlot: false);
            yield return SoloSessionTestScope.StartFromSave(LoopSlot);

            NetworkSessionPlayer restored = NetworkSessionPlayer.Local;
            yield return WaitUntil(
                () => restored.PersistentPlayerId == persistentId,
                "The restored session did not recognise the same player.");
            GameplayRuntimeBindings restoredBindings =
                MultiplayerSceneContext.Active.RuntimeRoot.Bindings;
            FleetStorageInventory restoredStorage = restoredBindings.FleetStorage;
            Assert.That(restoredStorage.Count(ore), Is.EqualTo(expectedOre));
            Assert.That(restoredStorage.Count(ingot), Is.EqualTo(expectedIngots));

            NetworkExplorerController restoredExplorer = FindOwnedExplorer();
            NetworkStarterShuttle restoredShip = FindAssignedShip(restored);
            Assert.That(restoredExplorer, Is.Not.Null);
            Assert.That(restoredShip, Is.Not.Null);
            Assert.That(
                restoredExplorer.GetComponentInChildren<InventoryContainerComponent>(true).Count(ore),
                Is.EqualTo(oreBefore));
            Assert.That(restoredShip.Cargo.Count(ore), Is.Zero);
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
            PlayerExplorerPlacement.PlaceAtPose(
                explorer.gameObject,
                position,
                explorer.transform.rotation);
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

        static IEnumerator StreamNearestDeposit(InventoryItemDefinition wanted)
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
            int attempts = Mathf.Min(12, spawner.GeneratedDeposits.Count);
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
                    if (FindHarvestableNode(wanted) != null)
                    {
                        yield break;
                    }
                }
            }
        }

        static ResourceNodeInteractable FindHarvestableNode(InventoryItemDefinition wanted)
        {
            ResourceNodeInteractable[] nodes = Object.FindObjectsByType<ResourceNodeInteractable>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                ResourceNodeInteractable node = nodes[i];
                if (node != null &&
                    node.DepositId.IsValid &&
                    !node.IsDepleted &&
                    node.Definition != null &&
                    node.Definition.YieldedItem == wanted)
                {
                    return node;
                }
            }

            return null;
        }
    }
}
