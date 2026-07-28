using System;

namespace Farion.Gameplay.Domain.Identity
{
    public readonly struct PersistentEntityId :
        IEquatable<PersistentEntityId>,
        IComparable<PersistentEntityId>
    {
        readonly string value;

        public PersistentEntityId(string value)
        {
            string normalized = Normalize(value);
            if (!IsValidValue(normalized))
            {
                throw new ArgumentException(
                    "Persistent entity ids must be non-empty and cannot contain whitespace or control characters.",
                    nameof(value));
            }

            this.value = normalized;
        }

        public static PersistentEntityId None => default;
        public string Value => value ?? string.Empty;
        public bool IsValid => IsValidValue(Value);

        public static PersistentEntityId New()
        {
            return new PersistentEntityId(Guid.NewGuid().ToString("N"));
        }

        public static bool TryCreate(string value, out PersistentEntityId id)
        {
            string normalized = Normalize(value);
            if (!IsValidValue(normalized))
            {
                id = default;
                return false;
            }

            id = new PersistentEntityId(normalized);
            return true;
        }

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static bool operator ==(PersistentEntityId left, PersistentEntityId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PersistentEntityId left, PersistentEntityId right)
        {
            return !left.Equals(right);
        }

        public bool Equals(PersistentEntityId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PersistentEntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public int CompareTo(PersistentEntityId other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return IsValid ? Value : "None";
        }

        static bool IsValidValue(string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            for (int i = 0; i < candidate.Length; i++)
            {
                if (char.IsWhiteSpace(candidate[i]) || char.IsControl(candidate[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
