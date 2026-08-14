using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Farion.Editor.Art
{
    internal static class ShuttleMaterialSync
    {
        const string MaterialFolder =
            "Assets/Project/Art/Materials/Spacecraft/PlayerStarterShuttle";

        readonly struct Source
        {
            public readonly string Material;
            public readonly float Metallic;
            public readonly float Smoothness;
            public readonly Color BaseColor;
            public readonly bool KeepSmoothness;
            public readonly bool KeepBaseColor;

            public Source(
                string material,
                float metallic,
                float smoothness,
                float r,
                float g,
                float b,
                bool keepSmoothness = false,
                bool keepBaseColor = false)
            {
                Material = material;
                Metallic = metallic;
                Smoothness = smoothness;
                BaseColor = new Color(r, g, b, 1f);
                KeepSmoothness = keepSmoothness;
                KeepBaseColor = keepBaseColor;
            }
        }

        static readonly Source[] Sources =
        {
            new("MAT_PlayerStarterShuttle_Back_Jet_Metal", 1f, 0.692f, 0.173f, 0.173f, 0.173f),
            new("MAT_PlayerStarterShuttle_Bolts_Window", 1f, 0.394f, 0.051f, 0.051f, 0.051f),
            new("MAT_PlayerStarterShuttle_Cannons_Paint_Colored", 0f, 0.494f, 0.286f, 0.179f, 0.117f),
            new("MAT_PlayerStarterShuttle_Cannons_Silver", 1f, 0.836f, 1f, 1f, 1f),
            new("MAT_PlayerStarterShuttle_Cords", 0f, 0.631f, 0f, 0f, 0f),
            new("MAT_PlayerStarterShuttle_Engines_Back_Metal", 1f, 0.797f, 0.306f, 0.306f, 0.306f),
            new("MAT_PlayerStarterShuttle_Front_Cannons_Metal", 1f, 0.612f, 0.299f, 0.299f, 0.299f),
            new("MAT_PlayerStarterShuttle_Front_Cannons_Metal_Dark", 1f, 0.647f, 0.366f, 0.366f, 0.366f),
            new("MAT_PlayerStarterShuttle_Front_Cannons_With_Stripes", 1f, 0.612f, 0.403f, 0.403f, 0.403f),
            new("MAT_PlayerStarterShuttle_Jet_Connectors", 0f, 0.423f, 0.073f, 0.073f, 0.073f),
            new("MAT_PlayerStarterShuttle_Jet_Paint_Metal", 0f, 0.655f, 0.156f, 0.156f, 0.156f),
            new("MAT_PlayerStarterShuttle_Landing_Gear", 0f, 0.902f, 0.064f, 0.064f, 0.064f),
            new("MAT_PlayerStarterShuttle_Landing_Gear_Dark", 0f, 0.902f, 0.015f, 0.015f, 0.015f),
            new("MAT_PlayerStarterShuttle_MetalPipe", 1f, 0.646f, 0.557f, 0.557f, 0.557f),
            new("MAT_PlayerStarterShuttle_Orange_Straps", 0f, 0.596f, 0.488f, 0.236f, 0.052f),
            new("MAT_PlayerStarterShuttle_Ship_Circle_Back_Parts_1", 0f, 0.458f, 0.171f, 0.171f, 0.171f),
            new("MAT_PlayerStarterShuttle_Ship_Door_Hinge", 1f, 0.5f, 0.437f, 0.437f, 0.437f),
            new("MAT_PlayerStarterShuttle_Ship_Metal_Smooth_1", 1f, 0f, 0.137f, 0.137f, 0.137f, keepSmoothness: true),
            new("MAT_PlayerStarterShuttle_Tanks", 1f, 0.724f, 0.608f, 0.608f, 0.608f),
            new("MAT_PlayerStarterShuttle_Window_Bars", 1f, 0f, 0f, 0f, 0f, keepSmoothness: true, keepBaseColor: true),
            new("MAT_PlayerStarterShuttle_Yoke", 1f, 0.431f, 0.113f, 0.113f, 0.113f),
        };

        [MenuItem("Farion/Art/Sync Starter Shuttle Materials From Blender")]
        static void Sync()
        {
            List<string> missing = new();
            StringBuilder report = new();
            report.AppendLine(
                $"{"MATERIAL",-46} {"METALLIC",-16} {"SMOOTHNESS",-16} BASE COLOR");

            int changed = 0;
            foreach (Source source in Sources)
            {
                string path = $"{MaterialFolder}/{source.Material}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    missing.Add(source.Material);
                    continue;
                }

                float oldMetallic = material.GetFloat("_Metallic");
                float oldSmoothness = material.GetFloat("_Smoothness");
                Color oldColor = material.GetColor("_BaseColor");

                Undo.RecordObject(material, "Sync Shuttle Materials");
                material.SetFloat("_Metallic", source.Metallic);
                if (!source.KeepSmoothness)
                {
                    material.SetFloat("_Smoothness", source.Smoothness);
                    material.SetFloat("_Glossiness", source.Smoothness);
                }

                if (!source.KeepBaseColor)
                {
                    material.SetColor("_BaseColor", source.BaseColor);
                    material.SetColor("_Color", source.BaseColor);
                }

                EditorUtility.SetDirty(material);
                changed++;

                report.AppendLine(string.Format(
                    "{0,-46} {1,-16} {2,-16} {3}",
                    source.Material.Replace("MAT_PlayerStarterShuttle_", ""),
                    $"{oldMetallic:0.00} -> {source.Metallic:0.00}",
                    source.KeepSmoothness
                        ? $"{oldSmoothness:0.00} (kept)"
                        : $"{oldSmoothness:0.00} -> {source.Smoothness:0.00}",
                    source.KeepBaseColor
                        ? $"{ToText(oldColor)} (kept)"
                        : $"{ToText(oldColor)} -> {ToText(source.BaseColor)}"));
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Synced {changed} starter shuttle materials.\n{report}");

            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    $"Materials not found under {MaterialFolder}: {string.Join(", ", missing)}");
            }

            Debug.Log(
                "Glass is intentionally excluded: the Blender source is a transmissive "
                + "material and needs a transparent URP setup rather than a value copy.");
        }

        static string ToText(Color color)
        {
            return $"[{color.r:0.00},{color.g:0.00},{color.b:0.00}]";
        }

        const string TextureFolder =
            "Assets/Project/Art/Textures/Spacecraft/PlayerStarterShuttle/Baked";

        static readonly string[] BakedParts =
        {
            "Back_Fins", "Back_Plate", "Back_Plates", "Cockpit_Floor", "Door",
            "Monitors", "Monitor_Stand", "Seat_1", "Seat_2", "Ship_Hull",
            "Ship_Plates_1", "Ship_Plates_2", "Ship_Side_Beams", "Wing_Fins", "Wings",
        };

        [MenuItem("Farion/Art/Rebind Baked Starter Shuttle Textures")]
        static void RebindBaked()
        {
            StringBuilder report = new();
            int bound = 0;

            foreach (string part in BakedParts)
            {
                string materialPath =
                    $"{MaterialFolder}/MAT_PlayerStarterShuttle_{part}_Baked.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    report.AppendLine($"{part,-20} material not found");
                    continue;
                }

                Texture2D baseColor = Configure(part, "BaseColor", MapKind.Color);
                Texture2D mask = Configure(part, "MetallicSmoothness", MapKind.Data);
                Texture2D normal = Configure(part, "Normal", MapKind.Normal);
                Texture2D occlusion = Configure(part, "Occlusion", MapKind.Data);
                Texture2D emission = Configure(part, "Emission", MapKind.Color);

                Undo.RecordObject(material, "Rebind Baked Shuttle Textures");
                Assign(material, "_BaseMap", baseColor);
                Assign(material, "_MainTex", baseColor);
                Assign(material, "_MetallicGlossMap", mask);
                Assign(material, "_BumpMap", normal);
                Assign(material, "_OcclusionMap", occlusion);

                Keyword(material, "_METALLICSPECGLOSSMAP", mask != null);
                Keyword(material, "_NORMALMAP", normal != null);
                Keyword(material, "_OCCLUSIONMAP", occlusion != null);

                if (emission != null)
                {
                    Assign(material, "_EmissionMap", emission);
                    Keyword(material, "_EMISSION", true);
                    material.globalIlluminationFlags &=
                        ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    if (material.GetColor("_EmissionColor").maxColorComponent <= 0f)
                    {
                        material.SetColor("_EmissionColor", Color.white);
                    }
                }

                material.SetColor("_BaseColor", Color.white);
                material.SetColor("_Color", Color.white);
                if (mask != null)
                {
                    material.SetFloat("_Metallic", 1f);
                    material.SetFloat("_Smoothness", 1f);
                    material.SetFloat("_GlossMapScale", 1f);
                }

                if (occlusion != null)
                {
                    material.SetFloat("_OcclusionStrength", 1f);
                }

                EditorUtility.SetDirty(material);
                bound++;
                report.AppendLine(string.Format(
                    "{0,-20} base={1} mask={2} normal={3} occlusion={4} emission={5}",
                    part,
                    Mark(baseColor), Mark(mask), Mark(normal), Mark(occlusion),
                    Mark(emission)));
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Rebound {bound} baked shuttle materials.\n{report}");
        }

        enum MapKind
        {
            Color,
            Data,
            Normal,
        }

        static Texture2D Configure(string part, string suffix, MapKind kind)
        {
            string path =
                $"{TextureFolder}/TX_PlayerStarterShuttle_{part}_{suffix}.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                return null;
            }

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return texture;
            }

            bool dirty = false;
            TextureImporterType wanted = kind == MapKind.Normal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            if (importer.textureType != wanted)
            {
                importer.textureType = wanted;
                dirty = true;
            }

            bool wantsSrgb = kind == MapKind.Color;
            if (kind != MapKind.Normal && importer.sRGBTexture != wantsSrgb)
            {
                importer.sRGBTexture = wantsSrgb;
                dirty = true;
            }

            if (kind == MapKind.Data && importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = false;
                dirty = true;
            }

            if (suffix == "MetallicSmoothness")
            {
                if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
                {
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    dirty = true;
                }

                if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
                {
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    dirty = true;
                }
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            return texture;
        }

        static void Assign(Material material, string property, Texture2D texture)
        {
            if (texture != null && material.HasProperty(property))
            {
                material.SetTexture(property, texture);
            }
        }

        static void Keyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        static string Mark(Texture2D texture)
        {
            return texture == null ? "-" : $"{texture.width}";
        }
    }
}
