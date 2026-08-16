using UnityEngine;

namespace Farion.Rendering.Space
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Star Dome Profile", fileName = "SO_StarDomeProfile")]
    public sealed class StarDomeProfile : ScriptableObject
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ZenithColorId = Shader.PropertyToID("_ZenithColor");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int StarDensityId = Shader.PropertyToID("_StarDensity");
        static readonly int StarBrightnessId = Shader.PropertyToID("_StarBrightness");
        static readonly int StarScaleId = Shader.PropertyToID("_StarScale");
        static readonly int StarSizeId = Shader.PropertyToID("_StarSize");
        static readonly int StarMagnitudeFalloffId = Shader.PropertyToID("_StarMagnitudeFalloff");
        static readonly int StarTemperatureMinId = Shader.PropertyToID("_StarTemperatureMin");
        static readonly int StarTemperatureMaxId = Shader.PropertyToID("_StarTemperatureMax");
        static readonly int StarTemperatureBiasId = Shader.PropertyToID("_StarTemperatureBias");
        static readonly int StarColorSaturationId = Shader.PropertyToID("_StarColorSaturation");
        static readonly int GalacticNormalId = Shader.PropertyToID("_GalacticNormal");
        static readonly int MilkyWayColorId = Shader.PropertyToID("_MilkyWayColor");
        static readonly int MilkyWayIntensityId = Shader.PropertyToID("_MilkyWayIntensity");
        static readonly int MilkyWayWidthId = Shader.PropertyToID("_MilkyWayWidth");
        static readonly int MilkyWayScaleId = Shader.PropertyToID("_MilkyWayScale");
        static readonly int MilkyWayDustStrengthId = Shader.PropertyToID("_MilkyWayDustStrength");
        static readonly int MilkyWayStarBoostId = Shader.PropertyToID("_MilkyWayStarBoost");

        [Header("Material")]
        [SerializeField] Material skyboxMaterial;

        [Header("Space Color")]
        [SerializeField] Color baseColor = new(0.0015f, 0.0018f, 0.0024f, 1f);
        [SerializeField] Color zenithColor = new(0.004f, 0.005f, 0.007f, 1f);
        [Min(0f)]
        [SerializeField] float exposure = 1f;
        [Min(0f)]
        [SerializeField] float seed = 19f;

        [Header("Stars")]
        [Range(0f, 1f)]
        [SerializeField] float starDensity = 0.035f;
        [Min(0f)]
        [SerializeField] float starBrightness = 1.8f;
        [Min(1f)]
        [SerializeField] float starScale = 260f;
        [Range(0.001f, 0.2f)]
        [SerializeField] float starSize = 0.045f;
        [Tooltip("Higher values mean fewer bright stars and many faint ones, which is how a real sky reads.")]
        [Range(1f, 8f)]
        [SerializeField] float starMagnitudeFalloff = 3.5f;

        [Header("Star Color")]
        [Tooltip("Stars are tinted by blackbody temperature instead of a two-colour blend.")]
        [Range(1500f, 10000f)]
        [SerializeField] float starTemperatureMin = 3200f;
        [Range(3000f, 40000f)]
        [SerializeField] float starTemperatureMax = 15000f;
        [Tooltip("Above 1 the population skews cool and orange, below 1 it skews hot and blue.")]
        [Range(0.25f, 4f)]
        [SerializeField] float starTemperatureBias = 1.6f;
        [Range(0f, 1f)]
        [SerializeField] float starColorSaturation = 0.65f;

        [Header("Milky Way")]
        [Tooltip("Normal of the galactic plane. The band appears perpendicular to this axis.")]
        [SerializeField] Vector3 galacticNormal = new(0.34f, 0.87f, 0.36f);
        [SerializeField] Color milkyWayColor = new(0.42f, 0.46f, 0.62f, 1f);
        [Range(0f, 4f)]
        [SerializeField] float milkyWayIntensity = 0.85f;
        [Range(0.02f, 1f)]
        [SerializeField] float milkyWayWidth = 0.19f;
        [Min(0.1f)]
        [SerializeField] float milkyWayScale = 5.5f;
        [Tooltip("Dark dust lanes cutting through the band.")]
        [Range(0f, 1f)]
        [SerializeField] float milkyWayDustStrength = 0.7f;
        [Tooltip("Extra star density inside the band, where the galactic disc is densest.")]
        [Range(0f, 8f)]
        [SerializeField] float milkyWayStarBoost = 2.6f;

        public event System.Action Changed;
        public Material SkyboxMaterial => skyboxMaterial;

        public void ApplyTo(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetColor(BaseColorId, baseColor);
            material.SetColor(ZenithColorId, zenithColor);
            material.SetFloat(ExposureId, exposure);
            material.SetFloat(SeedId, seed);
            material.SetFloat(StarDensityId, starDensity);
            material.SetFloat(StarBrightnessId, starBrightness);
            material.SetFloat(StarScaleId, starScale);
            material.SetFloat(StarSizeId, starSize);
            material.SetFloat(StarMagnitudeFalloffId, starMagnitudeFalloff);
            material.SetFloat(StarTemperatureMinId, starTemperatureMin);
            material.SetFloat(StarTemperatureMaxId, Mathf.Max(starTemperatureMin, starTemperatureMax));
            material.SetFloat(StarTemperatureBiasId, starTemperatureBias);
            material.SetFloat(StarColorSaturationId, starColorSaturation);
            material.SetVector(GalacticNormalId, ResolveGalacticNormal());
            material.SetColor(MilkyWayColorId, milkyWayColor);
            material.SetFloat(MilkyWayIntensityId, milkyWayIntensity);
            material.SetFloat(MilkyWayWidthId, milkyWayWidth);
            material.SetFloat(MilkyWayScaleId, milkyWayScale);
            material.SetFloat(MilkyWayDustStrengthId, milkyWayDustStrength);
            material.SetFloat(MilkyWayStarBoostId, milkyWayStarBoost);
        }

        Vector4 ResolveGalacticNormal()
        {
            Vector3 normal = galacticNormal.sqrMagnitude > 0.0001f
                ? galacticNormal.normalized
                : Vector3.up;
            return new Vector4(normal.x, normal.y, normal.z, 0f);
        }

        void OnValidate()
        {
            exposure = Mathf.Max(0f, exposure);
            seed = Mathf.Max(0f, seed);
            starBrightness = Mathf.Max(0f, starBrightness);
            starScale = Mathf.Max(1f, starScale);
            starTemperatureMax = Mathf.Max(starTemperatureMin, starTemperatureMax);
            milkyWayScale = Mathf.Max(0.1f, milkyWayScale);
            Changed?.Invoke();
        }
    }
}
