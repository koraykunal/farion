namespace Farion.Gameplay.Persistence
{
    public sealed class PersonalShipCargoSaveParticipant : IGameplaySaveParticipant
    {
        public bool CanCapture(GameplaySaveContext context)
        {
            return context.Definitions != null &&
                   context.PersonalShipCargo != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null &&
                   context.Definitions != null &&
                   context.PersonalShipCargo != null &&
                   saveData.PersonalShipCargo != null &&
                   context.PersonalShipCargo.CanApplyContainerSnapshot(
                       saveData.PersonalShipCargo,
                       context.Definitions);
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetPersonalShipCargo(
                context.PersonalShipCargo.CaptureContainerSnapshot());
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return context.PersonalShipCargo.ApplyContainerSnapshot(
                saveData.PersonalShipCargo,
                context.Definitions);
        }
    }
}
