namespace Farion.Gameplay.Persistence
{
    public sealed class PlayerInventorySaveParticipant : IGameplaySaveParticipant
    {
        public GameplaySaveParticipantScope Scope =>
            GameplaySaveParticipantScope.LocalPlayer;

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
                   context.PlayerInventory.CanApplyContainerSnapshot(
                       saveData.PlayerInventory,
                       context.Definitions);
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetPlayerInventory(
                context.PlayerInventory.CaptureContainerSnapshot());
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            context.PlayerInventory.ApplyContainerSnapshot(
                saveData.PlayerInventory,
                context.Definitions);
            return true;
        }
    }
}
