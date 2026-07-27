namespace Farion.Gameplay.Persistence
{
    public interface IGameplaySaveParticipant
    {
        bool CanCapture(GameplaySaveContext context);
        bool CanApply(GameplaySaveData saveData, GameplaySaveContext context);
        void Capture(GameplaySaveDataBuilder builder, GameplaySaveContext context);
        bool Apply(GameplaySaveData saveData, GameplaySaveContext context);
    }
}
