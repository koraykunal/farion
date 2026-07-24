using System;
using System.Globalization;

namespace Farion.Simulation.World.Coordinates
{
    public readonly struct WorldSectorCoordinate : IEquatable<WorldSectorCoordinate>
    {
        public static readonly WorldSectorCoordinate Zero = new(0, 0, 0);

        public WorldSectorCoordinate(long x, long y, long z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public long X { get; }
        public long Y { get; }
        public long Z { get; }

        public static WorldSectorCoordinate operator +(WorldSectorCoordinate left, WorldSectorCoordinate right)
        {
            return new WorldSectorCoordinate(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static WorldSectorCoordinate operator -(WorldSectorCoordinate left, WorldSectorCoordinate right)
        {
            return new WorldSectorCoordinate(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static bool operator ==(WorldSectorCoordinate left, WorldSectorCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldSectorCoordinate left, WorldSectorCoordinate right)
        {
            return !left.Equals(right);
        }

        public bool Equals(WorldSectorCoordinate other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldSectorCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X.GetHashCode();
                hash = hash * 31 + Y.GetHashCode();
                hash = hash * 31 + Z.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})", X, Y, Z);
        }
    }
}
