using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [System.Serializable]
    public sealed class BiomeVisualRule
    {
        [SerializeField] BiomeDefinition biome;

        [Header("Flat Terrain")]
        [SerializeField] Color flatLow = new(0.35f, 0.31f, 0.25f, 1f);
        [SerializeField] Color flatHigh = new(0.62f, 0.55f, 0.43f, 1f);

        [Header("Steep Terrain")]
        [SerializeField] Color steepLow = new(0.28f, 0.25f, 0.22f, 1f);
        [SerializeField] Color steepHigh = new(0.11f, 0.1f, 0.09f, 1f);

        [Header("Surface Response")]
        [Range(0f, 1f)]
        [SerializeField] float normalStrength = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] float smoothness = 0.22f;

        [Header("Textures")]
        [SerializeField] BiomeSurfaceTextureSet textures = new();

        public BiomeDefinition Biome => biome;
        public Color FlatLow => flatLow;
        public Color FlatHigh => flatHigh;
        public Color SteepLow => steepLow;
        public Color SteepHigh => steepHigh;
        public float NormalStrength => Mathf.Clamp01(normalStrength);
        public float Smoothness => Mathf.Clamp01(smoothness);
        public BiomeSurfaceTextureSet Textures => textures;
        public bool IsValid => biome != null;
    }
}
