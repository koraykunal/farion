using UnityEngine;
using UnityEngine.Experimental.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial Atmosphere Profile", fileName = "SO_CelestialAtmosphereProfile")]
    public sealed class CelestialAtmosphereProfile : ScriptableObject
    {
        [Header("Radius")]
        [Range(0.01f, 2f)]
        [SerializeField] float atmosphereScale = 0.322f;
        [SerializeField] float radiusOffset;

        [Header("Sampling")]
        [SerializeField] ComputeShader opticalDepthCompute;
        [Min(8)]
        [SerializeField] int textureSize = 256;
        [Range(2, 32)]
        [SerializeField] int inScatteringSteps = 10;
        [Range(2, 128)]
        [SerializeField] int opticalDepthSteps = 100;

        [Header("Density")]
        [Min(0.001f)]
        [SerializeField] float densityFalloff = 4.3f;

        [Header("Scattering")]
        [SerializeField] Vector3 wavelengths = new(700f, 530f, 460f);
        [Min(0f)]
        [SerializeField] float scatteringStrength = 21.23f;
        [Min(0f)]
        [SerializeField] float intensity = 1f;
        [Min(0.001f)]
        [SerializeField] float referenceLightIntensity = 6f;
        [Min(0f)]
        [SerializeField] float ditherStrength = 1f;
        [Min(0.001f)]
        [SerializeField] float ditherScale = 3.89f;
        [SerializeField] Texture2D blueNoise;

        RenderTexture opticalDepthTexture;
        bool opticalDepthDirty = true;
        int bakedTextureSize;
        int bakedOpticalDepthSteps;
        float bakedAtmosphereScale = -1f;
        float bakedDensityFalloff = -1f;

        public float AtmosphereScale => atmosphereScale;
        public ComputeShader OpticalDepthCompute => opticalDepthCompute;
        public int TextureSize => textureSize;
        public int InScatteringSteps => inScatteringSteps;
        public int OpticalDepthSteps => opticalDepthSteps;
        public float DensityFalloff => densityFalloff;
        public Vector3 Wavelengths => wavelengths;
        public float ScatteringStrength => scatteringStrength;
        public float Intensity => intensity;
        public float ReferenceLightIntensity => referenceLightIntensity;
        public float DitherStrength => ditherStrength;
        public float DitherScale => ditherScale;
        public Texture2D BlueNoise => blueNoise;

        public float GetAtmosphereRadius(float bodyRadius)
        {
            bodyRadius = Mathf.Max(0.01f, bodyRadius);
            return Mathf.Max(bodyRadius + 0.001f, bodyRadius * (1f + atmosphereScale) + radiusOffset);
        }

        public Vector3 GetScatteringCoefficients()
        {
            return new Vector3(
                Mathf.Pow(400f / Mathf.Max(1f, wavelengths.x), 4f),
                Mathf.Pow(400f / Mathf.Max(1f, wavelengths.y), 4f),
                Mathf.Pow(400f / Mathf.Max(1f, wavelengths.z), 4f)) * scatteringStrength;
        }

        public RenderTexture GetOpticalDepthTexture()
        {
            if (opticalDepthCompute == null)
            {
                return null;
            }

            if (!CanDispatchCompute())
            {
                return opticalDepthTexture;
            }

            int safeTextureSize = Mathf.Max(8, textureSize);
            bool recreateTexture = opticalDepthTexture == null
                || !opticalDepthTexture.IsCreated()
                || opticalDepthTexture.width != safeTextureSize
                || opticalDepthTexture.height != safeTextureSize
                || opticalDepthTexture.graphicsFormat != GraphicsFormat.R16G16B16A16_SFloat;

            if (recreateTexture)
            {
                if (opticalDepthTexture != null)
                {
                    opticalDepthTexture.Release();
                }

                opticalDepthTexture = new RenderTexture(safeTextureSize, safeTextureSize, 0)
                {
                    graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    enableRandomWrite = true,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = $"{name} Optical Depth"
                };
                opticalDepthTexture.Create();
                opticalDepthDirty = true;
            }

            bool bakeSettingsChanged = bakedTextureSize != safeTextureSize
                || bakedOpticalDepthSteps != opticalDepthSteps
                || !Mathf.Approximately(bakedAtmosphereScale, atmosphereScale)
                || !Mathf.Approximately(bakedDensityFalloff, densityFalloff);

            if (opticalDepthDirty || bakeSettingsChanged)
            {
                int kernelIndex = opticalDepthCompute.FindKernel("CSMain");
                opticalDepthCompute.SetTexture(kernelIndex, "Result", opticalDepthTexture);
                opticalDepthCompute.SetInt("textureSize", safeTextureSize);
                opticalDepthCompute.SetInt("numOutScatteringSteps", opticalDepthSteps);
                opticalDepthCompute.SetFloat("atmosphereRadius", 1f + atmosphereScale);
                opticalDepthCompute.SetFloat("densityFalloff", densityFalloff);
                opticalDepthCompute.GetKernelThreadGroupSizes(kernelIndex, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
                int threadGroupsX = Mathf.CeilToInt(safeTextureSize / (float)threadGroupSizeX);
                int threadGroupsY = Mathf.CeilToInt(safeTextureSize / (float)threadGroupSizeY);
                opticalDepthCompute.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
                opticalDepthDirty = false;
                bakedTextureSize = safeTextureSize;
                bakedOpticalDepthSteps = opticalDepthSteps;
                bakedAtmosphereScale = atmosphereScale;
                bakedDensityFalloff = densityFalloff;
            }

            return opticalDepthTexture;
        }

        static bool CanDispatchCompute()
        {
#if UNITY_EDITOR
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return false;
            }
#endif

            return true;
        }

        void OnValidate()
        {
            atmosphereScale = Mathf.Max(0.01f, atmosphereScale);
            textureSize = Mathf.Max(8, textureSize);
            opticalDepthSteps = Mathf.Max(2, opticalDepthSteps);
            densityFalloff = Mathf.Max(0.001f, densityFalloff);
            scatteringStrength = Mathf.Max(0f, scatteringStrength);
            intensity = Mathf.Max(0f, intensity);
            referenceLightIntensity = Mathf.Max(0.001f, referenceLightIntensity);
            ditherStrength = Mathf.Max(0f, ditherStrength);
            ditherScale = Mathf.Max(0.001f, ditherScale);
            wavelengths.x = Mathf.Max(1f, wavelengths.x);
            wavelengths.y = Mathf.Max(1f, wavelengths.y);
            wavelengths.z = Mathf.Max(1f, wavelengths.z);
            opticalDepthDirty = true;
        }

        void OnDisable()
        {
            if (opticalDepthTexture == null)
            {
                return;
            }

            opticalDepthTexture.Release();
            if (Application.isPlaying)
            {
                Destroy(opticalDepthTexture);
            }
            else
            {
                DestroyImmediate(opticalDepthTexture);
            }

            opticalDepthTexture = null;
            opticalDepthDirty = true;
        }
    }
}
