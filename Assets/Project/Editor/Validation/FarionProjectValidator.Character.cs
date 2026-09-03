using Farion.Audio.Character;
using Farion.Gameplay.Character;
using Farion.Gameplay.Presentation.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Farion.Editor.Validation
{
    public sealed partial class FarionProjectValidator
    {
        const string ExplorerAnimatorControllerPath =
            "Assets/Project/Art/Animations/Characters/PlayerExplorer/Controllers/AC_PlayerExplorer.controller";

        static readonly (string Name, AnimatorControllerParameterType Type)[]
            ExplorerAnimatorParameters =
            {
                ("MoveX", AnimatorControllerParameterType.Float),
                ("MoveY", AnimatorControllerParameterType.Float),
                ("StrideScale", AnimatorControllerParameterType.Float),
                ("SwimSpeed01", AnimatorControllerParameterType.Float),
                ("Submerged", AnimatorControllerParameterType.Float),
                ("VerticalSpeed", AnimatorControllerParameterType.Float),
                ("Grounded", AnimatorControllerParameterType.Bool),
                ("PlanarSpeed", AnimatorControllerParameterType.Float)
            };

        static readonly (string State, string Motion)[] ExplorerAnimatorStates =
        {
            ("Idle", "Idle"),
            ("Move", "Move"),
            ("MoveStop", "Walk_Stop"),
            ("JumpStart", "Jump_Start"),
            ("Airborne", "Jump_Loop"),
            ("Land", "Jump_Land"),
            ("Swim", "Swim")
        };

        static void ValidateCharacterAnimation(FarionValidationReport report)
        {
            string prefabPath = FarionAssetPaths.CoreExplorerPrefab;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.AddError($"{prefabPath}: explorer core prefab is missing.");
                return;
            }

            Transform visualRoot = prefab.transform.Find("VisualRoot");
            if (visualRoot == null)
            {
                report.AddError($"{prefabPath}: VisualRoot is missing.");
                return;
            }

            Animator animator = visualRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                report.AddError(
                    $"{prefabPath}: no Animator under VisualRoot, so the character cannot animate.");
                return;
            }

            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                report.AddError(
                    $"{prefabPath}: the character model must import as a humanoid avatar.");
            }

            if (animator.applyRootMotion)
            {
                report.AddError(
                    $"{prefabPath}: root motion must stay off; the motor owns movement.");
            }

            AnimatorController controller =
                animator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                report.AddError(
                    $"{prefabPath}: the Animator has no AC_PlayerExplorer controller assigned.");
            }
            else
            {
                ValidateExplorerAnimatorParameters(controller, report);
                ValidateExplorerAnimatorStates(controller, report);
                ValidateExplorerAnimatorIkPass(controller, report);
            }

            PlayerExplorerAnimator driver = visualRoot.GetComponent<PlayerExplorerAnimator>();
            if (driver == null)
            {
                report.AddError(
                    $"{prefabPath}: VisualRoot is missing PlayerExplorerAnimator.");
                return;
            }

            SerializedObject serialized = new(driver);
            if (serialized.FindProperty("motor").objectReferenceValue == null ||
                prefab.GetComponent<FirstPersonMotor>() == null)
            {
                report.AddError(
                    $"{prefabPath}: PlayerExplorerAnimator needs the FirstPersonMotor reference.");
            }

            if (serialized.FindProperty("animator").objectReferenceValue != animator)
            {
                report.AddError(
                    $"{prefabPath}: PlayerExplorerAnimator must point at the character Animator.");
            }

            ValidateExplorerAimRig(prefabPath, visualRoot, animator, report);
            ValidateExplorerFootIk(prefabPath, animator, report);
        }

        static void ValidateExplorerFeel(FarionValidationReport report)
        {
            string prefabPath = FarionAssetPaths.CoreExplorerPrefab;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return;
            }

            if (prefab.GetComponent<ExplorerLocomotionSignals>() == null)
            {
                report.AddError(
                    $"{prefabPath}: root is missing ExplorerLocomotionSignals.");
            }

            if (prefab.GetComponent<ExplorerAudioController>() == null)
            {
                report.AddError(
                    $"{prefabPath}: root is missing ExplorerAudioController.");
            }

            if (prefab.GetComponentInChildren<VisorEyes>(true) == null)
            {
                report.AddError(
                    $"{prefabPath}: VisorEyes missing; run Farion/Character/Install Visor Eyes.");
            }
        }

        static void ValidateExplorerAimRig(
            string prefabPath,
            Transform visualRoot,
            Animator animator,
            FarionValidationReport report)
        {
            RigBuilder rigBuilder = animator.GetComponent<RigBuilder>();
            Transform aimRigTransform = animator.transform.Find("AimRig");
            Rig aimRig = aimRigTransform != null
                ? aimRigTransform.GetComponent<Rig>()
                : null;
            if (rigBuilder == null || aimRig == null)
            {
                report.AddError(
                    $"{prefabPath}: aim rig is missing; run Farion/Character/Build Aim Rig.");
                return;
            }

            bool layered = false;
            foreach (RigLayer layer in rigBuilder.layers)
            {
                if (layer.rig == aimRig)
                {
                    layered = true;
                }
            }

            if (!layered)
            {
                report.AddError(
                    $"{prefabPath}: AimRig is not registered as a RigBuilder layer.");
            }

            if (aimRigTransform.GetComponentsInChildren<MultiAimConstraint>(true).Length == 0)
            {
                report.AddError(
                    $"{prefabPath}: AimRig has no MultiAimConstraint, so view pitch never reaches the body.");
            }

            PlayerExplorerAimRig driver = visualRoot.GetComponent<PlayerExplorerAimRig>();
            if (driver == null)
            {
                report.AddError(
                    $"{prefabPath}: VisualRoot is missing PlayerExplorerAimRig.");
                return;
            }

            SerializedObject serialized = new(driver);
            if (serialized.FindProperty("motor").objectReferenceValue == null ||
                serialized.FindProperty("rig").objectReferenceValue == null ||
                serialized.FindProperty("aimTarget").objectReferenceValue == null)
            {
                report.AddError(
                    $"{prefabPath}: PlayerExplorerAimRig needs motor, rig and aimTarget references.");
            }
        }

        static void ValidateExplorerFootIk(
            string prefabPath,
            Animator animator,
            FarionValidationReport report)
        {
            var solver = animator.GetComponent<PlayerExplorerFootIK>();
            if (solver == null)
            {
                report.AddError(
                    $"{prefabPath}: the Animator needs PlayerExplorerFootIK beside it; run Farion/Character/Build Foot IK.");
                return;
            }

            var serialized = new SerializedObject(solver);
            if (serialized.FindProperty("motor").objectReferenceValue == null ||
                serialized.FindProperty("animator").objectReferenceValue == null)
            {
                report.AddError(
                    $"{prefabPath}: PlayerExplorerFootIK needs motor and animator references.");
            }
        }

        static void ValidateExplorerAnimatorIkPass(
            AnimatorController controller,
            FarionValidationReport report)
        {
            if (controller.layers.Length > 0 && !controller.layers[0].iKPass)
            {
                report.AddError(
                    $"{ExplorerAnimatorControllerPath}: the base layer needs IK Pass on, otherwise foot IK never runs.");
            }
        }

        static void ValidateExplorerAnimatorParameters(
            AnimatorController controller,
            FarionValidationReport report)
        {
            foreach ((string name, AnimatorControllerParameterType type) in
                ExplorerAnimatorParameters)
            {
                bool found = false;
                foreach (AnimatorControllerParameter parameter in controller.parameters)
                {
                    if (parameter.name != name)
                    {
                        continue;
                    }

                    found = true;
                    if (parameter.type != type)
                    {
                        report.AddError(
                            $"{ExplorerAnimatorControllerPath}: parameter {name} must be {type}.");
                    }

                    break;
                }

                if (!found)
                {
                    report.AddError(
                        $"{ExplorerAnimatorControllerPath}: PlayerExplorerAnimator writes {name}, which the controller does not declare.");
                }
            }
        }

        static void ValidateExplorerAnimatorStates(
            AnimatorController controller,
            FarionValidationReport report)
        {
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            foreach ((string expectedState, string expectedMotion) in ExplorerAnimatorStates)
            {
                AnimatorState state = null;
                foreach (ChildAnimatorState child in states)
                {
                    if (child.state.name == expectedState)
                    {
                        state = child.state;
                        break;
                    }
                }

                if (state == null || state.motion == null || state.motion.name != expectedMotion)
                {
                    report.AddError(
                        $"{ExplorerAnimatorControllerPath}: state {expectedState} must use {expectedMotion}.");
                }
            }

            foreach (ChildAnimatorState child in states)
            {
                if (child.state.name is "Jump" or "JumpDown" or "LandSoft" or "LandHard"
                    or "GroundLocomotion" or "AirborneLoop")
                {
                    report.AddError(
                        $"{ExplorerAnimatorControllerPath}: obsolete state {child.state.name} is still reachable.");
                }
            }
        }
    }
}
