namespace Farion.Gameplay.Persistence
{
    public sealed class PlayerPossessionSaveParticipant : IGameplaySaveParticipant
    {
        public GameplaySaveParticipantScope Scope =>
            GameplaySaveParticipantScope.LocalPlayer;

        public bool CanCapture(GameplaySaveContext context)
        {
            return context.PossessionController != null &&
                   context.PossessionController.HasPersistentTargets;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null &&
                   saveData.PlayerPossession != null &&
                   context.PossessionController != null &&
                   context.PossessionController.CanApplySnapshot(saveData.PlayerPossession);
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetPlayerPossession(context.PossessionController.CaptureSnapshot());
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return context.PossessionController.ApplySnapshot(saveData.PlayerPossession);
        }
    }
}
