namespace Farion.Gameplay.Research
{
    public enum ResearchUnlockResult
    {
        Succeeded = 0,
        MissingResearch = 1,
        InvalidResearch = 2,
        MissingInventory = 3,
        MissingKnowledge = 4,
        AlreadyCompleted = 5,
        MissingIngredients = 6,
        UnauthorizedInventory = 7,
        InventoryRejected = 8,
        KnowledgeRejected = 9,
        RollbackFailed = 10,
        ResearchNotOffered = 11
    }
}
