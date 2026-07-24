using UnityEngine;

namespace Farion.Gameplay.Actors
{
    public readonly struct CelestialSurfacePlacementOptions
    {
        public CelestialSurfacePlacementOptions(
            float terrainSurfaceOffset,
            float oceanSurfaceOffset,
            CelestialSurfacePlacementMode mode = CelestialSurfacePlacementMode.TerrainOrOceanSurface)
        {
            TerrainSurfaceOffset = Mathf.Max(0f, terrainSurfaceOffset);
            OceanSurfaceOffset = Mathf.Max(0f, oceanSurfaceOffset);
            Mode = mode;
        }

        public float TerrainSurfaceOffset { get; }
        public float OceanSurfaceOffset { get; }
        public CelestialSurfacePlacementMode Mode { get; }
        public bool AllowsOceanSurface => Mode == CelestialSurfacePlacementMode.TerrainOrOceanSurface;
    }
}
