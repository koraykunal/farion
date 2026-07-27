namespace Farion.Gameplay.Persistence
{
    public sealed class WorldOriginSaveParticipant : IGameplaySaveParticipant
    {
        public bool CanCapture(GameplaySaveContext context)
        {
            return context.OriginRebaser != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null &&
                   context.OriginRebaser != null &&
                   (saveData.SchemaVersion < 4 || saveData.WorldOrigin.IsSupported);
        }

        public void Capture(GameplaySaveDataBuilder builder, GameplaySaveContext context)
        {
            builder.SetWorldOrigin(context.OriginRebaser.CaptureSnapshot());
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            if (saveData.SchemaVersion < 4)
            {
                context.OriginRebaser.ResetRuntimeState();
                return true;
            }

            return context.OriginRebaser.RestoreSnapshot(saveData.WorldOrigin);
        }
    }
}
