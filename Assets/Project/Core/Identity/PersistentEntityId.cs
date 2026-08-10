using System;

namespace Farion.Core.Identity
{
    public readonly struct PersistentEntityId :
        IEquatable<PersistentEntityId>,
        IComparable<PersistentEntityId>
    {
        readonly string value;

        public PersistentEntityId(string value)
        {
            string normalized = IdentifierText.Normalize(value);
            if (!IdentifierText.IsValid(normalized))
            {
                throw new ArgumentException(
                    "Persistent entity ids must be non-empty and cannot contain whitespace or control characters.",
                    nameof(value));
            }

            this.value = normalized;
        }

        public static PersistentEntityId None => default;
        public string Value => value ?? string.Empty;
        public bool IsValid => IdentifierText.IsValid(Value);

        public static PersistentEntityId New()
        {
            return new PersistentEntityId(Guid.NewGuid().ToString("N"));
        }

        public static bool TryCreate(string value, out PersistentEntityId id)
        {
            string normalized = IdentifierText.Normalize(value);
            if (!IdentifierText.IsValid(normalized))
            {
                id = default;
                return false;
            }

            id = new PersistentEntityId(normalized);
            return true;
        }

        public static string Normalize(string value)
        {
            return IdentifierText.Normalize(value);
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
    }
}
