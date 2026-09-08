using Farion.Simulation.Planetary;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial/Terrestrial Surface Profile", fileName = "SO_TerrestrialSurfaceProfile")]
    public sealed class TerrestrialSurfaceProfile : CelestialSurfaceProfileBase
    {
        const float FarNormalFadeStartRadiusFraction = 0.3f;
        const float FarNormalFadeEndRadiusFraction = 0.8f;

        [Header("Material")]
        [SerializeField] Material material;
        [FormerlySerializedAs("biomeVisualProfile")]
        [SerializeField] SurfaceVisualProfile surfaceVisualProfile;
        [SerializeField] SurfaceTextureSet lavaOverlay = new();
        [SerializeField] SurfaceTextureSet snowOverlay = new();
        [Range(0f, 1f)]
        [SerializeField] float metallic;
        [Range(0f, 1f)]
        [SerializeField] float landSmoothness = 0.2f;

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

        [Header("Textures")]
        [SerializeField] Texture2D noiseTexture;
        [SerializeField] Texture2D rockNormal;
        [Min(0.001f)]
        [SerializeField] float noiseScale = 10f;
        [Min(0.001f)]
        [SerializeField] float noiseScale2 = 50f;
        [Min(0.001f)]
        [SerializeField] float rockNormalTileSize = 10f;
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
        [Range(0.001f, 0.12f)]
        [SerializeField] float oceanEdgeBlend = 0.035f;
        [Range(0f, 1f)]
        [SerializeField] float shoreWetness = 0.38f;
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

        [System.NonSerialized] SurfaceVisualProfile subscribedSurfaceVisualProfile;

        public override Material Material => material;
        public Color SteepLow => steepLow;
        public Color SteepHigh => steepHigh;
        public SurfaceVisualProfile SurfaceVisualProfile => surfaceVisualProfile;

        void OnEnable()
        {
            SyncSurfaceVisualProfileSubscription();
        }

        void OnDisable()
        {
            UnsubscribeFromSurfaceVisualProfile();
        }

        public override void ApplyMaterialProperties(
            Material target,
            float bodyRadius,
            Vector2 radiusMinMax)
        {
            ApplyMaterialProperties(target, bodyRadius, radiusMinMax, 0f, false);
        }

        public void ApplyMaterialProperties(
            Material target,
            float bodyRadius,
            Vector2 radiusMinMax,
            float oceanLevelOverride)
        {
            ApplyMaterialProperties(target, bodyRadius, radiusMinMax, oceanLevelOverride, true);
        }

        public void ApplyMaterialProperties(
            Material target,
            float bodyRadius,
            Vector2 radiusMinMax,
            float oceanLevelOverride,
            bool hasOcean)
        {
            target.SetFloat("_BodyRadius", bodyRadius);
            target.SetVector("_RadiusMinMax", radiusMinMax);
            target.SetFloat("_FarNormalFadeStart", bodyRadius * FarNormalFadeStartRadiusFraction);
            target.SetFloat("_FarNormalFadeEnd", bodyRadius * FarNormalFadeEndRadiusFraction);
            target.SetFloat("_HasOcean", hasOcean ? 1f : 0f);
            target.SetFloat("_OceanLevel", Mathf.Clamp01(oceanLevelOverride));
            target.SetFloat("_Metallic", metallic);
            target.SetFloat("_LandSmoothness", landSmoothness);

            if (surfaceVisualProfile != null)
            {
                surfaceVisualProfile.ApplyMaterialProperties(target);
            }
            else
            {
                SurfaceVisualProfile.ClearMaterialProperties(target);
            }

            ApplyOverlayProperties(target, "_Lava", lavaOverlay);
            ApplyOverlayProperties(target, "_Snow", snowOverlay);

            target.SetColor("_OceanLow", oceanLow);
            target.SetColor("_OceanHigh", oceanHigh);
            target.SetColor("_ShoreLow", shoreLow);
            target.SetColor("_ShoreHigh", shoreHigh);
            target.SetColor("_FlatLowA", flatLowA);
            target.SetColor("_FlatHighA", flatHighA);
            target.SetColor("_FlatLowB", flatLowB);
            target.SetColor("_FlatHighB", flatHighB);
            target.SetColor("_SteepLow", steepLow);
            target.SetColor("_SteepHigh", steepHigh);

            target.SetFloat("_NoiseScale", noiseScale);
            target.SetFloat("_NoiseScale2", noiseScale2);
            target.SetFloat("_RockNormalTileSize", rockNormalTileSize);
            target.SetFloat("_NormalStrength", normalStrength);
            target.SetFloat("_FlatColorBlend", flatColorBlend);
            target.SetFloat("_FlatColorBlendNoise", flatColorBlendNoise);
            target.SetFloat("_ShoreHeight", shoreHeight);
            target.SetFloat("_ShoreBlend", shoreBlend);
            target.SetFloat("_OceanEdgeBlend", oceanEdgeBlend);
            target.SetFloat("_ShoreWetness", shoreWetness);
            target.SetFloat("_MaxFlatHeight", maxFlatHeight);
            target.SetFloat("_SteepBands", steepBands);
            target.SetFloat("_SteepBandStrength", steepBandStrength);
            target.SetFloat("_SteepnessThreshold", steepnessThreshold);
            target.SetFloat("_FlatToSteepBlend", flatToSteepBlend);
            target.SetFloat("_FlatToSteepNoise", flatToSteepNoise);

            if (noiseTexture != null)
            {
                target.SetTexture("_NoiseTex", noiseTexture);
            }

            if (rockNormal != null)
            {
                target.SetTexture("_RockNormal", rockNormal);
            }
        }

        public TerrestrialSurfaceProfile CreateVariant(SurfaceVisualProfile visualProfile)
        {
            TerrestrialSurfaceProfile variant = ProfileVariants.Clone(this);
            variant.surfaceVisualProfile = visualProfile;
            variant.SyncSurfaceVisualProfileSubscription();
            return variant;
        }

        void OnValidate()
        {
            noiseScale = Mathf.Max(0.001f, noiseScale);
            noiseScale2 = Mathf.Max(0.001f, noiseScale2);
            rockNormalTileSize = Mathf.Max(0.001f, rockNormalTileSize);
            oceanEdgeBlend = Mathf.Clamp(oceanEdgeBlend, 0.001f, 0.12f);
            shoreWetness = Mathf.Clamp01(shoreWetness);
            SyncSurfaceVisualProfileSubscription();
            NotifyChanged();
        }

        static void ApplyOverlayProperties(
            Material target,
            string propertyPrefix,
            SurfaceTextureSet textures)
        {
            bool enabled = textures != null && textures.HasSurfaceTextures;
            target.SetFloat($"{propertyPrefix}OverlayEnabled", enabled ? 1f : 0f);
            if (!enabled)
            {
                return;
            }

            target.SetTexture($"{propertyPrefix}BaseColor", textures.BaseColor);
            target.SetTexture($"{propertyPrefix}Normal", textures.Normal);
            target.SetTexture($"{propertyPrefix}Roughness", textures.Roughness);
            if (textures.Emission != null)
            {
                target.SetTexture($"{propertyPrefix}Emission", textures.Emission);
            }

            target.SetFloat($"{propertyPrefix}WorldTileSize", textures.WorldTileSize);
            target.SetFloat($"{propertyPrefix}NormalStrength", textures.NormalStrength);
            target.SetColor($"{propertyPrefix}EmissionTint", textures.EmissionTint);
            target.SetFloat($"{propertyPrefix}EmissionStrength", textures.EmissionStrength);
        }

        void SyncSurfaceVisualProfileSubscription()
        {
            if (subscribedSurfaceVisualProfile == surfaceVisualProfile)
            {
                return;
            }

            UnsubscribeFromSurfaceVisualProfile();
            subscribedSurfaceVisualProfile = surfaceVisualProfile;
            if (subscribedSurfaceVisualProfile != null)
            {
                subscribedSurfaceVisualProfile.Changed += HandleSurfaceVisualProfileChanged;
            }
        }

        void UnsubscribeFromSurfaceVisualProfile()
        {
            if (subscribedSurfaceVisualProfile == null)
            {
                return;
            }

            subscribedSurfaceVisualProfile.Changed -= HandleSurfaceVisualProfileChanged;
            subscribedSurfaceVisualProfile = null;
        }

        void HandleSurfaceVisualProfileChanged()
        {
            NotifyChanged();
        }
    }
}
