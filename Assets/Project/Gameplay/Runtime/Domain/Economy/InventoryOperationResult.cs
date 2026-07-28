namespace Farion.Gameplay.Domain.Economy
{
    public enum InventoryOperationResult
    {
        Succeeded = 0,
        InvalidContainer = 1,
        InvalidDefinition = 2,
        InvalidItemInstance = 3,
        InvalidQuantity = 4,
        InvalidStackLimit = 5,
        DefinitionPolicyMismatch = 6,
        InsufficientQuantity = 7,
        InsufficientCapacity = 8,
        DuplicateItemInstance = 9,
        MissingItemInstance = 10,
        StaleRevision = 11,
        InvalidRepository = 12,
        EquipmentNotRegistered = 13,
        OwnershipConflict = 14
    }
}
