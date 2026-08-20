using System.Collections.Generic;
using Farion.Gameplay.ResourceNodes;

namespace Farion.Gameplay.Persistence
{
    public sealed class ResourceDepositSaveParticipant : IGameplaySaveParticipant
    {
        public bool CanCapture(GameplaySaveContext context)
        {
            return context.ResourceStreamers != null;
        }

        public bool CanApply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            if (saveData == null || saveData.ResourceDepositDeltas == null || context.ResourceStreamers == null)
            {
                return false;
            }

            return saveData.ResourceDepositDeltas.Count == 0 || HasAtLeastOneStreamer(context.ResourceStreamers);
        }

        public void Capture(GameplaySaveCapture capture, GameplaySaveContext context)
        {
            IReadOnlyList<ResourceDepositRuntimeSpawner> streamers = context.ResourceStreamers;
            for (int i = 0; i < streamers.Count; i++)
            {
                streamers[i]?.CaptureDeltaSnapshot(capture.ResourceDepositDeltas);
            }
        }

        public bool Apply(GameplaySaveData saveData, GameplaySaveContext context)
        {
            IReadOnlyList<ResourceDepositRuntimeSpawner> streamers = context.ResourceStreamers;
            bool useLegacyIds = saveData.SourceSchemaVersion < 4;
            for (int i = 0; i < streamers.Count; i++)
            {
                streamers[i]?.ApplyDeltaSnapshot(saveData.ResourceDepositDeltas, useLegacyIds);
            }

            return true;
        }

        static bool HasAtLeastOneStreamer(IReadOnlyList<ResourceDepositRuntimeSpawner> streamers)
        {
            for (int i = 0; i < streamers.Count; i++)
            {
                if (streamers[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
