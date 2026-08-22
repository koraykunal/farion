namespace Farion.Gameplay.Persistence
{
    public sealed class CelestialSimulationSaveParticipant : IGameplaySaveParticipant
    {
        public GameplaySaveParticipantScope Scope =>
            GameplaySaveParticipantScope.World;

        public bool CanCapture(GameplaySaveContext context)
        {
            return context.GravitySimulation != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            if (saveData == null ||
                context.GravitySimulation == null ||
                !context.GravitySimulation.CanApplySnapshots(saveData.CelestialBodies))
            {
                return false;
            }

            if (saveData.CelestialLayoutHash != 0 &&
                saveData.CelestialLayoutHash != context.GravitySimulation.ComputeLayoutHash())
            {
                UnityEngine.Debug.LogWarning(
                    "Celestial save rejected: the authored celestial layout changed since this save was captured.");
                return false;
            }

            return true;
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetCelestialSimulationTime(context.GravitySimulation.SimulationTime);
            capture.SetCelestialLayoutHash(context.GravitySimulation.ComputeLayoutHash());
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
