namespace Farion.Gameplay.Persistence
{
    public interface IGameplaySaveParticipant
    {
        GameplaySaveParticipantScope Scope { get; }
        bool CanCapture(GameplaySaveContext context);
        bool CanApply(GameplaySaveData saveData, GameplaySaveContext context);
        void Capture(GameplaySaveCapture capture, GameplaySaveContext context);
        bool Apply(GameplaySaveData saveData, GameplaySaveContext context);
    }
}
