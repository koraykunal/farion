using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Earth-Like Surface Profile", fileName = "SO_EarthLikeSurfaceProfile")]
    public sealed class EarthLikeSurfaceProfile : CelestialSurfaceProfileBase
    {
        [Header("Material")]
        [SerializeField] Material material;
        [Range(0f, 1f)]
        [SerializeField] float metallic;
        [Range(0f, 1f)]
        [SerializeField] float landSmoothness = 0.2f;
        [Range(0f, 1f)]
        [SerializeField] float oceanSmoothness = 0.75f;

        [Header("Colors")]
        [SerializeField] Color oceanLow = new(0.015f, 0.08f, 0.16f, 1f);
        [SerializeField] Color oceanHigh = new(0.05f, 0.32f, 0.48f, 1f);
        [SerializeField] Color shoreLow = new(0.98f, 1f, 0.67f, 1f);
        [SerializeField] Color shoreHigh = new(0.95f, 0.91f, 0.38f, 1f);
        [SerializeField] Color flatLowA = new(0.79f, 0.86f, 0f, 1f);
        [SerializeField] Color flatHighA = new(0.19f, 0.46f, 0f, 1f);
        [SerializeField] Color flatLowB = new(0.58f, 0.86f, 0f, 1f);
        [SerializeField] Color flatHighB = new(0.19f, 0.46f, 0f, 1f);
        [SerializeField] Color steepLow = new(0.53f, 0.49f, 0.19f, 1f);
        [SerializeField] Color steepHigh = new(0.15f, 0.07f, 0f, 1f);
        [SerializeField] Color snowColor = Color.white;

        [Header("Textures")]
        [SerializeField] Texture2D noiseTexture;
        [SerializeField] Texture2D rockNormal;
        [SerializeField] Texture2D snowNormal;
        [Min(0.001f)]
        [SerializeField] float noiseScale = 10f;
        [Min(0.001f)]
        [SerializeField] float noiseScale2 = 50f;
        [Min(0.001f)]
        [SerializeField] float rockNormalScale = 21f;
        [Min(0.001f)]
        [SerializeField] float snowNormalScale = 21f;
        [Range(0f, 1f)]
        [SerializeField] float normalStrength = 0.5f;

        [Header("Blending")]
        [Range(0f, 3f)]
        [SerializeField] float flatColorBlend = 1.5f;
        [Range(0f, 1f)]
        [SerializeField] float flatColorBlendNoise = 0.3f;
        [Range(0f, 0.25f)]
        [SerializeField] float shoreHeight = 0.058f;
        [Range(0f, 0.25f)]
        [SerializeField] float shoreBlend = 0.089f;
        [Range(0f, 1f)]
        [SerializeField] float maxFlatHeight = 0.52f;
        [Range(1f, 20f)]
        [SerializeField] float steepBands = 8f;
        [Range(-1f, 1f)]
        [SerializeField] float steepBandStrength = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] float steepnessThreshold = 0.378f;
        [Range(0f, 0.3f)]
        [SerializeField] float flatToSteepBlend = 0.051f;
        [Range(0f, 0.2f)]
        [SerializeField] float flatToSteepNoise = 0.026f;

        [Header("Snow")]
        [SerializeField] bool useSnowyPoles;
        [Range(0f, 1f)]
        [SerializeField] float snowLongitude = 0.94f;
        [Range(0f, 0.2f)]
        [SerializeField] float snowBlend = 0.03f;
        [Range(0f, 1f)]
        [SerializeField] float snowSpecular = 0.7f;
        [Range(1f, 2f)]
        [SerializeField] float snowHighlight = 1.2f;
        [Range(0f, 10f)]
        [SerializeField] float snowNoiseA = 3f;
        [Range(0f, 10f)]
        [SerializeField] float snowNoiseB = 2.87f;

        public override Material Material => material;

        public override void ApplyMaterialProperties(
            MaterialPropertyBlock propertyBlock,
            float bodyRadius,
            Vector2 radiusMinMax)
        {
            ApplyMaterialProperties(propertyBlock, bodyRadius, radiusMinMax, 0f, false);
        }

        public void ApplyMaterialProperties(
            MaterialPropertyBlock propertyBlock,
            float bodyRadius,
            Vector2 radiusMinMax,
            float oceanLevelOverride)
        {
            ApplyMaterialProperties(propertyBlock, bodyRadius, radiusMinMax, oceanLevelOverride, true);
        }

        public void ApplyMaterialProperties(
            MaterialPropertyBlock propertyBlock,
            float bodyRadius,
            Vector2 radiusMinMax,
            float oceanLevelOverride,
            bool hasOcean)
        {
            propertyBlock.SetFloat("_BodyRadius", bodyRadius);
            propertyBlock.SetVector("_RadiusMinMax", radiusMinMax);
            propertyBlock.SetFloat("_HasOcean", hasOcean ? 1f : 0f);
            propertyBlock.SetFloat("_OceanLevel", Mathf.Clamp01(oceanLevelOverride));
            propertyBlock.SetFloat("_Metallic", metallic);
            propertyBlock.SetFloat("_LandSmoothness", landSmoothness);
            propertyBlock.SetFloat("_OceanSmoothness", oceanSmoothness);

            propertyBlock.SetColor("_OceanLow", oceanLow);
            propertyBlock.SetColor("_OceanHigh", oceanHigh);
            propertyBlock.SetColor("_ShoreLow", shoreLow);
            propertyBlock.SetColor("_ShoreHigh", shoreHigh);
            propertyBlock.SetColor("_FlatLowA", flatLowA);
            propertyBlock.SetColor("_FlatHighA", flatHighA);
            propertyBlock.SetColor("_FlatLowB", flatLowB);
            propertyBlock.SetColor("_FlatHighB", flatHighB);
            propertyBlock.SetColor("_SteepLow", steepLow);
            propertyBlock.SetColor("_SteepHigh", steepHigh);
            propertyBlock.SetColor("_SnowColor", snowColor);

            propertyBlock.SetFloat("_NoiseScale", noiseScale);
            propertyBlock.SetFloat("_NoiseScale2", noiseScale2);
            propertyBlock.SetFloat("_RockNormalScale", rockNormalScale);
            propertyBlock.SetFloat("_SnowNormalScale", snowNormalScale);
            propertyBlock.SetFloat("_NormalStrength", normalStrength);
            propertyBlock.SetFloat("_FlatColorBlend", flatColorBlend);
            propertyBlock.SetFloat("_FlatColorBlendNoise", flatColorBlendNoise);
            propertyBlock.SetFloat("_ShoreHeight", shoreHeight);
            propertyBlock.SetFloat("_ShoreBlend", shoreBlend);
            propertyBlock.SetFloat("_MaxFlatHeight", maxFlatHeight);
            propertyBlock.SetFloat("_SteepBands", steepBands);
            propertyBlock.SetFloat("_SteepBandStrength", steepBandStrength);
            propertyBlock.SetFloat("_SteepnessThreshold", steepnessThreshold);
            propertyBlock.SetFloat("_FlatToSteepBlend", flatToSteepBlend);
            propertyBlock.SetFloat("_FlatToSteepNoise", flatToSteepNoise);
            propertyBlock.SetFloat("_UseSnowyPoles", useSnowyPoles ? 1f : 0f);
            propertyBlock.SetFloat("_SnowLongitude", snowLongitude);
            propertyBlock.SetFloat("_SnowBlend", snowBlend);
            propertyBlock.SetFloat("_SnowSpecular", snowSpecular);
            propertyBlock.SetFloat("_SnowHighlight", snowHighlight);
            propertyBlock.SetFloat("_SnowNoiseA", snowNoiseA);
            propertyBlock.SetFloat("_SnowNoiseB", snowNoiseB);

            if (noiseTexture != null)
            {
                propertyBlock.SetTexture("_NoiseTex", noiseTexture);
            }

            if (rockNormal != null)
            {
                propertyBlock.SetTexture("_RockNormal", rockNormal);
            }

            if (snowNormal != null)
            {
                propertyBlock.SetTexture("_SnowNormal", snowNormal);
            }
        }

        void OnValidate()
        {
            noiseScale = Mathf.Max(0.001f, noiseScale);
            noiseScale2 = Mathf.Max(0.001f, noiseScale2);
            rockNormalScale = Mathf.Max(0.001f, rockNormalScale);
            snowNormalScale = Mathf.Max(0.001f, snowNormalScale);
            NotifyChanged();
        }
    }
}
