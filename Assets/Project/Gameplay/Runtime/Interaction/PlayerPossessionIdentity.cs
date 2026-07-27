using Farion.Core.Persistence;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    static class PlayerPossessionIdentity
    {
        public static string ResolveSpacecraftPersistentId(Transform spacecraftRoot, Rigidbody spacecraftRigidbody)
        {
            return ResolvePersistentId(spacecraftRoot)
                ?? ResolvePersistentId(spacecraftRigidbody)
                ?? string.Empty;
        }

        public static string ResolveExplorerPersistentId(GameObject explorerRoot, Rigidbody explorerRigidbody)
        {
            return ResolvePersistentId(explorerRoot)
                ?? ResolvePersistentId(explorerRigidbody)
                ?? string.Empty;
        }

        public static bool SnapshotIdMatches(string snapshotId, string sceneId)
        {
            return !string.IsNullOrEmpty(snapshotId) &&
                   !string.IsNullOrEmpty(sceneId) &&
                   string.Equals(snapshotId, sceneId, System.StringComparison.Ordinal);
        }

        static string ResolvePersistentId(GameObject target)
        {
            if (target != null && target.TryGetComponent(out PersistentObjectId persistentObjectId))
            {
                return persistentObjectId.HasId ? persistentObjectId.Id : null;
            }

            return null;
        }

        static string ResolvePersistentId(Component target)
        {
            if (target != null && target.TryGetComponent(out PersistentObjectId persistentObjectId))
            {
                return persistentObjectId.HasId ? persistentObjectId.Id : null;
            }

            return null;
        }
    }
}
