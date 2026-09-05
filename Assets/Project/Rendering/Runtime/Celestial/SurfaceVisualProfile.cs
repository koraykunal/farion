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
        [Tooltip("Library of every texture set a planet may use. The GPU texture arrays are built once from this list; a derived per-planet profile only carries slot-to-slice indices.")]
        [SerializeField] List<SurfaceVisualRule> rules = new();

        readonly Vector4[] flatLowColors = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] flatHighColors = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] steepLowColors = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] steepHighColors = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] surfaceParams = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] surfaceTextureParams = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] surfaceAuxTextureParams = new Vector4[MaxSurfaceSlots];
        readonly Vector4[] surfaceEmissionTints = new Vector4[MaxSurfaceSlots];
        readonly List<SurfaceTextureSet> surfaceSlices = new();
        readonly List<Texture2D> ambientOcclusionSlices = new();
        readonly List<Texture2D> heightSlices = new();
        readonly List<Texture2D> emissionSlices = new();
        readonly StringBuilder textureSignatureBuilder = new(768);

        [NonSerialized] SurfaceVisualProfile textureSource;
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
        public SurfaceVisualProfile TextureSource => textureSource != null ? textureSource : this;

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
            if (textureSource != null)
            {
                textureSource.Changed -= ForwardSourceChanged;
            }

            ReleaseTextureArrays();
            textureArraySignature = null;
        }

        void ForwardSourceChanged()
        {
            Changed?.Invoke();
        }

        public SurfaceVisualProfile CreateVariant(
            IReadOnlyList<SurfaceMaterialDefinition> materials,
            int seed,
            float hueShiftDegrees,
            float valueJitter)
        {
            SurfaceVisualProfile variant = ProfileVariants.Clone(this);
            variant.textureSource = TextureSource;
            variant.textureSource.Changed += variant.ForwardSourceChanged;
            variant.rules = new List<SurfaceVisualRule>(MaxSurfaceSlots);
            List<SurfaceVisualRule> candidates = new();
            float hueShift = (SeedUtility.Unit01(seed, "visual.surface.hue") * 2f - 1f) * hueShiftDegrees;
            for (int i = 0; i < materials.Count && variant.rules.Count < MaxSurfaceSlots; i++)
            {
                SurfaceMaterialDefinition material = materials[i];
                candidates.Clear();
                for (int r = 0; r < rules.Count; r++)
                {
                    if (rules[r] != null && rules[r].IsValid && rules[r].Material == material)
                    {
                        candidates.Add(rules[r]);
                    }
                }

                if (candidates.Count == 0)
                {
                    continue;
                }

                string stream = "visual.surface." + material.MaterialId;
                SurfaceVisualRule picked = candidates[Mathf.Min(
                    candidates.Count - 1,
                    Mathf.FloorToInt(SeedUtility.Unit01(seed, stream) * candidates.Count))];
                float valueScale = 1f + (SeedUtility.Unit01(seed, stream + ".value") * 2f - 1f) * valueJitter;
                variant.rules.Add(picked.CreateTinted(hueShift, valueScale));
            }

            return variant;
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

            SurfaceVisualProfile source = TextureSource;
            source.EnsureTextureArrays();
            int count = FillShaderArrays(source);
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
            source.ApplyTextureArrays(propertyBlock, BlendStrength);
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

        int FillShaderArrays(SurfaceVisualProfile source)
        {
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

            int slot = 0;
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
                surfaceParams[slot] = new Vector4(rule.NormalStrength, rule.Smoothness, 0f, 0f);

                SurfaceTextureSet textures = rule.Textures;
                int surfaceSlice = source.ResolveSurfaceSlice(textures);
                surfaceTextureParams[slot] = textures != null
                    ? new Vector4(
                        textures.WorldTileSize,
                        textures.HeightStrength,
                        surfaceSlice,
                        surfaceSlice >= 0 ? 1f : 0f)
                    : Vector4.zero;
                surfaceAuxTextureParams[slot] = new Vector4(
                    source.ambientOcclusionArray != null ? source.ambientOcclusionSlices.IndexOf(textures?.AmbientOcclusion) : -1,
                    source.heightArray != null && textures != null && textures.HasHeight
                        ? source.heightSlices.IndexOf(textures.Height)
                        : -1,
                    source.emissionArray != null && textures != null && textures.HasEmission
                        ? source.emissionSlices.IndexOf(textures.Emission)
                        : -1,
                    textures?.EmissionStrength ?? 0f);
                surfaceEmissionTints[slot] = textures?.EmissionTint ?? Color.black;
                slot++;
            }

            return slot;
        }

        int ResolveSurfaceSlice(SurfaceTextureSet textures)
        {
            if (baseColorArray == null || textures == null || !textures.HasSurfaceTextures)
            {
                return -1;
            }

            for (int i = 0; i < surfaceSlices.Count; i++)
            {
                if (SameSurfaceTextures(surfaceSlices[i], textures))
                {
                    return i;
                }
            }

            return -1;
        }

        static bool SameSurfaceTextures(SurfaceTextureSet a, SurfaceTextureSet b)
        {
            return a.BaseColor == b.BaseColor && a.Normal == b.Normal && a.Roughness == b.Roughness;
        }

        void ApplyTextureArrays(MaterialPropertyBlock propertyBlock, float blend)
        {
            int surfaceCount = baseColorArray != null ? surfaceSlices.Count : 0;
            int ambientOcclusionCount = ambientOcclusionArray != null ? ambientOcclusionSlices.Count : 0;
            int heightCount = heightArray != null ? heightSlices.Count : 0;
            int emissionCount = emissionArray != null ? emissionSlices.Count : 0;

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
            propertyBlock.SetFloat("_SurfaceTextureBlendStrength", surfaceCount > 0 ? blend : 0f);
            propertyBlock.SetFloat("_SurfaceAmbientOcclusionTextureCount", ambientOcclusionCount);
            propertyBlock.SetFloat("_SurfaceHeightTextureCount", heightCount);
            propertyBlock.SetFloat("_SurfaceEmissionTextureCount", emissionCount);
        }

        void EnsureTextureArrays()
        {
            CollectSlices();
            string signature = BuildTextureArraySignature();
            if (textureArraySignature == signature)
            {
                return;
            }

            RebuildTextureArrays();
            textureArraySignature = signature;
        }

        void CollectSlices()
        {
            surfaceSlices.Clear();
            ambientOcclusionSlices.Clear();
            heightSlices.Clear();
            emissionSlices.Clear();
            if (rules == null)
            {
                return;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                SurfaceTextureSet textures = rules[i]?.Textures;
                if (rules[i] == null || !rules[i].IsValid || textures == null)
                {
                    continue;
                }

                if (textures.HasSurfaceTextures && ResolveSliceIndex(textures) < 0)
                {
                    surfaceSlices.Add(textures);
                }

                AddSlice(ambientOcclusionSlices, textures.HasAmbientOcclusion ? textures.AmbientOcclusion : null);
                AddSlice(heightSlices, textures.HasHeight ? textures.Height : null);
                AddSlice(emissionSlices, textures.HasEmission ? textures.Emission : null);
            }
        }

        int ResolveSliceIndex(SurfaceTextureSet textures)
        {
            for (int i = 0; i < surfaceSlices.Count; i++)
            {
                if (SameSurfaceTextures(surfaceSlices[i], textures))
                {
                    return i;
                }
            }

            return -1;
        }

        static void AddSlice(List<Texture2D> slices, Texture2D texture)
        {
            if (texture != null && !slices.Contains(texture))
            {
                slices.Add(texture);
            }
        }

        void RebuildTextureArrays()
        {
            ReleaseTextureArrays();

            baseColorArray = TryBuildTextureArray("base color", surfaceSlices.Count, i => surfaceSlices[i].BaseColor, false);
            normalArray = TryBuildTextureArray("normal", surfaceSlices.Count, i => surfaceSlices[i].Normal, true);
            roughnessArray = TryBuildTextureArray("roughness", surfaceSlices.Count, i => surfaceSlices[i].Roughness, true);

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
                ambientOcclusionSlices.Count,
                i => ambientOcclusionSlices[i],
                true);
            heightArray = TryBuildTextureArray("height", heightSlices.Count, i => heightSlices[i], true);
            emissionArray = TryBuildTextureArray("emission", emissionSlices.Count, i => emissionSlices[i], false);
        }

        Texture2DArray TryBuildTextureArray(
            string label,
            int count,
            Func<int, Texture2D> selectTexture,
            bool linear)
        {
            if (count <= 0)
            {
                return null;
            }

            try
            {
                return BuildTextureArray(label, count, selectTexture, linear);
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
            AppendSignature("surface", surfaceSlices.Count, i => surfaceSlices[i].BaseColor);
            AppendSignature("surface-normal", surfaceSlices.Count, i => surfaceSlices[i].Normal);
            AppendSignature("surface-roughness", surfaceSlices.Count, i => surfaceSlices[i].Roughness);
            AppendSignature("ambient-occlusion", ambientOcclusionSlices.Count, i => ambientOcclusionSlices[i]);
            AppendSignature("height", heightSlices.Count, i => heightSlices[i]);
            AppendSignature("emission", emissionSlices.Count, i => emissionSlices[i]);
            return textureSignatureBuilder.ToString();
        }

        void AppendSignature(string label, int count, Func<int, Texture2D> selectTexture)
        {
            textureSignatureBuilder.Append(label);
            textureSignatureBuilder.Append('[');
            for (int i = 0; i < count; i++)
            {
                AppendTextureSignature(selectTexture(i));
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
            int count,
            Func<int, Texture2D> selectTexture,
            bool linear)
        {
            Texture2D firstTexture = selectTexture(0);
            int width = firstTexture.width;
            int height = firstTexture.height;
            int mipCount = firstTexture.mipmapCount;
            TextureFormat format = firstTexture.format;

            for (int i = 1; i < count; i++)
            {
                Texture2D texture = selectTexture(i);
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
                count,
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
                for (int layer = 0; layer < count; layer++)
                {
                    Texture2D texture = selectTexture(layer);
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
