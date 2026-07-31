namespace Farion.Gameplay.Persistence
{
    public sealed class FleetStorageSaveParticipant : IGameplaySaveParticipant
    {
        public bool CanCapture(GameplaySaveContext context)
        {
            return context.Definitions != null &&
                   context.FleetStorage != null;
        }

        public bool CanApply(
            GameplaySaveData saveData,
            GameplaySaveContext context)
        {
            return saveData != null &&
                   context.Definitions != null &&
                   context.FleetStorage != null &&
                   saveData.FleetStorage != null &&
                   context.FleetStorage.CanApplyContainerSnapshot(
                       saveData.FleetStorage,
                       context.Definitions);
        }

        public void Capture(
            GameplaySaveCapture capture,
            GameplaySaveContext context)
        {
            capture.SetFleetStorage(
                context.FleetStorage.CaptureContainerSnapshot());
        }

        public bool Apply(
            GameplaySaveData saveData,
            GameplaySaveContext context)
        {
            return context.FleetStorage.ApplyContainerSnapshot(
                saveData.FleetStorage,
                context.Definitions);
        }
    }
}
