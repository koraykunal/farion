using Farion.Gameplay.Flight;
using UnityEngine;

namespace Farion.Gameplay.Presentation.Character
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)]
    public sealed class PlayerExplorerSeatedPose : MonoBehaviour
    {
        [Header("Bindings")]
        [Tooltip("Graphical root the tick smoother drives. It is pinned to the seat while seated so the body never lags behind the ship.")]
        [SerializeField] Transform graphicalRoot;
        [SerializeField] Animator animator;

        [Header("Pose")]
        [Tooltip("Hip flexion in degrees. Lifts the thighs forward from the standing pose.")]
        [SerializeField] float thighAngle = 85f;
        [Tooltip("Knee flexion in degrees. Folds the shins back down after the hips bend.")]
        [SerializeField] float shinAngle = 80f;
        [Tooltip("Spine lean in degrees. Negative leans back into the seat.")]
        [SerializeField] float spineLean = -6f;
        [Tooltip("Shoulder flexion in degrees. Raises the upper arms toward the controls.")]
        [SerializeField] float upperArmAngle = 45f;
        [Tooltip("Elbow flexion in degrees. Brings the hands up to the controls.")]
        [SerializeField] float forearmAngle = 40f;
        [Tooltip("Offset from the pilot seat point to the character root, in seat space. Sinks the hips into the seat.")]
        [SerializeField] Vector3 seatOffset;

        [Header("Runtime")]
        [SerializeField] SpacecraftRig seatRig;

        public bool IsSeated => seatRig != null;

        void Awake()
        {
            animator ??= GetComponentInChildren<Animator>(true);
            graphicalRoot ??= transform;
        }

        public void SetSeat(SpacecraftRig rig)
        {
            seatRig = rig;
        }

        void LateUpdate()
        {
            if (seatRig == null ||
                animator == null ||
                !animator.isHuman ||
                !seatRig.TryGetGraphicalPose(
                    seatRig.PilotSeatPoint,
                    out Vector3 position,
                    out Quaternion rotation))
            {
                return;
            }

            graphicalRoot.SetPositionAndRotation(position + rotation * seatOffset, rotation);
            Vector3 right = graphicalRoot.right;
            Bend(HumanBodyBones.Spine, spineLean, right);
            Bend(HumanBodyBones.LeftUpperLeg, -thighAngle, right);
            Bend(HumanBodyBones.LeftLowerLeg, shinAngle, right);
            Bend(HumanBodyBones.RightUpperLeg, -thighAngle, right);
            Bend(HumanBodyBones.RightLowerLeg, shinAngle, right);
            Bend(HumanBodyBones.LeftUpperArm, -upperArmAngle, right);
            Bend(HumanBodyBones.LeftLowerArm, -forearmAngle, right);
            Bend(HumanBodyBones.RightUpperArm, -upperArmAngle, right);
            Bend(HumanBodyBones.RightLowerArm, -forearmAngle, right);
        }

        void Bend(HumanBodyBones bone, float degrees, Vector3 axis)
        {
            Transform target = animator.GetBoneTransform(bone);
            if (target != null)
            {
                target.rotation = Quaternion.AngleAxis(degrees, axis) * target.rotation;
            }
        }
    }
}
