using Farion.Core.Physics;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public readonly struct PlanetSurfaceSample
    {
        public PlanetSurfaceSample(
            PlanetGenerationContext context,
            CelestialSurfaceSample surface,
            Vector3 localDirection,
            float surfaceRadius,
            PlanetClimateSample climate,
            BiomeSample biome,
            SurfaceMaterialSample surfaceMaterial,
            PlanetSurfaceStateSample surfaceState,
            TerrainFeatureSample terrainFeature)
        {
            Context = context;
            Surface = surface;
            LocalDirection = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            SurfaceRadius = Mathf.Max(0.01f, surfaceRadius);
            TerrainAltitude = SurfaceRadius - context.Radius;
            Climate = climate;
            Biome = biome;
            SurfaceMaterial = surfaceMaterial;
            SurfaceState = surfaceState;
            TerrainFeature = terrainFeature;
        }

        public PlanetGenerationContext Context { get; }
        public CelestialSurfaceSample Surface { get; }
        public Vector3 LocalDirection { get; }
        public float SurfaceRadius { get; }
        public float TerrainAltitude { get; }
        public PlanetClimateSample Climate { get; }
        public BiomeSample Biome { get; }
        public SurfaceMaterialSample SurfaceMaterial { get; }
        public PlanetSurfaceStateSample SurfaceState { get; }
        public TerrainFeatureSample TerrainFeature { get; }
        public bool HasBody => Surface.HasBody;
    }
}
