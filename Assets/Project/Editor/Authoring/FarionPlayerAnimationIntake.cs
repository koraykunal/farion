using System.Collections.Generic;
using System.Text;
using Farion.Gameplay.Character;
using Farion.Gameplay.Presentation.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Farion.Editor.Authoring
{
    static class FarionPlayerAnimationIntake
    {
        const string ClipFolder =
            "Assets/Project/Art/Animations/Characters/PlayerExplorer/Clips";
        const string ControllerPath =
            "Assets/Project/Art/Animations/Characters/PlayerExplorer/Controllers/AC_PlayerExplorer.controller";
        const string ModelPath =
            "Assets/Project/Art/Models/Characters/PlayerExplorer/SM_PlayerExplorer_A.fbx";
        const string CorePrefabPath =
            "Assets/Project/Prefabs/Gameplay/Character/PF_PlayerExplorerCore.prefab";

        const float WalkRingSpeed = 1.5f;
        const float RunRingSpeed = 3.6f;
        const float ForwardClipSpeed = 1.5f;
        const float BackwardClipSpeed = 1.35f;
        const float StrafeClipSpeed = 2.6f;
        const float RunForwardClipSpeed = 3.6f;
        const float MaximumClipTimeScale = 2f;

        const float SubmergedThreshold = 0.5f;
        const float JumpRiseSpeed = 1f;
        const float HardLandingSpeed = -6f;
        const float FallEntrySpeed = -7f;

        static readonly StringBuilder Report = new();

        readonly struct ClipSource
        {
            public ClipSource(string asset, string clip, bool loop, string mirrored = null)
            {
                Asset = asset;
                Clip = clip;
                Loop = loop;
                Mirrored = mirrored;
            }

            public string Asset { get; }
            public string Clip { get; }
            public bool Loop { get; }
            public string Mirrored { get; }
        }

        static readonly ClipSource[] Sources =
        {
            new("AN_PlayerExplorer_Idle_Breathing_A", "Idle_Breathing", true),
            new("AN_PlayerExplorer_Walk_Forward_A", "Walk_Forward", true),
            new("AN_PlayerExplorer_Walk_Backward_A", "Walk_Backward", true),
            new("AN_PlayerExplorer_Strafe_Left_A", "Strafe_Left", true, "Strafe_Right"),
            new("AN_PlayerExplorer_Run_Forward_A", "Run_Forward", true),
            new("AN_PlayerExplorer_Fall_Loop_A", "Fall_Loop", true),
            new("AN_PlayerExplorer_Jump_A", "Jump", false),
            new("AN_PlayerExplorer_Jump_Down_A", "Jump_Down", false),
            new("AN_PlayerExplorer_Land_Hard_A", "Land_Hard", false),
            new("AN_PlayerExplorer_Swim_Idle_A", "Swim_Idle", true),
            new("AN_PlayerExplorer_Swim_Forward_A", "Swim_Forward", true)
        };

        public static void Run()
        {
            Report.Clear();
            try
            {
                ImportClips();
                Dictionary<string, AnimationClip> clips = CollectClips();
                BuildController(clips);
                BindPrefab();
                AssetDatabase.SaveAssets();
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
            Report.AppendLine(line);
        }

        static Avatar LoadSourceAvatar()
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is Avatar avatar)
                {
                    return avatar;
                }
            }

            throw new System.InvalidOperationException(
                $"No humanoid avatar found on {ModelPath}.");
        }

        static void ImportClips()
        {
            Avatar avatar = LoadSourceAvatar();
            foreach (ClipSource source in Sources)
            {
                string path = $"{ClipFolder}/{source.Asset}.fbx";
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                {
                    throw new System.InvalidOperationException($"Missing clip asset {path}.");
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importVisibility = false;
                importer.importBlendShapes = false;
                importer.importConstraints = false;
                importer.resampleCurves = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;

                ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
                if (defaults == null || defaults.Length == 0)
                {
                    throw new System.InvalidOperationException($"No takes inside {path}.");
                }

                List<ModelImporterClipAnimation> authored = new()
                {
                    BuildClip(defaults[0], source.Clip, source.Loop, false)
                };
                if (!string.IsNullOrEmpty(source.Mirrored))
                {
                    authored.Add(BuildClip(defaults[0], source.Mirrored, source.Loop, true));
                }

                importer.clipAnimations = authored.ToArray();
                importer.SaveAndReimport();
                Log($"Imported {source.Asset} as humanoid with {authored.Count} clip(s).");
            }
        }

        static ModelImporterClipAnimation BuildClip(
            ModelImporterClipAnimation take,
            string name,
            bool loop,
            bool mirror)
        {
            return new ModelImporterClipAnimation
            {
                name = name,
                takeName = take.takeName,
                firstFrame = take.firstFrame,
                lastFrame = take.lastFrame,
                wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever,
                loopTime = loop,
                loopPose = loop,
                mirror = mirror,
                lockRootRotation = true,
                keepOriginalOrientation = false,
                lockRootHeightY = true,
                heightFromFeet = true,
                lockRootPositionXZ = true,
                keepOriginalPositionXZ = false,
                maskType = ClipAnimationMaskType.None
            };
        }

        static Dictionary<string, AnimationClip> CollectClips()
        {
            Dictionary<string, AnimationClip> clips = new();
            foreach (ClipSource source in Sources)
            {
                string path = $"{ClipFolder}/{source.Asset}.fbx";
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        clips[clip.name] = clip;
                    }
                }
            }

            Log($"Collected {clips.Count} humanoid clips.");
            return clips;
        }

        static AnimationClip Require(
            Dictionary<string, AnimationClip> clips,
            string name)
        {
            if (!clips.TryGetValue(name, out AnimationClip clip) || clip == null)
            {
                throw new System.InvalidOperationException($"Missing clip {name}.");
            }

            return clip;
        }

        static void BuildController(Dictionary<string, AnimationClip> clips)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ControllerPath)))
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed01", AnimatorControllerParameterType.Float);
            controller.AddParameter("StrideScale", AnimatorControllerParameterType.Float);
            controller.AddParameter("Submerged", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            SetDefaultFloat(controller, "StrideScale", 1f);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            machine.entryPosition = new Vector3(-260f, 0f, 0f);
            machine.anyStatePosition = new Vector3(-260f, -160f, 0f);
            machine.exitPosition = new Vector3(-260f, 160f, 0f);

            AnimatorState ground = BuildGroundLocomotion(controller, clips);
            PlaceState(machine, ground, new Vector3(60f, 0f, 0f));
            ground.speedParameterActive = true;
            ground.speedParameter = "StrideScale";
            AnimatorState swim = BuildSwim(controller, clips);
            PlaceState(machine, swim, new Vector3(60f, 240f, 0f));

            AnimatorState jump = machine.AddState("Jump", new Vector3(340f, -160f, 0f));
            jump.motion = Require(clips, "Jump");
            AnimatorState jumpDown = machine.AddState("JumpDown", new Vector3(340f, 40f, 0f));
            jumpDown.motion = Require(clips, "Jump_Down");
            AnimatorState airborne = machine.AddState("AirborneLoop", new Vector3(620f, -60f, 0f));
            airborne.motion = Require(clips, "Fall_Loop");
            AnimatorState land = machine.AddState("Land", new Vector3(620f, 140f, 0f));
            land.motion = Require(clips, "Land_Hard");
            machine.defaultState = ground;

            AddAirExits(ground, jump, jumpDown);

            AddLandingExits(jump, land, ground);
            AnimatorStateTransition transition = Link(jump, airborne, 0.25f);
            transition.AddCondition(AnimatorConditionMode.Less, FallEntrySpeed, "VerticalSpeed");

            AddLandingExits(jumpDown, land, ground);
            transition = Link(jumpDown, airborne, 0.25f);
            transition.AddCondition(AnimatorConditionMode.Less, FallEntrySpeed, "VerticalSpeed");

            AddLandingExits(airborne, land, ground);

            AddAirExits(land, jump, jumpDown);
            Link(land, ground, 0.2f, 0.75f);

            AnimatorStateTransition toSwim = machine.AddAnyStateTransition(swim);
            toSwim.hasExitTime = false;
            toSwim.duration = 0.25f;
            toSwim.canTransitionToSelf = false;
            toSwim.AddCondition(AnimatorConditionMode.Greater, SubmergedThreshold, "Submerged");

            AnimatorStateTransition swimToGround = Link(swim, ground, 0.25f);
            swimToGround.AddCondition(AnimatorConditionMode.Less, SubmergedThreshold, "Submerged");
            swimToGround.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            AnimatorStateTransition swimToAir = Link(swim, airborne, 0.25f);
            swimToAir.AddCondition(AnimatorConditionMode.Less, SubmergedThreshold, "Submerged");
            swimToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Log($"Built {ControllerPath}.");
        }

        static AnimatorStateTransition Link(
            AnimatorState from,
            AnimatorState to,
            float duration,
            float exitTime = -1f)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = exitTime >= 0f;
            if (exitTime >= 0f)
            {
                transition.exitTime = exitTime;
            }

            transition.duration = duration;
            return transition;
        }

        static void AddAirExits(
            AnimatorState from,
            AnimatorState jump,
            AnimatorState jumpDown)
        {
            AnimatorStateTransition rise = Link(from, jump, 0.05f);
            rise.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            rise.AddCondition(AnimatorConditionMode.Greater, JumpRiseSpeed, "VerticalSpeed");

            AnimatorStateTransition drop = Link(from, jumpDown, 0.1f);
            drop.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            drop.AddCondition(AnimatorConditionMode.Less, JumpRiseSpeed, "VerticalSpeed");
        }

        static void AddLandingExits(
            AnimatorState from,
            AnimatorState land,
            AnimatorState ground)
        {
            AnimatorStateTransition hard = Link(from, land, 0.05f);
            hard.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            hard.AddCondition(AnimatorConditionMode.Less, HardLandingSpeed, "VerticalSpeed");

            AnimatorStateTransition soft = Link(from, ground, 0.15f);
            soft.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            soft.AddCondition(AnimatorConditionMode.Greater, HardLandingSpeed, "VerticalSpeed");
        }

        static void PlaceState(
            AnimatorStateMachine machine,
            AnimatorState state,
            Vector3 position)
        {
            ChildAnimatorState[] states = machine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state == state)
                {
                    states[i].position = position;
                }
            }

            machine.states = states;
        }

        static void SetDefaultFloat(AnimatorController controller, string name, float value)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name)
                {
                    parameters[i].defaultFloat = value;
                }
            }

            controller.parameters = parameters;
        }

        static AnimatorState BuildGroundLocomotion(
            AnimatorController controller,
            Dictionary<string, AnimationClip> clips)
        {
            AnimatorState state = controller.CreateBlendTreeInController(
                "GroundLocomotion",
                out BlendTree tree,
                0);
            tree.blendType = BlendTreeType.FreeformCartesian2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveY";
            tree.useAutomaticThresholds = false;

            AnimationClip forward = Require(clips, "Walk_Forward");
            AnimationClip backward = Require(clips, "Walk_Backward");
            AnimationClip left = Require(clips, "Strafe_Left");
            AnimationClip right = Require(clips, "Strafe_Right");
            AnimationClip run = Require(clips, "Run_Forward");

            tree.AddChild(Require(clips, "Idle_Breathing"), Vector2.zero);
            tree.AddChild(forward, new Vector2(0f, 1f));
            tree.AddChild(backward, new Vector2(0f, -1f));
            tree.AddChild(left, new Vector2(-1f, 0f));
            tree.AddChild(right, new Vector2(1f, 0f));
            tree.AddChild(run, new Vector2(0f, 2f));
            tree.AddChild(backward, new Vector2(0f, -2f));
            tree.AddChild(left, new Vector2(-2f, 0f));
            tree.AddChild(right, new Vector2(2f, 0f));

            ChildMotion[] children = tree.children;
            children[1].timeScale = TimeScale(WalkRingSpeed, ForwardClipSpeed);
            children[2].timeScale = TimeScale(WalkRingSpeed, BackwardClipSpeed);
            children[3].timeScale = TimeScale(WalkRingSpeed, StrafeClipSpeed);
            children[4].timeScale = TimeScale(WalkRingSpeed, StrafeClipSpeed);
            children[5].timeScale = TimeScale(RunRingSpeed, RunForwardClipSpeed);
            children[6].timeScale = TimeScale(RunRingSpeed, BackwardClipSpeed);
            children[7].timeScale = TimeScale(RunRingSpeed, StrafeClipSpeed);
            children[8].timeScale = TimeScale(RunRingSpeed, StrafeClipSpeed);
            tree.children = children;
            return state;
        }

        static float TimeScale(float ringSpeed, float clipSpeed)
        {
            return Mathf.Clamp(
                ringSpeed / clipSpeed,
                1f / MaximumClipTimeScale,
                MaximumClipTimeScale);
        }

        static AnimatorState BuildSwim(
            AnimatorController controller,
            Dictionary<string, AnimationClip> clips)
        {
            AnimatorState state = controller.CreateBlendTreeInController(
                "Swim",
                out BlendTree tree,
                0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed01";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Require(clips, "Swim_Idle"), 0f);
            tree.AddChild(Require(clips, "Swim_Forward"), 1f);
            return state;
        }

        static void BindPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CorePrefabPath);
            try
            {
                Transform visualRoot = root.transform.Find("VisualRoot");
                if (visualRoot == null)
                {
                    throw new System.InvalidOperationException(
                        "VisualRoot missing on the explorer core prefab.");
                }

                Animator animator = visualRoot.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    throw new System.InvalidOperationException(
                        "No Animator under VisualRoot; the character model must import as humanoid.");
                }

                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                PlayerExplorerAnimator driver =
                    visualRoot.GetComponent<PlayerExplorerAnimator>();
                if (driver == null)
                {
                    driver = visualRoot.gameObject.AddComponent<PlayerExplorerAnimator>();
                }

                SerializedObject serialized = new(driver);
                serialized.FindProperty("motor").objectReferenceValue =
                    root.GetComponent<FirstPersonMotor>();
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.FindProperty("walkClipSpeed").floatValue = WalkRingSpeed;
                serialized.FindProperty("runClipSpeed").floatValue = RunRingSpeed;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CorePrefabPath);
                Log("Bound the animator driver onto PF_PlayerExplorerCore.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
