using System;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Processing;
using Farion.Gameplay.Ships;

namespace Farion.Gameplay.Commands
{
    public interface IGameplayCommandEvents
    {
        event Action<InventoryItemDefinition, int> ItemAcquired;
        event Action<CargoTransferReceipt> CargoTransferCompleted;
    }

    public enum CargoTransferKind
    {
        LoadShuttle = 0,
        UnloadToFleet = 1
    }

    public readonly struct CargoTransferReceipt
    {
        public CargoTransferReceipt(
            CargoTransferKind kind,
            CargoTransferResult result,
            InventoryContainerSnapshot source,
            InventoryContainerSnapshot destination)
        {
            Kind = kind;
            Result = result;
            Source = source;
            Destination = destination;
        }

        public CargoTransferKind Kind { get; }
        public CargoTransferResult Result { get; }
        public InventoryContainerSnapshot Source { get; }
        public InventoryContainerSnapshot Destination { get; }
    }

    public interface IGameplayCommandGateway
    {
        ResourceHarvestResult CanHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination);

        ResourceHarvestResult TryHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination);

        CargoTransferResult CanLoadAssignedShuttleCargo(
            ShuttleCargoInventory destination);

        CargoTransferResult TryLoadAssignedShuttleCargo(
            ShuttleCargoInventory destination);

        CargoTransferResult CanUnloadAssignedShuttleCargo(
            ShuttleCargoInventory source);

        CargoTransferResult TryUnloadAssignedShuttleCargo(
            ShuttleCargoInventory source);

        FleetProcessingResult CanProcessFleetRecipe(
            ProcessingRecipeDefinition recipe);

        FleetProcessingResult TryProcessFleetRecipe(
            ProcessingRecipeDefinition recipe);
    }
}
