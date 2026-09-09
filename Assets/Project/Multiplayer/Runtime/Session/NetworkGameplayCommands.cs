using Farion.Core.Identity;
using Farion.Core.Physics;
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
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using Farion.Multiplayer.Spacecraft;

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
        [Tooltip("Minimum seconds between full session-state snapshot requests from one client.")]
        [SerializeField, Min(0f)] float sessionStateRequestInterval = 1f;

        const float HarvestEyeHeight = 1.4f;

        NetworkSessionPlayer sessionPlayer;
        bool commandInFlight;
        double lastSessionStateRequestTime = double.NegativeInfinity;
        NetworkExplorerController ownerExplorer;
        MultiplayerSceneContext ownerContext;
        InventoryContainerComponent ownerInventory;
        NetworkStarterShuttle ownerCargo;

        public event Action<InventoryItemDefinition, int> ItemAcquired;
        public event Action<ResourceHarvestResult> HarvestCompleted;
        public event Action<CargoTransferReceipt> CargoTransferCompleted;
        public event Action<FleetProcessingResult> FleetProcessingCompleted;
        public event Action<ShuttleRecallResult> ShuttleRecallCompleted;

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
            ownerContext = MultiplayerSceneContext.FindIn(player.gameObject.scene);
            ownerInventory =
                player.GetComponentInChildren<InventoryContainerComponent>(true);
            ownerCargo = null;
            commandInFlight = false;
            PlayerInteractionRaycaster raycaster =
                player.GetComponentInChildren<PlayerInteractionRaycaster>(true);
            raycaster?.SetCommandGateway(this);
            RequestSessionStateServerRpc();
        }

        public bool IsAssignedShuttleCargo(PersistentEntityId cargoId)
        {
            ShuttleCargoInventory cargo = ResolveOwnerCargo();
            return cargo != null &&
                ownerInventory != null &&
                cargoId.IsValid &&
                cargo.ContainerId == cargoId;
        }

        GameplayRuntimeBindings ZoneBindings
        {
            get
            {
                MultiplayerSceneContext context = ownerContext != null
                    ? ownerContext
                    : MultiplayerSceneContext.Active;
                return context != null && context.RuntimeRoot != null
                    ? context.RuntimeRoot.Bindings
                    : null;
            }
        }

        GameplayDefinitionRegistry ZoneDefinitions => ZoneBindings?.Definitions;

        FleetStorageInventory ZoneFleetStorage => ZoneBindings?.FleetStorage;

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

            if (commandInFlight)
            {
                return ResourceHarvestResult.Pending;
            }

            ResourceHarvestResult validation = CanHarvest(request);
            if (validation != ResourceHarvestResult.Succeeded)
            {
                HarvestCompleted?.Invoke(validation);
                return validation;
            }

            commandInFlight = true;
            HarvestServerRpc(
                request.DepositId.Value,
                request.ExpectedDestinationRevision);
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
            if (!IsOwner || commandInFlight)
            {
                return CargoTransferResult.Pending;
            }

            CargoTransferResult validation = CanLoadAssignedShuttleCargo(request);
            if (validation != CargoTransferResult.Succeeded)
            {
                RaiseLocalCargoResult(CargoTransferKind.LoadShuttle, validation);
                return validation;
            }

            commandInFlight = true;
            LoadShuttleCargoServerRpc(
                ownerInventory.Revision,
                request.ExpectedShuttleCargoRevision);
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
            if (!IsOwner || commandInFlight)
            {
                return CargoTransferResult.Pending;
            }

            CargoTransferResult validation = CanUnloadAssignedShuttleCargo(request);
            if (validation != CargoTransferResult.Succeeded)
            {
                RaiseLocalCargoResult(CargoTransferKind.UnloadToFleet, validation);
                return validation;
            }

            commandInFlight = true;
            UnloadShuttleCargoServerRpc(
                request.ExpectedShuttleCargoRevision,
                ZoneFleetStorage.Revision);
            return CargoTransferResult.Pending;
        }

        public bool RequestShuttleRecall()
        {
            if (!IsOwner || commandInFlight)
            {
                return false;
            }

            commandInFlight = true;
            RecallShuttleServerRpc();
            return true;
        }

        [ServerRpc]
        void RecallShuttleServerRpc(NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsActive || sender.ClientId != OwnerId)
            {
                return;
            }

            MultiplayerPlayerSpawner spawner = sessionPlayer.PlayerSpawner;
            ShuttleRecallResult result = ShuttleRecallResult.Failed;
            if (spawner != null)
            {
                spawner.TryRecallStarterShuttle(
                    sessionPlayer,
                    spawner.GetComponent<MultiplayerZoneCoordinator>(),
                    out result);
            }

            ShuttleRecallResultTargetRpc(sender, (byte)result);
        }

        [TargetRpc]
        void ShuttleRecallResultTargetRpc(NetworkConnection connection, byte result)
        {
            commandInFlight = false;
            ownerCargo = null;
            ShuttleRecallCompleted?.Invoke((ShuttleRecallResult)result);
        }

        void RaiseLocalCargoResult(CargoTransferKind kind, CargoTransferResult result)
        {
            ShuttleCargoInventory cargo = ResolveOwnerCargo();
            CargoTransferCompleted?.Invoke(
                new CargoTransferReceipt(
                    kind,
                    result,
                    kind == CargoTransferKind.LoadShuttle
                        ? ownerInventory?.CaptureContainerSnapshot()
                        : cargo?.CaptureContainerSnapshot(),
                    kind == CargoTransferKind.LoadShuttle
                        ? cargo?.CaptureContainerSnapshot()
                        : ZoneFleetStorage?.CaptureContainerSnapshot()));
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
            if (!IsOwner || commandInFlight)
            {
                return FleetProcessingResult.Pending;
            }

            FleetProcessingResult validation = CanProcessFleetRecipe(request);
            if (validation != FleetProcessingResult.Succeeded)
            {
                FleetProcessingCompleted?.Invoke(validation);
                return validation;
            }

            commandInFlight = true;
            ProcessFleetRecipeServerRpc(
                request.RecipeId.Value,
                request.ExpectedFleetStorageRevision);
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
            GeneratedEntityId assigned = sessionPlayer != null
                ? sessionPlayer.AssignedStarterShuttleId
                : GeneratedEntityId.None;
            if (!assigned.IsValid)
            {
                return null;
            }

            if (ownerCargo != null && ownerCargo.EntityId == assigned)
            {
                return ownerCargo.Cargo;
            }

            ownerCargo = NetworkStarterShuttle.FindByEntityId(assigned);
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
            long expectedDestinationRevision,
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

            if (destinationInventory.Revision != expectedDestinationRevision)
            {
                HarvestResultTargetRpc(
                    sender,
                    (byte)ResourceHarvestResult.StaleDestination,
                    string.Empty,
                    0,
                    JsonUtility.ToJson(destinationInventory.CaptureContainerSnapshot()));
                return;
            }

            GeneratedEntityId resolvedDepositId = new(depositId);
            int carriedBefore = 0;
            if (!TryResolveHarvestNode(
                    bindings,
                    explorer.transform.position,
                    resolvedDepositId,
                    maximumHarvestDistance,
                    out ResourceNodeInteractable node) ||
                !HasLineOfSight(
                    explorer.gameObject.scene.GetPhysicsScene(),
                    explorer.transform.position + explorer.transform.up * HarvestEyeHeight,
                    node))
            {
                HarvestResultTargetRpc(
                    sender,
                    (byte)ResourceHarvestResult.MissingSource,
                    string.Empty,
                    0,
                    string.Empty);
                return;
            }

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
        void LoadShuttleCargoServerRpc(
            long expectedExplorerRevision,
            long expectedCargoRevision,
            NetworkConnection sender = null)
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

            if (explorerInventory.Revision != expectedExplorerRevision ||
                cargo.Revision != expectedCargoRevision)
            {
                ReportCargoFailure(
                    sender,
                    CargoTransferKind.LoadShuttle,
                    CargoTransferResult.StaleState);
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
        void UnloadShuttleCargoServerRpc(
            long expectedCargoRevision,
            long expectedStorageRevision,
            NetworkConnection sender = null)
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

            if (cargo.Revision != expectedCargoRevision ||
                storage.Revision != expectedStorageRevision)
            {
                ReportCargoFailure(
                    sender,
                    CargoTransferKind.UnloadToFleet,
                    CargoTransferResult.StaleState);
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
            long expectedStorageRevision,
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

            if (bindings.FleetStorage.Revision != expectedStorageRevision)
            {
                FleetProcessingResultTargetRpc(
                    sender,
                    (byte)FleetProcessingResult.StaleStorage);
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
            commandInFlight = false;
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
                !sessionPlayer.AssignedStarterShuttleId.IsValid ||
                !TryResolveServerBindings(out GameplayRuntimeBindings bindings) ||
                !sessionPlayer.PlayerSpawner.TryGetSpawnedExplorer(
                    sessionPlayer,
                    out NetworkExplorerController explorer))
            {
                return false;
            }

            NetworkStarterShuttle ship = NetworkStarterShuttle.FindByEntityId(
                sessionPlayer.AssignedStarterShuttleId);
            if (ship == null ||
                ship.gameObject.scene != explorer.gameObject.scene)
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

        Transform ResolveFleetTransform()
        {
            if (IsServerStarted)
            {
                MultiplayerPlayerSpawner spawner = sessionPlayer != null
                    ? sessionPlayer.PlayerSpawner
                    : null;
                return spawner != null &&
                    spawner.TryGetStartingZoneBindings(
                        out GameplayRuntimeBindings bindings) &&
                    bindings.Fleet != null
                        ? bindings.Fleet.transform
                        : null;
            }

            return ZoneBindings?.Fleet != null
                ? ZoneBindings.Fleet.transform
                : null;
        }

        bool IsShipDocked(Transform shipTransform)
        {
            Transform fleet = ResolveFleetTransform();
            return shipTransform != null &&
                fleet != null &&
                shipTransform.gameObject.scene == fleet.gameObject.scene &&
                IsWithinHarvestDistance(
                    shipTransform.position,
                    fleet.position,
                    maximumDockingDistance);
        }

        bool IsExplorerNearFleet(NetworkExplorerController explorer)
        {
            Transform fleet = ResolveFleetTransform();
            return explorer != null &&
                fleet != null &&
                explorer.gameObject.scene == fleet.gameObject.scene &&
                IsWithinHarvestDistance(
                    explorer.transform.position,
                    fleet.position,
                    maximumDockingDistance);
        }

        bool TryResolveServerBindings(out GameplayRuntimeBindings bindings)
        {
            bindings = null;
            MultiplayerPlayerSpawner spawner = sessionPlayer.PlayerSpawner;
            return spawner != null &&
                spawner.TryGetStartingZoneBindings(out bindings);
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
            commandInFlight = false;
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

        void ApplyCargoSnapshot(string cargoContainerId, string snapshotJson)
        {
            IReadOnlyList<NetworkStarterShuttle> ships = NetworkStarterShuttle.ActiveShips;
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

        void ApplySnapshot(
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
            MultiplayerPlayerSpawner spawner = sessionPlayer.PlayerSpawner;
            double now = Time.unscaledTimeAsDouble;
            if (now - lastSessionStateRequestTime < sessionStateRequestInterval)
            {
                return;
            }

            lastSessionStateRequestTime = now;
            if (sender == null ||
                !sender.IsActive ||
                sender.ClientId != OwnerId ||
                spawner == null ||
                !spawner.TryGetSpawnedExplorer(
                    sessionPlayer,
                    out NetworkExplorerController explorer) ||
                !spawner.TryGetRuntimeBindings(
                    explorer.gameObject.scene,
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

            if (spawner.TryGetStartingZoneBindings(
                    out GameplayRuntimeBindings startingBindings) &&
                startingBindings.FleetStorage != null)
            {
                FleetStorageSnapshotTargetRpc(
                    sender,
                    JsonUtility.ToJson(
                        startingBindings.FleetStorage.CaptureContainerSnapshot()));
            }

            InventoryContainerComponent carried =
                explorer.GetComponentInChildren<InventoryContainerComponent>(true);
            if (carried != null)
            {
                OwnerInventorySnapshotTargetRpc(
                    sender,
                    JsonUtility.ToJson(carried.CaptureContainerSnapshot()));
            }

            IReadOnlyList<NetworkStarterShuttle> ships = NetworkStarterShuttle.ActiveShips;
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
            commandInFlight = false;
            if (!IsServerStarted)
            {
                ApplySnapshot(ownerInventory, snapshotJson);
            }

            ResourceHarvestResult resolved = (ResourceHarvestResult)result;
            if (resolved == ResourceHarvestResult.Succeeded)
            {
                RaiseItemAcquired(itemId, acquiredAmount);
            }

            HarvestCompleted?.Invoke(resolved);
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
            MultiplayerPlayerSpawner spawner = sessionPlayer.PlayerSpawner;
            if (spawner == null ||
                !spawner.TryGetSpawnedExplorer(
                    sessionPlayer,
                    out explorer) ||
                !spawner.TryGetRuntimeBindings(
                    explorer.gameObject.scene,
                    out bindings))
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
                streamers[i].ApplyAuthoritativeDelta(depositId, extractedAmount);
            }
        }

        static bool HasLineOfSight(
            PhysicsScene physics,
            Vector3 eye,
            ResourceNodeInteractable node)
        {
            Collider target = node.GetComponentInChildren<Collider>();
            Vector3 point = target != null ? target.ClosestPoint(eye) : node.transform.position;
            point += (eye - point).normalized * 0.02f;
            Vector3 toPoint = point - eye;
            float distance = toPoint.magnitude;
            return distance <= 0.001f ||
                   !physics.Raycast(
                       eye,
                       toPoint / distance,
                       out RaycastHit hit,
                       distance,
                       FarionLayers.CameraObstacleMask,
                       QueryTriggerInteraction.Ignore) ||
                   hit.collider.transform.IsChildOf(node.transform);
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
