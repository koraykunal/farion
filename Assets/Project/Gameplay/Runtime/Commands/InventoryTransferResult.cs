namespace Farion.Gameplay.Commands
{
    public enum InventoryTransferResult
    {
        Succeeded = 0,
        MissingSource = 1,
        MissingDestination = 2,
        SameContainer = 3,
        MissingItem = 4,
        InvalidQuantity = 5,
        UnauthorizedSource = 6,
        UnauthorizedDestination = 7,
        InsufficientQuantity = 8,
        InsufficientCapacity = 9,
        SourceRejected = 10,
        DestinationRejected = 11,
        RollbackFailed = 12
    }
}
