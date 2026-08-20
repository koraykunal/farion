using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Gameplay.Character
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Character/First Person Motor Profile", fileName = "SO_FirstPersonMotorProfile")]
    public sealed class FirstPersonMotorProfile : ScriptableObject
    {
        [Header("Movement")]
        [Min(0f)]
        [SerializeField] float walkSpeed = 5f;
        [Min(0f)]
        [SerializeField] float sprintSpeed = 8f;
        [Min(0f)]
        [SerializeField] float groundAcceleration = 28f;
        [Min(0f)]
        [SerializeField] float airAcceleration = 8f;
        [Min(0f)]
        [SerializeField] float brakingAcceleration = 16f;
        [Min(0f)]
        [SerializeField] float steepSlopeControlAcceleration = 16f;
        [Min(0f)]
        [SerializeField] float steepSlopeSlideAcceleration = 14f;

        [Header("Water")]
        [Min(0f)]
        [SerializeField] float underwaterMoveSpeed = 2.8f;
        [Min(0f)]
        [SerializeField] float underwaterAcceleration = 10f;
        [Min(0f)]
        [SerializeField] float underwaterAscendAcceleration = 14f;
        [Range(0f, 1f)]
        [SerializeField] float underwaterGravityScale = 0.35f;
        [Min(0f)]
        [SerializeField] float underwaterLinearDrag = 4f;

        [Header("Jump")]
        [Min(0f)]
        [SerializeField] float jumpHeight = 1.4f;
        [Min(0f)]
        [FormerlySerializedAs("jumpSpeed")]
        [SerializeField] float maximumJumpSpeed = 5f;
        [Min(0f)]
        [SerializeField] float minimumJumpSpeed = 1.2f;
        [Range(0f, 1f)]
        [SerializeField] float maxJumpEscapeSpeedRatio = 0.45f;
        [Min(0f)]
        [SerializeField] float jumpCooldown = 0.15f;
        [Min(0f)]
        [SerializeField] float jumpBufferTime = 0.12f;
        [Min(0f)]
        [SerializeField] float coyoteTime = 0.1f;
        [Min(0f)]
        [SerializeField] float postJumpGroundingSuppressionTime = 0.16f;

        [Header("Grounding")]
        [Range(0f, 89f)]
        [SerializeField] float maxWalkableSlopeAngle = 48f;
        [Min(0.01f)]
        [SerializeField] float groundProbeDistance = 0.35f;
        [Min(0.01f)]
        [SerializeField] float groundProbeRadius = 0.32f;
        [Min(0f)]
        [SerializeField] float groundStickAcceleration = 18f;
        [Min(0f)]
        [SerializeField] float groundStickGravityMultiplier = 2f;
        [Min(0f)]
        [SerializeField] float groundedVerticalDamping = 18f;
        [Min(0f)]
        [SerializeField] float groundNormalResponsiveness = 22f;

        [Header("Orientation")]
        [Min(0f)]
        [SerializeField] float yawDegreesPerMouseUnit = 3f;
        [Min(0f)]
        [SerializeField] float uprightResponsiveness = 14f;

        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float GroundAcceleration => groundAcceleration;
        public float AirAcceleration => airAcceleration;
        public float BrakingAcceleration => brakingAcceleration;
        public float SteepSlopeControlAcceleration => steepSlopeControlAcceleration;
        public float SteepSlopeSlideAcceleration => steepSlopeSlideAcceleration;
        public float UnderwaterMoveSpeed => underwaterMoveSpeed;
        public float UnderwaterAcceleration => underwaterAcceleration;
        public float UnderwaterAscendAcceleration => underwaterAscendAcceleration;
        public float UnderwaterGravityScale => underwaterGravityScale;
        public float UnderwaterLinearDrag => underwaterLinearDrag;
        public float JumpHeight => jumpHeight;
        public float MaximumJumpSpeed => maximumJumpSpeed;
        public float MinimumJumpSpeed => minimumJumpSpeed;
        public float MaxJumpEscapeSpeedRatio => maxJumpEscapeSpeedRatio;
        public float JumpCooldown => jumpCooldown;
        public float JumpBufferTime => jumpBufferTime;
        public float CoyoteTime => coyoteTime;
        public float PostJumpGroundingSuppressionTime => postJumpGroundingSuppressionTime;
        public float MaxWalkableSlopeAngle => maxWalkableSlopeAngle;
        public float GroundProbeDistance => groundProbeDistance;
        public float GroundProbeRadius => groundProbeRadius;
        public float GroundStickAcceleration => groundStickAcceleration;
        public float GroundStickGravityMultiplier => groundStickGravityMultiplier;
        public float GroundedVerticalDamping => groundedVerticalDamping;
        public float GroundNormalResponsiveness => groundNormalResponsiveness;
        public float YawDegreesPerMouseUnit => yawDegreesPerMouseUnit;
        public float UprightResponsiveness => uprightResponsiveness;

        void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed);
            groundAcceleration = Mathf.Max(0f, groundAcceleration);
            airAcceleration = Mathf.Max(0f, airAcceleration);
            brakingAcceleration = Mathf.Max(0f, brakingAcceleration);
            steepSlopeControlAcceleration = Mathf.Max(0f, steepSlopeControlAcceleration);
            steepSlopeSlideAcceleration = Mathf.Max(0f, steepSlopeSlideAcceleration);
            underwaterMoveSpeed = Mathf.Max(0f, underwaterMoveSpeed);
            underwaterAcceleration = Mathf.Max(0f, underwaterAcceleration);
            underwaterAscendAcceleration = Mathf.Max(0f, underwaterAscendAcceleration);
            underwaterGravityScale = Mathf.Clamp01(underwaterGravityScale);
            underwaterLinearDrag = Mathf.Max(0f, underwaterLinearDrag);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            maximumJumpSpeed = Mathf.Max(0f, maximumJumpSpeed);
            minimumJumpSpeed = Mathf.Max(0f, minimumJumpSpeed);
            maxJumpEscapeSpeedRatio = Mathf.Clamp01(maxJumpEscapeSpeedRatio);
            jumpCooldown = Mathf.Max(0f, jumpCooldown);
            jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
            coyoteTime = Mathf.Max(0f, coyoteTime);
            postJumpGroundingSuppressionTime = Mathf.Max(0f, postJumpGroundingSuppressionTime);
            maxWalkableSlopeAngle = Mathf.Clamp(maxWalkableSlopeAngle, 0f, 89f);
            groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
            groundProbeRadius = Mathf.Max(0.01f, groundProbeRadius);
            groundStickAcceleration = Mathf.Max(0f, groundStickAcceleration);
            groundStickGravityMultiplier = Mathf.Max(0f, groundStickGravityMultiplier);
            groundedVerticalDamping = Mathf.Max(0f, groundedVerticalDamping);
            groundNormalResponsiveness = Mathf.Max(0f, groundNormalResponsiveness);
            yawDegreesPerMouseUnit = Mathf.Max(0f, yawDegreesPerMouseUnit);
            uprightResponsiveness = Mathf.Max(0f, uprightResponsiveness);
        }
    }
}
