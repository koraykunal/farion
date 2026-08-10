using System;
using Farion.Core.Identity;

namespace Farion.Gameplay.Domain.Identity
{
    public readonly struct DefinitionId : IEquatable<DefinitionId>, IComparable<DefinitionId>
    {
        readonly string value;

        public DefinitionId(string value)
        {
            string normalized = IdentifierText.Normalize(value);
            if (!IdentifierText.IsValid(normalized))
            {
                throw new ArgumentException(
                    "Definition ids must be non-empty and cannot contain whitespace or control characters.",
                    nameof(value));
            }

            this.value = normalized;
        }

        public string Value => value ?? string.Empty;
        public bool IsValid => IdentifierText.IsValid(Value);

        public static bool TryCreate(string value, out DefinitionId id)
        {
            string normalized = IdentifierText.Normalize(value);
            if (!IdentifierText.IsValid(normalized))
            {
                id = default;
                return false;
            }

            id = new DefinitionId(normalized);
            return true;
        }

        public static string Normalize(string value)
        {
            return IdentifierText.Normalize(value);
        }

        public static bool operator ==(DefinitionId left, DefinitionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DefinitionId left, DefinitionId right)
        {
            return !left.Equals(right);
        }

        public bool Equals(DefinitionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is DefinitionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public int CompareTo(DefinitionId other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
