using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public readonly struct TerrainFeatureSample
    {
        public TerrainFeatureSample(
            TerrainFeatureDefinition feature,
            float featureNoise,
            float altitude,
            float slopeDegrees)
        {
            Feature = feature;
            FeatureNoise = Mathf.Clamp01(featureNoise);
            Altitude = altitude;
            SlopeDegrees = Mathf.Max(0f, slopeDegrees);
        }

        public TerrainFeatureDefinition Feature { get; }
        public float FeatureNoise { get; }
        public float Altitude { get; }
        public float SlopeDegrees { get; }
        public bool HasFeature => Feature != null;
    }
}
