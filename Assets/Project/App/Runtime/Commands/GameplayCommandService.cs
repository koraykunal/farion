using System;
using System.Collections.Generic;
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
using Farion.Gameplay.Ships;

namespace Farion.App.Commands
{
    public sealed class GameplayCommandService :
        IGameplayCommandGateway,
        IGameplayCommandEvents
    {
        readonly GameplaySessionRuntime session;

        public GameplayCommandService(GameplaySessionRuntime session)
        {
            this.session = session;
        }

        public event Action<InventoryItemDefinition, int> ItemAcquired;
        public event Action<CargoTransferReceipt> CargoTransferCompleted;
        public event Action<FleetProcessingResult> FleetProcessingCompleted;

        InventoryContainerComponent LocalInventory => session?.LocalInventory;
        FleetStorageInventory FleetStorage => session?.Fleet?.Storage;
        ShuttleCargoInventory AssignedShuttleCargo => session?.ShuttleBinding?.Cargo;

        public ResourceHarvestResult CanHarvest(ResourceHarvestRequest request)
        {
            return TryResolveHarvest(
                request,
                out ResourceNodeInteractable source,
                out InventoryContainerComponent destination,
                out ResourceHarvestResult failure)
                ? ResourceHarvestTransaction.CanExecute(source, destination)
                : failure;
        }

        public ResourceHarvestResult TryHarvest(ResourceHarvestRequest request)
        {
            TryResolveOwnedContainer(
                request.DestinationContainerId,
                out InventoryContainerComponent destination);
            InventoryItemDefinition item = ResolveHarvestItem(request);
            int quantityBefore = item != null && destination != null
                ? destination.Count(item)
                : 0;
            ResourceHarvestResult result = TryResolveHarvest(
                request,
                out ResourceNodeInteractable source,
                out InventoryContainerComponent resolvedDestination,
                out ResourceHarvestResult failure)
                ? ResourceHarvestTransaction.TryExecute(source, resolvedDestination)
                : failure;
            int acquired = result == ResourceHarvestResult.Succeeded && item != null
                ? destination.Count(item) - quantityBefore
                : 0;
            if (acquired > 0)
            {
                ItemAcquired?.Invoke(item, acquired);
            }

            return result;
        }

        public CargoTransferResult CanLoadAssignedShuttleCargo(CargoTransferRequest request)
        {
            return TryResolveLoad(
                request,
                out InventoryContainerComponent source,
                out ShuttleCargoInventory destination,
                out CargoTransferResult failure)
                ? CargoTransferTransaction.CanExecute(source, destination)
                : failure;
        }

        public CargoTransferResult TryLoadAssignedShuttleCargo(CargoTransferRequest request)
        {
            ShuttleCargoInventory cargo = ResolveAddressedCargo(request);
            CargoTransferResult result = TryResolveLoad(
                request,
                out InventoryContainerComponent source,
                out ShuttleCargoInventory destination,
                out CargoTransferResult failure)
                ? CargoTransferTransaction.TryExecute(source, destination)
                : failure;
            CargoTransferCompleted?.Invoke(
                new CargoTransferReceipt(
                    CargoTransferKind.LoadShuttle,
                    result,
                    session.LocalInventory?.CaptureContainerSnapshot(),
                    cargo?.CaptureContainerSnapshot()));
            return result;
        }

        public CargoTransferResult CanUnloadAssignedShuttleCargo(CargoTransferRequest request)
        {
            return TryResolveUnload(
                request,
                out ShuttleCargoInventory source,
                out FleetStorageInventory destination,
                out CargoTransferResult failure)
                ? CargoTransferTransaction.CanExecute(source, destination)
                : failure;
        }

        public CargoTransferResult TryUnloadAssignedShuttleCargo(CargoTransferRequest request)
        {
            ShuttleCargoInventory cargo = ResolveAddressedCargo(request);
            CargoTransferResult result = TryResolveUnload(
                request,
                out ShuttleCargoInventory source,
                out FleetStorageInventory destination,
                out CargoTransferResult failure)
                ? CargoTransferTransaction.TryExecute(source, destination)
                : failure;
            CargoTransferCompleted?.Invoke(
                new CargoTransferReceipt(
                    CargoTransferKind.UnloadToFleet,
                    result,
                    cargo?.CaptureContainerSnapshot(),
                    session.FleetStorage?.CaptureContainerSnapshot()));
            return result;
        }

        public FleetProcessingResult CanProcessFleetRecipe(FleetProcessingRequest request)
        {
            return TryResolveProcessing(
                request,
                out ProcessingRecipeDefinition recipe,
                out FleetStorageInventory storage,
                out FleetProcessingResult failure)
                ? storage.CanExchange(recipe.Inputs, recipe.Outputs)
                    ? FleetProcessingResult.Succeeded
                    : FleetProcessingResult.Rejected
                : failure;
        }

        public FleetProcessingResult TryProcessFleetRecipe(FleetProcessingRequest request)
        {
            FleetProcessingResult result = TryResolveProcessing(
                request,
                out ProcessingRecipeDefinition recipe,
                out FleetStorageInventory storage,
                out FleetProcessingResult failure)
                ? storage.TryExchange(recipe.Inputs, recipe.Outputs)
                    ? FleetProcessingResult.Succeeded
                    : FleetProcessingResult.Rejected
                : failure;
            FleetProcessingCompleted?.Invoke(result);
            return result;
        }

        public bool Matches(GameplaySessionRuntime candidate)
        {
            return ReferenceEquals(session, candidate);
        }

        InventoryItemDefinition ResolveHarvestItem(ResourceHarvestRequest request)
        {
            return TryResolveDeposit(
                request.DepositId,
                out ResourceNodeInteractable node)
                ? node.Definition?.YieldedItem
                : null;
        }

        bool TryResolveHarvest(
            ResourceHarvestRequest request,
            out ResourceNodeInteractable source,
            out InventoryContainerComponent destination,
            out ResourceHarvestResult failure)
        {
            source = null;
            destination = null;

            if (!TryResolveDeposit(request.DepositId, out source))
            {
                failure = ResourceHarvestResult.MissingSource;
                return false;
            }

            if (!request.DestinationContainerId.IsValid)
            {
                failure = ResourceHarvestResult.MissingDestination;
                return false;
            }

            if (!TryResolveOwnedContainer(
                    request.DestinationContainerId,
                    out destination))
            {
                failure = ResourceHarvestResult.UnauthorizedDestination;
                return false;
            }

            if (!RevisionMatches(destination, request.ExpectedDestinationRevision))
            {
                failure = ResourceHarvestResult.StaleDestination;
                return false;
            }

            failure = ResourceHarvestResult.Succeeded;
            return true;
        }

        bool TryResolveLoad(
            CargoTransferRequest request,
            out InventoryContainerComponent source,
            out ShuttleCargoInventory destination,
            out CargoTransferResult failure)
        {
            destination = null;
            if (!TryResolveOwnedLocalInventory(out source))
            {
                failure = CargoTransferResult.UnauthorizedSource;
                return false;
            }

            if (!request.IsAddressed)
            {
                failure = CargoTransferResult.MissingDestination;
                return false;
            }

            if (!TryResolveAssignedShuttleCargo(
                    request.ShuttleCargoId,
                    out destination))
            {
                failure = CargoTransferResult.UnauthorizedDestination;
                return false;
            }

            if (!RevisionMatches(destination, request.ExpectedShuttleCargoRevision))
            {
                failure = CargoTransferResult.StaleState;
                return false;
            }

            failure = CargoTransferResult.Succeeded;
            return true;
        }

        bool TryResolveUnload(
            CargoTransferRequest request,
            out ShuttleCargoInventory source,
            out FleetStorageInventory destination,
            out CargoTransferResult failure)
        {
            source = null;
            destination = null;

            if (!request.IsAddressed)
            {
                failure = CargoTransferResult.MissingSource;
                return false;
            }

            if (!TryResolveAssignedShuttleCargo(
                    request.ShuttleCargoId,
                    out source))
            {
                failure = CargoTransferResult.UnauthorizedSource;
                return false;
            }

            if (!RevisionMatches(source, request.ExpectedShuttleCargoRevision))
            {
                failure = CargoTransferResult.StaleState;
                return false;
            }

            if (!TryResolveOwnedFleetStorage(out destination))
            {
                failure = FleetStorage == null
                    ? CargoTransferResult.MissingDestination
                    : CargoTransferResult.UnauthorizedDestination;
                return false;
            }

            failure = CargoTransferResult.Succeeded;
            return true;
        }

        bool TryResolveProcessing(
            FleetProcessingRequest request,
            out ProcessingRecipeDefinition recipe,
            out FleetStorageInventory storage,
            out FleetProcessingResult failure)
        {
            recipe = null;
            storage = null;

            if (!request.RecipeId.IsValid)
            {
                failure = FleetProcessingResult.MissingRecipe;
                return false;
            }

            GameplayDefinitionRegistry definitions = session?.Bindings?.Definitions;
            if (definitions == null ||
                !definitions.TryGetProcessingRecipe(request.RecipeId, out recipe))
            {
                failure = FleetProcessingResult.MissingRecipe;
                return false;
            }

            if (!recipe.IsValid)
            {
                failure = FleetProcessingResult.InvalidRecipe;
                return false;
            }

            if (FleetStorage == null)
            {
                failure = FleetProcessingResult.MissingStorage;
                return false;
            }

            if (!TryResolveOwnedFleetStorage(out storage) ||
                storage.ContainerId != request.FleetStorageId)
            {
                failure = FleetProcessingResult.UnauthorizedStorage;
                return false;
            }

            if (!RevisionMatches(storage, request.ExpectedFleetStorageRevision))
            {
                failure = FleetProcessingResult.StaleStorage;
                return false;
            }

            failure = FleetProcessingResult.Succeeded;
            return true;
        }

        ShuttleCargoInventory ResolveAddressedCargo(CargoTransferRequest request)
        {
            return TryResolveAssignedShuttleCargo(
                request.ShuttleCargoId,
                out ShuttleCargoInventory cargo)
                ? cargo
                : null;
        }

        bool TryResolveOwnedContainer(
            PersistentEntityId containerId,
            out InventoryContainerComponent container)
        {
            container = null;
            if (!containerId.IsValid)
            {
                return false;
            }

            InventoryContainerComponent local = LocalInventory;
            if (local != null &&
                local.ContainerId == containerId &&
                OwnsLocalInventory(local))
            {
                container = local;
                return true;
            }

            ShuttleCargoInventory cargo = AssignedShuttleCargo;
            if (cargo != null &&
                cargo.ContainerId == containerId &&
                OwnsAssignedShuttleCargo(cargo))
            {
                container = cargo;
                return true;
            }

            return false;
        }

        bool TryResolveAssignedShuttleCargo(
            PersistentEntityId containerId,
            out ShuttleCargoInventory cargo)
        {
            cargo = AssignedShuttleCargo;
            if (cargo == null ||
                !containerId.IsValid ||
                cargo.ContainerId != containerId ||
                !OwnsAssignedShuttleCargo(cargo))
            {
                cargo = null;
                return false;
            }

            return true;
        }

        bool TryResolveOwnedFleetStorage(out FleetStorageInventory storage)
        {
            storage = FleetStorage;
            if (storage == null || !OwnsFleetStorage(storage))
            {
                storage = null;
                return false;
            }

            return true;
        }

        bool TryResolveOwnedLocalInventory(out InventoryContainerComponent inventory)
        {
            inventory = LocalInventory;
            if (inventory == null || !OwnsLocalInventory(inventory))
            {
                inventory = null;
                return false;
            }

            return true;
        }

        bool TryResolveDeposit(
            GeneratedEntityId depositId,
            out ResourceNodeInteractable node)
        {
            node = null;
            IReadOnlyList<ResourceDepositRuntimeSpawner> streamers =
                session?.Bindings?.ResourceStreamers;
            if (streamers == null || !depositId.IsValid)
            {
                return false;
            }

            for (int i = 0; i < streamers.Count; i++)
            {
                if (streamers[i] != null &&
                    streamers[i].TryGetSpawnedNode(depositId, out node))
                {
                    return true;
                }
            }

            return false;
        }

        static bool RevisionMatches(
            InventoryContainerComponent container,
            long expectedRevision)
        {
            return container != null && container.Revision == expectedRevision;
        }

        bool OwnsLocalInventory(InventoryContainerComponent inventory)
        {
            return session != null &&
                   inventory != null &&
                   ReferenceEquals(session.LocalInventory, inventory) &&
                   inventory.ContainerId == session.Identity.CarriedInventoryId;
        }

        bool OwnsAssignedShuttleCargo(ShuttleCargoInventory inventory)
        {
            ShuttleRuntimeBinding shuttle = session?.ShuttleBinding;
            return shuttle != null &&
                   inventory != null &&
                   ReferenceEquals(shuttle.Cargo, inventory) &&
                   shuttle.ShipId == session.Identity.AssignedShuttleId;
        }

        bool OwnsFleetStorage(FleetStorageInventory inventory)
        {
            FleetRuntime fleet = session?.Fleet;
            return fleet != null &&
                   inventory != null &&
                   ReferenceEquals(fleet.Storage, inventory) &&
                   fleet.FleetId == session.Identity.FleetId;
        }
    }
}
