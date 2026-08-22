using System.IO;
using UnityEditor;
using UnityEngine;

namespace Farion.Editor.Authoring
{
    static class FarionLightCookieAuthoring
    {
        const string OutputPath = "Assets/Project/Art/Textures/Lighting/TX_Light_FloodlightCookie.png";
        const int Resolution = 512;
        const int ReflectorLobes = 16;
        const float ReflectorDepth = 0.13f;
        const float FalloffPower = 1.6f;
        const float TopFlattenHeight = 0.62f;
        const float TopFlattenSoftness = 0.22f;

        [MenuItem("Farion/Authoring/Generate Floodlight Cookie")]
        public static void Generate()
        {
            Texture2D texture = new(Resolution, Resolution, TextureFormat.RGBA32, false, true);
            Color[] pixels = new Color[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float u = (x + 0.5f) / Resolution * 2f - 1f;
                    float v = (y + 0.5f) / Resolution * 2f - 1f;
                    float value = EvaluateCookie(u, v);
                    pixels[y * Resolution + x] = new Color(value, value, value, value);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
            ApplyImportSettings();
            Debug.Log($"Wrote {OutputPath}.");
        }

        static float EvaluateCookie(float u, float v)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            if (radius >= 1f)
            {
                return 0f;
            }

            float falloff = Mathf.Pow(1f - radius * radius, FalloffPower);
            float angle = Mathf.Atan2(v, u);
            float reflector = 1f - ReflectorDepth *
                (Mathf.Sin(angle * ReflectorLobes) * 0.5f + 0.5f) *
                Mathf.SmoothStep(0f, 1f, radius);
            float topFlatten = 1f - Mathf.SmoothStep(
                TopFlattenHeight,
                TopFlattenHeight + TopFlattenSoftness,
                v);

            return Mathf.Clamp01(falloff * reflector * Mathf.Lerp(0.35f, 1f, topFlatten));
        }

        static void ApplyImportSettings()
        {
            if (AssetImporter.GetAtPath(OutputPath) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }
}
