namespace Farion.Gameplay.Commands
{
    public enum CargoTransferResult
    {
        Succeeded = 0,
        MissingSource = 1,
        MissingDestination = 2,
        UnauthorizedSource = 3,
        UnauthorizedDestination = 4,
        EmptySource = 5,
        DefinitionMismatch = 6,
        InsufficientCapacity = 7,
        StaleState = 8,
        Rejected = 9
    }
}
