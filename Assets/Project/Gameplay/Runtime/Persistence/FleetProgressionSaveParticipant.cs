namespace Farion.Gameplay.Persistence
{
    public sealed class FleetProgressionSaveParticipant : IGameplaySaveParticipant
    {
        public bool CanCapture(GameplaySaveContext context)
        {
            return context.Definitions != null &&
                   context.FleetProgression != null &&
                   context.FleetProgression.CanApplySnapshot(
                       context.FleetProgression.CaptureSnapshot(),
                       context.Definitions);
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null &&
                   context.FleetProgression != null &&
                   saveData.FleetKnowledge != null &&
                   context.FleetProgression.CanApplySnapshot(
                       saveData.FleetKnowledge,
                       context.Definitions);
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetFleetKnowledge(
                context.FleetProgression.CaptureSnapshot());
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return context.FleetProgression.ApplySnapshot(
                saveData.FleetKnowledge,
                context.Definitions);
        }
    }
}
