using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial/Atmosphere Profile", fileName = "SO_CelestialAtmosphereProfile")]
    public sealed class CelestialAtmosphereProfile : ScriptableObject
    {
        [Header("Radius")]

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

        [Header("Rayleigh Scattering")]
        [SerializeField] Vector3 wavelengths = new(700f, 530f, 460f);
        [Min(0f)]
        [SerializeField] float scatteringStrength = 21.23f;

        [Header("Mie Scattering")]
        [Tooltip("Aerosol haze. Drives the warm forward-scattering halo around the star at sunrise and sunset.")]
        [Min(0f)]
        [SerializeField] float mieScatteringStrength = 1.2f;
        [Tooltip("Forward bias of the Henyey-Greenstein phase. Higher values tighten the halo around the star.")]
        [Range(0f, 0.95f)]
        [SerializeField] float mieAnisotropy = 0.76f;
        [Tooltip("Extinction relative to scattering. Aerosols absorb slightly more than they scatter.")]
        [Min(0f)]
        [SerializeField] float mieExtinctionRatio = 1.1f;
        [Tooltip("Aerosols sit far lower than the Rayleigh column, so this falloff is much steeper.")]
        [Min(0.001f)]
        [SerializeField] float mieDensityFalloff = 18f;

        [Header("Ozone Absorption")]
        [Tooltip("Absorbs green and red at grazing angles, which is what turns twilight deep blue.")]
        [Min(0f)]
        [SerializeField] float ozoneStrength = 1.5f;
        [Range(0f, 1f)]
        [SerializeField] float ozonePeakHeight = 0.25f;
        [Range(0.01f, 1f)]
        [SerializeField] float ozoneBandWidth = 0.15f;

        [Header("Exposure")]
        [Min(0f)]
        [SerializeField] float intensity = 1f;
        [Min(0.001f)]
        [SerializeField] float referenceLightIntensity = 1.1f;
        [Min(0f)]
        [SerializeField] float ditherStrength = 1f;
        [Min(0.001f)]
        [SerializeField] float ditherScale = 3.89f;
        [SerializeField] Texture2D blueNoise;

        const int MaxOpticalDepthBakes = 8;
        const float OpticalDepthScaleTolerance = 1e-4f;

        readonly List<OpticalDepthBake> opticalDepthBakes = new();
        bool opticalDepthDirty = true;

        public ComputeShader OpticalDepthCompute => opticalDepthCompute;
        public int TextureSize => textureSize;
        public int InScatteringSteps => inScatteringSteps;
        public int OpticalDepthSteps => opticalDepthSteps;
        public float DensityFalloff => densityFalloff;
        public Vector3 Wavelengths => wavelengths;
        public float ScatteringStrength => scatteringStrength;
        public float MieScatteringStrength => mieScatteringStrength;
        public float MieAnisotropy => mieAnisotropy;
        public float MieExtinctionRatio => mieExtinctionRatio;
        public float MieDensityFalloff => mieDensityFalloff;
        public float OzoneStrength => ozoneStrength;
        public float OzonePeakHeight => ozonePeakHeight;
        public float OzoneBandWidth => ozoneBandWidth;
        public float Intensity => intensity;
        public float ReferenceLightIntensity => referenceLightIntensity;
        public float DitherStrength => ditherStrength;
        public float DitherScale => ditherScale;
        public Texture2D BlueNoise => blueNoise;

        static readonly Vector3 OzoneAbsorptionShape = new(0.346f, 1f, 0.045f);

        public Vector3 GetScatteringCoefficients()
        {
            return new Vector3(
                Mathf.Pow(400f / Mathf.Max(1f, wavelengths.x), 4f),
                Mathf.Pow(400f / Mathf.Max(1f, wavelengths.y), 4f),
                Mathf.Pow(400f / Mathf.Max(1f, wavelengths.z), 4f)) * scatteringStrength;
        }

        public Vector3 GetOzoneCoefficients()
        {
            return OzoneAbsorptionShape * ozoneStrength;
        }

        public RenderTexture GetOpticalDepthTexture(float atmosphereScale)
        {
            if (opticalDepthCompute == null)
            {
                return null;
            }

            OpticalDepthBake bake = ResolveBake(atmosphereScale);
            if (!CanDispatchCompute())
            {
                return bake.Texture;
            }

            int safeTextureSize = Mathf.Max(8, textureSize);
            if (bake.Texture == null
                || !bake.Texture.IsCreated()
                || bake.Texture.width != safeTextureSize
                || bake.Texture.height != safeTextureSize
                || bake.Texture.graphicsFormat != GraphicsFormat.R16G16B16A16_SFloat)
            {
                bake.ReleaseTexture();
                bake.Texture = new RenderTexture(safeTextureSize, safeTextureSize, 0)
                {
                    graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    enableRandomWrite = true,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = $"{name} Optical Depth {atmosphereScale:F4}"
                };
                bake.Texture.Create();
                bake.Dirty = true;
            }

            bool bakeSettingsChanged = bake.TextureSize != safeTextureSize
                || bake.Steps != opticalDepthSteps
                || !Mathf.Approximately(bake.DensityFalloff, densityFalloff)
                || !Mathf.Approximately(bake.MieDensityFalloff, mieDensityFalloff)
                || !Mathf.Approximately(bake.OzonePeakHeight, ozonePeakHeight)
                || !Mathf.Approximately(bake.OzoneBandWidth, ozoneBandWidth);

            if (opticalDepthDirty || bake.Dirty || bakeSettingsChanged)
            {
                int kernelIndex = opticalDepthCompute.FindKernel("CSMain");
                opticalDepthCompute.SetTexture(kernelIndex, "Result", bake.Texture);
                opticalDepthCompute.SetInt("textureSize", safeTextureSize);
                opticalDepthCompute.SetInt("numOutScatteringSteps", opticalDepthSteps);
                opticalDepthCompute.SetFloat("atmosphereRadius", 1f + atmosphereScale);
                opticalDepthCompute.SetFloat("densityFalloff", densityFalloff);
                opticalDepthCompute.SetFloat("mieDensityFalloff", mieDensityFalloff);
                opticalDepthCompute.SetFloat("ozonePeakHeight", ozonePeakHeight);
                opticalDepthCompute.SetFloat("ozoneBandWidth", ozoneBandWidth);
                opticalDepthCompute.GetKernelThreadGroupSizes(kernelIndex, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
                int threadGroupsX = Mathf.CeilToInt(safeTextureSize / (float)threadGroupSizeX);
                int threadGroupsY = Mathf.CeilToInt(safeTextureSize / (float)threadGroupSizeY);
                opticalDepthCompute.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);

                bake.Dirty = false;
                bake.TextureSize = safeTextureSize;
                bake.Steps = opticalDepthSteps;
                bake.DensityFalloff = densityFalloff;
                bake.MieDensityFalloff = mieDensityFalloff;
                bake.OzonePeakHeight = ozonePeakHeight;
                bake.OzoneBandWidth = ozoneBandWidth;
            }

            if (opticalDepthDirty)
            {
                for (int i = 0; i < opticalDepthBakes.Count; i++)
                {
                    if (opticalDepthBakes[i] != bake)
                    {
                        opticalDepthBakes[i].Dirty = true;
                    }
                }

                opticalDepthDirty = false;
            }

            return bake.Texture;
        }

        public static bool SharesOpticalDepthScale(float left, float right)
        {
            return Mathf.Abs(left - right) < OpticalDepthScaleTolerance;
        }

        OpticalDepthBake ResolveBake(float atmosphereScale)
        {
            for (int i = 0; i < opticalDepthBakes.Count; i++)
            {
                if (SharesOpticalDepthScale(opticalDepthBakes[i].AtmosphereScale, atmosphereScale))
                {
                    return opticalDepthBakes[i];
                }
            }

            if (opticalDepthBakes.Count >= MaxOpticalDepthBakes)
            {
                OpticalDepthBake recycled = opticalDepthBakes[0];
                opticalDepthBakes.RemoveAt(0);
                recycled.AtmosphereScale = atmosphereScale;
                recycled.Dirty = true;
                opticalDepthBakes.Add(recycled);
                return recycled;
            }

            OpticalDepthBake created = new() { AtmosphereScale = atmosphereScale, Dirty = true };
            opticalDepthBakes.Add(created);
            return created;
        }

        sealed class OpticalDepthBake
        {
            public float AtmosphereScale;
            public RenderTexture Texture;
            public bool Dirty = true;
            public int TextureSize = -1;
            public int Steps = -1;
            public float DensityFalloff = -1f;
            public float MieDensityFalloff = -1f;
            public float OzonePeakHeight = -1f;
            public float OzoneBandWidth = -1f;

            public void ReleaseTexture()
            {
                if (Texture == null)
                {
                    return;
                }

                Texture.Release();
                if (Application.isPlaying)
                {
                    Destroy(Texture);
                }
                else
                {
                    DestroyImmediate(Texture);
                }

                Texture = null;
            }
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
            textureSize = Mathf.Max(8, textureSize);
            opticalDepthSteps = Mathf.Max(2, opticalDepthSteps);
            densityFalloff = Mathf.Max(0.001f, densityFalloff);
            scatteringStrength = Mathf.Max(0f, scatteringStrength);
            mieScatteringStrength = Mathf.Max(0f, mieScatteringStrength);
            mieAnisotropy = Mathf.Clamp(mieAnisotropy, 0f, 0.95f);
            mieExtinctionRatio = Mathf.Max(0f, mieExtinctionRatio);
            mieDensityFalloff = Mathf.Max(0.001f, mieDensityFalloff);
            ozoneStrength = Mathf.Max(0f, ozoneStrength);
            ozonePeakHeight = Mathf.Clamp01(ozonePeakHeight);
            ozoneBandWidth = Mathf.Clamp(ozoneBandWidth, 0.01f, 1f);
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
            for (int i = 0; i < opticalDepthBakes.Count; i++)
            {
                opticalDepthBakes[i].ReleaseTexture();
            }

            opticalDepthBakes.Clear();
            opticalDepthDirty = true;
        }
    }
}
