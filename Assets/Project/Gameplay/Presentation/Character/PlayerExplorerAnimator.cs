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
        static readonly int StrideScaleId = Animator.StringToHash("StrideScale");
        static readonly int SwimSpeed01Id = Animator.StringToHash("SwimSpeed01");
        static readonly int PlanarSpeedId = Animator.StringToHash("PlanarSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int SubmergedId = Animator.StringToHash("Submerged");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");

        [Header("Bindings")]
        [SerializeField] FirstPersonMotor motor;
        [SerializeField] Animator animator;

        [Header("Clip Calibration")]
        [Tooltip("Ground speed in metres per second that the walk clips were authored for. The walk ring of the locomotion blend tree sits at this speed.")]
        [SerializeField] float walkClipSpeed = 1.7f;
        [Tooltip("Ground speed in metres per second that the jog clip was authored for. The jog ring sits between walk and run so ordinary movement never blends two mismatched gaits.")]
        [SerializeField] float jogClipSpeed = 2.9f;
        [Tooltip("Ground speed in metres per second that the run clip was authored for. The run ring of the locomotion blend tree sits at this speed.")]
        [SerializeField] float runClipSpeed = 4.8f;
        [Tooltip("Ground speed in metres per second that the swim clip was authored for. Reaching it blends fully from treading water to swimming.")]
        [SerializeField] float swimClipSpeed = 1.4f;
        [Tooltip("Lower bound for the playback multiplier that keeps footfalls in step with real speed. Raise it when slow movement looks like slow motion.")]
        [SerializeField] float minimumStrideScale = 0.65f;
        [Tooltip("Upper bound for the playback multiplier that keeps footfalls in step with real speed. Raise it when the character outruns its stride, lower it when the legs blur.")]
        [SerializeField] float maximumStrideScale = 1.5f;

        [Header("Response")]
        [Tooltip("Seconds of smoothing applied to the movement axes so direction changes do not snap the blend tree.")]
        [SerializeField] float directionDamping = 0.1f;
        [Tooltip("Seconds the animator keeps reporting solid ground after the motor loses contact. Absorbs single-frame probe dropouts on rough terrain without delaying a real jump.")]
        [SerializeField] float groundedCoyoteTime = 0.08f;

        float groundedHoldRemaining;
        bool swimming;

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
                motor.transform.InverseTransformDirection(state.SurfaceVelocity);
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
                SwimSpeed01Id,
                Mathf.Clamp01(speed / Mathf.Max(0.01f, swimClipSpeed)),
                directionDamping,
                deltaTime);
            animator.SetFloat(PlanarSpeedId, speed);
            animator.SetBool(GroundedId, ResolveGrounded(state, deltaTime));
            if (state.WaterSubmergedFraction > 0.5f)
            {
                swimming = true;
            }
            else if (!state.TouchingWater || state.Grounded)
            {
                swimming = false;
            }

            animator.SetFloat(
                SubmergedId,
                swimming ? Mathf.Max(0.51f, state.WaterSubmergedFraction) : state.WaterSubmergedFraction,
                directionDamping,
                deltaTime);
            animator.SetFloat(VerticalSpeedId, state.VerticalSpeed);
        }

        bool ResolveGrounded(in FirstPersonMotorState state, float deltaTime)
        {
            if (state.Grounded)
            {
                groundedHoldRemaining = groundedCoyoteTime;
                return true;
            }

            if (state.VerticalSpeed > 0.5f)
            {
                groundedHoldRemaining = 0f;
                return false;
            }

            groundedHoldRemaining -= deltaTime;
            return groundedHoldRemaining > 0f;
        }

        float ResolveGait(float speed)
        {
            float walk = Mathf.Max(0.01f, walkClipSpeed);
            if (speed <= walk)
            {
                return speed / walk;
            }

            float jog = Mathf.Max(walk + 0.01f, jogClipSpeed);
            if (speed <= jog)
            {
                return 1f + (speed - walk) / (jog - walk);
            }

            float run = Mathf.Max(jog + 0.01f, runClipSpeed);
            return 2f + Mathf.Clamp01((speed - jog) / (run - jog));
        }

        float ResolveStrideScale(float speed, float gait)
        {
            if (speed <= 0.25f)
            {
                return 1f;
            }

            return Mathf.Clamp(
                speed / ResolveRingSpeed(gait),
                minimumStrideScale,
                Mathf.Max(1f, maximumStrideScale));
        }

        float ResolveRingSpeed(float gait)
        {
            float walk = Mathf.Max(0.01f, walkClipSpeed);
            if (gait <= 1f)
            {
                return walk;
            }

            float jog = Mathf.Max(walk + 0.01f, jogClipSpeed);
            if (gait <= 2f)
            {
                return Mathf.Lerp(walk, jog, gait - 1f);
            }

            return Mathf.Lerp(jog, Mathf.Max(jog + 0.01f, runClipSpeed), Mathf.Clamp01(gait - 2f));
        }

    }
}
