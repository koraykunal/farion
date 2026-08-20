using Farion.Gameplay.Flight;

namespace Farion.Gameplay.Persistence
{
    public sealed class ShuttleSystemsSaveParticipant : IGameplaySaveParticipant
    {
        public bool CanCapture(GameplaySaveContext context)
        {
            return context.ShuttleMotor != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null && context.ShuttleMotor != null;
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetShuttleFuel(context.ShuttleMotor.Fuel);
            SpacecraftHull hull = context.ShuttleHull;
            if (hull != null)
            {
                capture.SetShuttleHull(hull.Integrity);
            }
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            if (saveData.ShuttleFuel.Capacity > 0f)
            {
                context.ShuttleMotor.RestoreFuel(saveData.ShuttleFuel);
            }

            SpacecraftHull hull = context.ShuttleHull;
            if (saveData.ShuttleHull.Capacity > 0f && hull != null)
            {
                hull.RestoreIntegrity(saveData.ShuttleHull);
            }

            return true;
        }
    }
}
