using UnityEngine;

namespace Farion.Gameplay.Persistence
{
    public sealed class WorldOriginSaveParticipant : IGameplaySaveParticipant
    {
        public GameplaySaveParticipantScope Scope =>
            GameplaySaveParticipantScope.World;

        public bool CanCapture(GameplaySaveContext context)
        {
            return context.OriginRebaser != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            return saveData != null &&
                   context.OriginRebaser != null &&
                   saveData.WorldOrigin.IsSupported;
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            capture.SetWorldOrigin(context.OriginRebaser.CaptureSnapshot());
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            if (!saveData.WorldOrigin.IsSupported)
            {
                return false;
            }

            Vector3 delta = saveData.WorldOrigin.AccumulatedOffset -
                context.OriginRebaser.AccumulatedOriginOffset;
            if (delta.sqrMagnitude > Mathf.Epsilon)
            {
                context.OriginRebaser.Rebase(delta);
            }

            return context.OriginRebaser.RestoreSnapshot(saveData.WorldOrigin);
        }
    }
}
