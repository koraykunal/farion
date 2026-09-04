using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [System.Serializable]
    public sealed class SurfaceVisualRule
    {
        [SerializeField] SurfaceMaterialDefinition material;

        [Header("Flat Terrain")]
        [SerializeField] Color flatLow = new(0.35f, 0.31f, 0.25f, 1f);
        [SerializeField] Color flatHigh = new(0.62f, 0.55f, 0.43f, 1f);

        [Header("Steep Terrain")]
        [SerializeField] Color steepLow = new(0.28f, 0.25f, 0.22f, 1f);
        [SerializeField] Color steepHigh = new(0.11f, 0.1f, 0.09f, 1f);

        [Header("Surface Response")]
        [Range(0f, 1f)]
        [SerializeField] float smoothness = 0.22f;

        [Header("Textures")]
        [SerializeField] SurfaceTextureSet textures = new();

        public SurfaceMaterialDefinition Material => material;
        public Color FlatLow => flatLow;
        public Color FlatHigh => flatHigh;
        public Color SteepLow => steepLow;
        public Color SteepHigh => steepHigh;
        public float NormalStrength => textures != null ? textures.NormalStrength : 0f;
        public float Smoothness => Mathf.Clamp01(smoothness);
        public SurfaceTextureSet Textures => textures;
        public bool IsValid => material != null;

        internal SurfaceVisualRule CreateTinted(float hueShiftDegrees, float valueScale)
        {
            return new SurfaceVisualRule
            {
                material = material,
                flatLow = PaletteVariation.Shift(flatLow, hueShiftDegrees, valueScale),
                flatHigh = PaletteVariation.Shift(flatHigh, hueShiftDegrees, valueScale),
                steepLow = PaletteVariation.Shift(steepLow, hueShiftDegrees, valueScale),
                steepHigh = PaletteVariation.Shift(steepHigh, hueShiftDegrees, valueScale),
                smoothness = smoothness,
                textures = textures
            };
        }
    }
}
