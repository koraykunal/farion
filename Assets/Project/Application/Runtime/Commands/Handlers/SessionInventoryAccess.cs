using Farion.Gameplay.Inventory;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands.Handlers
{
    internal sealed class SessionInventoryAccess
    {
        readonly GameplaySessionRuntime session;

        public SessionInventoryAccess(GameplaySessionRuntime session)
        {
            this.session = session;
        }

        public InventoryContainerComponent LocalInventory =>
            session?.LocalInventory;

        public FleetStorageInventory FleetStorage =>
            session?.Fleet?.Storage;

        public bool Owns(IInventoryContainer inventory)
        {
            if (session == null || inventory == null)
            {
                return false;
            }

            if (OwnsLocalInventory(inventory))
            {
                return true;
            }

            return inventory is ShuttleCargoInventory shuttleCargo &&
                   OwnsAssignedShuttleCargo(shuttleCargo);
        }

        public bool OwnsLocalInventory(IInventoryContainer inventory)
        {
            return session != null &&
                   inventory != null &&
                   ReferenceEquals(session.LocalInventory, inventory) &&
                   inventory.ContainerId == session.Identity.CarriedInventoryId;
        }

        public bool OwnsAssignedShuttleCargo(
            ShuttleCargoInventory inventory)
        {
            ShuttleRuntimeBinding shuttle = session?.ShuttleBinding;
            return shuttle != null &&
                   inventory != null &&
                   ReferenceEquals(shuttle.Cargo, inventory) &&
                   inventory.ContainerId == shuttle.Cargo.ContainerId &&
                   shuttle.ShipId == session.Identity.AssignedShuttleId;
        }

        public bool OwnsFleetStorage(FleetStorageInventory inventory)
        {
            FleetRuntime fleet = session?.Fleet;
            return fleet != null &&
                   inventory != null &&
                   ReferenceEquals(fleet.Storage, inventory) &&
                   inventory.ContainerId == fleet.Storage.ContainerId &&
                   fleet.FleetId == session.Identity.FleetId;
        }
    }
}
