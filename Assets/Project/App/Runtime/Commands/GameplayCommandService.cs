using System;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands
{
    public sealed class GameplayCommandService :
        IGameplayCommandGateway,
        IGameplayCommandEvents
    {
        readonly GameplaySessionRuntime session;
        readonly SessionCommandScope scope;
        readonly InventoryCommandHandler inventory;
        readonly FleetCargoCommandHandler fleetCargo;
        readonly FleetProcessingCommandHandler fleetProcessing;

        public GameplayCommandService(GameplaySessionRuntime session)
        {
            this.session = session;
            scope = new SessionCommandScope(session);
            inventory = new InventoryCommandHandler(scope);
            fleetCargo = new FleetCargoCommandHandler(scope);
            fleetProcessing = new FleetProcessingCommandHandler(scope);
        }

        public GameplaySessionRuntime Session => session;

        public event Action<InventoryItemDefinition, int> ItemAcquired;
        public event Action<CargoTransferReceipt> CargoTransferCompleted;
        public event Action<FleetProcessingResult> FleetProcessingCompleted;

        public ResourceHarvestResult CanHarvest(ResourceHarvestRequest request)
        {
            return inventory.CanHarvest(request);
        }

        public ResourceHarvestResult TryHarvest(ResourceHarvestRequest request)
        {
            inventory.TryResolveDestination(request, out IInventoryContainer destination);
            InventoryItemDefinition item = ResolveHarvestItem(request);
            int quantityBefore = item != null && destination != null
                ? destination.Count(item)
                : 0;
            ResourceHarvestResult result = inventory.TryHarvest(request);
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
            return fleetCargo.CanLoadAssignedShuttleCargo(request);
        }

        public CargoTransferResult TryLoadAssignedShuttleCargo(CargoTransferRequest request)
        {
            ShuttleCargoInventory cargo = fleetCargo.ResolveAddressedCargo(request);
            CargoTransferResult result =
                fleetCargo.TryLoadAssignedShuttleCargo(request);
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
            return fleetCargo.CanUnloadAssignedShuttleCargo(request);
        }

        public CargoTransferResult TryUnloadAssignedShuttleCargo(CargoTransferRequest request)
        {
            ShuttleCargoInventory cargo = fleetCargo.ResolveAddressedCargo(request);
            CargoTransferResult result =
                fleetCargo.TryUnloadAssignedShuttleCargo(request);
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
            return fleetProcessing.CanExecute(request);
        }

        public FleetProcessingResult TryProcessFleetRecipe(FleetProcessingRequest request)
        {
            FleetProcessingResult result = fleetProcessing.TryExecute(request);
            FleetProcessingCompleted?.Invoke(result);
            return result;
        }

        public bool Matches(GameplaySessionRuntime candidate)
        {
            return ReferenceEquals(session, candidate);
        }

        InventoryItemDefinition ResolveHarvestItem(ResourceHarvestRequest request)
        {
            return scope.TryResolveDeposit(
                request.DepositId,
                out ResourceNodeInteractable node)
                ? node.Definition?.YieldedItem
                : null;
        }
    }
}
