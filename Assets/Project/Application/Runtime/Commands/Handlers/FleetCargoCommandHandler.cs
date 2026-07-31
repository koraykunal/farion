using Farion.Gameplay.Commands;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands.Handlers
{
    internal sealed class FleetCargoCommandHandler
    {
        readonly SessionInventoryAccess access;

        public FleetCargoCommandHandler(SessionInventoryAccess access)
        {
            this.access = access;
        }

        public CargoTransferResult CanLoadAssignedShuttleCargo(
            ShuttleCargoInventory destination)
        {
            if (!TryResolveAuthorizedLoad(
                    destination,
                    out InventoryContainerComponent source,
                    out CargoTransferResult failure))
            {
                return failure;
            }

            return CargoTransferTransaction.CanExecute(
                source,
                destination);
        }

        public CargoTransferResult TryLoadAssignedShuttleCargo(
            ShuttleCargoInventory destination)
        {
            if (!TryResolveAuthorizedLoad(
                    destination,
                    out InventoryContainerComponent source,
                    out CargoTransferResult failure))
            {
                return failure;
            }

            return CargoTransferTransaction.TryExecute(
                source,
                destination);
        }

        public CargoTransferResult CanUnloadAssignedShuttleCargo(
            ShuttleCargoInventory source)
        {
            if (!TryResolveAuthorizedUnload(
                    source,
                    out FleetStorageInventory destination,
                    out CargoTransferResult failure))
            {
                return failure;
            }

            return CargoTransferTransaction.CanExecute(source, destination);
        }

        public CargoTransferResult TryUnloadAssignedShuttleCargo(
            ShuttleCargoInventory source)
        {
            if (!TryResolveAuthorizedUnload(
                    source,
                    out FleetStorageInventory destination,
                    out CargoTransferResult failure))
            {
                return failure;
            }

            return CargoTransferTransaction.TryExecute(source, destination);
        }

        bool TryResolveAuthorizedLoad(
            ShuttleCargoInventory requestedDestination,
            out InventoryContainerComponent source,
            out CargoTransferResult failure)
        {
            source = access.LocalInventory;
            if (source == null)
            {
                failure = CargoTransferResult.MissingSource;
                return false;
            }

            if (requestedDestination == null)
            {
                failure = CargoTransferResult.MissingDestination;
                return false;
            }

            if (!access.OwnsLocalInventory(source))
            {
                failure = CargoTransferResult.UnauthorizedSource;
                return false;
            }

            if (!access.OwnsAssignedShuttleCargo(requestedDestination))
            {
                failure = CargoTransferResult.UnauthorizedDestination;
                return false;
            }

            failure = CargoTransferResult.Succeeded;
            return true;
        }

        bool TryResolveAuthorizedUnload(
            ShuttleCargoInventory requestedSource,
            out FleetStorageInventory destination,
            out CargoTransferResult failure)
        {
            destination = access.FleetStorage;
            if (requestedSource == null)
            {
                failure = CargoTransferResult.MissingSource;
                return false;
            }

            if (destination == null)
            {
                failure = CargoTransferResult.MissingDestination;
                return false;
            }

            if (!access.OwnsAssignedShuttleCargo(requestedSource))
            {
                failure = CargoTransferResult.UnauthorizedSource;
                return false;
            }

            if (!access.OwnsFleetStorage(destination))
            {
                failure = CargoTransferResult.UnauthorizedDestination;
                return false;
            }

            failure = CargoTransferResult.Succeeded;
            return true;
        }
    }
}
