namespace Farion.Gameplay.Domain.Economy
{
    public enum EquipmentLoadoutResult
    {
        Succeeded = 0,
        InvalidRepository = 1,
        InvalidContainer = 2,
        InvalidShip = 3,
        InvalidPolicy = 4,
        InvalidIdentifier = 5,
        EquipmentNotRegistered = 6,
        EquipmentNotStored = 7,
        EquipmentAlreadyInstalled = 8,
        SlotEmpty = 9,
        IncompatibleEquipment = 10,
        InsufficientCapacity = 11,
        ConflictingOwnership = 12,
        StaleRevision = 13
    }
}
