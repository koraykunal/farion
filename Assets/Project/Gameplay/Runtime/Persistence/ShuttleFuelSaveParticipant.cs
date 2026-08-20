namespace Farion.Gameplay.Persistence
{
    public sealed class ShuttleFuelSaveParticipant : IGameplaySaveParticipant
    {
        public bool CanCapture(GameplaySaveContext context)
        {
            return context.ShuttleMotor != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null &&
                   context.ShuttleMotor != null &&
                   saveData.ShuttleFuel.Capacity > 0f;
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetShuttleFuel(context.ShuttleMotor.Fuel);
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            context.ShuttleMotor.RestoreFuel(saveData.ShuttleFuel);
            return true;
        }
    }
}
