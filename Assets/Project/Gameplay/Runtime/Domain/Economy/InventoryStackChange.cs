using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public readonly struct InventoryStackChange
    {
        public InventoryStackChange(DefinitionId definitionId, int quantityDelta, int stackLimit)
        {
            DefinitionId = definitionId;
            QuantityDelta = quantityDelta;
            StackLimit = stackLimit;
        }

        public DefinitionId DefinitionId { get; }
        public int QuantityDelta { get; }
        public int StackLimit { get; }
    }
}
