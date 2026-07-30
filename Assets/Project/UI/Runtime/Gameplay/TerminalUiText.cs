using System.Globalization;
using Farion.UI.Localization;

namespace Farion.UI.Gameplay
{
    internal static class TerminalUiText
    {
        public static string Get(string key, string fallback)
        {
            return UiLocalization.Get($"terminal.{key}", fallback);
        }

        public static string Format(
            string key,
            string fallback,
            params object[] arguments)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Get(key, fallback),
                arguments);
        }
    }
}
