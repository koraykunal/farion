using System;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class ExplorerLocomotionSignals : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] ExplorerMotor motor;

        [Header("Stride")]
        [Tooltip("Meters of ground travel per footstep; a full left-right cycle covers twice this.")]
        [Min(0.1f)]
        [SerializeField] float strideLength = 1f;
        [Tooltip("Below this surface speed the stride accumulator pauses so idling produces no steps.")]
        [Min(0f)]
        [SerializeField] float minimumCadenceSpeed = 0.5f;

        [Header("Landing")]
        [Tooltip("Airborne time required before a grounding counts as a landing; filters stair pops and ground probe dropouts.")]
        [Min(0f)]
        [SerializeField] float minimumAirborneTime = 0.15f;
        [Tooltip("Minimum seconds between landing events; absorbs reconcile and probe flicker.")]
        [Min(0f)]
        [SerializeField] float landRefireGuard = 0.2f;

        float cycleMeters;
        float airborneSeconds;
        float peakFallSpeed;
        float lastLandingTime = float.NegativeInfinity;
        float lastJumpTime = float.NegativeInfinity;
        bool wasGrounded = true;
        ExplorerMotorState lastState;

        public event Action<float> Landed;
        public event Action Jumped;
        public event Action<float> Stepped;

        public float CyclePhase01 =>
            strideLength > 0f ? cycleMeters / (strideLength * 2f) : 0f;
        public ExplorerMotorState LastState => lastState;

        void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<ExplorerMotor>();
            }
        }

        void Update()
        {
            if (motor == null)
            {
                return;
            }

            ExplorerMotorState state = motor.CaptureState();
            lastState = state;
            float deltaTime = Time.deltaTime;

            if (state.LastJumpTime > lastJumpTime)
            {
                lastJumpTime = state.LastJumpTime;
                Jumped?.Invoke();
            }
            else if (state.LastJumpTime < lastJumpTime)
            {
                lastJumpTime = state.LastJumpTime;
            }

            if (!state.Grounded)
            {
                airborneSeconds += deltaTime;
                peakFallSpeed = Mathf.Max(peakFallSpeed, -state.VerticalSpeed);
            }

            bool landed = EvaluateLanding(
                wasGrounded,
                state.Grounded,
                state.Underwater,
                airborneSeconds,
                minimumAirborneTime,
                Time.time - lastLandingTime,
                landRefireGuard);
            if (landed)
            {
                lastLandingTime = Time.time;
                Landed?.Invoke(Mathf.Max(0f, peakFallSpeed));
            }

            if (state.Grounded)
            {
                airborneSeconds = 0f;
                peakFallSpeed = 0f;
            }

            wasGrounded = state.Grounded;

            bool striding = state.Grounded && !state.Underwater;
            int steps = AdvanceStride(
                ref cycleMeters,
                striding ? state.SurfaceSpeed : 0f,
                minimumCadenceSpeed,
                strideLength,
                deltaTime);
            for (int i = 0; i < steps; i++)
            {
                Stepped?.Invoke(state.SurfaceSpeed);
            }
        }

        internal static bool EvaluateLanding(
            bool wasGrounded,
            bool isGrounded,
            bool underwater,
            float airborneSeconds,
            float minimumAirborneSeconds,
            float secondsSinceLastLanding,
            float refireGuardSeconds)
        {
            return !wasGrounded
                && isGrounded
                && !underwater
                && airborneSeconds >= minimumAirborneSeconds
                && secondsSinceLastLanding >= refireGuardSeconds;
        }

        internal static int AdvanceStride(
            ref float cycleMeters,
            float planarSpeed,
            float minimumSpeed,
            float strideLength,
            float deltaTime)
        {
            if (strideLength <= 0f || deltaTime <= 0f || planarSpeed < minimumSpeed)
            {
                return 0;
            }

            float previous = cycleMeters;
            cycleMeters += planarSpeed * deltaTime;
            int steps = Mathf.FloorToInt(cycleMeters / strideLength) -
                Mathf.FloorToInt(previous / strideLength);
            float cycleLength = strideLength * 2f;
            if (cycleMeters >= cycleLength)
            {
                cycleMeters %= cycleLength;
            }

            return steps;
        }
    }
}
