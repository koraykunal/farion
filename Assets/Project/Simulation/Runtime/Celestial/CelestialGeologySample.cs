using UnityEngine;

namespace Farion.Simulation.Celestial
{
    public readonly struct CelestialGeologySample
    {
        public CelestialGeologySample(
            float featureStrength,
            float featureHeight,
            Vector3 featureDirection,
            float convexity)
        {
            FeatureStrength = Mathf.Clamp01(featureStrength);
            FeatureHeight = Mathf.Max(0f, featureHeight);
            FeatureDirection = featureDirection;
            Convexity = Mathf.Clamp(convexity, -1f, 1f);
        }

        public float FeatureStrength { get; }
        public float FeatureHeight { get; }
        public Vector3 FeatureDirection { get; }
        public float Convexity { get; }
        public bool HasFeature => FeatureStrength > 0.0001f && FeatureHeight > 0.01f;

        public static CelestialGeologySample None =>
            new(0f, 0f, Vector3.zero, 0f);
    }
}
