using Farion.Gameplay.Actors;
using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(CelestialActorProbe))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        const int MaxGroundHits = 8;

        [Header("Profile")]
        [SerializeField] FirstPersonMotorProfile profile;

        [Header("Input")]
        [SerializeField] MonoBehaviour inputSource;

        [Header("View")]
        [SerializeField] Transform viewReference;

        [Header("Gravity")]
        [SerializeField] bool applyCelestialGravity = true;

        [Header("Runtime Movement")]
        [SerializeField] bool grounded;
        [SerializeField] bool walkableGround;
        [SerializeField] float groundSlopeAngle;
        [SerializeField] float surfaceSpeed;
        [SerializeField] float verticalSpeed;
        [SerializeField] bool touchingWater;
        [SerializeField] bool underwater;
        [SerializeField] float waterDepth;
        [SerializeField] float waterSubmergedFraction;
        [SerializeField] Vector3 localUp = Vector3.up;
        [SerializeField] Vector3 groundNormal = Vector3.up;

        readonly RaycastHit[] groundHits = new RaycastHit[MaxGroundHits];
        Rigidbody cachedRigidbody;
        CapsuleCollider cachedCapsule;
        CelestialActorProbe actorProbe;
        IFirstPersonInputSource resolvedInput;
        FirstPersonInputState currentInput;
        bool jumpQueued;
        bool previousJumpHeld;
        float lastJumpTime = float.NegativeInfinity;
        float lastJumpRequestTime = float.NegativeInfinity;
        float lastWalkableGroundTime = float.NegativeInfinity;
        float pendingYawDegrees;
        Vector3 smoothedGroundNormal = Vector3.up;
        bool hasSmoothedGroundNormal;
        ArtificialGravityVolume artificialGravitySource;

        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();
        public CapsuleCollider Capsule => cachedCapsule != null ? cachedCapsule : cachedCapsule = GetComponent<CapsuleCollider>();
        public CelestialActorProbe ActorProbe => actorProbe != null ? actorProbe : actorProbe = GetComponent<CelestialActorProbe>();
        public bool Grounded => grounded;
        public bool WalkableGround => walkableGround;
        public Vector3 LocalUp => localUp;
        public Vector3 GroundNormal => groundNormal;

        void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            cachedCapsule = GetComponent<CapsuleCollider>();
            actorProbe = GetComponent<CelestialActorProbe>();
            ConfigureRigidbody();
            ResolveInputSource();
        }

        void OnValidate()
        {
            if (inputSource != null && inputSource is not IFirstPersonInputSource)
            {
                inputSource = null;
            }
        }

        void Update()
        {
            ResolveInputSource();
            currentInput = resolvedInput?.CurrentInput ?? FirstPersonInputState.None;
            if (profile != null)
            {
                pendingYawDegrees += currentInput.Look.x * profile.YawDegreesPerMouseUnit;
            }

            if (currentInput.Jump && !previousJumpHeld)
            {
                jumpQueued = true;
                lastJumpRequestTime = Time.time;
            }

            previousJumpHeld = currentInput.Jump;
        }

        void FixedUpdate()
        {
            CelestialFrameSample celestialFrame = ActorProbe.CurrentSample;
            bool hasArtificialGravity = artificialGravitySource != null;
            Vector3 gravityAcceleration = hasArtificialGravity
                ? artificialGravitySource.GravityAcceleration
                : celestialFrame.GravityAcceleration;
            Vector3 referenceVelocity = hasArtificialGravity
                ? artificialGravitySource.ReferenceVelocityAt(Rigidbody.position)
                : (celestialFrame.HasBody ? celestialFrame.BodyPointVelocity : Vector3.zero);
            localUp = hasArtificialGravity
                ? artificialGravitySource.Up
                : (celestialFrame.HasBody ? celestialFrame.LocalUp : transform.up);
            CelestialFrameSample environmentFrame = hasArtificialGravity
                ? CelestialFrameSample.Empty(Rigidbody.position, Rigidbody.linearVelocity)
                : celestialFrame;
            RefreshWaterState(environmentFrame);

            RefreshGrounding(localUp);
            ApplyGravity(gravityAcceleration, hasArtificialGravity || (applyCelestialGravity && celestialFrame.HasBody));
            StabilizeGroundContact(gravityAcceleration, referenceVelocity, localUp);
            ApplyMovement(referenceVelocity, localUp);
            ApplySteepSlopeSlide(gravityAcceleration);
            ApplyWaterForces(environmentFrame, localUp);
            ApplyJump(gravityAcceleration, referenceVelocity, celestialFrame, hasArtificialGravity, localUp);
            ApplyOrientation(localUp);
        }

        public void SetInputSource(IFirstPersonInputSource source)
        {
            resolvedInput = source;
            inputSource = source as MonoBehaviour;
        }

        public void SetArtificialGravitySource(ArtificialGravityVolume source)
        {
            artificialGravitySource = source;
        }

        public void ClearArtificialGravitySource(ArtificialGravityVolume source)
        {
            if (artificialGravitySource == source)
            {
                artificialGravitySource = null;
            }
        }

        public void ResetMotorState()
        {
            currentInput = FirstPersonInputState.None;
            jumpQueued = false;
            previousJumpHeld = false;
            pendingYawDegrees = 0f;
            lastJumpRequestTime = float.NegativeInfinity;
            lastWalkableGroundTime = float.NegativeInfinity;
            grounded = false;
            walkableGround = false;
            surfaceSpeed = 0f;
            verticalSpeed = 0f;
            touchingWater = false;
            underwater = false;
            waterDepth = 0f;
            waterSubmergedFraction = 0f;
            smoothedGroundNormal = Vector3.up;
            hasSmoothedGroundNormal = false;

            if (cachedRigidbody != null)
            {
                cachedRigidbody.angularVelocity = Vector3.zero;
            }
        }

        void ConfigureRigidbody()
        {
            Rigidbody.useGravity = false;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Rigidbody.freezeRotation = true;
        }

        void ResolveInputSource()
        {
            if (inputSource is IFirstPersonInputSource explicitSource)
            {
                resolvedInput = explicitSource;
                return;
            }

            resolvedInput ??= GetComponent<IFirstPersonInputSource>();
        }

        void RefreshGrounding(Vector3 up)
        {
            grounded = false;
            walkableGround = false;
            groundSlopeAngle = 0f;
            groundNormal = up;
            Vector3 detectedGroundNormal = up;

            if (profile == null)
            {
                hasSmoothedGroundNormal = false;
                return;
            }

            if (Time.time - lastJumpTime <= profile.PostJumpGroundingSuppressionTime)
            {
                hasSmoothedGroundNormal = false;
                return;
            }

            float capsuleHalfHeight = Mathf.Max(Capsule.height * 0.5f, Capsule.radius);
            Vector3 castOrigin = Rigidbody.position + up * Mathf.Max(0.02f, Capsule.radius * 0.25f);
            float castDistance = capsuleHalfHeight + profile.GroundProbeDistance;
            int hitCount = Physics.SphereCastNonAlloc(
                castOrigin,
                profile.GroundProbeRadius,
                -up,
                groundHits,
                castDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || hit.collider == Capsule)
                {
                    continue;
                }

                if (hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                detectedGroundNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : up;
            }

            grounded = closestDistance < float.PositiveInfinity;
            if (!grounded)
            {
                hasSmoothedGroundNormal = false;
                return;
            }

            groundNormal = SmoothGroundNormal(detectedGroundNormal, up);
            groundSlopeAngle = Vector3.Angle(up, groundNormal);
            walkableGround = groundSlopeAngle <= profile.MaxWalkableSlopeAngle;
            if (walkableGround)
            {
                lastWalkableGroundTime = Time.time;
            }
        }

        void ApplyGravity(Vector3 gravityAcceleration, bool shouldApply)
        {
            if (!shouldApply)
            {
                return;
            }

            float gravityScale = profile != null
                ? Mathf.Lerp(1f, profile.UnderwaterGravityScale, Smooth01(waterSubmergedFraction))
                : 1f;
            Rigidbody.AddForce(gravityAcceleration * gravityScale, ForceMode.Acceleration);
        }

        void ApplyMovement(Vector3 referenceVelocity, Vector3 up)
        {
            if (profile == null)
            {
                return;
            }

            Vector3 movementPlaneNormal = ResolveMovementPlaneNormal(up);
            Vector3 desiredDirection = BuildMoveDirection(movementPlaneNormal);
            float waterControl = Smooth01(waterSubmergedFraction);
            float drySpeed = currentInput.Sprint ? profile.SprintSpeed : profile.WalkSpeed;
            float speed = Mathf.Lerp(drySpeed, profile.UnderwaterMoveSpeed, waterControl);
            Vector3 desiredSurfaceVelocity = desiredDirection * speed;
            Vector3 relativeVelocity = Rigidbody.linearVelocity - referenceVelocity;
            Vector3 currentSurfaceVelocity = Vector3.ProjectOnPlane(relativeVelocity, movementPlaneNormal);
            verticalSpeed = Vector3.Dot(relativeVelocity, up);
            surfaceSpeed = currentSurfaceVelocity.magnitude;

            float dryAcceleration = grounded && walkableGround
                ? (desiredDirection.sqrMagnitude > 0.0001f ? profile.GroundAcceleration : profile.BrakingAcceleration)
                : (grounded ? profile.SteepSlopeControlAcceleration : profile.AirAcceleration);
            float acceleration = Mathf.Lerp(dryAcceleration, profile.UnderwaterAcceleration, waterControl);

            if (acceleration <= 0f)
            {
                return;
            }

            Vector3 velocityDelta = desiredSurfaceVelocity - currentSurfaceVelocity;
            Vector3 accelerationVector = Vector3.ClampMagnitude(
                velocityDelta / Mathf.Max(Time.fixedDeltaTime, 0.0001f),
                acceleration);

            Rigidbody.AddForce(accelerationVector, ForceMode.Acceleration);
        }

        void ApplySteepSlopeSlide(Vector3 gravityAcceleration)
        {
            if (profile == null || !grounded || walkableGround || waterSubmergedFraction >= 0.5f)
            {
                return;
            }

            if (profile.SteepSlopeSlideAcceleration <= 0f)
            {
                return;
            }

            Vector3 gravity = gravityAcceleration.sqrMagnitude > 0.0001f
                ? gravityAcceleration
                : -localUp * profile.SteepSlopeSlideAcceleration;
            Vector3 slideDirection = Vector3.ProjectOnPlane(gravity, groundNormal);
            if (slideDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Rigidbody.AddForce(
                slideDirection.normalized * profile.SteepSlopeSlideAcceleration,
                ForceMode.Acceleration);
        }

        void StabilizeGroundContact(
            Vector3 gravityAcceleration,
            Vector3 referenceVelocity,
            Vector3 up)
        {
            if (profile == null || !grounded || !walkableGround || waterSubmergedFraction >= 0.5f)
            {
                return;
            }

            Vector3 relativeVelocity = Rigidbody.linearVelocity - referenceVelocity;
            float currentVerticalSpeed = Vector3.Dot(relativeVelocity, up);
            verticalSpeed = currentVerticalSpeed;

            if (!jumpQueued && profile.GroundedVerticalDamping > 0f && Mathf.Abs(currentVerticalSpeed) > 0.001f)
            {
                float damping = 1f - Mathf.Exp(-profile.GroundedVerticalDamping * Time.fixedDeltaTime);
                Rigidbody.AddForce(-up * (currentVerticalSpeed * damping), ForceMode.VelocityChange);
            }

            float stickAcceleration = CalculateGroundStickAcceleration(gravityAcceleration);
            if (!jumpQueued && stickAcceleration > 0f)
            {
                Rigidbody.AddForce(-up * stickAcceleration, ForceMode.Acceleration);
            }
        }

        float CalculateGroundStickAcceleration(Vector3 gravityAcceleration)
        {
            float acceleration = profile != null ? profile.GroundStickAcceleration : 0f;
            if (acceleration <= 0f || profile == null || profile.GroundStickGravityMultiplier <= 0f)
            {
                return acceleration;
            }

            float gravityMagnitude = gravityAcceleration.magnitude;
            if (gravityMagnitude <= 0.0001f)
            {
                return acceleration;
            }

            return Mathf.Min(acceleration, gravityMagnitude * profile.GroundStickGravityMultiplier);
        }

        void ApplyJump(
            Vector3 gravityAcceleration,
            Vector3 referenceVelocity,
            CelestialFrameSample celestialFrame,
            bool hasArtificialGravity,
            Vector3 up)
        {
            if (profile == null)
            {
                jumpQueued = false;
                return;
            }

            if (!jumpQueued || Time.time - lastJumpRequestTime > profile.JumpBufferTime)
            {
                jumpQueued = false;
                return;
            }

            if (waterSubmergedFraction >= 0.35f)
            {
                jumpQueued = false;
                return;
            }

            bool canJump = Time.time - lastWalkableGroundTime <= profile.CoyoteTime &&
                Time.time - lastJumpTime >= profile.JumpCooldown;

            if (canJump)
            {
                Vector3 relativeVelocity = Rigidbody.linearVelocity - referenceVelocity;
                Vector3 tangentialVelocity = Vector3.ProjectOnPlane(relativeVelocity, up);
                float jumpSpeed = CalculateJumpSpeed(
                    gravityAcceleration,
                    celestialFrame,
                    hasArtificialGravity);
                float upwardSpeed = Mathf.Max(Vector3.Dot(relativeVelocity, up), jumpSpeed);
                Rigidbody.linearVelocity = referenceVelocity + tangentialVelocity + up * upwardSpeed;
                lastJumpTime = Time.time;
                grounded = false;
                walkableGround = false;
                hasSmoothedGroundNormal = false;
            }

            jumpQueued = false;
        }

        float CalculateJumpSpeed(
            Vector3 gravityAcceleration,
            CelestialFrameSample celestialFrame,
            bool hasArtificialGravity)
        {
            float gravityMagnitude = gravityAcceleration.magnitude;
            float speed = profile.MaximumJumpSpeed;

            if (gravityMagnitude > 0.0001f && profile.JumpHeight > 0f)
            {
                speed = Mathf.Sqrt(2f * gravityMagnitude * profile.JumpHeight);
            }

            speed = Mathf.Max(speed, profile.MinimumJumpSpeed);
            speed = Mathf.Min(speed, profile.MaximumJumpSpeed);

            if (!hasArtificialGravity &&
                celestialFrame.HasBody &&
                profile.MaxJumpEscapeSpeedRatio > 0f &&
                gravityMagnitude > 0.0001f)
            {
                float orbitalRadius = Mathf.Max(
                    celestialFrame.CenterDistance,
                    celestialFrame.BodyRadius);
                if (orbitalRadius > 0.0001f)
                {
                    float escapeSpeed = Mathf.Sqrt(2f * gravityMagnitude * orbitalRadius);
                    speed = Mathf.Min(speed, escapeSpeed * profile.MaxJumpEscapeSpeedRatio);
                }
            }

            return Mathf.Max(0f, speed);
        }

        Vector3 SmoothGroundNormal(Vector3 targetNormal, Vector3 fallbackUp)
        {
            if (targetNormal.sqrMagnitude <= 0.0001f)
            {
                targetNormal = fallbackUp;
            }

            targetNormal = targetNormal.normalized;
            if (profile == null || profile.GroundNormalResponsiveness <= 0f || !hasSmoothedGroundNormal)
            {
                smoothedGroundNormal = targetNormal;
                hasSmoothedGroundNormal = true;
                return smoothedGroundNormal;
            }

            float t = 1f - Mathf.Exp(-profile.GroundNormalResponsiveness * Time.fixedDeltaTime);
            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, targetNormal, t).normalized;
            return smoothedGroundNormal.sqrMagnitude > 0.0001f ? smoothedGroundNormal : targetNormal;
        }

        void ApplyOrientation(Vector3 up)
        {
            Rigidbody.angularVelocity = Vector3.zero;
            Vector3 forward = Vector3.ProjectOnPlane(Rigidbody.rotation * Vector3.forward, up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            if (Mathf.Abs(pendingYawDegrees) > 0.0001f)
            {
                forward = Quaternion.AngleAxis(pendingYawDegrees, up) * forward;
                pendingYawDegrees = 0f;
            }

            Quaternion targetRotation = Quaternion.LookRotation(forward, up);
            if (profile == null || profile.UprightResponsiveness <= 0f)
            {
                Rigidbody.MoveRotation(targetRotation);
                return;
            }

            float t = 1f - Mathf.Exp(-profile.UprightResponsiveness * Time.fixedDeltaTime);
            Rigidbody.MoveRotation(Quaternion.Slerp(Rigidbody.rotation, targetRotation, t));
        }

        Vector3 BuildMoveDirection(Vector3 up)
        {
            Vector3 forwardSource = viewReference != null ? viewReference.forward : transform.forward;
            Vector3 forward = Vector3.ProjectOnPlane(forwardSource, up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 move = right * currentInput.Movement.x + forward * currentInput.Movement.y;
            return move.sqrMagnitude > 0.0001f ? Vector3.ClampMagnitude(move, 1f) : Vector3.zero;
        }

        Vector3 ResolveMovementPlaneNormal(Vector3 up)
        {
            if (grounded && waterSubmergedFraction < 0.5f && groundNormal.sqrMagnitude > 0.0001f)
            {
                return groundNormal;
            }

            return up;
        }

        void RefreshWaterState(CelestialFrameSample frame)
        {
            touchingWater = false;
            underwater = false;
            waterDepth = 0f;
            waterSubmergedFraction = 0f;

            if (profile == null || !frame.HasOcean)
            {
                return;
            }

            float capsuleHalfHeight = Mathf.Max(Capsule.height * 0.5f, Capsule.radius);
            float immersionDepth = Mathf.Clamp(capsuleHalfHeight - frame.OceanAltitude, 0f, capsuleHalfHeight * 2f);
            waterSubmergedFraction = Mathf.Clamp01(immersionDepth / (capsuleHalfHeight * 2f));
            touchingWater = waterSubmergedFraction > 0f;
            underwater = frame.IsBelowOceanLevel;
            waterDepth = frame.WaterDepth;
        }

        void ApplyWaterForces(CelestialFrameSample frame, Vector3 up)
        {
            if (profile == null || waterSubmergedFraction <= 0f)
            {
                return;
            }

            float waterControl = Smooth01(waterSubmergedFraction);
            Vector3 bodyVelocity = frame.HasBody ? frame.BodyPointVelocity : Vector3.zero;
            Vector3 relativeVelocity = Rigidbody.linearVelocity - bodyVelocity;

            if (profile.UnderwaterLinearDrag > 0f)
            {
                Rigidbody.AddForce(-relativeVelocity * (profile.UnderwaterLinearDrag * waterControl), ForceMode.Acceleration);
            }

            if (currentInput.Jump && profile.UnderwaterAscendAcceleration > 0f)
            {
                Rigidbody.AddForce(up * (profile.UnderwaterAscendAcceleration * waterControl), ForceMode.Acceleration);
            }
        }

        static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
