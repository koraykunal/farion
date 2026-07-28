using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Farion.Editor.Art
{
    /// <summary>
    /// Keeps runtime-ready celestial surface maps on one predictable import contract.
    /// Texture arrays require matching dimensions, formats, and mip chains, so this
    /// policy is intentionally narrower than a project-wide texture postprocessor.
    /// </summary>
    internal sealed class CelestialTextureImportPolicy : AssetPostprocessor
    {
        const string CelestialSurfaceTextureRoot =
            "Assets/Project/Art/Textures/Celestial/SurfaceMaterials/";
        const string CelestialMoonTextureRoot =
            "Assets/Project/Art/Textures/Celestial/Moon/";
        const string CelestialOceanTextureRoot =
            "Assets/Project/Art/Textures/Celestial/Ocean/";

        public override uint GetVersion()
        {
            return 4;
        }

        void OnPreprocessTexture()
        {
            bool isSurfaceTexture =
                assetPath.StartsWith(CelestialSurfaceTextureRoot, StringComparison.Ordinal);
            bool isMoonTexture =
                assetPath.StartsWith(CelestialMoonTextureRoot, StringComparison.Ordinal);
            bool isOceanTexture =
                assetPath.StartsWith(CelestialOceanTextureRoot, StringComparison.Ordinal);
            if (!isSurfaceTexture && !isMoonTexture && !isOceanTexture)
            {
                return;
            }

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            bool isNormal = HasMapToken(fileName, "Normal");
            bool isLinear =
                isNormal
                || HasMapToken(fileName, "Roughness")
                || HasMapToken(fileName, "AO")
                || HasMapToken(fileName, "Height")
                || (isMoonTexture
                    && (fileName.IndexOf("SurfaceNoise", StringComparison.OrdinalIgnoreCase) >= 0
                        || fileName.IndexOf("EjectaMask", StringComparison.OrdinalIgnoreCase) >= 0));
            bool isClamp =
                isMoonTexture
                && fileName.IndexOf("EjectaMask", StringComparison.OrdinalIgnoreCase) >= 0;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = isNormal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.sRGBTexture = !isLinear;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = isClamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.StartsWith(CelestialSurfaceTextureRoot, StringComparison.Ordinal)
                || texture.width == texture.height)
            {
                return;
            }

            Debug.LogWarning(
                $"{assetPath} imported at {texture.width}x{texture.height}. "
                + "Celestial surface texture-array slices must be square and share the same dimensions; "
                + "keep this set unassigned until it is normalized.",
                texture);
        }

        static bool HasMapToken(string fileName, string mapName)
        {
            return fileName.IndexOf($"_{mapName}_", StringComparison.OrdinalIgnoreCase) >= 0
                || fileName.EndsWith($"_{mapName}", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith($"{mapName}_", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals(mapName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
