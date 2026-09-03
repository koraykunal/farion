using System.Collections.Generic;
using Farion.Gameplay.Character;
using Farion.Multiplayer.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Editor.Authoring
{
    static class FarionVisorEyesIntake
    {
        const string MaterialPath =
            "Assets/Project/Art/Materials/Characters/PlayerExplorer/MAT_PlayerExplorer_VisorEyes.mat";
        const string ShaderName = "Farion/Character/Visor Eyes";
        const string VisorMaterialName = "MAT_PlayerExplorer_HelmetGlass";
        const string HeadBoneName = "CC_Base_Head";
        const string EyesObjectName = "VisorEyes";
        const float SurfaceOffset = 0.0015f;
        const float WidthFraction = 0.7f;
        const float Aspect = 2f;

        [MenuItem("Farion/Character/Install Visor Eyes")]
        public static void Run()
        {
            Material material = EnsureMaterial();
            if (material == null)
            {
                return;
            }

            InstallOnCorePrefab(material);
            HideForOwner();
            AssetDatabase.SaveAssets();
        }

        static Material EnsureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader '{ShaderName}' not found.");
                return null;
            }

            material = new Material(shader) { name = "MAT_PlayerExplorer_VisorEyes" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        static void InstallOnCorePrefab(Material material)
        {
            string prefabPath = FarionAssetPaths.CoreExplorerPrefab;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponentInChildren<VisorEyes>(true) != null)
                {
                    return;
                }

                Transform head = FindChild(root.transform, HeadBoneName);
                if (head == null)
                {
                    Debug.LogError($"{prefabPath}: bone '{HeadBoneName}' not found.");
                    return;
                }

                if (!TryMeasureVisor(root, out Vector3 center, out float width))
                {
                    Debug.LogError(
                        $"{prefabPath}: no SkinnedMeshRenderer carries {VisorMaterialName}.");
                    return;
                }

                GameObject eyes = GameObject.CreatePrimitive(PrimitiveType.Quad);
                eyes.name = EyesObjectName;
                Object.DestroyImmediate(eyes.GetComponent<Collider>());

                Transform t = eyes.transform;
                t.SetParent(head, false);
                t.position = center + root.transform.forward * SurfaceOffset;
                t.rotation = root.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
                float quadWidth = width * WidthFraction;
                Vector3 lossy = head.lossyScale;
                t.localScale = new Vector3(
                    quadWidth / Mathf.Max(lossy.x, 1e-5f),
                    quadWidth / Aspect / Mathf.Max(lossy.y, 1e-5f),
                    1f);

                var renderer = eyes.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                eyes.AddComponent<VisorEyes>();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool TryMeasureVisor(GameObject root, out Vector3 center, out float width)
        {
            center = default;
            width = 0f;
            Transform rootTransform = root.transform;
            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] materials = smr.sharedMaterials;
                Mesh mesh = smr.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (int sub = 0; sub < materials.Length && sub < mesh.subMeshCount; sub++)
                {
                    if (materials[sub] == null || materials[sub].name != VisorMaterialName)
                    {
                        continue;
                    }

                    Vector3[] vertices = mesh.vertices;
                    var seen = new HashSet<int>();
                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minY = float.MaxValue, maxY = float.MinValue;
                    float maxZ = float.MinValue;
                    foreach (int index in mesh.GetTriangles(sub))
                    {
                        if (!seen.Add(index))
                        {
                            continue;
                        }

                        Vector3 local = rootTransform.InverseTransformPoint(
                            smr.transform.TransformPoint(vertices[index]));
                        minX = Mathf.Min(minX, local.x);
                        maxX = Mathf.Max(maxX, local.x);
                        minY = Mathf.Min(minY, local.y);
                        maxY = Mathf.Max(maxY, local.y);
                        maxZ = Mathf.Max(maxZ, local.z);
                    }

                    if (seen.Count == 0)
                    {
                        continue;
                    }

                    center = rootTransform.TransformPoint(
                        new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, maxZ));
                    width = (maxX - minX) * rootTransform.lossyScale.x;
                    return true;
                }
            }

            return false;
        }

        static void HideForOwner()
        {
            string prefabPath = FarionAssetPaths.NetworkExplorerPrefab;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var controller = root.GetComponent<NetworkExplorerController>();
                VisorEyes eyes = root.GetComponentInChildren<VisorEyes>(true);
                if (controller == null || eyes == null)
                {
                    Debug.LogError(
                        $"{prefabPath}: NetworkExplorerController or VisorEyes missing.");
                    return;
                }

                Renderer renderer = eyes.GetComponent<Renderer>();
                var serialized = new SerializedObject(controller);
                SerializedProperty list = serialized.FindProperty("ownerHiddenRenderers");
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer)
                    {
                        return;
                    }
                }

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
