namespace Farion.Gameplay.Commands
{
    public enum ResourceHarvestResult
    {
        Succeeded = 0,
        MissingSource = 1,
        MissingDestination = 2,
        UnauthorizedDestination = 3,
        InvalidYield = 4,
        Depleted = 5,
        InsufficientCapacity = 6,
        InventoryRejected = 7,
        SourceChanged = 8,
        RollbackFailed = 9,
        StaleDestination = 10
    }
}
