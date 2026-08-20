using System;
using Farion.Gameplay.Inventory;

namespace Farion.Gameplay.Commands
{
    public interface IGameplayCommandEvents
    {
        event Action<InventoryItemDefinition, int> ItemAcquired;
        event Action<CargoTransferReceipt> CargoTransferCompleted;
        event Action<FleetProcessingResult> FleetProcessingCompleted;
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
        ResourceHarvestResult CanHarvest(ResourceHarvestRequest request);

        ResourceHarvestResult TryHarvest(ResourceHarvestRequest request);

        CargoTransferResult CanLoadAssignedShuttleCargo(CargoTransferRequest request);

        CargoTransferResult TryLoadAssignedShuttleCargo(CargoTransferRequest request);

        CargoTransferResult CanUnloadAssignedShuttleCargo(CargoTransferRequest request);

        CargoTransferResult TryUnloadAssignedShuttleCargo(CargoTransferRequest request);

        FleetProcessingResult CanProcessFleetRecipe(FleetProcessingRequest request);

        FleetProcessingResult TryProcessFleetRecipe(FleetProcessingRequest request);
    }
}
