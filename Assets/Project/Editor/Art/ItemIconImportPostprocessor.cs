using Farion.Editor;
using UnityEditor;

namespace Farion.Editor.Art
{
    public sealed class ItemIconImportPostprocessor : AssetPostprocessor
    {
        const string ItemIconRoot = FarionAssetPaths.ItemIconFolder;
        const int MaxIconSize = 512;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(
                    ItemIconRoot,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.maxTextureSize = MaxIconSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
        }
    }
}
