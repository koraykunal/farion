using System;
using System.Collections.Generic;
using System.Globalization;
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
            return string.IsNullOrWhiteSpace(value) ? entryKey : value;
        }

        public static string GetPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return string.Empty;
            }

            int separator = prompt.IndexOf(
                Farion.Gameplay.Interaction.InteractionPromptKeys.ArgumentSeparator);
            if (separator < 0)
            {
                return Get(prompt.Trim());
            }

            string template = Get(prompt[..separator].Trim());
            string argument = prompt[(separator + 1)..].Trim();
            return template.Contains("{0}")
                ? string.Format(template, argument)
                : $"{template} {argument}";
        }

        public static string ToDisplayUpper(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.ToUpper(ResolveCulture());
        }

        public static CultureInfo ResolveCulture()
        {
            Locale locale = LocalizationSettings.HasSettings
                ? LocalizationSettings.SelectedLocale
                : null;
            return locale != null && locale.Identifier.CultureInfo != null
                ? locale.Identifier.CultureInfo
                : CultureInfo.InvariantCulture;
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
