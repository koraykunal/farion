using System;
using Farion.App.Commands.Handlers;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Processing;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands
{
    public sealed class GameplayCommandService :
        IGameplayCommandGateway,
        IGameplayCommandEvents
    {
        readonly GameplaySessionRuntime session;
        readonly InventoryCommandHandler inventory;
        readonly FleetCargoCommandHandler fleetCargo;
        readonly FleetProcessingCommandHandler fleetProcessing;

        public GameplayCommandService(GameplaySessionRuntime session)
        {
            this.session = session;
            SessionInventoryAccess access = new(session);
            inventory = new InventoryCommandHandler(access);
            fleetCargo = new FleetCargoCommandHandler(access);
            fleetProcessing = new FleetProcessingCommandHandler(access);
        }

        public GameplaySessionRuntime Session => session;

        public event Action<InventoryItemDefinition, int> ItemAcquired;
        public event Action<CargoTransferReceipt> CargoTransferCompleted;

        public ResourceHarvestResult CanHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination)
        {
            return inventory.CanHarvest(source, destination);
        }

        public ResourceHarvestResult TryHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination)
        {
            InventoryItemDefinition item = source?.Definition?.YieldedItem;
            int quantityBefore = item != null && destination != null
                ? destination.Count(item)
                : 0;
            ResourceHarvestResult result = inventory.TryHarvest(source, destination);
            int acquired = result == ResourceHarvestResult.Succeeded && item != null
                ? destination.Count(item) - quantityBefore
                : 0;
            if (acquired > 0)
            {
                ItemAcquired?.Invoke(item, acquired);
            }

            return result;
        }

        public CargoTransferResult CanLoadAssignedShuttleCargo(
            ShuttleCargoInventory destination)
        {
            return fleetCargo.CanLoadAssignedShuttleCargo(destination);
        }

        public CargoTransferResult TryLoadAssignedShuttleCargo(
            ShuttleCargoInventory destination)
        {
            CargoTransferResult result =
                fleetCargo.TryLoadAssignedShuttleCargo(destination);
            CargoTransferCompleted?.Invoke(
                new CargoTransferReceipt(
                    CargoTransferKind.LoadShuttle,
                    result,
                    session.LocalInventory?.CaptureContainerSnapshot(),
                    destination?.CaptureContainerSnapshot()));
            return result;
        }

        public CargoTransferResult CanUnloadAssignedShuttleCargo(
            ShuttleCargoInventory source)
        {
            return fleetCargo.CanUnloadAssignedShuttleCargo(source);
        }

        public CargoTransferResult TryUnloadAssignedShuttleCargo(
            ShuttleCargoInventory source)
        {
            CargoTransferResult result =
                fleetCargo.TryUnloadAssignedShuttleCargo(source);
            CargoTransferCompleted?.Invoke(
                new CargoTransferReceipt(
                    CargoTransferKind.UnloadToFleet,
                    result,
                    source?.CaptureContainerSnapshot(),
                    session.FleetStorage?.CaptureContainerSnapshot()));
            return result;
        }

        public FleetProcessingResult CanProcessFleetRecipe(
            ProcessingRecipeDefinition recipe)
        {
            return fleetProcessing.CanExecute(recipe);
        }

        public FleetProcessingResult TryProcessFleetRecipe(
            ProcessingRecipeDefinition recipe)
        {
            return fleetProcessing.TryExecute(recipe);
        }

        public bool Matches(GameplaySessionRuntime candidate)
        {
            return ReferenceEquals(session, candidate);
        }
    }
}
