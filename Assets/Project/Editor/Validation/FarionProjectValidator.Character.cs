using Farion.Gameplay.Character;
using Farion.Gameplay.Presentation.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

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
                ("Speed01", AnimatorControllerParameterType.Float),
                ("StrideScale", AnimatorControllerParameterType.Float),
                ("Submerged", AnimatorControllerParameterType.Float),
                ("Grounded", AnimatorControllerParameterType.Bool),
                ("VerticalSpeed", AnimatorControllerParameterType.Float),
                ("TurnRate", AnimatorControllerParameterType.Float)
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
    }
}
