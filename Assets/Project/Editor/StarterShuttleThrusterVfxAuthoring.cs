using System;
using Farion.Gameplay.Flight;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace Farion.Editor.Vfx
{
    public static class StarterShuttleThrusterVfxAuthoring
    {
        const string PrefabPath =
            "Assets/Project/Prefabs/Gameplay/Spacecraft/PF_PlayerStarterShuttle.prefab";
        const string CoreMaterialPath =
            "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_Core.mat";
        const string InnerMaterialPath =
            "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_InnerPlasma.mat";
        const string OuterMaterialPath =
            "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_OuterPlasma.mat";
        const string ShockMaterialPath =
            "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_ShockDiamonds.mat";
        const string DistortionMaterialPath =
            "Assets/Project/Art/Materials/VFX/Spacecraft/MAT_VFX_Thruster_Distortion.mat";

        [MenuItem("Farion/VFX/Rebuild Starter Shuttle Thrusters")]
        public static void Rebuild()
        {
            Material coreMaterial = LoadRequired<Material>(CoreMaterialPath);
            Material innerMaterial = LoadRequired<Material>(InnerMaterialPath);
            Material outerMaterial = LoadRequired<Material>(OuterMaterialPath);
            Material shockMaterial = LoadRequired<Material>(ShockMaterialPath);
            Material distortionMaterial = LoadRequired<Material>(DistortionMaterialPath);

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform vfxRoot = FindRequired(prefabRoot.transform, "VisualRoot/VFX");
                ConfigureNozzle(
                    FindRequired(vfxRoot, "THR_MainRear_L"),
                    expectedSideSign: -1f,
                    seedOffset: 1100u,
                    coreMaterial,
                    innerMaterial,
                    outerMaterial,
                    shockMaterial,
                    distortionMaterial);
                ConfigureNozzle(
                    FindRequired(vfxRoot, "THR_MainRear_R"),
                    expectedSideSign: 1f,
                    seedOffset: 2200u,
                    coreMaterial,
                    innerMaterial,
                    outerMaterial,
                    shockMaterial,
                    distortionMaterial);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt production starter-shuttle thrusters at {PrefabPath}.");
        }

        static void ConfigureNozzle(
            Transform nozzleRoot,
            float expectedSideSign,
            uint seedOffset,
            Material coreMaterial,
            Material innerMaterial,
            Material outerMaterial,
            Material shockMaterial,
            Material distortionMaterial)
        {
            SpacecraftThrusterNozzleVfx nozzle =
                nozzleRoot.GetComponent<SpacecraftThrusterNozzleVfx>();
            if (nozzle == null)
            {
                throw new InvalidOperationException(
                    $"{nozzleRoot.name} is missing {nameof(SpacecraftThrusterNozzleVfx)}.");
            }

            Transform coreRoot = ResetChild(nozzleRoot, "CoreGlow");
            Transform innerRoot = ResetChild(nozzleRoot, "InnerCone");
            Transform outerRoot = ResetChild(nozzleRoot, "OuterPlasma");
            Transform shockRoot = ResetChild(nozzleRoot, "ShockDiamonds");
            Transform distortionRoot = ResetChild(nozzleRoot, "Distortion");
            Transform smokeRoot = ResetChild(nozzleRoot, "VFX_Smoke");
            Transform sparksRoot = ResetChild(nozzleRoot, "VFX_Sparks");
            Transform lightRoot = ResetChild(nozzleRoot, "LT_Thruster");

            SpacecraftThrusterMeshLayer core = ConfigureLayer(
                coreRoot,
                SpacecraftThrusterMeshLayerKind.CoreGlow,
                coreMaterial,
                radius: 0.58f,
                minimumLength: 0.025f,
                maximumLength: 0.08f,
                idleVisibility: 0.08f,
                opacity: 1f,
                response: 22f);
            SpacecraftThrusterMeshLayer inner = ConfigureLayer(
                innerRoot,
                SpacecraftThrusterMeshLayerKind.InnerPlasma,
                innerMaterial,
                radius: 0.42f,
                minimumLength: 0.35f,
                maximumLength: 4.8f,
                idleVisibility: 0f,
                opacity: 0.92f,
                response: 18f);
            SpacecraftThrusterMeshLayer outer = ConfigureLayer(
                outerRoot,
                SpacecraftThrusterMeshLayerKind.OuterPlasma,
                outerMaterial,
                radius: 0.68f,
                minimumLength: 0.5f,
                maximumLength: 6.2f,
                idleVisibility: 0f,
                opacity: 0.42f,
                response: 13f);
            SpacecraftThrusterMeshLayer shock = ConfigureLayer(
                shockRoot,
                SpacecraftThrusterMeshLayerKind.ShockDiamonds,
                shockMaterial,
                radius: 0.5f,
                minimumLength: 0.8f,
                maximumLength: 5.6f,
                idleVisibility: 0f,
                opacity: 0.48f,
                response: 15f);
            SpacecraftThrusterMeshLayer distortion = ConfigureLayer(
                distortionRoot,
                SpacecraftThrusterMeshLayerKind.Distortion,
                distortionMaterial,
                radius: 0.88f,
                minimumLength: 0.9f,
                maximumLength: 6.8f,
                idleVisibility: 0f,
                opacity: 0.34f,
                response: 10f);

            ConfigureGraph(smokeRoot, seedOffset + 29u);
            ConfigureGraph(sparksRoot, seedOffset + 47u);

            SerializedObject serializedNozzle = new(nozzle);
            serializedNozzle.FindProperty("sideSign").floatValue = expectedSideSign;
            serializedNozzle.FindProperty("steeringRoot").objectReferenceValue = nozzleRoot;
            serializedNozzle.FindProperty("coreGlow").objectReferenceValue = core;
            serializedNozzle.FindProperty("innerPlasma").objectReferenceValue = inner;
            serializedNozzle.FindProperty("outerPlasma").objectReferenceValue = outer;
            serializedNozzle.FindProperty("shockDiamonds").objectReferenceValue = shock;
            serializedNozzle.FindProperty("distortion").objectReferenceValue = distortion;
            serializedNozzle.FindProperty("smokeGraph").objectReferenceValue =
                smokeRoot.GetComponent<VisualEffect>();
            serializedNozzle.FindProperty("sparksGraph").objectReferenceValue =
                sparksRoot.GetComponent<VisualEffect>();
            serializedNozzle.FindProperty("thrusterLight").objectReferenceValue =
                lightRoot.GetComponent<Light>();
            serializedNozzle.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(nozzle);
        }

        static SpacecraftThrusterMeshLayer ConfigureLayer(
            Transform layerRoot,
            SpacecraftThrusterMeshLayerKind kind,
            Material material,
            float radius,
            float minimumLength,
            float maximumLength,
            float idleVisibility,
            float opacity,
            float response)
        {
            SpacecraftThrusterMeshLayer layer =
                GetOrAdd<SpacecraftThrusterMeshLayer>(layerRoot.gameObject);
            MeshFilter meshFilter = layerRoot.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = layerRoot.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshRenderer == null)
            {
                throw new InvalidOperationException(
                    $"{layerRoot.name} could not create its required mesh components.");
            }

            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            layer.Configure(
                kind,
                material,
                radius,
                minimumLength,
                maximumLength,
                idleVisibility,
                opacity,
                response);

            EditorUtility.SetDirty(meshFilter);
            EditorUtility.SetDirty(meshRenderer);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static void ConfigureGraph(Transform graphRoot, uint seed)
        {
            VisualEffect effect = graphRoot.GetComponent<VisualEffect>();
            if (effect == null)
            {
                throw new InvalidOperationException(
                    $"{graphRoot.name} is missing {nameof(VisualEffect)}.");
            }

            graphRoot.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SerializedObject serializedEffect = new(effect);
            SerializedProperty startSeed = serializedEffect.FindProperty("m_StartSeed");
            if (startSeed != null)
            {
                startSeed.uintValue = seed;
            }

            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            effect.enabled = false;
            EditorUtility.SetDirty(effect);
        }

        static Transform ResetChild(Transform parent, string childName)
        {
            Transform child = FindRequired(parent, childName);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            EditorUtility.SetDirty(child);
            return child;
        }

        static T GetOrAdd<T>(GameObject target)
            where T : Component
        {
            return target.GetComponent<T>() ?? target.AddComponent<T>();
        }

        static Transform FindRequired(Transform parent, string relativePath)
        {
            Transform result = parent.Find(relativePath);
            if (result == null)
            {
                throw new InvalidOperationException(
                    $"Missing authored thruster path '{parent.name}/{relativePath}'.");
            }

            return result;
        }

        static T LoadRequired<T>(string path)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Missing required thruster asset at '{path}'.");
            }

            return asset;
        }
    }
}
