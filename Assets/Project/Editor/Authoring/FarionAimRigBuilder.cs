using System.Text;
using Farion.Gameplay.Character;
using Farion.Gameplay.Presentation.Character;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Farion.Editor.Authoring
{
    static class FarionAimRigBuilder
    {
        const string AimRigName = "AimRig";
        const string AimTargetName = "AimTarget";
        const float AimPivotHeight = 0.65f;
        const float AimDistance = 6f;
        const float AimLimitDegrees = 85f;

        static readonly (HumanBodyBones Bone, float Weight)[] AimBones =
        {
            (HumanBodyBones.Spine, 0.25f),
            (HumanBodyBones.Chest, 0.3f),
            (HumanBodyBones.UpperChest, 0.35f),
            (HumanBodyBones.Head, 0.7f)
        };

        static readonly StringBuilder Report = new();

        [MenuItem("Farion/Character/Build Aim Rig")]
        public static void Run()
        {
            Report.Clear();
            try
            {
                Build();
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception exception)
            {
                Log("FAILED: " + exception);
                Debug.Log(Report.ToString());
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }

            Debug.Log(Report.ToString());
        }

        static void Log(string line)
        {
            Report.AppendLine(line);
        }

        static void Build()
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(FarionAssetPaths.CoreExplorerPrefab);
            try
            {
                Transform visualRoot = root.transform.Find("VisualRoot");
                if (visualRoot == null)
                {
                    throw new System.InvalidOperationException(
                        "VisualRoot missing on the explorer core prefab.");
                }

                Animator animator = visualRoot.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    throw new System.InvalidOperationException(
                        "No humanoid Animator under VisualRoot.");
                }

                Transform existing = animator.transform.Find(AimRigName);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                    Log("Removed the previous aim rig.");
                }

                GameObject rigObject = new(AimRigName);
                rigObject.transform.SetParent(animator.transform, false);
                Rig rig = rigObject.AddComponent<Rig>();

                GameObject targetObject = new(AimTargetName);
                targetObject.transform.SetParent(rigObject.transform, false);
                targetObject.transform.position = root.transform.TransformPoint(
                    Vector3.up * AimPivotHeight + Vector3.forward * AimDistance);

                Vector3 characterForward = root.transform.forward;
                int constraintCount = 0;
                foreach ((HumanBodyBones bone, float weight) in AimBones)
                {
                    Transform boneTransform = animator.GetBoneTransform(bone);
                    if (boneTransform == null)
                    {
                        Log($"Skipped {bone}: not mapped on the avatar.");
                        continue;
                    }

                    BuildConstraint(
                        rigObject.transform,
                        targetObject.transform,
                        boneTransform,
                        bone,
                        weight,
                        characterForward);
                    constraintCount++;
                }

                if (constraintCount == 0)
                {
                    throw new System.InvalidOperationException(
                        "No aim bones found on the avatar.");
                }

                RigBuilder rigBuilder = animator.GetComponent<RigBuilder>();
                if (rigBuilder == null)
                {
                    rigBuilder = animator.gameObject.AddComponent<RigBuilder>();
                }

                rigBuilder.layers.RemoveAll(layer => layer.rig == null);
                rigBuilder.layers.Add(new RigLayer(rig));

                PlayerExplorerAimRig driver =
                    visualRoot.GetComponent<PlayerExplorerAimRig>();
                if (driver == null)
                {
                    driver = visualRoot.gameObject.AddComponent<PlayerExplorerAimRig>();
                }

                SerializedObject serialized = new(driver);
                serialized.FindProperty("motor").objectReferenceValue =
                    root.GetComponent<FirstPersonMotor>();
                serialized.FindProperty("rig").objectReferenceValue = rig;
                serialized.FindProperty("aimTarget").objectReferenceValue =
                    targetObject.transform;
                serialized.FindProperty("pivotHeight").floatValue = AimPivotHeight;
                serialized.FindProperty("aimDistance").floatValue = AimDistance;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, FarionAssetPaths.CoreExplorerPrefab);
                Log($"Built the aim rig with {constraintCount} constraint(s) onto PF_PlayerExplorerCore.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void BuildConstraint(
            Transform rigRoot,
            Transform aimTarget,
            Transform boneTransform,
            HumanBodyBones bone,
            float weight,
            Vector3 characterForward)
        {
            GameObject constraintObject = new($"Aim_{bone}");
            constraintObject.transform.SetParent(rigRoot, false);
            MultiAimConstraint constraint =
                constraintObject.AddComponent<MultiAimConstraint>();
            MultiAimConstraintData data = constraint.data;
            data.constrainedObject = boneTransform;
            data.aimAxis = ResolveAimAxis(boneTransform, characterForward);
            data.worldUpType = MultiAimConstraintData.WorldUpType.None;
            data.maintainOffset = true;
            data.offset = Vector3.zero;
            data.constrainedXAxis = true;
            data.constrainedYAxis = true;
            data.constrainedZAxis = true;
            data.limits = new Vector2(-AimLimitDegrees, AimLimitDegrees);
            WeightedTransformArray sources = new();
            sources.Add(new WeightedTransform(aimTarget, 1f));
            data.sourceObjects = sources;
            constraint.data = data;
            constraint.weight = weight;
            Log($"Constrained {bone} (aim axis {data.aimAxis}, weight {weight:F2}).");
        }

        static MultiAimConstraintData.Axis ResolveAimAxis(
            Transform boneTransform,
            Vector3 characterForward)
        {
            Vector3 local = boneTransform.InverseTransformDirection(characterForward);
            float absX = Mathf.Abs(local.x);
            float absY = Mathf.Abs(local.y);
            float absZ = Mathf.Abs(local.z);
            if (absX >= absY && absX >= absZ)
            {
                return local.x >= 0f
                    ? MultiAimConstraintData.Axis.X
                    : MultiAimConstraintData.Axis.X_NEG;
            }

            if (absY >= absZ)
            {
                return local.y >= 0f
                    ? MultiAimConstraintData.Axis.Y
                    : MultiAimConstraintData.Axis.Y_NEG;
            }

            return local.z >= 0f
                ? MultiAimConstraintData.Axis.Z
                : MultiAimConstraintData.Axis.Z_NEG;
        }
    }
}
