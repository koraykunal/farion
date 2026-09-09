using Farion.Core.Numerics;
using Farion.Core.Physics;
using Farion.Gameplay.Character;
using UnityEngine;

namespace Farion.Gameplay.Presentation.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerExplorerFootIK : MonoBehaviour
    {

        [Header("Bindings")]
        [SerializeField] ExplorerMotor motor;
        [SerializeField] Animator animator;

        [Header("Probe")]
        [Tooltip("How far above the animated ankle the ground ray starts. Must clear the tallest step the character can walk onto, or the foot snaps to the wrong surface.")]
        [SerializeField] float probeRise = 0.5f;
        [Tooltip("How far below the animated ankle the ground ray reaches. Longer values let the foot follow a drop-off; too long and the leg over-extends on ledges.")]
        [SerializeField] float probeDepth = 0.7f;
        [Tooltip("Extra clearance between the sole and the ground, on top of the height Unity reports for this avatar. Raise it when feet sink into terrain, lower it when they hover.")]
        [SerializeField] float soleOffset = 0.02f;
        [Tooltip("Slope angle in degrees above which the ground is treated as a wall and the foot returns to its animated pose.")]
        [SerializeField] float maximumGroundAngle = 55f;
        [Tooltip("Animated lift above the model floor over which a foot is treated as swinging and released from the ground. Keeps walk cycles from being dragged flat.")]
        [SerializeField] float swingLift = 0.08f;

        [Header("Response")]
        [Tooltip("Furthest the hips may sink so the lower foot can reach the ground. Beyond this the character crouches unnaturally on steep terrain.")]
        [SerializeField] float maximumPelvisDrop = 0.35f;
        [Tooltip("How quickly a foot settles onto a new ground height. Higher tracks bumps faster; lower rides over small debris.")]
        [SerializeField] float footResponsiveness = 16f;
        [Tooltip("How quickly the hips follow the lower foot. Keep it below the foot response so the body lags the feet rather than leading them.")]
        [SerializeField] float pelvisResponsiveness = 10f;
        [Tooltip("How quickly the whole solver fades in and out when the character leaves or regains the ground.")]
        [SerializeField] float weightResponsiveness = 8f;

        struct FootPlant
        {
            public Vector3 Target;
            public Vector3 Normal;
            public float Drop;
            public float Weight;
        }

        FootPlant leftPlant;
        FootPlant rightPlant;
        float pelvisDrop;
        float currentWeight;

        void Reset()
        {
            ResolveBindings();
        }

        void Awake()
        {
            ResolveBindings();
        }

        void ResolveBindings()
        {
            animator ??= GetComponent<Animator>();
            motor ??= GetComponentInParent<ExplorerMotor>();
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (motor == null || animator == null || !animator.isHuman)
            {
                return;
            }

            ExplorerMotorState state = motor.CaptureState();
            float deltaTime = Time.deltaTime;
            bool planted =
                state.Grounded && !state.Swimming;
            currentWeight = FarionMath.Smooth(
                currentWeight,
                planted ? 1f : 0f,
                weightResponsiveness,
                deltaTime);

            if (currentWeight <= 0.001f)
            {
                ReleaseGoals();
                return;
            }

            Vector3 up = state.LocalUp.sqrMagnitude > 0.0001f
                ? state.LocalUp.normalized
                : transform.up;
            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();

            Plant(
                physicsScene,
                up,
                AvatarIKGoal.LeftFoot,
                animator.leftFeetBottomHeight,
                deltaTime,
                ref leftPlant);
            Plant(
                physicsScene,
                up,
                AvatarIKGoal.RightFoot,
                animator.rightFeetBottomHeight,
                deltaTime,
                ref rightPlant);

            float desiredPelvisDrop = Mathf.Clamp(
                Mathf.Min(Mathf.Min(leftPlant.Drop * leftPlant.Weight, rightPlant.Drop * rightPlant.Weight), 0f),
                -Mathf.Abs(maximumPelvisDrop),
                0f);
            pelvisDrop = FarionMath.Smooth(
                pelvisDrop,
                desiredPelvisDrop,
                pelvisResponsiveness,
                deltaTime);
            animator.bodyPosition += up * (pelvisDrop * currentWeight);

            ApplyGoal(AvatarIKGoal.LeftFoot, up, leftPlant);
            ApplyGoal(AvatarIKGoal.RightFoot, up, rightPlant);
        }

        void Plant(
            PhysicsScene physicsScene,
            Vector3 up,
            AvatarIKGoal goal,
            float soleHeight,
            float deltaTime,
            ref FootPlant plant)
        {
            Vector3 ankle = animator.GetIKPosition(goal);
            float lift = Vector3.Dot(ankle - animator.transform.position, up) - soleHeight;
            float plantedWeight = 1f - Mathf.Clamp01(lift / Mathf.Max(0.001f, swingLift));
            float drop = 0f;
            Vector3 normal = up;
            if (physicsScene.Raycast(
                    ankle + up * probeRise,
                    -up,
                    out RaycastHit hit,
                    probeRise + probeDepth,
                    FarionLayers.GroundMask,
                    QueryTriggerInteraction.Ignore) &&
                Vector3.Angle(hit.normal, up) <= maximumGroundAngle)
            {
                Vector3 target = hit.point + up * (soleHeight + soleOffset);
                drop = Vector3.Dot(target - ankle, up);
                normal = hit.normal;
            }

            float blend = FarionMath.SmoothFactor(footResponsiveness, deltaTime);
            plant.Weight = Mathf.Lerp(plant.Weight, plantedWeight, blend);
            plant.Drop = Mathf.Lerp(plant.Drop, drop, blend);
            plant.Normal = plant.Normal.sqrMagnitude > 0.0001f
                ? Vector3.Slerp(plant.Normal, normal, blend)
                : normal;
            plant.Target = ankle + up * plant.Drop;
        }

        void ApplyGoal(AvatarIKGoal goal, Vector3 up, in FootPlant plant)
        {
            float weight = currentWeight * plant.Weight;
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight);
            animator.SetIKPosition(goal, plant.Target);
            animator.SetIKRotation(
                goal,
                Quaternion.FromToRotation(up, plant.Normal) *
                animator.GetIKRotation(goal));
        }

        void ReleaseGoals()
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            leftPlant.Drop = 0f;
            rightPlant.Drop = 0f;
            leftPlant.Weight = 0f;
            rightPlant.Weight = 0f;
            pelvisDrop = 0f;
        }

    }
}
