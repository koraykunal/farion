using System;

namespace Farion.Simulation.World.Coordinates
{
    public static class WorldCoordinateUtility
    {
        public const double SectorSizeMeters = 1000000d;
        public const double HalfSectorSizeMeters = SectorSizeMeters * 0.5d;

        public static void Normalize(
            WorldSectorCoordinate sector,
            WorldVector3d localPositionMeters,
            out WorldSectorCoordinate normalizedSector,
            out WorldVector3d normalizedLocalPositionMeters)
        {
            ValidateFinite(localPositionMeters);

            long sectorOffsetX = CalculateSectorOffset(localPositionMeters.X);
            long sectorOffsetY = CalculateSectorOffset(localPositionMeters.Y);
            long sectorOffsetZ = CalculateSectorOffset(localPositionMeters.Z);

            normalizedSector = new WorldSectorCoordinate(
                sector.X + sectorOffsetX,
                sector.Y + sectorOffsetY,
                sector.Z + sectorOffsetZ);
            normalizedLocalPositionMeters = new WorldVector3d(
                localPositionMeters.X - sectorOffsetX * SectorSizeMeters,
                localPositionMeters.Y - sectorOffsetY * SectorSizeMeters,
                localPositionMeters.Z - sectorOffsetZ * SectorSizeMeters);
        }

        public static WorldCoordinate FromAbsoluteMeters(WorldVector3d absolutePositionMeters)
        {
            return new WorldCoordinate(WorldSectorCoordinate.Zero, absolutePositionMeters);
        }

        public static WorldVector3d ToAbsoluteMeters(WorldCoordinate coordinate)
        {
            return new WorldVector3d(
                coordinate.Sector.X * SectorSizeMeters + coordinate.LocalPositionMeters.X,
                coordinate.Sector.Y * SectorSizeMeters + coordinate.LocalPositionMeters.Y,
                coordinate.Sector.Z * SectorSizeMeters + coordinate.LocalPositionMeters.Z);
        }

        public static bool IsNormalized(WorldVector3d localPositionMeters)
        {
            return localPositionMeters.IsFinite &&
                localPositionMeters.X >= -HalfSectorSizeMeters &&
                localPositionMeters.X < HalfSectorSizeMeters &&
                localPositionMeters.Y >= -HalfSectorSizeMeters &&
                localPositionMeters.Y < HalfSectorSizeMeters &&
                localPositionMeters.Z >= -HalfSectorSizeMeters &&
                localPositionMeters.Z < HalfSectorSizeMeters;
        }

        static long CalculateSectorOffset(double localPositionMeters)
        {
            return (long)Math.Floor((localPositionMeters + HalfSectorSizeMeters) / SectorSizeMeters);
        }

        static void ValidateFinite(WorldVector3d value)
        {
            if (!value.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "World coordinates must be finite.");
            }
        }
    }
}
