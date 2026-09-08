using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public readonly struct TerrainSculptLayer
    {
        public TerrainSculptLayer(
            TerrainSculptStyle style,
            float amplitudeMeters,
            float footprintMeters,
            Vector2 noiseRange,
            float noiseFeather,
            int seed)
        {
            Style = style;
            AmplitudeMeters = Mathf.Max(0f, amplitudeMeters);
            FootprintMeters = Mathf.Max(1f, footprintMeters);
            NoiseRange = noiseRange;
            NoiseFeather = Mathf.Max(0f, noiseFeather);
            Seed = seed;
        }

        public TerrainSculptStyle Style { get; }
        public float AmplitudeMeters { get; }
        public float FootprintMeters { get; }
        public Vector2 NoiseRange { get; }
        public float NoiseFeather { get; }
        public int Seed { get; }
    }
}
