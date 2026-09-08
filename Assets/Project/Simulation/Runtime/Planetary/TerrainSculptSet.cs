using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public readonly struct TerrainSculptSet
    {
        static readonly TerrainSculptLayer[] NoLayers = System.Array.Empty<TerrainSculptLayer>();

        public TerrainSculptSet(int seed, float noiseScale, IReadOnlyList<TerrainSculptLayer> layers)
        {
            Seed = seed;
            NoiseScale = Mathf.Max(0.01f, noiseScale);
            Layers = layers ?? NoLayers;
        }

        public int Seed { get; }
        public float NoiseScale { get; }
        public IReadOnlyList<TerrainSculptLayer> Layers { get; }
        public bool IsEmpty => Layers == null || Layers.Count == 0;
    }
}
