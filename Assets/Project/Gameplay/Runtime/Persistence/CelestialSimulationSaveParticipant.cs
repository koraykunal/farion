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

        public void Capture(GameplaySaveDataBuilder builder, GameplaySaveContext context)
        {
            context.GravitySimulation.CaptureSnapshots(builder.CelestialBodies);
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return context.GravitySimulation.ApplySnapshots(saveData.CelestialBodies);
        }
    }
}
