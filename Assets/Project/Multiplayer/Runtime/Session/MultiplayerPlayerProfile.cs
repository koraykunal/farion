using System.Text;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    public static class MultiplayerPlayerProfile
    {
        const string DisplayNameKey = "farion.player.name";
        const string PersistentIdKey = "farion.player.id";
        const int MaximumLength = 24;

        public static string DisplayName
        {
            get
            {
                string platformName = MultiplayerLobbyGateway.IsAvailable
                    ? MultiplayerLobbyGateway.Service.LocalDisplayName
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(platformName))
                {
                    return Sanitize(platformName, MaximumLength, 0UL);
                }

                string stored = PlayerPrefs.GetString(DisplayNameKey, string.Empty);
                return string.IsNullOrWhiteSpace(stored)
                    ? string.Empty
                    : stored;
            }
            set
            {
                PlayerPrefs.SetString(
                    DisplayNameKey,
                    Sanitize(value, MaximumLength, 0UL));
                PlayerPrefs.Save();
            }
        }

        public static string PersistentPlayerId
        {
            get
            {
                string platformId = MultiplayerLobbyGateway.IsAvailable
                    ? MultiplayerLobbyGateway.Service.LocalPersistentId
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(platformId))
                {
                    return platformId;
                }

                string stored = PlayerPrefs.GetString(PersistentIdKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(stored))
                {
                    return stored;
                }

                string created = System.Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PersistentIdKey, created);
                PlayerPrefs.Save();
                return created;
            }
        }

        public static string Sanitize(
            string value,
            int maximumLength,
            ulong fallbackSessionPlayerId)
        {
            StringBuilder builder = new(maximumLength);
            if (value != null)
            {
                for (int i = 0; i < value.Length && builder.Length < maximumLength; i++)
                {
                    char current = value[i];
                    if (char.IsLetterOrDigit(current) ||
                        current == ' ' ||
                        current == '_' ||
                        current == '-')
                    {
                        builder.Append(current);
                    }
                }
            }

            string sanitized = builder.ToString().Trim();
            return sanitized.Length > 0
                ? sanitized
                : $"Explorer {fallbackSessionPlayerId}";
        }
    }
}
