using UnityEngine;

namespace Farion.Rendering.Space
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Star Dome Profile", fileName = "SO_StarDomeProfile")]
    public sealed class StarDomeProfile : ScriptableObject
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ZenithColorId = Shader.PropertyToID("_ZenithColor");
        static readonly int StarColorAId = Shader.PropertyToID("_StarColorA");
        static readonly int StarColorBId = Shader.PropertyToID("_StarColorB");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int StarDensityId = Shader.PropertyToID("_StarDensity");
        static readonly int StarBrightnessId = Shader.PropertyToID("_StarBrightness");
        static readonly int StarScaleId = Shader.PropertyToID("_StarScale");
        static readonly int StarSizeId = Shader.PropertyToID("_StarSize");
        static readonly int NebulaColorId = Shader.PropertyToID("_NebulaColor");
        static readonly int NebulaStrengthId = Shader.PropertyToID("_NebulaStrength");
        static readonly int NebulaScaleId = Shader.PropertyToID("_NebulaScale");
        static readonly int NebulaSharpnessId = Shader.PropertyToID("_NebulaSharpness");
        static readonly int NebulaAxisId = Shader.PropertyToID("_NebulaAxis");

        [Header("Material")]
        [SerializeField] Material skyboxMaterial;

        [Header("Space Color")]
        [SerializeField] Color baseColor = new(0.0015f, 0.0018f, 0.0024f, 1f);
        [SerializeField] Color zenithColor = new(0.004f, 0.005f, 0.007f, 1f);
        [Min(0f)]
        [SerializeField] float exposure = 1f;

        [Header("Stars")]
        [SerializeField] Color starColorA = new(0.9f, 0.94f, 1f, 1f);
        [SerializeField] Color starColorB = new(1f, 0.78f, 0.55f, 1f);
        [Min(0f)]
        [SerializeField] float seed = 19f;
        [Range(0f, 1f)]
        [SerializeField] float starDensity = 0.035f;
        [Min(0f)]
        [SerializeField] float starBrightness = 1.8f;
        [Min(1f)]
        [SerializeField] float starScale = 260f;
        [Range(0.001f, 0.2f)]
        [SerializeField] float starSize = 0.045f;

        [Header("Galactic Band")]
        [SerializeField] Color nebulaColor = new(0.22f, 0.3f, 0.42f, 1f);
        [Range(0f, 1f)]
        [SerializeField] float nebulaStrength = 0.12f;
        [Min(1f)]
        [SerializeField] float nebulaScale = 18f;
        [Min(0.1f)]
        [SerializeField] float nebulaSharpness = 5f;
        [SerializeField] Vector3 nebulaAxis = new(0.35f, 0.85f, 0.25f);

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
            material.SetColor(StarColorAId, starColorA);
            material.SetColor(StarColorBId, starColorB);
            material.SetFloat(ExposureId, exposure);
            material.SetFloat(SeedId, seed);
            material.SetFloat(StarDensityId, starDensity);
            material.SetFloat(StarBrightnessId, starBrightness);
            material.SetFloat(StarScaleId, starScale);
            material.SetFloat(StarSizeId, starSize);
            material.SetColor(NebulaColorId, nebulaColor);
            material.SetFloat(NebulaStrengthId, nebulaStrength);
            material.SetFloat(NebulaScaleId, nebulaScale);
            material.SetFloat(NebulaSharpnessId, nebulaSharpness);
            material.SetVector(NebulaAxisId, ResolveNebulaAxis());
        }

        Vector3 ResolveNebulaAxis()
        {
            return nebulaAxis.sqrMagnitude > 0.0001f ? nebulaAxis.normalized : Vector3.up;
        }

        void OnValidate()
        {
            exposure = Mathf.Max(0f, exposure);
            seed = Mathf.Max(0f, seed);
            starBrightness = Mathf.Max(0f, starBrightness);
            starScale = Mathf.Max(1f, starScale);
            nebulaScale = Mathf.Max(1f, nebulaScale);
            nebulaSharpness = Mathf.Max(0.1f, nebulaSharpness);
            Changed?.Invoke();
        }
    }
}
