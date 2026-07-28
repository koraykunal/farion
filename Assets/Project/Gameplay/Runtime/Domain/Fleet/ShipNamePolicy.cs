using System.Text;

namespace Farion.Gameplay.Domain.Fleet
{
    public static class ShipNamePolicy
    {
        public const int MaximumLength = 32;

        public static bool TryNormalize(string candidate, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            StringBuilder builder = new(candidate.Length);
            bool previousWasWhitespace = false;
            string trimmed = candidate.Trim();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char character = trimmed[i];
                if (char.IsControl(character))
                {
                    return false;
                }

                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasWhitespace)
                    {
                        builder.Append(' ');
                    }

                    previousWasWhitespace = true;
                    continue;
                }

                builder.Append(character);
                previousWasWhitespace = false;
            }

            if (builder.Length < 1 || builder.Length > MaximumLength)
            {
                return false;
            }

            normalized = builder.ToString();
            return true;
        }
    }
}
