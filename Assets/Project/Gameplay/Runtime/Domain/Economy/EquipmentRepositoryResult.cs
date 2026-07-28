namespace Farion.Gameplay.Domain.Economy
{
    public enum EquipmentRepositoryResult
    {
        Succeeded = 0,
        InvalidInstance = 1,
        DuplicateInstance = 2,
        DuplicateSerialNumber = 3,
        InvalidLocation = 4,
        OwnershipConflict = 5,
        StaleRevision = 6
    }
}
