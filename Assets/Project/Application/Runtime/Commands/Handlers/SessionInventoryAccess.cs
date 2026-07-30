using Farion.Gameplay.Inventory;
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

        public bool Owns(IInventoryContainer inventory)
        {
            if (session == null || inventory == null)
            {
                return false;
            }

            if (ReferenceEquals(session.LocalInventory, inventory) &&
                inventory.ContainerId == session.Identity.CarriedInventoryId)
            {
                return true;
            }

            ShuttleRuntimeBinding ship = session.ShuttleBinding;
            return ship != null &&
                   ReferenceEquals(ship.Cargo, inventory) &&
                   inventory.ContainerId == ship.Cargo.ContainerId;
        }
    }
}
