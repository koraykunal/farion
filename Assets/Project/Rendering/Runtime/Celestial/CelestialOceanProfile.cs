using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial Ocean Profile", fileName = "SO_CelestialOceanProfile")]
    public sealed class CelestialOceanProfile : ScriptableObject
    {
        [Header("Radius")]
        [Min(0f)]
        [SerializeField] float radiusOffset;

        [Header("Color")]
        [SerializeField] Color deepColor = new(0.01f, 0.055f, 0.115f, 1f);
        [SerializeField] Color shallowColor = new(0.055f, 0.32f, 0.42f, 1f);
        [SerializeField] Color fresnelColor = new(0.48f, 0.82f, 0.92f, 1f);

        [Header("Waves")]
        [SerializeField] Texture2D waveNormalA;
        [SerializeField] Texture2D waveNormalB;
        [Min(0.001f)]
        [SerializeField] float waveNormalScale = 18f;
        [Min(0f)]
        [SerializeField] float waveSpeed = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] float waveStrength = 0.35f;

        [Header("Optical")]
        [Min(0f)]
        [SerializeField] float depthMultiplier = 10f;
        [Min(0f)]
        [SerializeField] float alphaMultiplier = 70f;

        [Header("Lighting")]
        [Range(0f, 1f)]
        [SerializeField] float smoothness = 0.92f;
        [Range(0f, 4f)]
        [SerializeField] float specularStrength = 1.35f;
        [SerializeField] Color specularColor = new(0.96f, 1f, 0.88f, 1f);
        [Range(0.5f, 8f)]
        [SerializeField] float fresnelPower = 4f;
        [Range(0f, 1f)]
        [SerializeField] float fresnelStrength = 0.45f;

        public event System.Action Changed;

        public Color DeepColor => deepColor;
        public Color ShallowColor => shallowColor;
        public Color FresnelColor => fresnelColor;
        public Texture2D WaveNormalA => waveNormalA;
        public Texture2D WaveNormalB => waveNormalB;
        public float WaveNormalScale => waveNormalScale;
        public float WaveSpeed => waveSpeed;
        public float WaveStrength => waveStrength;
        public float DepthMultiplier => depthMultiplier;
        public float AlphaMultiplier => alphaMultiplier;
        public float Smoothness => smoothness;
        public float SpecularStrength => specularStrength;
        public Color SpecularColor => specularColor;
        public float FresnelPower => fresnelPower;
        public float FresnelStrength => fresnelStrength;

        public float GetOceanRadius(float bodyRadius, Vector2 terrainRadiusMinMax, float oceanLevel)
        {
            bodyRadius = Mathf.Max(0.01f, bodyRadius);
            float minRadius = terrainRadiusMinMax.x > 0f ? terrainRadiusMinMax.x : bodyRadius;
            float seaRadius = Mathf.Lerp(minRadius, bodyRadius, Mathf.Clamp01(oceanLevel));
            return Mathf.Max(0.01f, seaRadius + radiusOffset);
        }

        void OnValidate()
        {
            radiusOffset = Mathf.Max(0f, radiusOffset);
            waveNormalScale = Mathf.Max(0.001f, waveNormalScale);
            waveSpeed = Mathf.Max(0f, waveSpeed);
            depthMultiplier = Mathf.Max(0f, depthMultiplier);
            alphaMultiplier = Mathf.Max(0f, alphaMultiplier);
            Changed?.Invoke();
        }
    }
}
