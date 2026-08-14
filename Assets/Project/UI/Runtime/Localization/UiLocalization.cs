using System;
using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Farion.UI.Localization
{
    public static class UiLocalization
    {
        public const string TableName = "Farion UI";
        public const string EnglishLocaleCode = "en";
        public const string TurkishLocaleCode = "tr";

        static readonly HashSet<string> missingEntryKeys = new(StringComparer.Ordinal);

        public static IReadOnlyCollection<string> MissingEntryKeys => missingEntryKeys;

        public static string Get(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                return string.Empty;
            }

            // StringDatabase caches the resolved table and invalidates it on locale change.
            StringTable table = LocalizationSettings.HasSettings
                ? LocalizationSettings.StringDatabase.GetTable(TableName)
                : null;

            string value = table?.GetEntry(entryKey)?.GetLocalizedString();
            if (string.IsNullOrWhiteSpace(value))
            {
                missingEntryKeys.Add(entryKey);
                return entryKey;
            }

            return value;
        }

        public static bool TrySelectLocale(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode) ||
                !LocalizationSettings.HasSettings ||
                LocalizationSettings.AvailableLocales == null)
            {
                return false;
            }

            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
            if (locale == null)
            {
                return false;
            }

            LocalizationSettings.SelectedLocale = locale;
            return true;
        }

        public static string NormalizeLocaleCode(string localeCode)
        {
            return string.Equals(
                localeCode,
                TurkishLocaleCode,
                StringComparison.OrdinalIgnoreCase)
                ? TurkishLocaleCode
                : EnglishLocaleCode;
        }
    }
}
