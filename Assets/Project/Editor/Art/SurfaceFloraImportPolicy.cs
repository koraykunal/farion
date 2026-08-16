using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Farion.Editor.Art
{
    internal sealed class SurfaceFloraImportPolicy : AssetPostprocessor
    {
        public override uint GetVersion()
        {
            return 2;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(FarionAssetPaths.SurfaceFloraModelFolder, StringComparison.Ordinal))
            {
                return;
            }

            ModelImporter importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.isReadable = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FarionAssetPaths.SurfaceFloraTextureFolder, StringComparison.Ordinal))
            {
                return;
            }

            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool isNormal = Contains(name, "_nor_") || Contains(name, "_normal");
            bool isLinear = isNormal || Contains(name, "_rough_") || Contains(name, "_roughness") ||
                Contains(name, "_mask") ||
                Contains(name, "_disp_") || Contains(name, "_height") || Contains(name, "_alpha_");
            bool carriesColorAlpha = Contains(name, "_diff_") || Contains(name, "_basecolor");

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isLinear;
            importer.alphaSource = carriesColorAlpha
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = carriesColorAlpha;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        static bool Contains(string value, string token)
        {
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
