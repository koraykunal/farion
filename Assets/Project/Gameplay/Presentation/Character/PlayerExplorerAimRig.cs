using Farion.Core.Numerics;
using Farion.Gameplay.Character;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Farion.Gameplay.Presentation.Character
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public sealed class PlayerExplorerAimRig : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] ExplorerMotor motor;
        [SerializeField] Rig rig;
        [SerializeField] Transform aimTarget;

        [Header("Aim")]
        [Tooltip("Height above the character origin where the aim ray pivots. Keep it near the chest so the spine chain bends toward where the camera looks.")]
        [SerializeField] float pivotHeight = 0.65f;
        [Tooltip("Distance in metres from the pivot to the aim target. Longer distances make the bone rotation less sensitive to target placement noise.")]
        [SerializeField] float aimDistance = 6f;
        [Tooltip("How quickly the body follows the camera direction. Higher values track faster; lower values give a heavier, more deliberate feel.")]
        [SerializeField] float responsiveness = 14f;
        [Tooltip("Limit torso bending independently from the camera so looking at the feet does not fold the whole body forward.")]
        [SerializeField, Range(0f, 85f)] float maximumBodyPitch = 35f;
        [Tooltip("While exploring, the head and chest follow the camera only up to this yaw offset from the body; beyond it the rig fades out over the next 40 degrees.")]
        [SerializeField, Range(0f, 180f)] float exploreYawLimit = 80f;
        [Tooltip("How quickly the rig weight fades when the aim rig enables or disables.")]
        [SerializeField] float weightResponsiveness = 6f;

        Vector3 smoothedLook;
        PlayerExplorerSeatedPose seatedPose;

        void Awake() => seatedPose = GetComponentInChildren<PlayerExplorerSeatedPose>(true);

        void Reset()
        {
            motor ??= GetComponentInParent<ExplorerMotor>();
            rig ??= GetComponentInChildren<Rig>(true);
        }

        void Update()
        {
            if (motor == null || rig == null || aimTarget == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            Vector3 up = motor.LocalUp.sqrMagnitude > 0.0001f ? motor.LocalUp.normalized : motor.transform.up;
            Vector3 look = ClampPitch(motor.LookDirection, up);
            smoothedLook = smoothedLook.sqrMagnitude > 0.0001f
                ? Vector3.Slerp(smoothedLook, look, FarionMath.SmoothFactor(responsiveness, deltaTime))
                : look;

            float yawOffset = Vector3.Angle(
                Vector3.ProjectOnPlane(look, up),
                Vector3.ProjectOnPlane(motor.transform.forward, up));
            ExplorerMotorState state = motor.CaptureState();
            bool suppressed = state.Swimming || (seatedPose != null && seatedPose.IsSeated);
            float desiredWeight = suppressed ? 0f
                : motor.Aiming ? 1f
                : 1f - Mathf.InverseLerp(exploreYawLimit, exploreYawLimit + 40f, yawOffset);
            rig.weight = Mathf.Lerp(rig.weight, desiredWeight, FarionMath.SmoothFactor(weightResponsiveness, deltaTime));

            aimTarget.position = motor.transform.position + up * pivotHeight + smoothedLook * aimDistance;
        }

        Vector3 ClampPitch(Vector3 look, Vector3 up)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(look, up);
            if (horizontal.sqrMagnitude <= 0.0001f)
            {
                horizontal = Vector3.ProjectOnPlane(motor.transform.forward, up);
            }

            horizontal = horizontal.normalized;
            float pitch = -Mathf.Asin(Mathf.Clamp(Vector3.Dot(look.normalized, up), -1f, 1f)) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -maximumBodyPitch, maximumBodyPitch);
            return Quaternion.AngleAxis(pitch, Vector3.Cross(up, horizontal)) * horizontal;
        }
    }
}
