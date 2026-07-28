namespace Farion.Gameplay.Domain.Economy
{
    public enum EquipmentPlacementResult
    {
        Succeeded = 0,
        InvalidRepository = 1,
        InvalidContainer = 2,
        InvalidIdentifier = 3,
        EquipmentNotRegistered = 4,
        EquipmentAlreadyAssigned = 5,
        InsufficientCapacity = 6,
        StaleRevision = 7
    }
}
