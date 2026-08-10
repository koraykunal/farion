namespace Farion.Gameplay.Commands
{
    public enum FleetProcessingResult
    {
        Succeeded = 0,
        MissingRecipe = 1,
        InvalidRecipe = 2,
        MissingStorage = 3,
        UnauthorizedStorage = 4,
        Rejected = 5,
        StaleStorage = 6
    }
}
