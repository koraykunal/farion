using Farion.Core.Physics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Farion.Editor.Authoring
{
    static class FarionResourceModelIntake
    {
        const string ModelFolder = "Assets/Project/Art/Models/ResourceNodes";
        const string TextureFolder = "Assets/Project/Art/Textures/ResourceNodes";
        const string MaterialPath = "Assets/Project/Art/Materials/ResourceNodes/MAT_Resource_IronOre.mat";
        const string PrefabPath = "Assets/Project/Prefabs/Gameplay/ResourceNodes/PF_IronOreNode.prefab";
        const string DefinitionPath = "Assets/Project/Design/Gameplay/ResourceNodes/SO_IronOreNode.asset";

        const string IncomingModel = ModelFolder + "/Meshy_AI__0824230732_texture.fbx";
        const string IncomingBaseColor = ModelFolder + "/Meshy_AI__0824230732_texture.png";
        const string IncomingNormal = ModelFolder + "/Meshy_AI__0824230732_texture_normal.png";
        const string IncomingMetallic = ModelFolder + "/Meshy_AI__0824230732_texture_metallic.png";
        const string IncomingRoughness = ModelFolder + "/Meshy_AI__0824230732_texture_roughness.png";

        const string ModelPath = ModelFolder + "/SM_Resource_IronOre_A.fbx";
        const string BaseColorPath = TextureFolder + "/TX_Resource_IronOre_BaseColor.png";
        const string NormalPath = TextureFolder + "/TX_Resource_IronOre_Normal.png";
        const string MetallicSmoothnessPath = TextureFolder + "/TX_Resource_IronOre_MetallicSmoothness.png";

        static readonly Vector3 SourceUpAxisCorrectionEuler = new(-90f, 0f, 0f);

        const float TargetLargestDimension = 2f;
        const float SceneSurfaceOffset = 1.96f;
        const float BurialDepth = 0.15f;
        const float TriggerPadding = 0.4f;

        static readonly StringBuilder Report = new();

        public static void Run()
        {
            Report.Clear();
            try
            {
                ReplaceIncomingAssets();
                ConfigureModelImporter();
                Material material = BuildMaterial();
                BuildPrefab(material);
            }
            catch (System.Exception exception)
            {
                Log("FAILED: " + exception);
                Debug.Log(Report.ToString());
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log(Report.ToString());
        }

        public static void RebuildPrefab()
        {
            Report.Clear();
            try
            {
                Material material = BuildMaterial();
                BuildPrefab(material);
            }
            catch (System.Exception exception)
            {
                Log("FAILED: " + exception);
                Debug.Log(Report.ToString());
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log(Report.ToString());
        }

        static void Log(string line)
        {
            Report.AppendLine("[intake] " + line);
        }

        static void ReplaceIncomingAssets()
        {
            DeleteIfPresent(ModelPath);
            DeleteIfPresent(BaseColorPath);
            DeleteIfPresent(NormalPath);
            DeleteIfPresent(MetallicSmoothnessPath);

            PackMetallicSmoothness();

            MoveAsset(IncomingModel, ModelPath);
            MoveAsset(IncomingBaseColor, BaseColorPath);
            MoveAsset(IncomingNormal, NormalPath);
            DeleteIfPresent(IncomingMetallic);
            DeleteIfPresent(IncomingRoughness);
            AssetDatabase.Refresh();

            ConfigureTexture(BaseColorPath, TextureImporterType.Default, sRgb: true, hasAlpha: false);
            ConfigureTexture(NormalPath, TextureImporterType.NormalMap, sRgb: false, hasAlpha: false);
            ConfigureTexture(MetallicSmoothnessPath, TextureImporterType.Default, sRgb: false, hasAlpha: true);
        }

        static void DeleteIfPresent(string path)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            {
                AssetDatabase.DeleteAsset(path);
                Log("deleted " + path);
            }
        }

        static void MoveAsset(string from, string to)
        {
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
            {
                throw new System.InvalidOperationException($"move {from} -> {to}: {error}");
            }

            Log($"moved {Path.GetFileName(from)} -> {Path.GetFileName(to)}");
        }

        static void PackMetallicSmoothness()
        {
            Texture2D metallic = LoadRaw(IncomingMetallic);
            Texture2D roughness = LoadRaw(IncomingRoughness);
            if (metallic.width != roughness.width || metallic.height != roughness.height)
            {
                throw new System.InvalidOperationException(
                    $"metallic {metallic.width}x{metallic.height} != roughness {roughness.width}x{roughness.height}");
            }

            Color32[] metallicPixels = metallic.GetPixels32();
            Color32[] roughnessPixels = roughness.GetPixels32();
            Color32[] packed = new Color32[metallicPixels.Length];
            int metallicSum = 0;
            int smoothnessSum = 0;
            for (int i = 0; i < packed.Length; i++)
            {
                byte metallicValue = metallicPixels[i].r;
                byte smoothnessValue = (byte)(255 - roughnessPixels[i].r);
                packed[i] = new Color32(metallicValue, 0, 0, smoothnessValue);
                metallicSum += metallicValue;
                smoothnessSum += smoothnessValue;
            }

            Texture2D output = new(metallic.width, metallic.height, TextureFormat.RGBA32, mipChain: false, linear: true);
            output.SetPixels32(packed);
            output.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            File.WriteAllBytes(
                Path.Combine(Directory.GetCurrentDirectory(), MetallicSmoothnessPath),
                output.EncodeToPNG());
            Object.DestroyImmediate(output);
            Object.DestroyImmediate(metallic);
            Object.DestroyImmediate(roughness);

            Log($"packed metallic+smoothness {packed.Length} px, " +
                $"meanMetallic={metallicSum / (float)packed.Length / 255f:0.###}, " +
                $"meanSmoothness={smoothnessSum / (float)packed.Length / 255f:0.###}");
        }

        static Texture2D LoadRaw(string assetPath)
        {
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, mipChain: false, linear: true);
            if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath))))
            {
                throw new System.InvalidOperationException("could not decode " + assetPath);
            }

            return texture;
        }

        static void ConfigureTexture(string path, TextureImporterType type, bool sRgb, bool hasAlpha)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                throw new System.InvalidOperationException("no texture importer at " + path);
            }

            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            if (type == TextureImporterType.Default)
            {
                importer.alphaSource = hasAlpha
                    ? TextureImporterAlphaSource.FromInput
                    : TextureImporterAlphaSource.None;
            }

            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
            Log($"texture {Path.GetFileName(path)}: type={type}, sRGB={sRgb}, alpha={hasAlpha}");
        }

        static void ConfigureModelImporter()
        {
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (importer == null)
            {
                throw new System.InvalidOperationException("no model importer at " + ModelPath);
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importConstraints = false;
            importer.generateSecondaryUV = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
            Log("model importer hardened");
        }

        static Material BuildMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new System.InvalidOperationException("URP/Lit shader not found");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
                Log("created " + MaterialPath);
            }

            material.shader = shader;
            Texture baseColor = AssetDatabase.LoadAssetAtPath<Texture>(BaseColorPath);
            Texture normal = AssetDatabase.LoadAssetAtPath<Texture>(NormalPath);
            Texture metallicSmoothness = AssetDatabase.LoadAssetAtPath<Texture>(MetallicSmoothnessPath);
            if (baseColor == null || normal == null || metallicSmoothness == null)
            {
                throw new System.InvalidOperationException("one or more iron ore textures failed to import");
            }

            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_MainTex", baseColor);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", metallicSmoothness);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_GlossMapScale", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Log("material bound: baseMap, bumpMap, metallicGlossMap; emission off; instancing on");
            return material;
        }

        static void BuildPrefab(Material material)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                throw new System.InvalidOperationException("model prefab failed to load");
            }

            GameObject instance = Object.Instantiate(model);
            instance.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.Euler(SourceUpAxisCorrectionEuler));
            instance.transform.localScale = Vector3.one;

            MeshFilter filter = instance.GetComponentInChildren<MeshFilter>();
            MeshRenderer sourceRenderer = instance.GetComponentInChildren<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || sourceRenderer == null)
            {
                Object.DestroyImmediate(instance);
                throw new System.InvalidOperationException("model has no mesh renderer");
            }

            Mesh mesh = filter.sharedMesh;
            Bounds bounds = sourceRenderer.bounds;
            Vector3 rawSize = bounds.size;
            float largest = Mathf.Max(rawSize.x, Mathf.Max(rawSize.y, rawSize.z));
            float normalisation = largest > 0.0001f ? TargetLargestDimension / largest : 1f;

            Vector3 center = bounds.center * normalisation;
            Vector3 extents = bounds.extents * normalisation;
            Vector3 meshPosition = filter.transform.position * normalisation;
            Quaternion meshRotation = filter.transform.rotation;
            Vector3 meshScale = filter.transform.lossyScale * normalisation;

            float visualY = -SceneSurfaceOffset - BurialDepth - center.y + extents.y;
            Vector3 visualPosition = new(
                meshPosition.x - center.x,
                meshPosition.y + visualY,
                meshPosition.z - center.z);

            Object.DestroyImmediate(instance);

            GameObject root = new("PF_IronOreNode")
            {
                layer = FarionLayers.Interactable
            };

            Vector3 triggerSize = extents * 2f + Vector3.one * TriggerPadding;
            Vector3 triggerCenter = new(0f, -SceneSurfaceOffset - BurialDepth + extents.y, 0f);
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = triggerSize;
            trigger.center = triggerCenter;

            GameObject visual = new("VisualRoot")
            {
                layer = FarionLayers.Interactable
            };
            visual.transform.SetParent(root.transform, worldPositionStays: false);
            visual.transform.localPosition = visualPosition;
            visual.transform.localRotation = meshRotation;
            visual.transform.localScale = meshScale;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            MeshCollider solid = visual.AddComponent<MeshCollider>();
            solid.sharedMesh = mesh;
            solid.convex = true;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);

            RelinkDefinition();

            Log($"mesh='{mesh.name}' verts={mesh.vertexCount} tris={CountTriangles(mesh)} submeshes={mesh.subMeshCount}");
            Log($"raw bounds size=({rawSize.x:0.###}, {rawSize.y:0.###}, {rawSize.z:0.###}) -> normalisation x{normalisation:0.####}");
            Log($"final size=({extents.x * 2f:0.###}, {extents.y * 2f:0.###}, {extents.z * 2f:0.###})");
            Log($"visualRoot pos=({visualPosition.x:0.###}, {visualPosition.y:0.###}, {visualPosition.z:0.###}) " +
                $"rot={meshRotation.eulerAngles} scale=({meshScale.x:0.####}, {meshScale.y:0.####}, {meshScale.z:0.####})");
            Log($"trigger size=({triggerSize.x:0.###}, {triggerSize.y:0.###}, {triggerSize.z:0.###}) center={triggerCenter}");
        }

        static long CountTriangles(Mesh mesh)
        {
            try
            {
                long indexCount = 0;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    indexCount += mesh.GetIndexCount(i);
                }

                return indexCount / 3;
            }
            catch (System.Exception)
            {
                return -1;
            }
        }

        static void RelinkDefinition()
        {
            Object definition = AssetDatabase.LoadAssetAtPath<Object>(DefinitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (definition == null || prefab == null)
            {
                throw new System.InvalidOperationException("could not relink node definition");
            }

            SerializedObject serialized = new(definition);
            serialized.FindProperty("visualPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("tintByBiome").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Log("SO_IronOreNode relinked to rebuilt prefab, tintByBiome off");
        }
    }
}
