using Farion.Core.Numerics;
using Farion.Gameplay.Input;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DefaultExecutionOrder(210)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonViewEffects : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] FirstPersonCameraRig rig;

        [Header("Landing Spring")]
        [Tooltip("Downward camera dip velocity per m/s of landing impact speed.")]
        [Min(0f)]
        [SerializeField] float landDipVelocityScale = 0.05f;
        [Tooltip("Cap on the dip velocity so terminal falls do not bury the camera.")]
        [Min(0f)]
        [SerializeField] float maxLandDipVelocity = 0.6f;
        [Tooltip("Spring pull toward rest; higher settles faster.")]
        [Min(0f)]
        [SerializeField] float springStiffness = 180f;
        [Tooltip("Spring resistance; lower values bounce more before settling.")]
        [Min(0f)]
        [SerializeField] float springDamping = 12f;
        [Tooltip("Upper clamp on the total additive view offset; guards against stacked impulses.")]
        [Min(0f)]
        [SerializeField] float maxViewOffset = 0.3f;

        [Header("Head Bob")]
        [Tooltip("Vertical bob height at the reference speed, in meters.")]
        [Min(0f)]
        [SerializeField] float bobVerticalAmplitude = 0.035f;
        [Tooltip("Lateral bob sway at the reference speed, in meters.")]
        [Min(0f)]
        [SerializeField] float bobLateralAmplitude = 0.02f;
        [Tooltip("Surface speed at which bob reaches full amplitude; aligned with the run clip speed.")]
        [Min(0.1f)]
        [SerializeField] float bobSpeedReference = 4.8f;
        [Tooltip("Smoothing response for bob amplitude changes.")]
        [Min(0f)]
        [SerializeField] float bobResponsiveness = 10f;

        [Header("Strafe Lean")]
        [Tooltip("Camera roll per m/s of lateral surface velocity; the camera leans into the strafe.")]
        [Min(0f)]
        [SerializeField] float strafeLeanDegreesPerMps = 0.6f;
        [Tooltip("Cap on the strafe lean roll.")]
        [Min(0f)]
        [SerializeField] float maxStrafeLeanDegrees = 3f;
        [Tooltip("Smoothing response for lean changes.")]
        [Min(0f)]
        [SerializeField] float leanResponsiveness = 8f;

        Vector3 springOffset;
        Vector3 springVelocity;
        float smoothedBobBlend;
        float smoothedLeanDegrees;
        ExplorerLocomotionSignals signals;
        FirstPersonMotor observedTarget;

        void Awake()
        {
            if (rig == null)
            {
                rig = GetComponent<FirstPersonCameraRig>();
            }
        }

        void OnDisable()
        {
            Detach();
            ResetState();
        }

        public void AddImpulse(Vector3 localVelocity)
        {
            if (PlayerViewPreferences.ReducedMotion)
            {
                return;
            }

            springVelocity += localVelocity;
        }

        void LateUpdate()
        {
            if (rig == null || !rig.enabled || rig.Target == null)
            {
                Detach();
                ResetState();
                return;
            }

            FirstPersonMotor target = rig.Target;
            if (!ReferenceEquals(target, observedTarget))
            {
                Attach(target);
            }

            bool reducedMotion = PlayerViewPreferences.ReducedMotion;
            float deltaTime = Time.deltaTime;

            FarionMath.Spring(
                ref springOffset,
                ref springVelocity,
                Vector3.zero,
                springStiffness,
                springDamping,
                deltaTime);

            float bobTarget = 0f;
            float phase = 0f;
            if (!reducedMotion && signals != null)
            {
                FirstPersonMotorState state = signals.LastState;
                if (state.Grounded && !state.Underwater)
                {
                    bobTarget = Mathf.Clamp01(state.SurfaceSpeed / bobSpeedReference);
                }

                phase = signals.CyclePhase01;
            }

            smoothedBobBlend = FarionMath.Smooth(
                smoothedBobBlend,
                bobTarget,
                bobResponsiveness,
                deltaTime);

            float leanTarget = 0f;
            if (!reducedMotion)
            {
                float lateralSpeed = target.transform
                    .InverseTransformDirection(target.SurfaceVelocity).x;
                leanTarget = Mathf.Clamp(
                    -lateralSpeed * strafeLeanDegreesPerMps,
                    -maxStrafeLeanDegrees,
                    maxStrafeLeanDegrees);
            }

            smoothedLeanDegrees = FarionMath.Smooth(
                smoothedLeanDegrees,
                leanTarget,
                leanResponsiveness,
                deltaTime);

            float verticalBob = Mathf.Sin(phase * Mathf.PI * 4f) *
                bobVerticalAmplitude * smoothedBobBlend;
            float lateralBob = Mathf.Sin(phase * Mathf.PI * 2f) *
                bobLateralAmplitude * smoothedBobBlend;

            Vector3 up = target.LocalUp.sqrMagnitude > 0.0001f
                ? target.LocalUp
                : transform.up;
            Vector3 worldOffset =
                transform.right * (lateralBob + springOffset.x) +
                up * (verticalBob + springOffset.y) +
                transform.forward * springOffset.z;
            worldOffset = Vector3.ClampMagnitude(worldOffset, maxViewOffset);
            transform.position += worldOffset;

            if (Mathf.Abs(smoothedLeanDegrees) > 0.001f)
            {
                transform.rotation *= Quaternion.AngleAxis(
                    smoothedLeanDegrees,
                    Vector3.forward);
            }
        }

        void Attach(FirstPersonMotor target)
        {
            Detach();
            observedTarget = target;
            signals = target != null
                ? target.GetComponent<ExplorerLocomotionSignals>()
                : null;
            if (signals != null)
            {
                signals.Landed += HandleLanded;
            }

            ResetState();
        }

        void Detach()
        {
            if (signals != null)
            {
                signals.Landed -= HandleLanded;
            }

            signals = null;
            observedTarget = null;
        }

        void ResetState()
        {
            springOffset = Vector3.zero;
            springVelocity = Vector3.zero;
            smoothedBobBlend = 0f;
            smoothedLeanDegrees = 0f;
        }

        void HandleLanded(float impactSpeed)
        {
            AddImpulse(Vector3.down * Mathf.Min(
                impactSpeed * landDipVelocityScale,
                maxLandDipVelocity));
        }
    }
}
