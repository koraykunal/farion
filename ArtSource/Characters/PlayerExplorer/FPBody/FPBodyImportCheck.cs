// Run in an isolated Unity project containing the candidate FBXs and source avatar.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class FPBodyImportCheck
{
    const string Root = "Assets/Project/Art/Models/Characters/PlayerExplorer/FPBody/";
    const string Model = Root + "SM_PlayerExplorer_FPBody_A.fbx";

    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh();
            var source = (ModelImporter)AssetImporter.GetAtPath("Assets/Source.fbx");
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var description = importer.humanDescription;
            description.human = source.humanDescription.human;
            importer.humanDescription = description;
            importer.importAnimation = false;
            importer.preserveHierarchy = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 6;
            importer.minBoneWeight = 0.00001f;
            foreach (var pair in new[] {
                ("FPBody_Suit", "MAT_PlayerExplorer_Suit_White"),
                ("FPBody_HardSurface", "MAT_PlayerExplorer_HardSurface_White"),
                ("FPBody_Belt", "MAT_PlayerExplorer_Belt_White") })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + pair.Item2 + ".mat");
                Require(material != null, "Missing material " + pair.Item2);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Item1), material);
            }
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().Single();
            Require(avatar.isValid && avatar.isHuman, "Invalid Humanoid avatar");
            var controllerPath = Root + "AC_PlayerExplorer_FPBodyPreview.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (string name in new[] { "AN_FPBody_Relaxed", "AN_FPBody_ToolReady", "AN_FPBody_TwoHandReady" })
            {
                string path = Root + name + ".fbx";
                var clipImporter = (ModelImporter)AssetImporter.GetAtPath(path);
                clipImporter.animationType = ModelImporterAnimationType.Human;
                clipImporter.preserveHierarchy = true;
                clipImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                clipImporter.sourceAvatar = avatar;
                clipImporter.animationCompression = ModelImporterAnimationCompression.Off;
                clipImporter.importCameras = false;
                clipImporter.importLights = false;
                clipImporter.SaveAndReimport();
                var clips = clipImporter.defaultClipAnimations;
                Require(clips.Length == 1, name + " must contain exactly one take");
                clips[0].name = name;
                clips[0].loopTime = true;
                clipImporter.clipAnimations = clips;
                clipImporter.SaveAndReimport();
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Single(c => !c.name.StartsWith("__preview__"));
                Require(clip.humanMotion && Math.Abs(clip.length - 2f) < 0.04f, name + " duration or Humanoid conversion failed");
                controller.AddMotion(clip);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            instance.name = "PF_PlayerExplorerFPBody_Visual";
            var animator = instance.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            Require(renderers.Length == 3, "Expected suit, belt and tube renderers");
            int triangleCount = renderers.Sum(r => r.sharedMesh.triangles.Length / 3);
            Require(triangleCount == 108698,
                "Unity triangle count: " + triangleCount + " (expected 108698)");
            foreach (var renderer in renderers)
            {
                Require(renderer.sharedMesh != null && renderer.bones.All(b => b != null), "Invalid skin binding");
                Require(renderer.sharedMaterials.All(m => m != null), "Missing material remap");
                renderer.updateWhenOffscreen = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var mask = new AvatarMask();
            mask.name = "AM_PlayerExplorer_FPArms";
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
                    i == (int)AvatarMaskBodyPart.LeftArm || i == (int)AvatarMaskBodyPart.RightArm ||
                    i == (int)AvatarMaskBodyPart.LeftFingers || i == (int)AvatarMaskBodyPart.RightFingers);
            mask.AddTransformPath(instance.transform);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                mask.SetTransformActive(i, path.Contains("CC_Base_L_Clavicle") || path.Contains("CC_Base_R_Clavicle"));
            }
            AssetDatabase.CreateAsset(mask, Root + "AM_PlayerExplorer_FPArms.mask");
            foreach (var name in new[] { "AN_FPBody_Relaxed", "AN_FPBody_ToolReady", "AN_FPBody_TwoHandReady" })
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Root+name+".fbx").OfType<AnimationClip>()
                    .Single(c => !c.name.StartsWith("__preview__"));
                clip.SampleAnimation(instance, 0f);
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                clip.SampleAnimation(instance, clip.length);
                Require(Vector3.Distance(hand, animator.GetBoneTransform(HumanBodyBones.RightHand).position) < 0.001f,
                    name + " hand loop is discontinuous");
                foreach (var renderer in renderers)
                {
                    var baked = new Mesh();
                    renderer.BakeMesh(baked);
                    Require(baked.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), "Non-finite skinned vertex");
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            }
            var relaxed = AssetDatabase.LoadAllAssetsAtPath(Root+"AN_FPBody_Relaxed.fbx").OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__"));
            relaxed.SampleAnimation(instance, 0);
            PrefabUtility.SaveAsPrefabAsset(instance, Root + "PF_PlayerExplorerFPBody_Visual.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            File.WriteAllText("FPBody-result.txt", "PASS: Humanoid avatar; 3 full 2-second looping clips; 3 skinned renderers; material remaps; finite baked vertices; matching hand loop endpoints. Runtime possession, locomotion, equipment and multiplayer not tested.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText("FPBody-result.txt", "FAIL: " + exception);
            EditorApplication.Exit(1);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
