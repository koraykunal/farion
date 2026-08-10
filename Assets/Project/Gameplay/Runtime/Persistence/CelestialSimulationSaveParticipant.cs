namespace Farion.Gameplay.Persistence
{
    public sealed class CelestialSimulationSaveParticipant : IGameplaySaveParticipant
    {
        public bool CanCapture(GameplaySaveContext context)
        {
            return context.GravitySimulation != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null &&
                   context.GravitySimulation != null &&
                   context.GravitySimulation.CanApplySnapshots(saveData.CelestialBodies);
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetCelestialSimulationTime(context.GravitySimulation.SimulationTime);
            context.GravitySimulation.CaptureSnapshots(capture.CelestialBodies);
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            if (!context.GravitySimulation.ApplySnapshots(saveData.CelestialBodies))
            {
                return false;
            }

            context.GravitySimulation.SetSimulationTime(saveData.CelestialSimulationTime);
            return true;
        }
    }
}
