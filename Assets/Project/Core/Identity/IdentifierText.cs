namespace Farion.Core.Identity
{
    public static class IdentifierText
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static bool IsValid(string normalizedValue)
        {
            if (string.IsNullOrEmpty(normalizedValue))
            {
                return false;
            }

            for (int i = 0; i < normalizedValue.Length; i++)
            {
                char character = normalizedValue[i];
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsValidRaw(string value)
        {
            return IsValid(Normalize(value));
        }
    }
}
