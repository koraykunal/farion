using System;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(
        menuName = "Farion/Rendering/Celestial/Star Visual Profile",
        fileName = "SO_CelestialStarVisualProfile")]
    public sealed class CelestialStarVisualProfile : ScriptableObject
    {
        static readonly int PhotosphereColorId = Shader.PropertyToID("_PhotosphereColor");
        static readonly int LimbColorId = Shader.PropertyToID("_LimbColor");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int LimbDarkeningId = Shader.PropertyToID("_LimbDarkening");
        static readonly int GranulationScaleId = Shader.PropertyToID("_GranulationScale");
        static readonly int GranulationStrengthId = Shader.PropertyToID("_GranulationStrength");
        static readonly int GranulationSpeedId = Shader.PropertyToID("_GranulationSpeed");
        static readonly int CellScaleId = Shader.PropertyToID("_CellScale");
        static readonly int CellStrengthId = Shader.PropertyToID("_CellStrength");
        static readonly int CellSpeedId = Shader.PropertyToID("_CellSpeed");
        static readonly int SunspotScaleId = Shader.PropertyToID("_SunspotScale");
        static readonly int SunspotStrengthId = Shader.PropertyToID("_SunspotStrength");
        static readonly int SunspotThresholdId = Shader.PropertyToID("_SunspotThreshold");
        static readonly int SurfaceRotationSpeedId = Shader.PropertyToID("_SurfaceRotationSpeed");
        static readonly int PulseAmplitudeId = Shader.PropertyToID("_PulseAmplitude");
        static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");

        [Header("Material")]
        [SerializeField] Material material;

        [Header("Photosphere")]
        [ColorUsage(true, true)]
        [SerializeField] Color photosphereColor = new(1f, 0.58f, 0.16f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] Color limbColor = new(1f, 0.18f, 0.015f, 1f);
        [Min(0f)]
        [SerializeField] float intensity = 5.5f;
        [Range(0.1f, 4f)]
        [SerializeField] float limbDarkening = 0.72f;

        [Header("Surface Motion")]
        [Min(0.01f)]
        [SerializeField] float granulationScale = 46f;
        [Range(0f, 1f)]
        [SerializeField] float granulationStrength = 0.22f;
        [Min(0f)]
        [SerializeField] float granulationSpeed = 0.018f;
        [Min(0.01f)]
        [SerializeField] float cellScale = 8f;
        [Range(0f, 1f)]
        [SerializeField] float cellStrength = 0.12f;
        [Min(0f)]
        [SerializeField] float cellSpeed = 0.035f;
        [Min(0f)]
        [SerializeField] float surfaceRotationSpeed = 0.35f;

        [Header("Sunspots")]
        [Min(0.01f)]
        [SerializeField] float sunspotScale = 5.5f;
        [Range(0f, 1f)]
        [SerializeField] float sunspotStrength = 0.42f;
        [Range(0f, 1f)]
        [SerializeField] float sunspotThreshold = 0.7f;

        [Header("Pulse")]
        [Range(0f, 0.2f)]
        [SerializeField] float pulseAmplitude = 0.012f;
        [Min(0f)]
        [SerializeField] float pulseSpeed = 0.65f;

        public event Action Changed;

        public Material Material => material;

        public void ApplyMaterialProperties(MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock == null)
            {
                return;
            }

            propertyBlock.SetColor(PhotosphereColorId, photosphereColor);
            propertyBlock.SetColor(LimbColorId, limbColor);
            propertyBlock.SetFloat(IntensityId, intensity);
            propertyBlock.SetFloat(LimbDarkeningId, limbDarkening);
            propertyBlock.SetFloat(GranulationScaleId, granulationScale);
            propertyBlock.SetFloat(GranulationStrengthId, granulationStrength);
            propertyBlock.SetFloat(GranulationSpeedId, granulationSpeed);
            propertyBlock.SetFloat(CellScaleId, cellScale);
            propertyBlock.SetFloat(CellStrengthId, cellStrength);
            propertyBlock.SetFloat(CellSpeedId, cellSpeed);
            propertyBlock.SetFloat(SunspotScaleId, sunspotScale);
            propertyBlock.SetFloat(SunspotStrengthId, sunspotStrength);
            propertyBlock.SetFloat(SunspotThresholdId, sunspotThreshold);
            propertyBlock.SetFloat(SurfaceRotationSpeedId, surfaceRotationSpeed);
            propertyBlock.SetFloat(PulseAmplitudeId, pulseAmplitude);
            propertyBlock.SetFloat(PulseSpeedId, pulseSpeed);
        }

        void OnValidate()
        {
            intensity = Mathf.Max(0f, intensity);
            limbDarkening = Mathf.Clamp(limbDarkening, 0.1f, 4f);
            granulationScale = Mathf.Max(0.01f, granulationScale);
            granulationStrength = Mathf.Clamp01(granulationStrength);
            granulationSpeed = Mathf.Max(0f, granulationSpeed);
            cellScale = Mathf.Max(0.01f, cellScale);
            cellStrength = Mathf.Clamp01(cellStrength);
            cellSpeed = Mathf.Max(0f, cellSpeed);
            surfaceRotationSpeed = Mathf.Max(0f, surfaceRotationSpeed);
            sunspotScale = Mathf.Max(0.01f, sunspotScale);
            sunspotStrength = Mathf.Clamp01(sunspotStrength);
            sunspotThreshold = Mathf.Clamp01(sunspotThreshold);
            pulseAmplitude = Mathf.Clamp(pulseAmplitude, 0f, 0.2f);
            pulseSpeed = Mathf.Max(0f, pulseSpeed);
            Changed?.Invoke();
        }
    }
}
