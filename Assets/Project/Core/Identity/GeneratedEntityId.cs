using System;
using System.Globalization;

namespace Farion.Core.Identity
{
    public readonly struct GeneratedEntityId : IEquatable<GeneratedEntityId>
    {
        public static readonly GeneratedEntityId None = new(0UL, allowNone: true);

        public GeneratedEntityId(ulong value)
            : this(StableHashUtility.NormalizeNonZero(value), allowNone: false)
        {
        }

        GeneratedEntityId(ulong value, bool allowNone)
        {
            if (!allowNone && value == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Generated entity ids must not be zero.");
            }

            Value = value;
        }

        public ulong Value { get; }
        public bool IsValid => Value != 0UL;

        public static GeneratedEntityId FromHash(ulong hash)
        {
            return new GeneratedEntityId(hash);
        }

        public static bool operator ==(GeneratedEntityId left, GeneratedEntityId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GeneratedEntityId left, GeneratedEntityId right)
        {
            return !left.Equals(right);
        }

        public bool Equals(GeneratedEntityId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GeneratedEntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid
                ? Value.ToString("X16", CultureInfo.InvariantCulture)
                : "None";
        }
    }
}
