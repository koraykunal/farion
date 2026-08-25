using System;
using System.Collections.Generic;
using System.Reflection;
using Farion.Gameplay.Presentation.Flight;
using UnityEditor;
using UnityEngine;

namespace Farion.Editor.Authoring
{
    static class FarionThrusterVfxUpgrade
    {
        const string ShuttlePrefabPath =
            "Assets/Project/Prefabs/Gameplay/Spacecraft/PF_PlayerStarterShuttle.prefab";

        static readonly Dictionary<string, LayerTuning> LayerTunings = new()
        {
            ["CoreGlow"] = new LayerTuning(0.35f, 0f, 0f),
            ["InnerCone"] = new LayerTuning(1.2f, 0.55f, 0.6f),
            ["OuterPlasma"] = new LayerTuning(1.8f, 0.85f, 1.1f),
            ["ShockDiamonds"] = new LayerTuning(1.2f, 0.4f, 0.35f),
            ["Distortion"] = new LayerTuning(1.8f, 0.85f, 1.1f)
        };

        public static void Run()
        {
            UpgradeMaterials();
            UpgradeShuttlePrefab();
            UpgradeVfxAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FarionThrusterVfxUpgrade] completed");
        }

        static void UpgradeMaterials()
        {
            ApplyPlasmaTint(
                "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_Core.mat",
                new Color(1.5f, 1.54f, 1.6f),
                new Color(0.72f, 0.6f, 0.92f),
                3.4f);
            ApplyPlasmaTint(
                "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_InnerPlasma.mat",
                new Color(1.45f, 1.5f, 1.6f),
                new Color(0.4f, 0.24f, 0.6f),
                2.1f);
            ApplyPlasmaTint(
                "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_OuterPlasma.mat",
                new Color(1.18f, 1.28f, 1.45f),
                new Color(0.32f, 0.2f, 0.62f),
                1.5f);
            ApplyPlasmaTint(
                "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_ShockDiamonds.mat",
                new Color(1.5f, 1.55f, 1.62f),
                new Color(0.5f, 0.3f, 0.7f),
                2.4f);

            Material distortion = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_Distortion.mat");
            if (distortion == null)
            {
                Debug.LogError("[FarionThrusterVfxUpgrade] distortion material missing");
                return;
            }

            distortion.SetFloat("_Opacity", 0.72f);
            distortion.SetFloat("_DistortionStrength", 0.021f);
            distortion.SetFloat("_DistortionReferenceDistance", 18f);
            EditorUtility.SetDirty(distortion);
        }

        static void ApplyPlasmaTint(string path, Color throat, Color tail, float coolingRate)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Debug.LogError($"[FarionThrusterVfxUpgrade] missing material {path}");
                return;
            }

            material.SetColor("_ThroatTint", throat);
            material.SetColor("_TailTint", tail);
            material.SetFloat("_CoolingRate", coolingRate);
            RemoveSavedColor(material, "_DamageColor");
            RemoveSavedFloat(material, "_Damage");
            EditorUtility.SetDirty(material);
        }

        static void RemoveSavedColor(Material material, string propertyName)
        {
            RemoveSavedProperty(material, "m_SavedProperties.m_Colors", propertyName);
        }

        static void RemoveSavedFloat(Material material, string propertyName)
        {
            RemoveSavedProperty(material, "m_SavedProperties.m_Floats", propertyName);
        }

        static void RemoveSavedProperty(Material material, string arrayPath, string propertyName)
        {
            SerializedObject serialized = new(material);
            SerializedProperty array = serialized.FindProperty(arrayPath);
            if (array == null || !array.isArray)
            {
                return;
            }

            for (int i = array.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty key = array
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("first");
                if (key != null && key.stringValue == propertyName)
                {
                    array.DeleteArrayElementAtIndex(i);
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void UpgradeShuttlePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ShuttlePrefabPath);
            if (root == null)
            {
                Debug.LogError("[FarionThrusterVfxUpgrade] shuttle prefab missing");
                return;
            }

            try
            {
                SymmetriseNozzles(root);
                ApplyLayerTunings(root);
                PrefabUtility.SaveAsPrefabAsset(root, ShuttlePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SymmetriseNozzles(GameObject root)
        {
            Transform left = FindDeep(root.transform, "THR_MainRear_L");
            Transform right = FindDeep(root.transform, "THR_MainRear_R");
            if (left == null || right == null)
            {
                Debug.LogError("[FarionThrusterVfxUpgrade] nozzle roots missing");
                return;
            }

            Vector3 leftPosition = left.localPosition;
            Vector3 rightPosition = right.localPosition;
            float x = (leftPosition.x + rightPosition.x) * 0.5f;
            float y = (leftPosition.y + rightPosition.y) * 0.5f;
            float halfSpan = (Mathf.Abs(leftPosition.z) + Mathf.Abs(rightPosition.z)) * 0.5f;

            left.localPosition = new Vector3(x, y, Mathf.Sign(leftPosition.z) * halfSpan);
            right.localPosition = new Vector3(x, y, Mathf.Sign(rightPosition.z) * halfSpan);
            Debug.Log(
                $"[FarionThrusterVfxUpgrade] nozzles symmetrised to x={x:F3} y={y:F3} span={halfSpan:F3}");
        }

        static void ApplyLayerTunings(GameObject root)
        {
            SpacecraftThrusterMeshLayer[] layers =
                root.GetComponentsInChildren<SpacecraftThrusterMeshLayer>(true);
            if (layers == null || layers.Length == 0)
            {
                Debug.LogError("[FarionThrusterVfxUpgrade] no thruster mesh layers found");
                return;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                SpacecraftThrusterMeshLayer layer = layers[i];
                if (!LayerTunings.TryGetValue(layer.name, out LayerTuning tuning))
                {
                    Debug.LogWarning($"[FarionThrusterVfxUpgrade] unknown layer name {layer.name}");
                    continue;
                }

                SerializedObject serialized = new(layer);
                SetFloat(serialized, "softFadeDistance", tuning.SoftFadeDistance);
                SetFloat(serialized, "plumeBendGain", tuning.PlumeBendGain);
                SetFloat(serialized, "groundSplashGain", tuning.GroundSplashGain);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log($"[FarionThrusterVfxUpgrade] tuned {layers.Length} mesh layers");
        }

        static void SetFloat(SerializedObject serialized, string path, float value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
            {
                Debug.LogError($"[FarionThrusterVfxUpgrade] missing serialized field {path}");
                return;
            }

            property.floatValue = value;
        }

        static void UpgradeVfxAssets()
        {
            UpgradeVfxAsset(
                "Assets/Project/Art/VFX/Spacecraft/Thrusters/VFX_Thruster_AtmosphereSmoke.vfx",
                192,
                1.4f);
            UpgradeVfxAsset(
                "Assets/Project/Art/VFX/Spacecraft/Thrusters/VFX_Thruster_Sparks.vfx",
                128,
                0.5f);
        }

        static void UpgradeVfxAsset(string path, int capacity, float softParticleFadeDistance)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"[FarionThrusterVfxUpgrade] missing vfx asset {path}");
                return;
            }

            Debug.Log($"[FarionThrusterVfxUpgrade] {path} exposes {assets.Length} sub-assets");
            bool changed = false;
            UnityEngine.Object graph = null;
            for (int i = 0; i < assets.Length; i++)
            {
                UnityEngine.Object asset = assets[i];
                if (asset == null)
                {
                    continue;
                }

                string typeName = asset.GetType().Name;
                if (typeName == "VFXGraph")
                {
                    graph = asset;
                }

                SerializedObject serialized = new(asset);
                SerializedProperty capacityProperty = serialized.FindProperty("capacity");
                if (capacityProperty != null)
                {
                    Debug.Log(
                        $"[FarionThrusterVfxUpgrade] {typeName}.capacity is {capacityProperty.propertyType}");
                    if (capacityProperty.propertyType == SerializedPropertyType.Integer)
                    {
                        capacityProperty.intValue = capacity;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }

                if (TrySetSoftParticle(asset, softParticleFadeDistance))
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                Debug.LogWarning($"[FarionThrusterVfxUpgrade] nothing changed in {path}");
                LogSubAssetTypes(assets);
                return;
            }

            TryRecompile(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[FarionThrusterVfxUpgrade] upgraded {path}");
        }

        static void LogSubAssetTypes(UnityEngine.Object[] assets)
        {
            HashSet<string> names = new();
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] != null)
                {
                    names.Add(assets[i].GetType().FullName);
                }
            }

            Debug.Log($"[FarionThrusterVfxUpgrade] sub-asset types: {string.Join(", ", names)}");
        }

        static bool TrySetSoftParticle(UnityEngine.Object asset, float fadeDistance)
        {
            SerializedObject serialized = new(asset);
            SerializedProperty useSoftParticle = serialized.FindProperty("useSoftParticle");
            if (useSoftParticle == null || useSoftParticle.propertyType != SerializedPropertyType.Boolean)
            {
                return false;
            }

            if (useSoftParticle.boolValue)
            {
                return false;
            }

            MethodInfo setSetting = asset.GetType().GetMethod(
                "SetSettingValue",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(string), typeof(object) },
                modifiers: null);
            if (setSetting == null)
            {
                Debug.LogWarning(
                    "[FarionThrusterVfxUpgrade] SetSettingValue unavailable; enable soft particles in the VFX Graph editor");
                return false;
            }

            setSetting.Invoke(asset, new object[] { "useSoftParticle", true });
            EditorUtility.SetDirty(asset);
            TrySetSoftParticleDistance(asset, fadeDistance);
            return true;
        }

        static void TrySetSoftParticleDistance(UnityEngine.Object asset, float fadeDistance)
        {
            PropertyInfo inputSlots = asset.GetType().GetProperty(
                "inputSlots",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (inputSlots?.GetValue(asset) is not System.Collections.IEnumerable slots)
            {
                return;
            }

            foreach (object slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                Type slotType = slot.GetType();
                object slotName = slotType
                    .GetProperty("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(slot);
                if (slotName is not string name ||
                    !name.Contains("oft", StringComparison.Ordinal))
                {
                    continue;
                }

                PropertyInfo valueProperty = slotType.GetProperty(
                    "value",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                valueProperty?.SetValue(slot, fadeDistance);
            }
        }

        static void TryRecompile(UnityEngine.Object graph)
        {
            if (graph == null)
            {
                return;
            }

            Type type = graph.GetType();
            type.GetMethod(
                    "SetExpressionGraphDirty",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null)
                ?.Invoke(graph, null);
            type.GetMethod(
                    "RecompileIfNeeded",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null)
                ?.Invoke(graph, null);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        readonly struct LayerTuning
        {
            public LayerTuning(float softFadeDistance, float plumeBendGain, float groundSplashGain)
            {
                SoftFadeDistance = softFadeDistance;
                PlumeBendGain = plumeBendGain;
                GroundSplashGain = groundSplashGain;
            }

            public float SoftFadeDistance { get; }
            public float PlumeBendGain { get; }
            public float GroundSplashGain { get; }
        }
    }
}
