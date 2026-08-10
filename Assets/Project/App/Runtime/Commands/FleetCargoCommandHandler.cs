using Farion.Gameplay.Commands;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands
{
    internal sealed class FleetCargoCommandHandler
    {
        readonly SessionCommandScope scope;

        public FleetCargoCommandHandler(SessionCommandScope scope)
        {
            this.scope = scope;
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
            return TryResolveLoad(
                request,
                out InventoryContainerComponent source,
                out ShuttleCargoInventory destination,
                out CargoTransferResult failure)
                ? CargoTransferTransaction.TryExecute(source, destination)
                : failure;
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
            return TryResolveUnload(
                request,
                out ShuttleCargoInventory source,
                out FleetStorageInventory destination,
                out CargoTransferResult failure)
                ? CargoTransferTransaction.TryExecute(source, destination)
                : failure;
        }

        public ShuttleCargoInventory ResolveAddressedCargo(CargoTransferRequest request)
        {
            return scope.TryResolveAssignedShuttleCargo(
                request.ShuttleCargoId,
                out ShuttleCargoInventory cargo)
                ? cargo
                : null;
        }

        bool TryResolveLoad(
            CargoTransferRequest request,
            out InventoryContainerComponent source,
            out ShuttleCargoInventory destination,
            out CargoTransferResult failure)
        {
            destination = null;
            if (!scope.TryResolveOwnedLocalInventory(out source))
            {
                failure = CargoTransferResult.UnauthorizedSource;
                return false;
            }

            if (!request.IsAddressed)
            {
                failure = CargoTransferResult.MissingDestination;
                return false;
            }

            if (!scope.TryResolveAssignedShuttleCargo(
                    request.ShuttleCargoId,
                    out destination))
            {
                failure = CargoTransferResult.UnauthorizedDestination;
                return false;
            }

            if (!SessionCommandScope.RevisionMatches(
                    destination,
                    request.ExpectedShuttleCargoRevision))
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

            if (!scope.TryResolveAssignedShuttleCargo(
                    request.ShuttleCargoId,
                    out source))
            {
                failure = CargoTransferResult.UnauthorizedSource;
                return false;
            }

            if (!SessionCommandScope.RevisionMatches(
                    source,
                    request.ExpectedShuttleCargoRevision))
            {
                failure = CargoTransferResult.StaleState;
                return false;
            }

            if (!scope.TryResolveOwnedFleetStorage(out destination))
            {
                failure = scope.FleetStorage == null
                    ? CargoTransferResult.MissingDestination
                    : CargoTransferResult.UnauthorizedDestination;
                return false;
            }

            failure = CargoTransferResult.Succeeded;
            return true;
        }
    }
}
