using Farion.App.Commands.Handlers;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Session;

namespace Farion.App.Commands
{
    public sealed class GameplayCommandService : IGameplayCommandGateway
    {
        readonly GameplaySessionRuntime session;
        readonly InventoryCommandHandler inventory;

        public GameplayCommandService(GameplaySessionRuntime session)
        {
            this.session = session;
            inventory = new InventoryCommandHandler(
                new SessionInventoryAccess(session));
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

        public bool Matches(GameplaySessionRuntime candidate)
        {
            return ReferenceEquals(session, candidate);
        }
    }
}
