using Farion.Simulation.Celestial;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Gameplay.Actors
{
    public readonly struct CelestialSurfacePlacementResult
    {
        public CelestialSurfacePlacementResult(
            CelestialFrameSample frame,
            Vector3 position,
            Quaternion rotation,
            Vector3 placementUp,
            float terrainRadius,
            bool usesOceanSurface,
            bool hasPlanetSurface,
            PlanetSurfaceSample planetSurface)
        {
            Frame = frame;
            Position = position;
            Rotation = rotation;
            PlacementUp = placementUp.sqrMagnitude > 0.0001f ? placementUp.normalized : Vector3.up;
            TerrainRadius = Mathf.Max(0f, terrainRadius);
            UsesOceanSurface = usesOceanSurface;
            HasPlanetSurface = hasPlanetSurface;
            PlanetSurface = planetSurface;
        }

        public CelestialFrameSample Frame { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 PlacementUp { get; }
        public float TerrainRadius { get; }
        public bool UsesOceanSurface { get; }
        public bool HasPlanetSurface { get; }
        public PlanetSurfaceSample PlanetSurface { get; }
        public bool HasBiome => HasPlanetSurface && PlanetSurface.Biome.Biome != null;
    }
}
