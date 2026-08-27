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
        [SerializeField] FirstPersonMotor motor;
        [SerializeField] Rig rig;
        [SerializeField] Transform aimTarget;

        [Header("Aim")]
        [Tooltip("Height above the character origin where the aim ray pivots. Keep it at eye level so the spine chain bends toward where the player is actually looking.")]
        [SerializeField] float pivotHeight = 0.65f;
        [Tooltip("Distance in metres from the pivot to the aim target. Longer distances make the bone rotation less sensitive to target placement noise.")]
        [SerializeField] float aimDistance = 6f;
        [Tooltip("How quickly the body follows the view pitch. Higher values track faster; lower values give a heavier, more deliberate feel.")]
        [SerializeField] float pitchResponsiveness = 14f;
        [Tooltip("Submerged fraction above which the aim rig fades out so the authored swim poses stay untouched.")]
        [SerializeField] float submergedCutoff = 0.5f;
        [Tooltip("How quickly the rig weight fades when the aim rig enables or disables.")]
        [SerializeField] float weightResponsiveness = 6f;

        float currentPitchDegrees;

        void Reset()
        {
            motor ??= GetComponentInParent<FirstPersonMotor>();
            rig ??= GetComponentInChildren<Rig>(true);
        }

        void Update()
        {
            if (motor == null || rig == null || aimTarget == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            currentPitchDegrees = Mathf.Lerp(
                currentPitchDegrees,
                motor.ViewPitchDegrees,
                FarionMath.SmoothFactor(pitchResponsiveness, deltaTime));

            FirstPersonMotorState state = motor.CaptureState();
            float desiredWeight =
                state.WaterSubmergedFraction >= submergedCutoff ? 0f : 1f;
            rig.weight = Mathf.Lerp(
                rig.weight,
                desiredWeight,
                FarionMath.SmoothFactor(weightResponsiveness, deltaTime));

            Vector3 localAim = Vector3.up * pivotHeight +
                Quaternion.AngleAxis(currentPitchDegrees, Vector3.right) *
                (Vector3.forward * aimDistance);
            aimTarget.position = motor.transform.TransformPoint(localAim);
        }

    }
}
