namespace Farion.Gameplay.Persistence
{
    public sealed class PlayerInventorySaveParticipant : IGameplaySaveParticipant
    {
        public bool CanCapture(GameplaySaveContext context)
        {
            return context.Definitions != null && context.PlayerInventory != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null &&
                   saveData.PlayerInventory != null &&
                   context.Definitions != null &&
                   context.PlayerInventory != null &&
                   context.PlayerInventory.CanApplySnapshot(saveData.PlayerInventory, context.Definitions);
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetPlayerInventory(context.PlayerInventory.CaptureSnapshot());
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            context.PlayerInventory.ApplySnapshot(saveData.PlayerInventory, context.Definitions);
            return true;
        }
    }
}
