using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Farion.Core.Persistence
{
    public static class SaveGameSlotCatalog
    {
        public const string DefaultSlotName = "autosave";
        public const string ManualSlotOneName = "manual-01";
        public const string ManualSlotTwoName = "manual-02";
        public const string ManualSlotThreeName = "manual-03";

        const string SaveDirectoryName = "Saves";
        const string SaveFileExtension = ".json";

        static readonly ReadOnlyCollection<string> playerSlotNames =
            new(
                new[]
                {
                    DefaultSlotName,
                    ManualSlotOneName,
                    ManualSlotTwoName,
                    ManualSlotThreeName
                });

        public static IReadOnlyList<string> PlayerSlotNames => playerSlotNames;

        public static string GetSaveDirectory()
        {
            return Path.Combine(Application.persistentDataPath, SaveDirectoryName);
        }

        public static string GetSlotPath(string slotName)
        {
            return Path.Combine(GetSaveDirectory(), ResolveSlotFileName(slotName));
        }

        public static bool SlotExists(string slotName)
        {
            return File.Exists(GetSlotPath(slotName));
        }

        public static string ResolveSlotName(string slotName)
        {
            return string.IsNullOrWhiteSpace(slotName) ? DefaultSlotName : slotName.Trim();
        }

        static string ResolveSlotFileName(string slotName)
        {
            string resolvedSlotName = ResolveSlotName(slotName);
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                resolvedSlotName = resolvedSlotName.Replace(invalidChar, '_');
            }

            return resolvedSlotName.EndsWith(SaveFileExtension, System.StringComparison.OrdinalIgnoreCase)
                ? resolvedSlotName
                : resolvedSlotName + SaveFileExtension;
        }
    }
}
