using System.Collections.Generic;
using System.Text;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Biome Visual Profile", fileName = "SO_BiomeVisualProfile")]
    public sealed class BiomeVisualProfile : ScriptableObject
    {
        public const int MaxBiomeSlots = 8;

        [SerializeField, Range(0f, 1f)] float blendStrength = 1f;
        [SerializeField, Range(0f, 1f)] float edgeFeatherStrength = 0.75f;
        [SerializeField, Range(0f, 0.08f)] float edgeFeatherSampleStep = 0.025f;
        [SerializeField, Range(4, 16)] int edgeFeatherSampleCount = 8;
        [SerializeField] List<BiomeVisualRule> rules = new();

        readonly Vector4[] flatLowColors = new Vector4[MaxBiomeSlots];
        readonly Vector4[] flatHighColors = new Vector4[MaxBiomeSlots];
        readonly Vector4[] steepLowColors = new Vector4[MaxBiomeSlots];
        readonly Vector4[] steepHighColors = new Vector4[MaxBiomeSlots];
        readonly Vector4[] biomeParams = new Vector4[MaxBiomeSlots];
        readonly Vector4[] biomeTextureParams = new Vector4[MaxBiomeSlots];
        readonly List<BiomeVisualRule> activeRules = new(MaxBiomeSlots);
        readonly StringBuilder textureSignatureBuilder = new(512);

        [System.NonSerialized] string textureArraySignature;
        [System.NonSerialized] Texture2DArray baseColorArray;
        [System.NonSerialized] Texture2DArray normalArray;
        [System.NonSerialized] Texture2DArray roughnessArray;
        [System.NonSerialized] Texture2DArray ambientOcclusionArray;

        public float BlendStrength => Mathf.Clamp01(blendStrength);
        public float EdgeFeatherStrength => Mathf.Clamp01(edgeFeatherStrength);
        public float EdgeFeatherSampleStep => Mathf.Clamp(edgeFeatherSampleStep, 0f, 0.08f);
        public int EdgeFeatherSampleCount => Mathf.Clamp(edgeFeatherSampleCount, 4, 16);
        public IReadOnlyList<BiomeVisualRule> Rules => rules;

        void OnValidate()
        {
            blendStrength = Mathf.Clamp01(blendStrength);
            edgeFeatherStrength = Mathf.Clamp01(edgeFeatherStrength);
            edgeFeatherSampleStep = Mathf.Clamp(edgeFeatherSampleStep, 0f, 0.08f);
            edgeFeatherSampleCount = Mathf.Clamp(edgeFeatherSampleCount, 4, 16);
            rules ??= new List<BiomeVisualRule>();
            textureArraySignature = null;
        }

        public int ResolveBiomeIndex(BiomeDefinition biome)
        {
            if (biome == null || rules == null)
            {
                return -1;
            }

            int slot = 0;
            for (int i = 0; i < rules.Count && slot < MaxBiomeSlots; i++)
            {
                BiomeVisualRule rule = rules[i];
                if (rule == null || !rule.IsValid)
                {
                    continue;
                }

                if (rule.Biome == biome)
                {
                    return slot;
                }

                slot++;
            }

            return -1;
        }

        public void ApplyMaterialProperties(MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock == null)
            {
                return;
            }

            int count = FillShaderArrays();
            propertyBlock.SetFloat("_BiomeVisualCount", count);
            propertyBlock.SetFloat("_BiomeVisualBlendStrength", BlendStrength);
            propertyBlock.SetVectorArray("_BiomeFlatLow", flatLowColors);
            propertyBlock.SetVectorArray("_BiomeFlatHigh", flatHighColors);
            propertyBlock.SetVectorArray("_BiomeSteepLow", steepLowColors);
            propertyBlock.SetVectorArray("_BiomeSteepHigh", steepHighColors);
            propertyBlock.SetVectorArray("_BiomeParams", biomeParams);
            propertyBlock.SetVectorArray("_BiomeTextureParams", biomeTextureParams);

            int textureCount = TryApplyTextureArrays(propertyBlock);
            propertyBlock.SetFloat("_BiomeTextureCount", textureCount);
            propertyBlock.SetFloat("_BiomeTextureBlendStrength", textureCount > 0 ? BlendStrength : 0f);
        }

        public static void ClearMaterialProperties(MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock == null)
            {
                return;
            }

            propertyBlock.SetFloat("_BiomeVisualCount", 0f);
            propertyBlock.SetFloat("_BiomeVisualBlendStrength", 0f);
            propertyBlock.SetFloat("_BiomeTextureCount", 0f);
            propertyBlock.SetFloat("_BiomeTextureBlendStrength", 0f);
        }

        int FillShaderArrays()
        {
            activeRules.Clear();

            int slot = 0;
            for (int i = 0; i < MaxBiomeSlots; i++)
            {
                flatLowColors[i] = Vector4.zero;
                flatHighColors[i] = Vector4.zero;
                steepLowColors[i] = Vector4.zero;
                steepHighColors[i] = Vector4.zero;
                biomeParams[i] = Vector4.zero;
                biomeTextureParams[i] = Vector4.zero;
            }

            if (rules == null)
            {
                return 0;
            }

            for (int i = 0; i < rules.Count && slot < MaxBiomeSlots; i++)
            {
                BiomeVisualRule rule = rules[i];
                if (rule == null || !rule.IsValid)
                {
                    continue;
                }

                flatLowColors[slot] = rule.FlatLow;
                flatHighColors[slot] = rule.FlatHigh;
                steepLowColors[slot] = rule.SteepLow;
                steepHighColors[slot] = rule.SteepHigh;
                biomeParams[slot] = new Vector4(
                    rule.NormalStrength,
                    rule.Smoothness,
                    0f,
                    0f);
                BiomeSurfaceTextureSet textures = rule.Textures;
                biomeTextureParams[slot] = textures != null
                    ? new Vector4(textures.TriplanarScale, textures.HeightStrength, 0f, 0f)
                    : Vector4.zero;
                activeRules.Add(rule);
                slot++;
            }

            return slot;
        }

        int TryApplyTextureArrays(MaterialPropertyBlock propertyBlock)
        {
            try
            {
                return ApplyTextureArrays(propertyBlock);
            }
            catch (UnityException exception)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{name}: biome texture arrays could not be prepared; biome color layers remain active. {exception.Message}", this);
#endif
                ReleaseTextureArrays();
                textureArraySignature = null;
                return 0;
            }
        }

        int ApplyTextureArrays(MaterialPropertyBlock propertyBlock)
        {
            int count = activeRules.Count;
            if (count <= 0 || !AllRulesHaveRequiredTextures())
            {
                return 0;
            }

            string signature = BuildTextureArraySignature();
            if (textureArraySignature != signature)
            {
                ReleaseTextureArrays();
                baseColorArray = BuildTextureArray("base color", texture => texture.BaseColor, false);
                normalArray = BuildTextureArray("normal", texture => texture.Normal, true);
                roughnessArray = BuildTextureArray("roughness", texture => texture.Roughness, true);
                ambientOcclusionArray = BuildTextureArray("ambient occlusion", texture => texture.AmbientOcclusion, true);
                textureArraySignature = signature;
            }

            if (baseColorArray == null || normalArray == null || roughnessArray == null || ambientOcclusionArray == null)
            {
                return 0;
            }

            propertyBlock.SetTexture("_BiomeBaseColorArray", baseColorArray);
            propertyBlock.SetTexture("_BiomeNormalArray", normalArray);
            propertyBlock.SetTexture("_BiomeRoughnessArray", roughnessArray);
            propertyBlock.SetTexture("_BiomeAmbientOcclusionArray", ambientOcclusionArray);
            return count;
        }

        bool AllRulesHaveRequiredTextures()
        {
            for (int i = 0; i < activeRules.Count; i++)
            {
                BiomeSurfaceTextureSet textures = activeRules[i].Textures;
                if (textures == null
                    || textures.BaseColor == null
                    || textures.Normal == null
                    || textures.Roughness == null
                    || textures.AmbientOcclusion == null)
                {
                    return false;
                }
            }

            return true;
        }

        string BuildTextureArraySignature()
        {
            textureSignatureBuilder.Clear();
            for (int i = 0; i < activeRules.Count; i++)
            {
                BiomeSurfaceTextureSet textures = activeRules[i].Textures;
                AppendTextureSignature(textures.BaseColor);
                AppendTextureSignature(textures.Normal);
                AppendTextureSignature(textures.Roughness);
                AppendTextureSignature(textures.AmbientOcclusion);
            }

            return textureSignatureBuilder.ToString();
        }

        void AppendTextureSignature(Texture2D texture)
        {
            textureSignatureBuilder.Append(texture.name);
            textureSignatureBuilder.Append(':');
            textureSignatureBuilder.Append(texture.width);
            textureSignatureBuilder.Append('x');
            textureSignatureBuilder.Append(texture.height);
            textureSignatureBuilder.Append(':');
            textureSignatureBuilder.Append(texture.format);
            textureSignatureBuilder.Append(':');
            textureSignatureBuilder.Append(texture.mipmapCount);
            textureSignatureBuilder.Append('|');
        }

        Texture2DArray BuildTextureArray(
            string label,
            System.Func<BiomeSurfaceTextureSet, Texture2D> selectTexture,
            bool linear)
        {
            Texture2D firstTexture = selectTexture(activeRules[0].Textures);
            int width = firstTexture.width;
            int height = firstTexture.height;
            int mipCount = firstTexture.mipmapCount;
            TextureFormat format = firstTexture.format;

            for (int i = 1; i < activeRules.Count; i++)
            {
                Texture2D texture = selectTexture(activeRules[i].Textures);
                if (texture.width != width || texture.height != height || texture.format != format || texture.mipmapCount != mipCount)
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"{name}: biome {label} textures must share size, format, and mip count to build a texture array.",
                        this);
#endif
                    return null;
                }
            }

            Texture2DArray textureArray = new(width, height, activeRules.Count, format, mipCount > 1, linear)
            {
                wrapMode = firstTexture.wrapMode,
                filterMode = firstTexture.filterMode,
                anisoLevel = firstTexture.anisoLevel,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int layer = 0; layer < activeRules.Count; layer++)
            {
                Texture2D texture = selectTexture(activeRules[layer].Textures);
                for (int mip = 0; mip < mipCount; mip++)
                {
                    Graphics.CopyTexture(texture, 0, mip, textureArray, layer, mip);
                }
            }

            return textureArray;
        }

        void ReleaseTextureArrays()
        {
            ReleaseTextureArray(baseColorArray);
            ReleaseTextureArray(normalArray);
            ReleaseTextureArray(roughnessArray);
            ReleaseTextureArray(ambientOcclusionArray);
            baseColorArray = null;
            normalArray = null;
            roughnessArray = null;
            ambientOcclusionArray = null;
        }

        static void ReleaseTextureArray(Texture2DArray textureArray)
        {
            if (textureArray == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(textureArray);
            }
            else
            {
                DestroyImmediate(textureArray);
            }
        }
    }
}
