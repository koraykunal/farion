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
        const string ModelPath =
            "Assets/Project/Art/Models/Characters/PlayerExplorer/SM_PlayerExplorer_A.fbx";

        const float MoveEnterSpeed = 0.7f;
        const float MoveExitSpeed = 0.3f;
        const float MinimumClipTimeScale = 0.6f;
        const float MaximumClipTimeScale = 1.6f;
        const float SampleRate = 60f;
        const float Diagonal = 0.70710678f;

        static readonly string[] Directions =
        {
            "Forward", "ForwardRight", "Right", "BackwardRight",
            "Backward", "BackwardLeft", "Left", "ForwardLeft"
        };

        static readonly Vector2[] DirectionVectors =
        {
            new(0f, 1f), new(Diagonal, Diagonal), new(1f, 0f), new(Diagonal, -Diagonal),
            new(0f, -1f), new(-Diagonal, -Diagonal), new(-1f, 0f), new(-Diagonal, Diagonal)
        };

        static readonly Dictionary<string, AnimationClip> clipCache = new();
        static readonly Dictionary<string, float> speedCache = new();

        const float JumpStartLaunchOffset = 0.575f;
        const float JumpStartExitTime = 0.9f;

        [MenuItem("Farion/Character/Rebuild Locomotion")]
        internal static void Run()
        {
            clipCache.Clear();
            speedCache.Clear();
            MeasureClipSpeeds();
            CalibrateDriver();
            RebuildController();
            AssetDatabase.SaveAssets();
            Debug.Log("[FarionPlayerLocomotionSetup] locomotion rebuilt.");
        }

        static void MeasureClipSpeeds()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                throw new InvalidOperationException($"{ModelPath} is missing.");
            }

            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                AnimationMode.StartAnimationMode();
                try
                {
                    foreach (string direction in Directions)
                    {
                        Measure(instance, hips, left, right, "Walk_" + direction);
                        Measure(instance, hips, left, right, "Jog_" + direction);
                    }

                    Measure(instance, hips, left, right, "Run_Forward");
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        static void Measure(
            GameObject instance,
            Transform hips,
            Transform left,
            Transform right,
            string name)
        {
            AnimationClip clip = Clip(name);
            int samples = Mathf.Max(8, Mathf.RoundToInt(clip.length * SampleRate));
            float step = clip.length / samples;
            var stanceSpeeds = new List<float>(samples);
            Vector3 previousLeft = Vector3.zero;
            Vector3 previousRight = Vector3.zero;
            for (int i = 0; i <= samples; i++)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(instance, clip, i * step);
                AnimationMode.EndSampling();
                Vector3 currentLeft = left.position - hips.position;
                Vector3 currentRight = right.position - hips.position;
                if (i > 0)
                {
                    bool leftPlanted = left.position.y <= right.position.y;
                    Vector3 delta = leftPlanted
                        ? currentLeft - previousLeft
                        : currentRight - previousRight;
                    delta.y = 0f;
                    stanceSpeeds.Add(delta.magnitude / step);
                }

                previousLeft = currentLeft;
                previousRight = currentRight;
            }

            stanceSpeeds.Sort();
            float speed = stanceSpeeds[stanceSpeeds.Count / 2];
            speedCache[name] = speed;
            Debug.Log($"[FarionPlayerLocomotionSetup] {name}: {speed:F2} m/s over {clip.length:F2} s.");
        }

        static void CalibrateDriver()
        {
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
                serialized.FindProperty("walkClipSpeed").floatValue = speedCache["Walk_Forward"];
                serialized.FindProperty("jogClipSpeed").floatValue = speedCache["Jog_Forward"];
                serialized.FindProperty("runClipSpeed").floatValue = speedCache["Run_Forward"];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static AnimationClip Clip(string name)
        {
            if (clipCache.Count == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder })
                    .Concat(AssetDatabase.FindAssets("t:Model", new[] { ClipFolder }))
                    .Distinct()
                    .ToArray();
                foreach (string guid in guids)
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

            AnimatorState idle = AddState(machine, "Idle", new Vector3(40f, 120f, 0f), Clip("Idle"));
            AnimatorState move = AddState(machine, "Move", new Vector3(560f, 120f, 0f), BuildMoveTree(controller));
            AnimatorState moveStop = AddState(machine, "MoveStop", new Vector3(300f, 200f, 0f), Clip("Walk_Stop"));
            AnimatorState jump = AddState(machine, "JumpStart", new Vector3(40f, -160f, 0f), Clip("Jump_Start"));
            AnimatorState airborne = AddState(machine, "Airborne", new Vector3(300f, -240f, 0f), Clip("Jump_Loop"));
            AnimatorState land = AddState(machine, "Land", new Vector3(560f, -160f, 0f), Clip("Jump_Land"));
            AnimatorState swim = AddState(machine, "Swim", new Vector3(40f, 380f, 0f), BuildSwimTree(controller));

            machine.defaultState = idle;
            move.speedParameter = "StrideScale";
            move.speedParameterActive = true;
            moveStop.speed = 1.15f;

            AnimatorStateTransition toSwim = machine.AddAnyStateTransition(swim);
            Shape(toSwim, 0.25f);
            toSwim.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Submerged");

            foreach (AnimatorState groundState in new[] { idle, move, moveStop, land })
            {
                AnimatorStateTransition toJump = Shape(groundState.AddTransition(jump), 0.05f);
                toJump.offset = JumpStartLaunchOffset;
                toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                toJump.AddCondition(AnimatorConditionMode.Greater, 0.5f, "VerticalSpeed");
                toJump.AddCondition(AnimatorConditionMode.Less, 0.5f, "Submerged");

                AnimatorStateTransition toFall = Shape(groundState.AddTransition(airborne), 0.2f);
                toFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                toFall.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalSpeed");
                toFall.AddCondition(AnimatorConditionMode.Less, 0.5f, "Submerged");
            }

            Moving(Shape(idle.AddTransition(move), 0.12f), true);
            Moving(Shape(move.AddTransition(moveStop), 0.1f), false);
            Moving(Shape(moveStop.AddTransition(move), 0.1f), true);

            Shape(moveStop.AddTransition(idle), 0.15f, ExitAfter(moveStop, 0.55f, 0.3f, 0.9f));

            Shape(jump.AddTransition(airborne), 0.2f, JumpStartExitTime);
            Shape(jump.AddTransition(airborne), 0.2f).AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalSpeed");
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

            var children = new List<ChildMotion> { Child("Idle", Vector2.zero, 1f) };
            AddRing(children, "Walk_", 1f);
            AddRing(children, "Jog_", 2f);
            children.Add(Child("Run_Forward", new Vector2(0f, 3f), 1f));
            tree.children = children.ToArray();
            return tree;
        }

        static void AddRing(List<ChildMotion> children, string prefix, float radius)
        {
            float ringSpeed = speedCache[prefix + "Forward"];
            for (int i = 0; i < Directions.Length; i++)
            {
                string name = prefix + Directions[i];
                float timeScale = Mathf.Clamp(
                    ringSpeed / Mathf.Max(0.01f, speedCache[name]),
                    MinimumClipTimeScale,
                    MaximumClipTimeScale);
                children.Add(Child(name, DirectionVectors[i] * radius, timeScale));
            }
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

        static ChildMotion Child(string clip, Vector2 position, float timeScale) =>
            new()
            {
                motion = Clip(clip),
                position = position,
                timeScale = timeScale,
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
