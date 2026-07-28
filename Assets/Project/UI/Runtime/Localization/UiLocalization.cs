using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Farion.UI.Localization
{
    public static class UiLocalization
    {
        public const string TableName = "Farion UI";
        public const string EnglishLocaleCode = "en";
        public const string TurkishLocaleCode = "tr";

        public static string Get(string entryKey, string fallback)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                return fallback ?? string.Empty;
            }

            try
            {
                if (!LocalizationSettings.InitializationOperation.IsDone ||
                    LocalizationSettings.SelectedLocale == null)
                {
                    return fallback ?? string.Empty;
                }

                AsyncOperationHandle<string> operation =
                    new LocalizedString(TableName, entryKey)
                        .GetLocalizedStringAsync();
                if (!operation.IsDone ||
                    operation.Status != AsyncOperationStatus.Succeeded)
                {
                    return fallback ?? string.Empty;
                }

                string value = operation.Result;
                return string.IsNullOrWhiteSpace(value) ||
                       value.StartsWith("No translation found", System.StringComparison.Ordinal)
                    ? fallback ?? string.Empty
                    : value;
            }
            catch
            {
                return fallback ?? string.Empty;
            }
        }

        public static bool TrySelectLocale(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode) ||
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
                System.StringComparison.OrdinalIgnoreCase)
                ? TurkishLocaleCode
                : EnglishLocaleCode;
        }
    }
}
