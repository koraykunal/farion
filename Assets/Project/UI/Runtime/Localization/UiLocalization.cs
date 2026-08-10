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
        static StringTable cachedTable;
        static Locale cachedLocale;
        static bool localeChangeHooked;

        public static IReadOnlyCollection<string> MissingEntryKeys => missingEntryKeys;

        public static string Get(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                return string.Empty;
            }

            StringTable table = ResolveTable();
            StringTableEntry entry = table?.GetEntry(entryKey);
            if (entry == null)
            {
                missingEntryKeys.Add(entryKey);
                return entryKey;
            }

            string value = entry.GetLocalizedString();
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

        static StringTable ResolveTable()
        {
            if (!LocalizationSettings.HasSettings)
            {
                return null;
            }

            HookLocaleChange();

            Locale locale = LocalizationSettings.SelectedLocale;
            if (locale == null)
            {
                return null;
            }

            if (cachedTable != null && ReferenceEquals(cachedLocale, locale))
            {
                return cachedTable;
            }

            cachedTable = LocalizationSettings.StringDatabase.GetTable(TableName, locale);
            cachedLocale = locale;
            return cachedTable;
        }

        static void HookLocaleChange()
        {
            if (localeChangeHooked)
            {
                return;
            }

            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            localeChangeHooked = true;
        }

        static void OnSelectedLocaleChanged(Locale locale)
        {
            cachedTable = null;
            cachedLocale = null;
        }
    }
}
