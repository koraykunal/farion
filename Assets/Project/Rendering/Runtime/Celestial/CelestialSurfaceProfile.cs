using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial Surface Profile", fileName = "SO_CelestialSurfaceProfile")]
    public sealed class CelestialSurfaceProfile : CelestialSurfaceProfileBase
    {
        [Header("Material")]
        [SerializeField] Material material;
        [SerializeField] Color baseColor = Color.white;
        [SerializeField] Color secondaryColor = new(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] Color steepColor = new(0.18f, 0.18f, 0.18f, 1f);
        [SerializeField] Color ejectaColor = new(0.85f, 0.82f, 0.75f, 1f);
        [Range(0f, 1f)]
        [SerializeField] float metallic;
        [Range(0f, 1f)]
        [SerializeField] float smoothness = 0.45f;
        [Range(0f, 1f)]
        [SerializeField] float ejectaSmoothness = 0.12f;

        [Header("Triplanar")]
        [FormerlySerializedAs("albedoTexture")]
        [SerializeField] Texture2D surfaceNoiseTexture;
        [SerializeField] Texture2D ejectaRayTexture;
        [SerializeField] Texture2D normalMapFlat;
        [SerializeField] Texture2D normalMapSteep;
        [Min(0.001f)]
        [FormerlySerializedAs("albedoScale")]
        [SerializeField] float surfaceNoiseWorldTileSize = 180f;
        [Min(0.001f)]
        [FormerlySerializedAs("normalFlatScale")]
        [SerializeField] float normalFlatWorldTileSize = 10f;
        [Min(0.001f)]
        [FormerlySerializedAs("normalSteepScale")]
        [SerializeField] float normalSteepWorldTileSize = 7.5f;
        [Range(0f, 1f)]
        [SerializeField] float normalStrength = 0.35f;
        [Range(0f, 1f)]
        [SerializeField] float steepColorStrength = 0.75f;
        [Range(0f, 2f)]
        [SerializeField] float biomeBlendStrength = 0.8f;
        [Range(0f, 2f)]
        [SerializeField] float ejectaStrength = 0.65f;
        [Min(1f)]
        [SerializeField] float ejectaRayFrequency = 36f;

        [Header("Shape")]
        [Min(0f)]
        [SerializeField] float heightAmplitude;
        [Min(0.001f)]
        [SerializeField] float noiseScale = 2f;
        [Range(1, 8)]
        [SerializeField] int noiseOctaves = 4;
        [Range(0f, 1f)]
        [SerializeField] float noisePersistence = 0.5f;
        [Min(1f)]
        [SerializeField] float noiseLacunarity = 2f;
        [SerializeField] int seed = 1;

        public override Material Material => material;
        public Color BaseColor => baseColor;
        public Color SecondaryColor => secondaryColor;
        public Color SteepColor => steepColor;
        public Color EjectaColor => ejectaColor;
        public float Metallic => metallic;
        public float Smoothness => smoothness;
        public float EjectaSmoothness => ejectaSmoothness;
        public Texture2D SurfaceNoiseTexture => surfaceNoiseTexture;
        public Texture2D EjectaRayTexture => ejectaRayTexture;
        public Texture2D NormalMapFlat => normalMapFlat;
        public Texture2D NormalMapSteep => normalMapSteep;
        public float SurfaceNoiseWorldTileSize => surfaceNoiseWorldTileSize;
        public float NormalFlatWorldTileSize => normalFlatWorldTileSize;
        public float NormalSteepWorldTileSize => normalSteepWorldTileSize;
        public float NormalStrength => normalStrength;
        public float SteepColorStrength => steepColorStrength;
        public float BiomeBlendStrength => biomeBlendStrength;
        public float EjectaStrength => ejectaStrength;
        public float EjectaRayFrequency => ejectaRayFrequency;
        public bool HasDisplacement => heightAmplitude > 0f;

        public override float EvaluateRadius(float baseRadius, Vector3 unitDirection)
        {
            baseRadius = Mathf.Max(0.01f, baseRadius);
            if (heightAmplitude <= 0f)
            {
                return baseRadius;
            }

            float elevation = FractalNoise(unitDirection) * heightAmplitude;
            return Mathf.Max(0.01f, baseRadius + elevation);
        }

        public override void ApplyMaterialProperties(
            MaterialPropertyBlock propertyBlock,
            float bodyRadius,
            Vector2 radiusMinMax)
        {
            propertyBlock.SetColor("_BaseColor", baseColor);
            propertyBlock.SetColor("_SecondaryColor", secondaryColor);
            propertyBlock.SetColor("_SteepColor", steepColor);
            propertyBlock.SetColor("_EjectaColor", ejectaColor);
            propertyBlock.SetFloat("_Metallic", metallic);
            propertyBlock.SetFloat("_Smoothness", smoothness);
            propertyBlock.SetFloat("_EjectaSmoothness", ejectaSmoothness);
            propertyBlock.SetFloat("_BodyRadius", bodyRadius);
            propertyBlock.SetVector("_RadiusMinMax", radiusMinMax);
            propertyBlock.SetFloat("_SurfaceNoiseWorldTileSize", surfaceNoiseWorldTileSize);
            propertyBlock.SetFloat("_NormalFlatWorldTileSize", normalFlatWorldTileSize);
            propertyBlock.SetFloat("_NormalSteepWorldTileSize", normalSteepWorldTileSize);
            propertyBlock.SetFloat("_NormalStrength", normalStrength);
            propertyBlock.SetFloat("_SteepColorStrength", steepColorStrength);
            propertyBlock.SetFloat("_BiomeBlendStrength", biomeBlendStrength);
            propertyBlock.SetFloat("_EjectaStrength", ejectaStrength);
            propertyBlock.SetFloat("_EjectaRayFrequency", ejectaRayFrequency);

            if (surfaceNoiseTexture != null)
            {
                propertyBlock.SetTexture("_SurfaceNoiseTex", surfaceNoiseTexture);
            }

            if (ejectaRayTexture != null)
            {
                propertyBlock.SetTexture("_EjectaRayTex", ejectaRayTexture);
                propertyBlock.SetFloat("_UseEjectaRayTex", 1f);
            }
            else
            {
                propertyBlock.SetFloat("_UseEjectaRayTex", 0f);
            }

            if (normalMapFlat != null)
            {
                propertyBlock.SetTexture("_NormalMapFlat", normalMapFlat);
            }

            if (normalMapSteep != null)
            {
                propertyBlock.SetTexture("_NormalMapSteep", normalMapSteep);
            }
        }

        float FractalNoise(Vector3 unitDirection)
        {
            float frequency = noiseScale;
            float amplitude = 1f;
            float total = 0f;
            float amplitudeTotal = 0f;

            for (int octave = 0; octave < noiseOctaves; octave++)
            {
                total += SampleSignedNoise(unitDirection, frequency, seed + octave * 101) * amplitude;
                amplitudeTotal += amplitude;
                amplitude *= noisePersistence;
                frequency *= noiseLacunarity;
            }

            if (amplitudeTotal <= 0f)
            {
                return 0f;
            }

            return total / amplitudeTotal;
        }

        static float SampleSignedNoise(Vector3 direction, float frequency, int sampleSeed)
        {
            float seedOffset = sampleSeed * 0.137f;

            float xy = Mathf.PerlinNoise(
                (direction.x + seedOffset) * frequency,
                (direction.y - seedOffset) * frequency);

            float yz = Mathf.PerlinNoise(
                (direction.y + seedOffset) * frequency,
                (direction.z - seedOffset) * frequency);

            float zx = Mathf.PerlinNoise(
                (direction.z + seedOffset) * frequency,
                (direction.x - seedOffset) * frequency);

            return ((xy + yz + zx) / 3f) * 2f - 1f;
        }

        void OnValidate()
        {
            heightAmplitude = Mathf.Max(0f, heightAmplitude);
            noiseScale = Mathf.Max(0.001f, noiseScale);
            noiseLacunarity = Mathf.Max(1f, noiseLacunarity);
            surfaceNoiseWorldTileSize = Mathf.Max(0.001f, surfaceNoiseWorldTileSize);
            normalFlatWorldTileSize = Mathf.Max(0.001f, normalFlatWorldTileSize);
            normalSteepWorldTileSize = Mathf.Max(0.001f, normalSteepWorldTileSize);
            ejectaRayFrequency = Mathf.Max(1f, ejectaRayFrequency);
            NotifyChanged();
        }
    }
}
