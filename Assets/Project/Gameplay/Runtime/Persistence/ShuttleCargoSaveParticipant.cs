namespace Farion.Gameplay.Persistence
{
    public sealed class ShuttleCargoSaveParticipant : IGameplaySaveParticipant
    {
        public GameplaySaveParticipantScope Scope =>
            GameplaySaveParticipantScope.LocalPlayer;

        public bool CanCapture(GameplaySaveContext context)
        {
            return context.Definitions != null &&
                   context.ShuttleCargo != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null &&
                   context.Definitions != null &&
                   context.ShuttleCargo != null &&
                   saveData.ShuttleCargo != null &&
                   context.ShuttleCargo.CanApplyContainerSnapshot(
                       saveData.ShuttleCargo,
                       context.Definitions);
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetShuttleCargo(
                context.ShuttleCargo.CaptureContainerSnapshot());
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return context.ShuttleCargo.ApplyContainerSnapshot(
                saveData.ShuttleCargo,
                context.Definitions);
        }
    }
}
