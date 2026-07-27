using System;
using UnityEngine;

namespace Farion.Core.Persistence
{
    public static class SaveGameSchema
    {
        public const int MinimumSupportedVersion = 3;
        public const int CurrentVersion = 4;

        public static bool IsSupportedVersion(int version)
        {
            return version >= MinimumSupportedVersion && version <= CurrentVersion;
        }

        public static bool PayloadLooksSupported(string payload)
        {
            return TryReadVersion(payload, out int version) && IsSupportedVersion(version);
        }

        public static bool TryReadVersion(string payload, out int version)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                version = 0;
                return false;
            }

            try
            {
                SaveGameHeader header = JsonUtility.FromJson<SaveGameHeader>(payload);
                version = header != null ? header.SchemaVersion : 0;
                return version > 0;
            }
            catch (ArgumentException)
            {
                version = 0;
                return false;
            }
        }

        [Serializable]
        sealed class SaveGameHeader
        {
            [SerializeField] int schemaVersion;

            public int SchemaVersion => schemaVersion;
        }
    }
}
