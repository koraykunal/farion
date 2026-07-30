using Farion.App.Commands.Handlers;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Crafting;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands
{
    public sealed class GameplayCommandService : IGameplayCommandGateway
    {
        readonly GameplaySessionRuntime session;
        readonly InventoryCommandHandler inventory;
        readonly CraftingCommandHandler crafting;
        readonly ResearchCommandHandler research;
        readonly PersonalShipCommandHandler personalShip;

        public GameplayCommandService(GameplaySessionRuntime session)
        {
            this.session = session;
            SessionInventoryAccess inventoryAccess = new(session);
            inventory = new InventoryCommandHandler(inventoryAccess);
            crafting = new CraftingCommandHandler(session, inventoryAccess);
            research = new ResearchCommandHandler(session, inventoryAccess);
            personalShip = new PersonalShipCommandHandler(session);
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

        public CraftingRecipeResult CanCraft(
            CraftingStationRuntime station,
            RecipeDefinition recipe,
            IInventoryContainer targetInventory)
        {
            return crafting.CanCraft(station, recipe, targetInventory);
        }

        public CraftingRecipeResult TryCraft(
            CraftingStationRuntime station,
            RecipeDefinition recipe,
            IInventoryContainer targetInventory)
        {
            return crafting.TryCraft(station, recipe, targetInventory);
        }

        public InventoryTransferResult CanTransfer(
            IInventoryContainer source,
            IInventoryContainer destination,
            InventoryItemDefinition item,
            int amount)
        {
            return inventory.CanTransfer(source, destination, item, amount);
        }

        public InventoryTransferResult TryTransfer(
            IInventoryContainer source,
            IInventoryContainer destination,
            InventoryItemDefinition item,
            int amount)
        {
            return inventory.TryTransfer(source, destination, item, amount);
        }

        public bool HasCompletedResearch(ResearchDefinition definition)
        {
            return research.HasCompleted(definition);
        }

        public ResearchUnlockResult CanCompleteResearch(
            ResearchTerminalRuntime terminal,
            ResearchDefinition definition,
            IInventoryContainer inputInventory)
        {
            return research.CanComplete(
                terminal,
                definition,
                inputInventory);
        }

        public ResearchUnlockResult TryCompleteResearch(
            ResearchTerminalRuntime terminal,
            ResearchDefinition definition,
            IInventoryContainer inputInventory)
        {
            return research.TryComplete(
                terminal,
                definition,
                inputInventory);
        }

        public bool Matches(GameplaySessionRuntime candidate)
        {
            return ReferenceEquals(session, candidate);
        }

        public PersonalShipCommandResult RenamePersonalShip(
            PersistentEntityId shipId,
            string displayName,
            long expectedRevision = -1L)
        {
            return personalShip.Rename(
                shipId,
                displayName,
                expectedRevision);
        }

        public PersonalShipCommandResult RefuelPersonalShip(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision = -1L)
        {
            return personalShip.Refuel(shipId, amount, expectedRevision);
        }

        public PersonalShipCommandResult RepairPersonalShip(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision = -1L)
        {
            return personalShip.Repair(shipId, amount, expectedRevision);
        }

        public PersonalShipCommandResult ApplyPersonalShipDamage(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision = -1L)
        {
            return personalShip.ApplyDamage(shipId, amount, expectedRevision);
        }

        public PersonalShipCommandResult ConsumePersonalShipFuel(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision = -1L)
        {
            return personalShip.ConsumeFuel(shipId, amount, expectedRevision);
        }
    }
}
