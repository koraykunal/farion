using System;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [Serializable]
    public sealed class SurfaceScatterShaderBinding
    {
        [SerializeField] string baseTintProperty = "Color_EFB080B8";
        [SerializeField] string snowIntensityProperty = "Vector1_8";
        [SerializeField] string mossIntensityProperty = "Vector1_9";
        [SerializeField, Range(0f, 1f)] float steepColourBias = 0.35f;
        [SerializeField, Range(0.25f, 2f)] float tintLuminance = 1f;
        [SerializeField, Range(1f, 3f)] float saturationBoost = 1.35f;

        public bool IsBound =>
            !string.IsNullOrWhiteSpace(baseTintProperty) ||
            !string.IsNullOrWhiteSpace(snowIntensityProperty) ||
            !string.IsNullOrWhiteSpace(mossIntensityProperty);

        public void Apply(
            MaterialPropertyBlock propertyBlock,
            SurfaceVisualProfile visualProfile,
            in PlanetSurfaceSample sample,
            float tintStrength,
            float snowResponse,
            float mossResponse)
        {
            if (propertyBlock == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(baseTintProperty))
            {
                propertyBlock.SetColor(
                    baseTintProperty,
                    ResolveTint(visualProfile, sample, tintStrength));
            }

            if (!string.IsNullOrWhiteSpace(snowIntensityProperty))
            {
                propertyBlock.SetFloat(
                    snowIntensityProperty,
                    Mathf.Clamp01(sample.SurfaceState.SnowCover * snowResponse));
            }

            if (!string.IsNullOrWhiteSpace(mossIntensityProperty))
            {
                propertyBlock.SetFloat(
                    mossIntensityProperty,
                    Mathf.Clamp01(sample.Climate.EffectiveMoisture * mossResponse));
            }
        }

        Color ResolveTint(
            SurfaceVisualProfile visualProfile,
            in PlanetSurfaceSample sample,
            float tintStrength)
        {
            Color neutral = new(tintLuminance, tintLuminance, tintLuminance, 1f);
            if (!TryResolveRule(visualProfile, sample.SurfaceMaterial.Material, out SurfaceVisualRule rule))
            {
                return neutral;
            }

            Color surfaceColour = Color.Lerp(rule.SteepLow, rule.SteepHigh, steepColourBias);
            return Color.Lerp(
                neutral,
                Normalise(surfaceColour, tintLuminance, saturationBoost),
                Mathf.Clamp01(tintStrength));
        }

        static bool TryResolveRule(
            SurfaceVisualProfile visualProfile,
            SurfaceMaterialDefinition material,
            out SurfaceVisualRule result)
        {
            result = null;
            if (visualProfile == null || material == null)
            {
                return false;
            }

            for (int i = 0; i < visualProfile.Rules.Count; i++)
            {
                SurfaceVisualRule rule = visualProfile.Rules[i];
                if (rule != null && rule.IsValid && rule.Material == material)
                {
                    result = rule;
                    return true;
                }
            }

            return false;
        }

        static Color Normalise(Color colour, float targetValue, float saturation)
        {
            float peak = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
            if (peak <= 0.001f)
            {
                return new Color(targetValue, targetValue, targetValue, 1f);
            }

            return new Color(
                Saturate(colour.r / peak, saturation) * targetValue,
                Saturate(colour.g / peak, saturation) * targetValue,
                Saturate(colour.b / peak, saturation) * targetValue,
                1f);
        }

        static float Saturate(float channel, float saturation)
        {
            return Mathf.Max(0f, 1f - (1f - channel) * saturation);
        }

        internal void Validate()
        {
            steepColourBias = Mathf.Clamp01(steepColourBias);
            tintLuminance = Mathf.Clamp(tintLuminance, 0.25f, 2f);
            saturationBoost = Mathf.Clamp(saturationBoost, 1f, 3f);
        }
    }
}
