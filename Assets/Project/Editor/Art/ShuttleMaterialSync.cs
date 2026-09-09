using System.Text;
using UnityEditor;
using UnityEngine;

namespace Farion.Editor.Art
{
    internal static class ShuttleMaterialSync
    {
        const string MaterialFolder =
            "Assets/Project/Art/Materials/Spacecraft/PlayerStarterShuttle";

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
