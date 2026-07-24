using System;
using System.Globalization;
using UnityEngine;

namespace Farion.Simulation.World.Coordinates
{
    public readonly struct WorldVector3d : IEquatable<WorldVector3d>
    {
        public static readonly WorldVector3d Zero = new(0d, 0d, 0d);

        public WorldVector3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double SqrMagnitude => X * X + Y * Y + Z * Z;

        public bool IsFinite => IsFiniteComponent(X) && IsFiniteComponent(Y) && IsFiniteComponent(Z);

        public Vector3 ToVector3()
        {
            return new Vector3((float)X, (float)Y, (float)Z);
        }

        public static WorldVector3d FromVector3(Vector3 value)
        {
            return new WorldVector3d(value.x, value.y, value.z);
        }

        public static WorldVector3d operator +(WorldVector3d left, WorldVector3d right)
        {
            return new WorldVector3d(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static WorldVector3d operator -(WorldVector3d left, WorldVector3d right)
        {
            return new WorldVector3d(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static WorldVector3d operator *(WorldVector3d value, double scalar)
        {
            return new WorldVector3d(value.X * scalar, value.Y * scalar, value.Z * scalar);
        }

        public static bool operator ==(WorldVector3d left, WorldVector3d right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldVector3d left, WorldVector3d right)
        {
            return !left.Equals(right);
        }

        public bool Equals(WorldVector3d other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldVector3d other && Equals(other);
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
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.###}, {1:0.###}, {2:0.###})",
                X,
                Y,
                Z);
        }

        static bool IsFiniteComponent(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
