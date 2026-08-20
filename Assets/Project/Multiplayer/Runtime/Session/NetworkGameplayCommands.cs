using Farion.Core.Identity;
using System;
using System.Collections.Generic;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Processing;
using Farion.Gameplay.Ships;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Spawning;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkSessionPlayer))]
    public sealed class NetworkGameplayCommands :
        NetworkBehaviour,
        IGameplayCommandGateway,
        IGameplayCommandEvents
    {
        [SerializeField, Min(0.1f)] float maximumHarvestDistance = 6f;
        [SerializeField, Min(0.1f)] float maximumCargoDistance = 12f;
        [SerializeField, Min(0.1f)] float maximumDockingDistance = 60f;

        NetworkSessionPlayer sessionPlayer;
        NetworkExplorerController ownerExplorer;
        InventoryContainerComponent ownerInventory;
        NetworkStarterShip ownerCargo;

        public event Action<InventoryItemDefinition, int> ItemAcquired;
        public event Action<CargoTransferReceipt> CargoTransferCompleted;
        public event Action<FleetProcessingResult> FleetProcessingCompleted;

        void Awake()
        {
            sessionPlayer = GetComponent<NetworkSessionPlayer>();
        }

        public void BindOwnerExplorer(
            NetworkExplorerController player,
            GameplayRuntimeBindings bindings)
        {
            if (!IsOwner || player == null)
            {
                return;
            }

            ownerExplorer = player;
            ownerInventory =
                player.GetComponentInChildren<InventoryContainerComponent>(true);
            PlayerInteractionRaycaster raycaster =
                player.GetComponentInChildren<PlayerInteractionRaycaster>(true);
            raycaster?.SetCommandGateway(this);
            RequestSessionStateServerRpc();
        }

        static GameplayRuntimeBindings ZoneBindings =>
            MultiplayerSceneContext.Active != null &&
            MultiplayerSceneContext.Active.RuntimeRoot != null
                ? MultiplayerSceneContext.Active.RuntimeRoot.Bindings
                : null;

        static GameplayDefinitionRegistry ZoneDefinitions =>
            ZoneBindings?.Definitions;

        static FleetStorageInventory ZoneFleetStorage =>
            ZoneBindings?.FleetStorage;

        public ResourceHarvestResult CanHarvest(ResourceHarvestRequest request)
        {
            if (!request.DepositId.IsValid)
            {
                return ResourceHarvestResult.MissingSource;
            }

            if (ownerInventory == null || !request.DestinationContainerId.IsValid)
            {
                return ResourceHarvestResult.MissingDestination;
            }

            if (ownerInventory.ContainerId != request.DestinationContainerId)
            {
                return ResourceHarvestResult.UnauthorizedDestination;
            }

            if (ownerInventory.Revision != request.ExpectedDestinationRevision)
            {
                return ResourceHarvestResult.StaleDestination;
            }

            return TryFindLoadedNode(request.DepositId, out ResourceNodeInteractable node)
                ? ResourceHarvestTransaction.CanExecute(node, ownerInventory)
                : ResourceHarvestResult.MissingSource;
        }

        public ResourceHarvestResult TryHarvest(ResourceHarvestRequest request)
        {
            if (!IsOwner)
            {
                return ResourceHarvestResult.MissingDestination;
            }

            ResourceHarvestResult validation = CanHarvest(request);
            if (validation != ResourceHarvestResult.Succeeded)
            {
                return validation;
            }

            HarvestServerRpc(request.DepositId.Value);
            return ResourceHarvestResult.Pending;
        }

        public CargoTransferResult CanLoadAssignedShuttleCargo(
            CargoTransferRequest request)
        {
            if (!TryResolveOwnerCargo(request, out ShuttleCargoInventory cargo))
            {
                return CargoTransferResult.MissingDestination;
            }

            return IsExplorerNearOwnedShip()
                ? CargoTransferTransaction.CanExecute(ownerInventory, cargo)
                : CargoTransferResult.OutOfRange;
        }

        public CargoTransferResult TryLoadAssignedShuttleCargo(
            CargoTransferRequest request)
        {
            CargoTransferResult validation = CanLoadAssignedShuttleCargo(request);
            if (!IsOwner || validation != CargoTransferResult.Succeeded)
            {
                return validation;
            }

            LoadShuttleCargoServerRpc();
            return CargoTransferResult.Pending;
        }

        public CargoTransferResult CanUnloadAssignedShuttleCargo(
            CargoTransferRequest request)
        {
            if (!TryResolveOwnerCargo(request, out ShuttleCargoInventory cargo))
            {
                return CargoTransferResult.MissingSource;
            }

            if (ZoneFleetStorage == null)
            {
                return CargoTransferResult.MissingDestination;
            }

            return IsExplorerNearOwnedShip() &&
                   IsShipDocked(ownerCargo != null ? ownerCargo.transform : null)
                ? CargoTransferTransaction.CanExecute(cargo, ZoneFleetStorage)
                : CargoTransferResult.OutOfRange;
        }

        public CargoTransferResult TryUnloadAssignedShuttleCargo(
            CargoTransferRequest request)
        {
            CargoTransferResult validation = CanUnloadAssignedShuttleCargo(request);
            if (!IsOwner || validation != CargoTransferResult.Succeeded)
            {
                return validation;
            }

            UnloadShuttleCargoServerRpc();
            return CargoTransferResult.Pending;
        }

        public FleetProcessingResult CanProcessFleetRecipe(
            FleetProcessingRequest request)
        {
            if (ZoneFleetStorage == null)
            {
                return FleetProcessingResult.MissingStorage;
            }

            if (ZoneFleetStorage.ContainerId != request.FleetStorageId)
            {
                return FleetProcessingResult.UnauthorizedStorage;
            }

            if (!TryResolveRecipe(
                    ZoneDefinitions,
                    request.RecipeId,
                    out ProcessingRecipeDefinition recipe,
                    out FleetProcessingResult failure))
            {
                return failure;
            }

            if (!IsExplorerNearFleet(ownerExplorer))
            {
                return FleetProcessingResult.OutOfRange;
            }

            return ZoneFleetStorage.CanExchange(recipe.Inputs, recipe.Outputs)
                ? FleetProcessingResult.Succeeded
                : FleetProcessingResult.Rejected;
        }

        bool IsExplorerNearOwnedShip()
        {
            return ownerExplorer != null &&
                ownerCargo != null &&
                IsWithinHarvestDistance(
                    ownerExplorer.transform.position,
                    ownerCargo.transform.position,
                    maximumCargoDistance);
        }

        public FleetProcessingResult TryProcessFleetRecipe(
            FleetProcessingRequest request)
        {
            FleetProcessingResult validation = CanProcessFleetRecipe(request);
            if (!IsOwner || validation != FleetProcessingResult.Succeeded)
            {
                return validation;
            }

            ProcessFleetRecipeServerRpc(request.RecipeId.Value);
            return FleetProcessingResult.Pending;
        }

        bool TryResolveOwnerCargo(
            CargoTransferRequest request,
            out ShuttleCargoInventory cargo)
        {
            cargo = ResolveOwnerCargo();
            return cargo != null &&
                ownerInventory != null &&
                request.ShuttleCargoId.IsValid &&
                cargo.ContainerId == request.ShuttleCargoId;
        }

        ShuttleCargoInventory ResolveOwnerCargo()
        {
            GeneratedEntityId claimed = sessionPlayer != null
                ? sessionPlayer.ClaimedStarterShipId
                : GeneratedEntityId.None;
            if (!claimed.IsValid)
            {
                return null;
            }

            if (ownerCargo != null && ownerCargo.EntityId == claimed)
            {
                return ownerCargo.Cargo;
            }

            ownerCargo = NetworkStarterShip.FindByEntityId(claimed);
            return ownerCargo != null ? ownerCargo.Cargo : null;
        }

        static bool TryResolveRecipe(
            GameplayDefinitionRegistry definitions,
            DefinitionId recipeId,
            out ProcessingRecipeDefinition recipe,
            out FleetProcessingResult failure)
        {
            recipe = null;
            if (!recipeId.IsValid ||
                definitions == null ||
                !definitions.TryGetProcessingRecipe(recipeId, out recipe))
            {
                failure = FleetProcessingResult.MissingRecipe;
                return false;
            }

            if (!recipe.IsValid)
            {
                failure = FleetProcessingResult.InvalidRecipe;
                return false;
            }

            failure = FleetProcessingResult.Succeeded;
            return true;
        }

        [ServerRpc]
        void HarvestServerRpc(
            ulong depositId,
            NetworkConnection sender = null)
        {
            if (sender == null ||
                !sender.IsActive ||
                sender.ClientId != OwnerId ||
                depositId == 0UL ||
                sessionPlayer.PossessionMode != PlayerPossessionMode.OnFoot ||
                !TryResolveServerContext(
                    out GameplayRuntimeBindings bindings,
                    out NetworkExplorerController explorer,
                    out InventoryContainerComponent destinationInventory))
            {
                return;
            }

            GeneratedEntityId resolvedDepositId = new(depositId);
            int carriedBefore = 0;
            if (!TryResolveHarvestNode(
                    bindings,
                    explorer.transform.position,
                    resolvedDepositId,
                    maximumHarvestDistance,
                    out ResourceNodeInteractable node))
            {
                HarvestResultTargetRpc(
                    sender,
                    (byte)ResourceHarvestResult.MissingSource,
                    string.Empty,
                    0,
                    string.Empty);
                return;
            }

            // The client can't track the server's revision (snapshots carry no
            // revision), so the server always addresses its own inventory at
            // its own current revision instead of trusting anything from the
            // wire.
            InventoryItemDefinition expectedItem = node.Definition?.YieldedItem;
            carriedBefore = expectedItem != null
                ? destinationInventory.Count(expectedItem)
                : 0;
            ResourceHarvestResult result =
                ResourceHarvestTransaction.TryExecute(node, destinationInventory);
            if (result != ResourceHarvestResult.Succeeded)
            {
                Debug.LogWarning(
                    $"NetworkGameplayCommands: harvest failed for deposit {depositId} with result {result}.");
            }

            string snapshotJson = result == ResourceHarvestResult.Succeeded
                ? JsonUtility.ToJson(destinationInventory.CaptureContainerSnapshot())
                : string.Empty;
            InventoryItemDefinition yielded = node.Definition?.YieldedItem;
            HarvestResultTargetRpc(
                sender,
                (byte)result,
                yielded != null ? yielded.ItemId : string.Empty,
                result == ResourceHarvestResult.Succeeded && yielded != null
                    ? destinationInventory.Count(yielded) - carriedBefore
                    : 0,
                snapshotJson);
            if (result == ResourceHarvestResult.Succeeded)
            {
                ResourceDeltaObserversRpc(
                    depositId,
                    node.InitialQuantity - node.RemainingQuantity);
            }
        }

        [ServerRpc]
        void LoadShuttleCargoServerRpc(NetworkConnection sender = null)
        {
            if (!TryResolveServerTransfer(
                    sender,
                    out InventoryContainerComponent explorerInventory,
                    out ShuttleCargoInventory cargo,
                    out _,
                    out _,
                    out CargoTransferResult failure))
            {
                ReportCargoFailure(
                    sender,
                    CargoTransferKind.LoadShuttle,
                    failure);
                return;
            }

            CargoTransferResult result =
                CargoTransferTransaction.TryExecute(explorerInventory, cargo);
            CargoTransferResultTargetRpc(
                sender,
                (byte)CargoTransferKind.LoadShuttle,
                (byte)result,
                result == CargoTransferResult.Succeeded
                    ? JsonUtility.ToJson(explorerInventory.CaptureContainerSnapshot())
                    : string.Empty);
            if (result == CargoTransferResult.Succeeded)
            {
                BroadcastCargoSnapshot(cargo);
            }
        }

        [ServerRpc]
        void UnloadShuttleCargoServerRpc(NetworkConnection sender = null)
        {
            if (!TryResolveServerTransfer(
                    sender,
                    out _,
                    out ShuttleCargoInventory cargo,
                    out FleetStorageInventory storage,
                    out Transform shipTransform,
                    out CargoTransferResult failure))
            {
                ReportCargoFailure(
                    sender,
                    CargoTransferKind.UnloadToFleet,
                    failure);
                return;
            }

            if (storage == null)
            {
                ReportCargoFailure(
                    sender,
                    CargoTransferKind.UnloadToFleet,
                    CargoTransferResult.MissingDestination);
                return;
            }

            if (!IsShipDocked(shipTransform))
            {
                ReportCargoFailure(
                    sender,
                    CargoTransferKind.UnloadToFleet,
                    CargoTransferResult.OutOfRange);
                return;
            }

            CargoTransferResult result =
                CargoTransferTransaction.TryExecute(cargo, storage);
            CargoTransferResultTargetRpc(
                sender,
                (byte)CargoTransferKind.UnloadToFleet,
                (byte)result,
                string.Empty);
            if (result != CargoTransferResult.Succeeded)
            {
                return;
            }

            BroadcastCargoSnapshot(cargo);
            BroadcastFleetStorageSnapshot(storage);
        }

        [ServerRpc]
        void ProcessFleetRecipeServerRpc(
            string recipeId,
            NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsActive || sender.ClientId != OwnerId)
            {
                return;
            }

            if (!DefinitionId.TryCreate(recipeId, out DefinitionId resolvedRecipeId) ||
                !TryResolveServerBindings(out GameplayRuntimeBindings bindings) ||
                !TryResolveRecipe(
                    bindings.Definitions,
                    resolvedRecipeId,
                    out ProcessingRecipeDefinition recipe,
                    out FleetProcessingResult recipeFailure))
            {
                FleetProcessingResultTargetRpc(
                    sender,
                    (byte)FleetProcessingResult.MissingRecipe);
                return;
            }

            if (bindings.FleetStorage == null)
            {
                FleetProcessingResultTargetRpc(
                    sender,
                    (byte)FleetProcessingResult.MissingStorage);
                return;
            }

            if (recipeFailure != FleetProcessingResult.Succeeded)
            {
                FleetProcessingResultTargetRpc(sender, (byte)recipeFailure);
                return;
            }

            if (!sessionPlayer.PlayerSpawner.TryGetSpawnedExplorer(
                    sessionPlayer,
                    out NetworkExplorerController explorer) ||
                !IsExplorerNearFleet(explorer))
            {
                FleetProcessingResultTargetRpc(
                    sender,
                    (byte)FleetProcessingResult.OutOfRange);
                return;
            }

            bool exchanged =
                bindings.FleetStorage.TryExchange(recipe.Inputs, recipe.Outputs);
            FleetProcessingResultTargetRpc(
                sender,
                (byte)(exchanged
                    ? FleetProcessingResult.Succeeded
                    : FleetProcessingResult.Rejected));
            if (exchanged)
            {
                BroadcastFleetStorageSnapshot(bindings.FleetStorage);
            }
        }

        void ReportCargoFailure(
            NetworkConnection sender,
            CargoTransferKind kind,
            CargoTransferResult failure)
        {
            if (sender != null && sender.IsActive)
            {
                CargoTransferResultTargetRpc(
                    sender,
                    (byte)kind,
                    (byte)failure,
                    string.Empty);
            }
        }

        [TargetRpc]
        void FleetProcessingResultTargetRpc(
            NetworkConnection connection,
            byte result)
        {
            FleetProcessingCompleted?.Invoke((FleetProcessingResult)result);
        }

        bool TryResolveServerTransfer(
            NetworkConnection sender,
            out InventoryContainerComponent explorerInventory,
            out ShuttleCargoInventory cargo,
            out FleetStorageInventory storage,
            out Transform shipTransform,
            out CargoTransferResult failure)
        {
            explorerInventory = null;
            cargo = null;
            storage = null;
            shipTransform = null;
            failure = CargoTransferResult.Rejected;
            if (sender == null ||
                !sender.IsActive ||
                sender.ClientId != OwnerId ||
                !sessionPlayer.ClaimedStarterShipId.IsValid ||
                !TryResolveServerBindings(out GameplayRuntimeBindings bindings) ||
                !sessionPlayer.PlayerSpawner.TryGetSpawnedExplorer(
                    sessionPlayer,
                    out NetworkExplorerController explorer))
            {
                return false;
            }

            NetworkStarterShip ship = NetworkStarterShip.FindByEntityId(
                sessionPlayer.ClaimedStarterShipId);
            if (ship == null ||
                !ship.IsClaimedBy(sessionPlayer.SessionPlayerId))
            {
                failure = CargoTransferResult.MissingSource;
                return false;
            }

            if (!IsWithinHarvestDistance(
                    explorer.transform.position,
                    ship.transform.position,
                    maximumCargoDistance))
            {
                failure = CargoTransferResult.OutOfRange;
                return false;
            }

            explorerInventory =
                explorer.GetComponentInChildren<InventoryContainerComponent>(true);
            cargo = ship.Cargo;
            storage = bindings.FleetStorage;
            shipTransform = ship.transform;
            if (explorerInventory == null || cargo == null)
            {
                failure = CargoTransferResult.MissingSource;
                return false;
            }

            failure = CargoTransferResult.Succeeded;
            return true;
        }

        bool IsShipDocked(Transform shipTransform)
        {
            Transform fleet = ZoneBindings?.Fleet != null
                ? ZoneBindings.Fleet.transform
                : null;
            return shipTransform != null &&
                fleet != null &&
                IsWithinHarvestDistance(
                    shipTransform.position,
                    fleet.position,
                    maximumDockingDistance);
        }

        bool IsExplorerNearFleet(NetworkExplorerController explorer)
        {
            Transform fleet = ZoneBindings?.Fleet != null
                ? ZoneBindings.Fleet.transform
                : null;
            return explorer != null &&
                fleet != null &&
                IsWithinHarvestDistance(
                    explorer.transform.position,
                    fleet.position,
                    maximumDockingDistance);
        }

        bool TryResolveServerBindings(out GameplayRuntimeBindings bindings)
        {
            bindings = null;
            NetworkPlayerSpawner spawner = sessionPlayer.PlayerSpawner;
            return spawner != null && spawner.TryGetRuntimeBindings(out bindings);
        }

        void BroadcastCargoSnapshot(ShuttleCargoInventory cargo)
        {
            CargoSnapshotObserversRpc(
                cargo.ContainerId.Value,
                JsonUtility.ToJson(cargo.CaptureContainerSnapshot()));
        }

        void BroadcastFleetStorageSnapshot(FleetStorageInventory storage)
        {
            FleetStorageSnapshotObserversRpc(
                JsonUtility.ToJson(storage.CaptureContainerSnapshot()));
        }

        [ObserversRpc]
        void CargoSnapshotObserversRpc(string cargoContainerId, string snapshotJson)
        {
            if (IsServerStarted)
            {
                return;
            }

            ApplyCargoSnapshot(cargoContainerId, snapshotJson);
        }

        [ObserversRpc]
        void FleetStorageSnapshotObserversRpc(string snapshotJson)
        {
            if (IsServerStarted || ZoneFleetStorage == null)
            {
                return;
            }

            ApplySnapshot(ZoneFleetStorage, snapshotJson);
        }

        [TargetRpc]
        void CargoTransferResultTargetRpc(
            NetworkConnection connection,
            byte kind,
            byte result,
            string inventorySnapshotJson)
        {
            if (!IsServerStarted &&
                result == (byte)CargoTransferResult.Succeeded &&
                ownerInventory != null)
            {
                ApplySnapshot(ownerInventory, inventorySnapshotJson);
            }

            CargoTransferKind resolvedKind = (CargoTransferKind)kind;
            ShuttleCargoInventory cargo = ResolveOwnerCargo();
            CargoTransferCompleted?.Invoke(
                new CargoTransferReceipt(
                    resolvedKind,
                    (CargoTransferResult)result,
                    resolvedKind == CargoTransferKind.LoadShuttle
                        ? ownerInventory?.CaptureContainerSnapshot()
                        : cargo?.CaptureContainerSnapshot(),
                    resolvedKind == CargoTransferKind.LoadShuttle
                        ? cargo?.CaptureContainerSnapshot()
                        : ZoneFleetStorage?.CaptureContainerSnapshot()));
        }

        static void ApplyCargoSnapshot(string cargoContainerId, string snapshotJson)
        {
            IReadOnlyList<NetworkStarterShip> ships = NetworkStarterShip.ActiveShips;
            for (int i = 0; i < ships.Count; i++)
            {
                ShuttleCargoInventory cargo = ships[i] != null ? ships[i].Cargo : null;
                if (cargo != null && cargo.ContainerId.Value == cargoContainerId)
                {
                    ApplySnapshot(cargo, snapshotJson);
                    return;
                }
            }
        }

        static void ApplySnapshot(
            InventoryContainerComponent container,
            string snapshotJson)
        {
            if (string.IsNullOrEmpty(snapshotJson) ||
                container == null ||
                ZoneDefinitions == null)
            {
                return;
            }

            InventoryContainerSnapshot snapshot =
                JsonUtility.FromJson<InventoryContainerSnapshot>(snapshotJson);
            if (!container.ApplyContainerSnapshot(snapshot, ZoneDefinitions))
            {
                Debug.LogWarning(
                    $"NetworkGameplayCommands: failed to apply snapshot for container '{snapshot?.ContainerId}'.");
            }
        }

        [ObserversRpc]
        void ResourceDeltaObserversRpc(ulong depositId, int extractedAmount)
        {
            ApplyResourceDelta(
                new GeneratedEntityId(depositId),
                extractedAmount);
        }

        [ServerRpc]
        void RequestSessionStateServerRpc(NetworkConnection sender = null)
        {
            NetworkPlayerSpawner spawner = sessionPlayer.PlayerSpawner;
            if (sender == null ||
                !sender.IsActive ||
                sender.ClientId != OwnerId ||
                spawner == null ||
                !spawner.TryGetRuntimeBindings(
                    out GameplayRuntimeBindings bindings))
            {
                return;
            }

            List<ResourceDepositDeltaSnapshot> snapshots = new();
            foreach (ResourceDepositRuntimeSpawner streamer in
                     bindings.ResourceStreamers)
            {
                if (streamer != null)
                {
                    streamer.CaptureDeltaSnapshot(snapshots);
                }
            }

            foreach (ResourceDepositDeltaSnapshot snapshot in snapshots)
            {
                ResourceDeltaTargetRpc(
                    sender,
                    snapshot.DepositId.Value,
                    snapshot.ExtractedAmount);
            }

            if (bindings.FleetStorage != null)
            {
                FleetStorageSnapshotTargetRpc(
                    sender,
                    JsonUtility.ToJson(
                        bindings.FleetStorage.CaptureContainerSnapshot()));
            }

            if (spawner.TryGetSpawnedExplorer(
                    sessionPlayer,
                    out NetworkExplorerController explorer))
            {
                InventoryContainerComponent carried =
                    explorer.GetComponentInChildren<InventoryContainerComponent>(true);
                if (carried != null)
                {
                    OwnerInventorySnapshotTargetRpc(
                        sender,
                        JsonUtility.ToJson(carried.CaptureContainerSnapshot()));
                }
            }

            IReadOnlyList<NetworkStarterShip> ships = NetworkStarterShip.ActiveShips;
            for (int i = 0; i < ships.Count; i++)
            {
                ShuttleCargoInventory cargo = ships[i] != null ? ships[i].Cargo : null;
                if (cargo != null)
                {
                    CargoSnapshotTargetRpc(
                        sender,
                        cargo.ContainerId.Value,
                        JsonUtility.ToJson(cargo.CaptureContainerSnapshot()));
                }
            }
        }

        internal void PushOwnerInventory(InventoryContainerComponent inventory)
        {
            if (!IsServerStarted ||
                inventory == null ||
                Owner == null ||
                !Owner.IsActive ||
                IsOwner)
            {
                return;
            }

            OwnerInventorySnapshotTargetRpc(
                Owner,
                JsonUtility.ToJson(inventory.CaptureContainerSnapshot()));
        }

        [TargetRpc]
        void OwnerInventorySnapshotTargetRpc(
            NetworkConnection connection,
            string snapshotJson)
        {
            if (!IsServerStarted)
            {
                ApplySnapshot(ownerInventory, snapshotJson);
            }
        }

        [TargetRpc]
        void FleetStorageSnapshotTargetRpc(
            NetworkConnection connection,
            string snapshotJson)
        {
            if (!IsServerStarted)
            {
                ApplySnapshot(ZoneFleetStorage, snapshotJson);
            }
        }

        [TargetRpc]
        void CargoSnapshotTargetRpc(
            NetworkConnection connection,
            string cargoContainerId,
            string snapshotJson)
        {
            if (!IsServerStarted)
            {
                ApplyCargoSnapshot(cargoContainerId, snapshotJson);
            }
        }

        [TargetRpc]
        void ResourceDeltaTargetRpc(
            NetworkConnection connection,
            ulong depositId,
            int extractedAmount)
        {
            ApplyResourceDelta(
                new GeneratedEntityId(depositId),
                extractedAmount);
        }

        [TargetRpc]
        void HarvestResultTargetRpc(
            NetworkConnection connection,
            byte result,
            string itemId,
            int acquiredAmount,
            string snapshotJson)
        {
            if (result != (byte)ResourceHarvestResult.Succeeded)
            {
                Debug.LogWarning(
                    $"NetworkGameplayCommands: harvest failed with result {(ResourceHarvestResult)result}.");
                return;
            }

            if (!IsServerStarted)
            {
                ApplySnapshot(ownerInventory, snapshotJson);
            }

            RaiseItemAcquired(itemId, acquiredAmount);
        }

        void RaiseItemAcquired(string itemId, int amount)
        {
            if (amount <= 0 ||
                ZoneDefinitions == null ||
                !DefinitionId.TryCreate(itemId, out DefinitionId resolvedItemId) ||
                !ZoneDefinitions.TryGetInventoryItem(
                    resolvedItemId,
                    out InventoryItemDefinition item))
            {
                return;
            }

            ItemAcquired?.Invoke(item, amount);
        }

        bool TryResolveServerContext(
            out GameplayRuntimeBindings bindings,
            out NetworkExplorerController explorer,
            out InventoryContainerComponent inventory)
        {
            bindings = null;
            explorer = null;
            inventory = null;
            NetworkPlayerSpawner spawner = sessionPlayer.PlayerSpawner;
            if (spawner == null ||
                !spawner.TryGetRuntimeBindings(
                    out bindings) ||
                !spawner.TryGetSpawnedExplorer(
                    sessionPlayer,
                    out explorer))
            {
                return false;
            }

            inventory =
                explorer.GetComponentInChildren<InventoryContainerComponent>(true);
            return inventory != null;
        }

        internal static bool IsWithinHarvestDistance(
            Vector3 explorerPosition,
            Vector3 depositPosition,
            float maximumDistance)
        {
            float limit = Mathf.Max(0f, maximumDistance);
            return (depositPosition - explorerPosition).sqrMagnitude <= limit * limit;
        }

        static bool TryResolveHarvestNode(
            GameplayRuntimeBindings bindings,
            Vector3 explorerPosition,
            GeneratedEntityId depositId,
            float maximumDistance,
            out ResourceNodeInteractable node)
        {
            node = null;
            if (bindings == null)
            {
                return false;
            }

            foreach (ResourceDepositRuntimeSpawner streamer in
                     bindings.ResourceStreamers)
            {
                if (streamer != null &&
                    streamer.TryGetDepositPosition(
                        depositId,
                        out Vector3 depositPosition) &&
                    IsWithinHarvestDistance(
                        explorerPosition,
                        depositPosition,
                        maximumDistance) &&
                    streamer.TryGetOrCreateSpawnedNode(depositId, out node))
                {
                    return true;
                }
            }

            return false;
        }

        static void ApplyResourceDelta(
            GeneratedEntityId depositId,
            int extractedAmount)
        {
            IReadOnlyList<ResourceDepositRuntimeSpawner> streamers =
                ResourceDepositRuntimeSpawner.RegisteredSpawners;
            for (int i = 0; i < streamers.Count; i++)
            {
                if (streamers[i].ApplyAuthoritativeDelta(depositId, extractedAmount))
                {
                    return;
                }
            }
        }

        static bool TryFindLoadedNode(
            GeneratedEntityId depositId,
            out ResourceNodeInteractable node)
        {
            IReadOnlyList<ResourceDepositRuntimeSpawner> streamers =
                ResourceDepositRuntimeSpawner.RegisteredSpawners;
            for (int i = 0; i < streamers.Count; i++)
            {
                if (streamers[i].TryGetSpawnedNode(depositId, out node))
                {
                    return true;
                }
            }

            node = null;
            return false;
        }
    }
}
