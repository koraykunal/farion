using Farion.Gameplay.Character;
using UnityEngine;

namespace Farion.Gameplay.Presentation.Character
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public sealed class PlayerExplorerAnimator : MonoBehaviour
    {
        static readonly int MoveXId = Animator.StringToHash("MoveX");
        static readonly int MoveYId = Animator.StringToHash("MoveY");
        static readonly int Speed01Id = Animator.StringToHash("Speed01");
        static readonly int StrideScaleId = Animator.StringToHash("StrideScale");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int SubmergedId = Animator.StringToHash("Submerged");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int TurnRateId = Animator.StringToHash("TurnRate");

        const float MaximumTurnRate = 360f;

        [Header("Bindings")]
        [SerializeField] FirstPersonMotor motor;
        [SerializeField] Animator animator;

        [Header("Clip Calibration")]
        [Tooltip("Ground speed in metres per second that the walk clips were authored for. The walk ring of the locomotion blend tree sits at this speed.")]
        [SerializeField] float walkClipSpeed = 1.5f;
        [Tooltip("Ground speed in metres per second that the run clips were authored for. The run ring of the locomotion blend tree sits at this speed.")]
        [SerializeField] float runClipSpeed = 3.6f;
        [Tooltip("Ground speed in metres per second that the swim clip was authored for. Reaching it blends fully from treading water to swimming.")]
        [SerializeField] float swimClipSpeed = 1.4f;
        [Tooltip("Upper bound for the playback multiplier that keeps footfalls in step with real speed. Raise it when the character outruns its stride, lower it when the legs blur.")]
        [SerializeField] float maximumStrideScale = 2.5f;

        [Header("Response")]
        [Tooltip("Seconds of smoothing applied to the movement axes so direction changes do not snap the blend tree.")]
        [SerializeField] float directionDamping = 0.1f;
        [Tooltip("Seconds of smoothing applied to the yaw rate so mouse jitter does not flicker the turn-in-place states.")]
        [SerializeField] float turnRateDamping = 0.15f;

        bool previousGrounded = true;
        float airborneVerticalSpeed;
        bool hasPreviousForward;
        Vector3 previousForward;

        void Reset()
        {
            ResolveBindings();
        }

        void Awake()
        {
            ResolveBindings();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        void ResolveBindings()
        {
            motor ??= GetComponentInParent<FirstPersonMotor>();
            animator ??= GetComponentInChildren<Animator>(true);
        }

        void Update()
        {
            if (motor == null || animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            FirstPersonMotorState state = motor.CaptureState();
            float deltaTime = Time.deltaTime;
            Vector3 localVelocity =
                motor.transform.InverseTransformDirection(motor.SurfaceVelocity);
            Vector2 planar = new(localVelocity.x, localVelocity.z);
            float speed = planar.magnitude;
            float gait = ResolveGait(speed);
            Vector2 axes = speed > 0.05f ? planar / speed * gait : Vector2.zero;

            animator.SetFloat(MoveXId, axes.x, directionDamping, deltaTime);
            animator.SetFloat(MoveYId, axes.y, directionDamping, deltaTime);
            animator.SetFloat(
                StrideScaleId,
                ResolveStrideScale(speed, gait),
                directionDamping,
                deltaTime);
            animator.SetFloat(
                Speed01Id,
                Mathf.Clamp01(speed / Mathf.Max(0.01f, swimClipSpeed)),
                directionDamping,
                deltaTime);
            animator.SetBool(GroundedId, state.Grounded);
            animator.SetFloat(
                SubmergedId,
                state.WaterSubmergedFraction,
                directionDamping,
                deltaTime);
            animator.SetFloat(VerticalSpeedId, ResolveVerticalSpeed(state));
            animator.SetFloat(
                TurnRateId,
                ResolveTurnRate(state, deltaTime),
                turnRateDamping,
                deltaTime);
        }

        float ResolveVerticalSpeed(FirstPersonMotorState state)
        {
            float verticalSpeed = state.VerticalSpeed;
            if (!state.Grounded)
            {
                airborneVerticalSpeed = verticalSpeed;
            }
            else if (!previousGrounded)
            {
                verticalSpeed = airborneVerticalSpeed;
            }

            previousGrounded = state.Grounded;
            return verticalSpeed;
        }

        float ResolveTurnRate(FirstPersonMotorState state, float deltaTime)
        {
            Vector3 up = state.LocalUp.sqrMagnitude > 0.5f
                ? state.LocalUp
                : motor.transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(motor.transform.forward, up);
            float turnRate = 0f;
            if (hasPreviousForward &&
                deltaTime > 0f &&
                forward.sqrMagnitude > 0.0001f &&
                previousForward.sqrMagnitude > 0.0001f)
            {
                turnRate = Mathf.Clamp(
                    Vector3.SignedAngle(previousForward, forward, up) / deltaTime,
                    -MaximumTurnRate,
                    MaximumTurnRate);
            }

            previousForward = forward;
            hasPreviousForward = true;
            return turnRate;
        }

        float ResolveGait(float speed)
        {
            float walk = Mathf.Max(0.01f, walkClipSpeed);
            if (speed <= walk)
            {
                return speed / walk;
            }

            float run = Mathf.Max(walk + 0.01f, runClipSpeed);
            return 1f + Mathf.Clamp01((speed - walk) / (run - walk));
        }

        float ResolveStrideScale(float speed, float gait)
        {
            if (speed <= 0.25f)
            {
                return 1f;
            }

            float reference = Mathf.Lerp(
                Mathf.Max(0.01f, walkClipSpeed),
                Mathf.Max(0.01f, runClipSpeed),
                Mathf.Clamp01(gait - 1f));
            return Mathf.Clamp(speed / reference, 0.5f, Mathf.Max(1f, maximumStrideScale));
        }

    }
}
