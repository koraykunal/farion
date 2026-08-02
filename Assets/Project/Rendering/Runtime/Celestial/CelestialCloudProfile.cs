using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial Cloud Profile", fileName = "SO_CelestialCloudProfile")]
    public sealed class CelestialCloudProfile : ScriptableObject
    {
        [Header("Volume Textures")]
        [SerializeField] Texture3D shapeNoise;
        [SerializeField] Texture3D detailNoise;
        [SerializeField] Texture2D blueNoise;

        [Header("Layer")]
        [Range(0f, 1f)] [SerializeField] float layerBottom = 0.02f;
        [Range(0f, 1f)] [SerializeField] float layerTop = 0.1f;

        [Header("Density")]
        [Min(0.001f)] [SerializeField] float shapeScale = 2.8f;
        [Min(0.001f)] [SerializeField] float detailScale = 18f;
        [SerializeField] Vector4 shapeWeights = new(1f, 0.3f, 0.15f, 0.05f);
        [SerializeField] Vector3 detailWeights = new(1f, 0.5f, 0.25f);
        [Range(0f, 1f)] [SerializeField] float coverage = 0.52f;
        [Min(0f)] [SerializeField] float densityMultiplier = 1.25f;
        [Range(0f, 1f)] [SerializeField] float detailErosion = 0.3f;

        [Header("Lighting")]
        [Min(0f)] [SerializeField] float lightAbsorptionThroughCloud = 0.8f;
        [Min(0f)] [SerializeField] float lightAbsorptionTowardStar = 1f;
        [Range(0f, 1f)] [SerializeField] float darknessThreshold = 0.18f;
        [Range(0f, 0.99f)] [SerializeField] float forwardScattering = 0.75f;
        [Range(0f, 0.99f)] [SerializeField] float backScattering = 0.25f;
        [Range(0f, 2f)] [SerializeField] float baseBrightness = 0.55f;
        [Range(0f, 2f)] [SerializeField] float phaseStrength = 0.65f;

        [Header("Motion")]
        [SerializeField] Vector3 localWindAxis = new(0.25f, 1f, 0.1f);
        [SerializeField] float baseAngularSpeed = 0.25f;
        [SerializeField] float detailAngularSpeed = 0.7f;

        [Header("Sampling")]
        [Range(8, 96)] [SerializeField] int viewSteps = 48;
        [Range(1, 12)] [SerializeField] int lightSteps = 6;
        [Range(0f, 2f)] [SerializeField] float ditherStrength = 1f;

        public Texture3D ShapeNoise => shapeNoise;
        public Texture3D DetailNoise => detailNoise;
        public Texture2D BlueNoise => blueNoise;
        public float LayerBottom => layerBottom;
        public float LayerTop => layerTop;
        public float ShapeScale => shapeScale;
        public float DetailScale => detailScale;
        public Vector4 ShapeWeights => shapeWeights;
        public Vector3 DetailWeights => detailWeights;
        public float Coverage => coverage;
        public float DensityMultiplier => densityMultiplier;
        public float DetailErosion => detailErosion;
        public float LightAbsorptionThroughCloud => lightAbsorptionThroughCloud;
        public float LightAbsorptionTowardStar => lightAbsorptionTowardStar;
        public float DarknessThreshold => darknessThreshold;
        public Vector4 PhaseParameters => new(forwardScattering, backScattering, baseBrightness, phaseStrength);
        public Vector3 LocalWindAxis => localWindAxis.sqrMagnitude > 0.0001f ? localWindAxis.normalized : Vector3.up;
        public float BaseAngularSpeed => baseAngularSpeed;
        public float DetailAngularSpeed => detailAngularSpeed;
        public int ViewSteps => viewSteps;
        public int LightSteps => lightSteps;
        public float DitherStrength => ditherStrength;

        public void GetLayerRadii(float surfaceRadius, float atmosphereRadius, out float innerRadius, out float outerRadius)
        {
            surfaceRadius = Mathf.Max(0.01f, surfaceRadius);
            atmosphereRadius = Mathf.Max(surfaceRadius + 0.001f, atmosphereRadius);
            innerRadius = Mathf.Lerp(surfaceRadius, atmosphereRadius, layerBottom);
            outerRadius = Mathf.Max(innerRadius + 0.001f, Mathf.Lerp(surfaceRadius, atmosphereRadius, layerTop));
        }

        void OnValidate()
        {
            layerBottom = Mathf.Clamp(layerBottom, 0f, 0.999f);
            layerTop = Mathf.Clamp(layerTop, layerBottom + 0.001f, 1f);
            shapeScale = Mathf.Max(0.001f, shapeScale);
            detailScale = Mathf.Max(0.001f, detailScale);
            shapeWeights = new Vector4(
                Mathf.Max(0f, shapeWeights.x),
                Mathf.Max(0f, shapeWeights.y),
                Mathf.Max(0f, shapeWeights.z),
                Mathf.Max(0f, shapeWeights.w));
            if (shapeWeights.sqrMagnitude <= 0.0001f)
            {
                shapeWeights = new Vector4(1f, 0f, 0f, 0f);
            }

            detailWeights = new Vector3(
                Mathf.Max(0f, detailWeights.x),
                Mathf.Max(0f, detailWeights.y),
                Mathf.Max(0f, detailWeights.z));
            if (detailWeights.sqrMagnitude <= 0.0001f)
            {
                detailWeights = new Vector3(1f, 0f, 0f);
            }

            densityMultiplier = Mathf.Max(0f, densityMultiplier);
            lightAbsorptionThroughCloud = Mathf.Max(0f, lightAbsorptionThroughCloud);
            lightAbsorptionTowardStar = Mathf.Max(0f, lightAbsorptionTowardStar);
            viewSteps = Mathf.Clamp(viewSteps, 8, 96);
            lightSteps = Mathf.Clamp(lightSteps, 1, 12);
        }
    }
}
