using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public readonly struct InventoryStackState
    {
        internal InventoryStackState(DefinitionId definitionId, int quantity, int stackLimit)
        {
            DefinitionId = definitionId;
            Quantity = quantity;
            StackLimit = stackLimit;
        }

        public DefinitionId DefinitionId { get; }
        public int Quantity { get; }
        public int StackLimit { get; }
        public int OccupiedSlots =>
            (int)(((long)Quantity + StackLimit - 1L) / StackLimit);
        public int RemainingCapacityInOccupiedSlots =>
            (int)((long)OccupiedSlots * StackLimit - Quantity);

        internal InventoryStackState WithQuantity(int quantity)
        {
            return new InventoryStackState(DefinitionId, quantity, StackLimit);
        }
    }
}
