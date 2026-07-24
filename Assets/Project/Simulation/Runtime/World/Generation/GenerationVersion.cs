using System;
using System.Globalization;

namespace Farion.Simulation.World.Generation
{
    public readonly struct GenerationVersion : IEquatable<GenerationVersion>
    {
        public static readonly GenerationVersion Current = new(1);

        public GenerationVersion(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Generation version must be positive.");
            }

            Value = value;
        }

        public int Value { get; }

        public static bool operator ==(GenerationVersion left, GenerationVersion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GenerationVersion left, GenerationVersion right)
        {
            return !left.Equals(right);
        }

        public bool Equals(GenerationVersion other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GenerationVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
