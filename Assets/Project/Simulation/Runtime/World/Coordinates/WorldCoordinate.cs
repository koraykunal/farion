using System;
using System.Globalization;
using UnityEngine;

namespace Farion.Simulation.World.Coordinates
{
    public readonly struct WorldCoordinate : IEquatable<WorldCoordinate>
    {
        public WorldCoordinate(WorldSectorCoordinate sector, WorldVector3d localPositionMeters)
        {
            WorldCoordinateUtility.Normalize(
                sector,
                localPositionMeters,
                out WorldSectorCoordinate normalizedSector,
                out WorldVector3d normalizedLocalPositionMeters);

            Sector = normalizedSector;
            LocalPositionMeters = normalizedLocalPositionMeters;
        }

        public WorldSectorCoordinate Sector { get; }
        public WorldVector3d LocalPositionMeters { get; }

        public Vector3 ToUnityPosition()
        {
            return LocalPositionMeters.ToVector3();
        }

        public WorldCoordinate AddLocalOffset(WorldVector3d offsetMeters)
        {
            return new WorldCoordinate(Sector, LocalPositionMeters + offsetMeters);
        }

        public static WorldCoordinate FromUnityPosition(Vector3 unityPosition)
        {
            return new WorldCoordinate(WorldSectorCoordinate.Zero, WorldVector3d.FromVector3(unityPosition));
        }

        public static bool operator ==(WorldCoordinate left, WorldCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldCoordinate left, WorldCoordinate right)
        {
            return !left.Equals(right);
        }

        public bool Equals(WorldCoordinate other)
        {
            return Sector.Equals(other.Sector) && LocalPositionMeters.Equals(other.LocalPositionMeters);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Sector.GetHashCode() * 397) ^ LocalPositionMeters.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Sector {0} Local {1}",
                Sector,
                LocalPositionMeters);
        }
    }
}
