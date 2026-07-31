using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Processing;
using Farion.Gameplay.Ships;

namespace Farion.Gameplay.Commands
{
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
