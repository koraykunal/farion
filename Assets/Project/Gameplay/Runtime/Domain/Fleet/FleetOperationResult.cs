namespace Farion.Gameplay.Domain.Fleet
{
    public enum FleetOperationResult
    {
        Succeeded = 0,
        InvalidIdentifier = 1,
        InvalidName = 2,
        InvalidAmount = 3,
        StaleRevision = 4,
        AlreadyExists = 5,
        NotFound = 6,
        CapacityExceeded = 7,
        Conflict = 8,
        InsufficientResource = 9,
        ShipDisabled = 10
    }
}
