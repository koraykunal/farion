using System;
using System.Collections.Generic;
using System.Linq;
using Farion.Gameplay.Presentation.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Farion.Editor.Authoring
{
    internal static class FarionPlayerLocomotionSetup
    {
        const string ClipFolder =
            "Assets/Project/Art/Animations/Characters/PlayerExplorer/Clips";
        const string ControllerPath =
            "Assets/Project/Art/Animations/Characters/PlayerExplorer/Controllers/AC_PlayerExplorer.controller";
        const string PrefabPath =
            "Assets/Project/Prefabs/Gameplay/Character/PF_PlayerExplorerCore.prefab";

        const float MoveEnterSpeed = 0.7f;
        const float MoveExitSpeed = 0.3f;

        [MenuItem("Farion/Character/Rebuild Locomotion")]
        internal static void Run()
        {
            CalibrateClipSpeeds();
            RebuildController();
            AssetDatabase.SaveAssets();
            Debug.Log("[FarionPlayerLocomotionSetup] locomotion rebuilt.");
        }

        static void CalibrateClipSpeeds()
        {
            var measured = new Dictionary<string, float>
            {
                ["walkClipSpeed"] = Measure("AN_PlayerExplorer_Walk_Forward_A"),
                ["jogClipSpeed"] = Measure("AN_PlayerExplorer_Jog_Forward_A"),
                ["runClipSpeed"] = Measure("AN_PlayerExplorer_Run_Forward_A")
            };

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var driver = contents.GetComponentInChildren<PlayerExplorerAnimator>(true);
                if (driver == null)
                {
                    throw new InvalidOperationException(
                        $"{PrefabPath} has no PlayerExplorerAnimator.");
                }

                var serialized = new SerializedObject(driver);
                bool dirty = false;
                foreach ((string field, float speed) in measured)
                {
                    SerializedProperty property = serialized.FindProperty(field);
                    if (speed <= 0.05f)
                    {
                        Debug.LogWarning(
                            $"[FarionPlayerLocomotionSetup] {field}: clip carries no root motion, " +
                            $"keeping authored {property.floatValue:F2} m/s.");
                        continue;
                    }

                    Debug.Log(
                        $"[FarionPlayerLocomotionSetup] {field}: measured {speed:F2} m/s " +
                        $"(was {property.floatValue:F2}).");
                    property.floatValue = speed;
                    dirty = true;
                }

                if (dirty)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static float Measure(string file)
        {
            string path = $"{ClipFolder}/{file}.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return 0f;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0)
            {
                return 0f;
            }

            bool rotation = clips[0].lockRootRotation;
            bool height = clips[0].lockRootHeightY;
            bool planar = clips[0].lockRootPositionXZ;
            clips[0].lockRootRotation = false;
            clips[0].lockRootHeightY = false;
            clips[0].lockRootPositionXZ = false;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            float speed = LoadClip(path, clips[0].name) is { } measured
                ? new Vector2(measured.averageSpeed.x, measured.averageSpeed.z).magnitude
                : 0f;

            clips = importer.clipAnimations;
            clips[0].lockRootRotation = rotation;
            clips[0].lockRootHeightY = height;
            clips[0].lockRootPositionXZ = planar;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            return speed;
        }

        static AnimationClip LoadClip(string path, string name) =>
            AssetDatabase
                .LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == name);

        static readonly Dictionary<string, AnimationClip> clipCache = new();

        static AnimationClip Clip(string name)
        {
            if (clipCache.Count == 0)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ClipFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (asset is AnimationClip candidate &&
                            !candidate.name.StartsWith("__preview__", StringComparison.Ordinal))
                        {
                            clipCache[candidate.name] = candidate;
                        }
                    }
                }
            }

            if (!clipCache.TryGetValue(name, out AnimationClip clip))
            {
                throw new InvalidOperationException(
                    $"Clip '{name}' not found under {ClipFolder}.");
            }

            return clip;
        }

        static void RebuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException($"{ControllerPath} is missing.");
            }

            while (controller.layers.Length > 0)
            {
                controller.RemoveLayer(0);
            }

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (asset != controller && asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset, true);
                }
            }

            foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            {
                controller.RemoveParameter(parameter);
            }

            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("StrideScale", AnimatorControllerParameterType.Float);
            controller.AddParameter("PlanarSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("SwimSpeed01", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Submerged", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

            controller.AddLayer("Base Layer");
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            machine.anyStatePosition = new Vector3(-280f, -40f, 0f);
            machine.entryPosition = new Vector3(-280f, 120f, 0f);
            machine.exitPosition = new Vector3(-280f, 280f, 0f);

            AnimatorState idle = AddState(machine, "Idle", new Vector3(40f, 120f, 0f), Clip("Idle_Breathing"));
            AnimatorState moveStart = AddState(machine, "MoveStart", new Vector3(300f, 40f, 0f), Clip("Walk_Start"));
            AnimatorState move = AddState(machine, "Move", new Vector3(560f, 120f, 0f), BuildMoveTree(controller));
            AnimatorState moveStop = AddState(machine, "MoveStop", new Vector3(300f, 200f, 0f), Clip("Walk_Stop"));
            AnimatorState jump = AddState(machine, "JumpStart", new Vector3(40f, -160f, 0f), Clip("Jump_Start"));
            AnimatorState airborne = AddState(machine, "Airborne", new Vector3(300f, -240f, 0f), Clip("Fall_Loop"));
            AnimatorState land = AddState(machine, "Land", new Vector3(560f, -160f, 0f), Clip("Land"));
            AnimatorState swim = AddState(machine, "Swim", new Vector3(40f, 380f, 0f), BuildSwimTree(controller));

            machine.defaultState = idle;
            move.speedParameter = "StrideScale";
            move.speedParameterActive = true;
            moveStart.speedParameter = "StrideScale";
            moveStart.speedParameterActive = true;
            moveStop.speed = 1.15f;

            AnimatorStateTransition toSwim = machine.AddAnyStateTransition(swim);
            Shape(toSwim, 0.25f);
            toSwim.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Submerged");

            AnimatorStateTransition toJump = machine.AddAnyStateTransition(jump);
            Shape(toJump, 0.08f);
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            toJump.AddCondition(AnimatorConditionMode.Greater, 0.5f, "VerticalSpeed");
            toJump.AddCondition(AnimatorConditionMode.Less, 0.5f, "Submerged");

            AnimatorStateTransition toAirborne = machine.AddAnyStateTransition(airborne);
            Shape(toAirborne, 0.15f);
            toAirborne.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            toAirborne.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalSpeed");
            toAirborne.AddCondition(AnimatorConditionMode.Less, 0.5f, "Submerged");

            Moving(Shape(idle.AddTransition(moveStart), 0.1f), true);
            Moving(Shape(move.AddTransition(moveStop), 0.1f), false);
            Moving(Shape(moveStart.AddTransition(idle), 0.15f), false);
            Moving(Shape(moveStop.AddTransition(move), 0.1f), true);

            Shape(moveStart.AddTransition(move), 0.12f, ExitAfter(moveStart, 0.25f, 0.1f, 0.75f));
            Shape(moveStop.AddTransition(idle), 0.15f, ExitAfter(moveStop, 0.55f, 0.3f, 0.9f));

            Grounded(Shape(jump.AddTransition(land), 0.08f), true);
            Grounded(Shape(airborne.AddTransition(land), 0.1f), true);

            Moving(Shape(land.AddTransition(move), 0.12f, ExitAfter(land, 0.15f, 0.1f, 0.5f)), true);
            Shape(land.AddTransition(idle), 0.15f, ExitAfter(land, 0.35f, 0.3f, 0.85f));

            AnimatorStateTransition swimToIdle = Shape(swim.AddTransition(idle), 0.25f);
            swimToIdle.AddCondition(AnimatorConditionMode.Less, 0.5f, "Submerged");
            swimToIdle.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            AnimatorStateTransition swimToAir = Shape(swim.AddTransition(airborne), 0.25f);
            swimToAir.AddCondition(AnimatorConditionMode.Less, 0.5f, "Submerged");
            swimToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");

            EditorUtility.SetDirty(controller);
        }

        static AnimatorState AddState(
            AnimatorStateMachine machine,
            string name,
            Vector3 position,
            Motion motion)
        {
            AnimatorState state = machine.AddState(name, position);
            state.motion = motion;
            return state;
        }

        static BlendTree BuildMoveTree(AnimatorController controller)
        {
            var tree = new BlendTree
            {
                name = "Move",
                hideFlags = HideFlags.HideInHierarchy,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.children = new[]
            {
                Child(Clip("Idle_Breathing"), 0f, 0f),
                Child(Clip("Walk_Forward"), 0f, 1f),
                Child(Clip("Jog_Forward"), 0f, 2f),
                Child(Clip("Run_Forward"), 0f, 3f),
                Child(Clip("Walk_Backward"), 0f, -1f),
                Child(Clip("Strafe_Left"), -1f, 0f),
                Child(Clip("Strafe_Right"), 1f, 0f)
            };
            return tree;
        }

        static BlendTree BuildSwimTree(AnimatorController controller)
        {
            var tree = new BlendTree
            {
                name = "Swim",
                hideFlags = HideFlags.HideInHierarchy,
                blendType = BlendTreeType.Simple1D,
                blendParameter = "SwimSpeed01",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.children = new[]
            {
                new ChildMotion
                {
                    motion = Clip("Swim_Idle"),
                    threshold = 0f,
                    timeScale = 1f,
                    directBlendParameter = "StrideScale"
                },
                new ChildMotion
                {
                    motion = Clip("Swim_Forward"),
                    threshold = 1f,
                    timeScale = 1f,
                    directBlendParameter = "StrideScale"
                }
            };
            return tree;
        }

        static ChildMotion Child(AnimationClip clip, float x, float y) =>
            new()
            {
                motion = clip,
                position = new Vector2(x, y),
                timeScale = 1f,
                directBlendParameter = "StrideScale"
            };

        static float ExitAfter(AnimatorState state, float seconds, float minimum, float maximum)
        {
            float length = state.motion != null ? state.motion.averageDuration : 0f;
            return length > 0.01f
                ? Mathf.Clamp(seconds / length, minimum, maximum)
                : minimum;
        }

        static AnimatorStateTransition Shape(
            AnimatorStateTransition transition,
            float duration,
            float exitTime = -1f)
        {
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.offset = 0f;
            transition.canTransitionToSelf = false;
            transition.hasExitTime = exitTime >= 0f;
            if (exitTime >= 0f)
            {
                transition.exitTime = exitTime;
            }

            return transition;
        }

        static void Moving(AnimatorStateTransition transition, bool moving) =>
            transition.AddCondition(
                moving ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less,
                moving ? MoveEnterSpeed : MoveExitSpeed,
                "PlanarSpeed");

        static void Grounded(AnimatorStateTransition transition, bool grounded) =>
            transition.AddCondition(
                grounded ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                "Grounded");
    }
}
