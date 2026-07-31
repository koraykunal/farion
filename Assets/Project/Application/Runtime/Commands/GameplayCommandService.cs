using Farion.App.Commands.Handlers;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Processing;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands
{
    public sealed class GameplayCommandService : IGameplayCommandGateway
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
            return inventory.TryHarvest(source, destination);
        }

        public CargoTransferResult CanLoadAssignedShuttleCargo(
            ShuttleCargoInventory destination)
        {
            return fleetCargo.CanLoadAssignedShuttleCargo(destination);
        }

        public CargoTransferResult TryLoadAssignedShuttleCargo(
            ShuttleCargoInventory destination)
        {
            return fleetCargo.TryLoadAssignedShuttleCargo(destination);
        }

        public CargoTransferResult CanUnloadAssignedShuttleCargo(
            ShuttleCargoInventory source)
        {
            return fleetCargo.CanUnloadAssignedShuttleCargo(source);
        }

        public CargoTransferResult TryUnloadAssignedShuttleCargo(
            ShuttleCargoInventory source)
        {
            return fleetCargo.TryUnloadAssignedShuttleCargo(source);
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
