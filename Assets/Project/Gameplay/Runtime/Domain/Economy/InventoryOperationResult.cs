namespace Farion.Gameplay.Domain.Economy
{
    public enum InventoryOperationResult
    {
        Succeeded = 0,
        InvalidDefinition = 1,
        InvalidQuantity = 2,
        InvalidStackLimit = 3,
        DefinitionPolicyMismatch = 4,
        InsufficientQuantity = 5,
        InsufficientCapacity = 6,
        StaleRevision = 7
    }
}
