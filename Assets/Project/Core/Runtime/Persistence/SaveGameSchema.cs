using System;
using System.Globalization;
using UnityEngine;

namespace Farion.Core.Persistence
{
    public static class SaveGameSchema
    {
        public const int MinimumSupportedVersion = 3;
        public const int CurrentVersion = 5;

        public static bool IsSupportedVersion(int version)
        {
            return version >= MinimumSupportedVersion && version <= CurrentVersion;
        }

        public static bool PayloadLooksSupported(string payload)
        {
            return TryReadHeader(payload, out int version, out _) &&
                   IsSupportedVersion(version);
        }

        public static bool TryReadVersion(string payload, out int version)
        {
            return TryReadHeader(payload, out version, out _);
        }

        public static bool TryReadHeader(
            string payload,
            out int version,
            out DateTime? savedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                version = 0;
                savedAtUtc = null;
                return false;
            }

            try
            {
                SaveGameHeader header = JsonUtility.FromJson<SaveGameHeader>(payload);
                version = header != null ? header.SchemaVersion : 0;
                savedAtUtc = ParseUtcTimestamp(header?.SavedAtUtc);
                return version > 0;
            }
            catch (ArgumentException)
            {
                version = 0;
                savedAtUtc = null;
                return false;
            }
        }

        static DateTime? ParseUtcTimestamp(string value)
        {
            if (!DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime timestamp))
            {
                return null;
            }

            return timestamp;
        }

        [Serializable]
        sealed class SaveGameHeader
        {
            [SerializeField] int schemaVersion;
            [SerializeField] string savedAtUtc;

            public int SchemaVersion => schemaVersion;
            public string SavedAtUtc => savedAtUtc;
        }
    }
}
