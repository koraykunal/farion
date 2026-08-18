using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial Ocean Profile", fileName = "SO_CelestialOceanProfile")]
    public sealed class CelestialOceanProfile : ScriptableObject
    {
        [Header("Color")]
        [SerializeField] Color deepColor = new(0.01f, 0.055f, 0.115f, 1f);
        [SerializeField] Color shallowColor = new(0.055f, 0.32f, 0.42f, 1f);
        [Tooltip("Reflected sky colour looking straight up.")]
        [SerializeField] Color fresnelColor = new(0.48f, 0.82f, 0.92f, 1f);
        [Tooltip("Reflected sky colour towards the horizon. Warm it up for sunset skies.")]
        [SerializeField] Color horizonColor = new(0.62f, 0.72f, 0.8f, 1f);

        [Header("Detail Ripples")]
        [SerializeField] Texture2D waveNormalA;
        [SerializeField] Texture2D waveNormalB;
        [Tooltip("World-space size of one detail normal tile. The large swell comes from the hydrosphere profile.")]
        [Min(0.01f)]
        [SerializeField] float detailTileSize = 30f;
        [Min(0f)]
        [SerializeField] float waveSpeed = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] float waveStrength = 0.35f;

        [Header("Foam")]
        [SerializeField] Color foamColor = new(0.86f, 0.92f, 0.95f, 1f);
        [Tooltip("Sea-floor distance below which shoreline foam appears, in world units. Must stay well under the typical ocean depth of this body.")]
        [Min(0f)]
        [SerializeField] float foamWidth = 1.5f;
        [Range(0f, 1f)]
        [SerializeField] float foamStrength = 0.16f;

        [Header("Optical")]
        [Min(0f)]
        [SerializeField] float depthMultiplier = 10f;
        [Min(0f)]
        [SerializeField] float alphaMultiplier = 70f;
        [Header("Underwater")]
        [SerializeField] Color underwaterColor = new(0.01f, 0.12f, 0.18f, 1f);
        [Tooltip("World-space distance at which roughly 95% of the least-absorbed direct light has been lost underwater.")]
        [Min(0.01f)]
        [SerializeField] float underwaterVisibilityDistance = 50f;
        [Range(0f, 1f)]
        [SerializeField] float underwaterSpecularStrength = 0.08f;

        [Header("Lighting")]
        [Range(0f, 1f)]
        [SerializeField] float smoothness = 0.92f;
        [Range(0f, 4f)]
        [SerializeField] float specularStrength = 1.35f;
        [SerializeField] Color specularColor = new(0.96f, 1f, 0.88f, 1f);
        [Min(0.001f)]
        [SerializeField] float referenceLightIntensity = 1.1f;
        [Tooltip("Optical index of refraction. Pure water is approximately 1.333.")]
        [Range(1.0001f, 2f)]
        [SerializeField] float indexOfRefraction = 1.333f;
        [Tooltip("Overall strength of light scattered by the water body.")]
        [Range(0f, 2f)]
        [SerializeField] float scatterStrength = 0.55f;

        public event System.Action Changed;

        public Color DeepColor => deepColor;
        public Color ShallowColor => shallowColor;
        public Color FresnelColor => fresnelColor;
        public Color HorizonColor => horizonColor;
        public Texture2D WaveNormalA => waveNormalA;
        public Texture2D WaveNormalB => waveNormalB;
        public float DetailNormalScale => 1f / Mathf.Max(0.01f, detailTileSize);
        public float WaveSpeed => waveSpeed;
        public float WaveStrength => waveStrength;
        public Color FoamColor => foamColor;
        public float FoamWidth => foamWidth;
        public float FoamStrength => foamStrength;
        public float DepthMultiplier => depthMultiplier;
        public float AlphaMultiplier => alphaMultiplier;
        public Color UnderwaterColor => underwaterColor;
        public Vector3 UnderwaterExtinctionCoefficients
        {
            get
            {
                float baseExtinction = 2.995732f / Mathf.Max(0.01f, underwaterVisibilityDistance);
                float strongestChannel = Mathf.Max(
                    Mathf.Max(underwaterColor.r, underwaterColor.g),
                    Mathf.Max(underwaterColor.b, 0.001f));
                return new Vector3(
                    Mathf.Lerp(2.4f, 1f, underwaterColor.r / strongestChannel),
                    Mathf.Lerp(2.4f, 1f, underwaterColor.g / strongestChannel),
                    Mathf.Lerp(2.4f, 1f, underwaterColor.b / strongestChannel)) * baseExtinction;
            }
        }
        public float UnderwaterVisibilityDistance => underwaterVisibilityDistance;
        public float UnderwaterSpecularStrength => underwaterSpecularStrength;
        public float Smoothness => smoothness;
        public float SpecularStrength => specularStrength;
        public Color SpecularColor => specularColor;
        public float ReferenceLightIntensity => referenceLightIntensity;
        public float IndexOfRefraction => indexOfRefraction;
        public float ScatterStrength => scatterStrength;

        void OnValidate()
        {
            detailTileSize = Mathf.Max(0.01f, detailTileSize);
            waveSpeed = Mathf.Max(0f, waveSpeed);
            depthMultiplier = Mathf.Max(0f, depthMultiplier);
            alphaMultiplier = Mathf.Max(0f, alphaMultiplier);
            underwaterVisibilityDistance = Mathf.Max(0.01f, underwaterVisibilityDistance);
            referenceLightIntensity = Mathf.Max(0.001f, referenceLightIntensity);
            indexOfRefraction = Mathf.Clamp(indexOfRefraction, 1.0001f, 2f);
            foamWidth = Mathf.Max(0f, foamWidth);
            Changed?.Invoke();
        }
    }
}
