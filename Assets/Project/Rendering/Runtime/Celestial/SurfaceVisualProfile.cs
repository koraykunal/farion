using System;
using System.Collections.Generic;
using System.Text;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial/Surface Visual Profile", fileName = "SO_SurfaceVisualProfile")]
    public sealed class SurfaceVisualProfile : ScriptableObject
    {
        public const int MaxSurfaceSlots = 8;

        [SerializeField, Range(0f, 1f)] float blendStrength = 1f;
        [SerializeField, Range(16, 256)] int surfaceMapResolution = 64;
        [SerializeField] List<SurfaceVisualRule> rules = new();

        readonly Vector4[] flatLowColors = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] flatHighColors = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] steepLowColors = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] steepHighColors = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] surfaceParams = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] surfaceTextureParams = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] surfaceAuxTextureParams = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] surfaceEmissionTints = new Vector4[MaxSurfaceSlots];
        readonly List<SurfaceVisualRule> surfaceRules = new(MaxSurfaceSlots);
        readonly List<SurfaceVisualRule> ambientOcclusionRules = new(MaxSurfaceSlots);
        readonly List<SurfaceVisualRule> heightRules = new(MaxSurfaceSlots);
        readonly List<SurfaceVisualRule> emissionRules = new(MaxSurfaceSlots);
        readonly StringBuilder textureSignatureBuilder = new(768);

        [NonSerialized] string textureArraySignature;
        [NonSerialized] Texture2DArray baseColorArray;
        [NonSerialized] Texture2DArray normalArray;
        [NonSerialized] Texture2DArray roughnessArray;
        [NonSerialized] Texture2DArray ambientOcclusionArray;
        [NonSerialized] Texture2DArray heightArray;
        [NonSerialized] Texture2DArray emissionArray;

        public event Action Changed;

        public float BlendStrength => Mathf.Clamp01(blendStrength);
        public int SurfaceMapResolution => Mathf.Clamp(surfaceMapResolution, 16, 256);
        public IReadOnlyList<SurfaceVisualRule> Rules => rules;

        void OnValidate()
        {
            blendStrength = Mathf.Clamp01(blendStrength);
            surfaceMapResolution = Mathf.Clamp(surfaceMapResolution, 16, 256);
            rules ??= new List<SurfaceVisualRule>();
            textureArraySignature = null;
            Changed?.Invoke();
        }

        void OnDisable()
        {
            ReleaseTextureArrays();
            textureArraySignature = null;
        }

        public int ResolveMaterialIndex(SurfaceMaterialDefinition material)
        {
            if (material == null || rules == null)
            {
                return -1;
            }

            int slot = 0;
            for (int i = 0; i < rules.Count && slot < MaxSurfaceSlots; i++)
            {
                SurfaceVisualRule rule = rules[i];
                if (rule == null || !rule.IsValid)
                {
                    continue;
                }

                if (rule.Material == material)
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
            propertyBlock.SetFloat("_SurfaceVisualCount", count);
            propertyBlock.SetFloat("_SurfaceVisualBlendStrength", BlendStrength);
            propertyBlock.SetVectorArray("_SurfaceFlatLow", flatLowColors);
            propertyBlock.SetVectorArray("_SurfaceFlatHigh", flatHighColors);
            propertyBlock.SetVectorArray("_SurfaceSteepLow", steepLowColors);
            propertyBlock.SetVectorArray("_SurfaceSteepHigh", steepHighColors);
            propertyBlock.SetVectorArray("_SurfaceParams", surfaceParams);
            propertyBlock.SetVectorArray("_SurfaceTextureParams", surfaceTextureParams);
            propertyBlock.SetVectorArray("_SurfaceAuxTextureParams", surfaceAuxTextureParams);
            propertyBlock.SetVectorArray("_SurfaceEmissionTints", surfaceEmissionTints);

            ApplyTextureArrays(propertyBlock);
        }

        public static void ClearMaterialProperties(MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock == null)
            {
                return;
            }

            propertyBlock.SetFloat("_SurfaceVisualCount", 0f);
            propertyBlock.SetFloat("_SurfaceVisualBlendStrength", 0f);
            propertyBlock.SetFloat("_SurfaceTextureCount", 0f);
            propertyBlock.SetFloat("_SurfaceTextureBlendStrength", 0f);
            propertyBlock.SetFloat("_SurfaceAmbientOcclusionTextureCount", 0f);
            propertyBlock.SetFloat("_SurfaceHeightTextureCount", 0f);
            propertyBlock.SetFloat("_SurfaceEmissionTextureCount", 0f);
        }

        int FillShaderArrays()
        {
            surfaceRules.Clear();
            ambientOcclusionRules.Clear();
            heightRules.Clear();
            emissionRules.Clear();

            int slot = 0;
            for (int i = 0; i < MaxSurfaceSlots; i++)
            {
                flatLowColors[i] = Vector4.zero;
                flatHighColors[i] = Vector4.zero;
                steepLowColors[i] = Vector4.zero;
                steepHighColors[i] = Vector4.zero;
                surfaceParams[i] = Vector4.zero;
                surfaceTextureParams[i] = Vector4.zero;
                surfaceAuxTextureParams[i] = new Vector4(-1f, -1f, -1f, 0f);
                surfaceEmissionTints[i] = Vector4.zero;
            }

            if (rules == null)
            {
                return 0;
            }

            for (int i = 0; i < rules.Count && slot < MaxSurfaceSlots; i++)
            {
                SurfaceVisualRule rule = rules[i];
                if (rule == null || !rule.IsValid)
                {
                    continue;
                }

                flatLowColors[slot] = rule.FlatLow;
                flatHighColors[slot] = rule.FlatHigh;
                steepLowColors[slot] = rule.SteepLow;
                steepHighColors[slot] = rule.SteepHigh;
                surfaceParams[slot] = new Vector4(
                    rule.NormalStrength,
                    rule.Smoothness,
                    0f,
                    0f);

                SurfaceTextureSet textures = rule.Textures;
                int surfaceSlice = AddRuleIf(textures != null && textures.HasSurfaceTextures, surfaceRules, rule);
                int ambientOcclusionSlice = AddRuleIf(
                    textures != null && textures.HasAmbientOcclusion,
                    ambientOcclusionRules,
                    rule);
                int heightSlice = AddRuleIf(textures != null && textures.HasHeight, heightRules, rule);
                int emissionSlice = AddRuleIf(textures != null && textures.HasEmission, emissionRules, rule);

                surfaceTextureParams[slot] = textures != null
                    ? new Vector4(
                        textures.WorldTileSize,
                        textures.HeightStrength,
                        surfaceSlice,
                        surfaceSlice >= 0 ? 1f : 0f)
                    : Vector4.zero;
                surfaceAuxTextureParams[slot] = new Vector4(
                    ambientOcclusionSlice,
                    heightSlice,
                    emissionSlice,
                    textures?.EmissionStrength ?? 0f);
                surfaceEmissionTints[slot] = textures?.EmissionTint ?? Color.black;
                slot++;
            }

            return slot;
        }

        static int AddRuleIf(bool condition, List<SurfaceVisualRule> target, SurfaceVisualRule rule)
        {
            if (!condition)
            {
                return -1;
            }

            int slice = target.Count;
            target.Add(rule);
            return slice;
        }

        void ApplyTextureArrays(MaterialPropertyBlock propertyBlock)
        {
            string signature = BuildTextureArraySignature();
            if (textureArraySignature != signature)
            {
                RebuildTextureArrays();
                textureArraySignature = signature;
            }

            int surfaceCount =
                baseColorArray != null
                && normalArray != null
                && roughnessArray != null
                    ? surfaceRules.Count
                    : 0;
            int ambientOcclusionCount = ambientOcclusionArray != null ? ambientOcclusionRules.Count : 0;
            int heightCount = heightArray != null ? heightRules.Count : 0;
            int emissionCount = emissionArray != null ? emissionRules.Count : 0;

            if (surfaceCount > 0)
            {
                propertyBlock.SetTexture("_SurfaceBaseColorArray", baseColorArray);
                propertyBlock.SetTexture("_SurfaceNormalArray", normalArray);
                propertyBlock.SetTexture("_SurfaceRoughnessArray", roughnessArray);
            }

            if (ambientOcclusionCount > 0)
            {
                propertyBlock.SetTexture("_SurfaceAmbientOcclusionArray", ambientOcclusionArray);
            }

            if (heightCount > 0)
            {
                propertyBlock.SetTexture("_SurfaceHeightArray", heightArray);
            }

            if (emissionCount > 0)
            {
                propertyBlock.SetTexture("_SurfaceEmissionArray", emissionArray);
            }

            propertyBlock.SetFloat("_SurfaceTextureCount", surfaceCount);
            propertyBlock.SetFloat("_SurfaceTextureBlendStrength", surfaceCount > 0 ? BlendStrength : 0f);
            propertyBlock.SetFloat("_SurfaceAmbientOcclusionTextureCount", ambientOcclusionCount);
            propertyBlock.SetFloat("_SurfaceHeightTextureCount", heightCount);
            propertyBlock.SetFloat("_SurfaceEmissionTextureCount", emissionCount);
        }

        void RebuildTextureArrays()
        {
            ReleaseTextureArrays();

            baseColorArray = TryBuildTextureArray(
                "base color",
                surfaceRules,
                rule => rule.Textures.BaseColor,
                false);
            normalArray = TryBuildTextureArray(
                "normal",
                surfaceRules,
                rule => rule.Textures.Normal,
                true);
            roughnessArray = TryBuildTextureArray(
                "roughness",
                surfaceRules,
                rule => rule.Textures.Roughness,
                true);

            if (baseColorArray == null || normalArray == null || roughnessArray == null)
            {
                ReleaseTextureArray(baseColorArray);
                ReleaseTextureArray(normalArray);
                ReleaseTextureArray(roughnessArray);
                baseColorArray = null;
                normalArray = null;
                roughnessArray = null;
            }

            ambientOcclusionArray = TryBuildTextureArray(
                "ambient occlusion",
                ambientOcclusionRules,
                rule => rule.Textures.AmbientOcclusion,
                true);
            heightArray = TryBuildTextureArray(
                "height",
                heightRules,
                rule => rule.Textures.Height,
                true);
            emissionArray = TryBuildTextureArray(
                "emission",
                emissionRules,
                rule => rule.Textures.Emission,
                false);
        }

        Texture2DArray TryBuildTextureArray(
            string label,
            IReadOnlyList<SurfaceVisualRule> sourceRules,
            Func<SurfaceVisualRule, Texture2D> selectTexture,
            bool linear)
        {
            if (sourceRules.Count <= 0)
            {
                return null;
            }

            try
            {
                return BuildTextureArray(label, sourceRules, selectTexture, linear);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"{name}: the surface {label} texture array could not be prepared; that channel remains disabled. {exception.Message}",
                    this);
#else
                _ = exception;
#endif
                return null;
            }
        }

        string BuildTextureArraySignature()
        {
            textureSignatureBuilder.Clear();
            AppendRuleSignature("surface", surfaceRules, rule => rule.Textures.BaseColor);
            AppendRuleSignature("surface-normal", surfaceRules, rule => rule.Textures.Normal);
            AppendRuleSignature("surface-roughness", surfaceRules, rule => rule.Textures.Roughness);
            AppendRuleSignature(
                "ambient-occlusion",
                ambientOcclusionRules,
                rule => rule.Textures.AmbientOcclusion);
            AppendRuleSignature("height", heightRules, rule => rule.Textures.Height);
            AppendRuleSignature("emission", emissionRules, rule => rule.Textures.Emission);
            return textureSignatureBuilder.ToString();
        }

        void AppendRuleSignature(
            string label,
            IReadOnlyList<SurfaceVisualRule> sourceRules,
            Func<SurfaceVisualRule, Texture2D> selectTexture)
        {
            textureSignatureBuilder.Append(label);
            textureSignatureBuilder.Append('[');
            for (int i = 0; i < sourceRules.Count; i++)
            {
                AppendTextureSignature(selectTexture(sourceRules[i]));
            }

            textureSignatureBuilder.Append(']');
        }

        void AppendTextureSignature(Texture2D texture)
        {
            if (texture == null)
            {
                textureSignatureBuilder.Append("null|");
                return;
            }

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
            IReadOnlyList<SurfaceVisualRule> sourceRules,
            Func<SurfaceVisualRule, Texture2D> selectTexture,
            bool linear)
        {
            Texture2D firstTexture = selectTexture(sourceRules[0]);
            int width = firstTexture.width;
            int height = firstTexture.height;
            int mipCount = firstTexture.mipmapCount;
            TextureFormat format = firstTexture.format;

            for (int i = 1; i < sourceRules.Count; i++)
            {
                Texture2D texture = selectTexture(sourceRules[i]);
                if (texture.width != width
                    || texture.height != height
                    || texture.format != format
                    || texture.mipmapCount != mipCount)
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"{name}: surface {label} textures must share size, format, and mip count to build a texture array.",
                        this);
#endif
                    return null;
                }
            }

            Texture2DArray textureArray = new(
                width,
                height,
                sourceRules.Count,
                format,
                mipCount > 1,
                linear)
            {
                wrapMode = firstTexture.wrapMode,
                filterMode = firstTexture.filterMode,
                anisoLevel = firstTexture.anisoLevel,
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                for (int layer = 0; layer < sourceRules.Count; layer++)
                {
                    Texture2D texture = selectTexture(sourceRules[layer]);
                    for (int mip = 0; mip < mipCount; mip++)
                    {
                        Graphics.CopyTexture(texture, 0, mip, textureArray, layer, mip);
                    }
                }

                return textureArray;
            }
            catch
            {
                ReleaseTextureArray(textureArray);
                throw;
            }
        }

        void ReleaseTextureArrays()
        {
            ReleaseTextureArray(baseColorArray);
            ReleaseTextureArray(normalArray);
            ReleaseTextureArray(roughnessArray);
            ReleaseTextureArray(ambientOcclusionArray);
            ReleaseTextureArray(heightArray);
            ReleaseTextureArray(emissionArray);
            baseColorArray = null;
            normalArray = null;
            roughnessArray = null;
            ambientOcclusionArray = null;
            heightArray = null;
            emissionArray = null;
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
