namespace Farion.Gameplay.Persistence
{
    public sealed class FleetKnowledgeSaveParticipant :
        IGameplaySaveParticipant
    {
        public GameplaySaveParticipantScope Scope =>
            GameplaySaveParticipantScope.World;

        public bool CanCapture(GameplaySaveContext context)
        {
            return context.FleetKnowledge != null;
        }

        public bool CanApply(
            GameplaySaveData saveData,
            GameplaySaveContext context)
        {
            return saveData != null &&
                   context.FleetKnowledge != null &&
                   context.FleetKnowledge.CanApplySnapshot(
                       saveData.FleetKnowledge);
        }

        public void Capture(
            GameplaySaveCapture capture,
            GameplaySaveContext context)
        {
            capture.SetFleetKnowledge(
                context.FleetKnowledge.CaptureSnapshot());
        }

        public bool Apply(
            GameplaySaveData saveData,
            GameplaySaveContext context)
        {
            return context.FleetKnowledge.ApplySnapshot(
                saveData.FleetKnowledge);
        }
    }
}
