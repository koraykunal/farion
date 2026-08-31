using Farion.Core.Numerics;
using Farion.Core.Physics;
using Farion.Gameplay.Actors;
using Farion.Simulation.Celestial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Gameplay.Character
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(CelestialActorProbe))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        public const float MaximumViewPitchDegrees = 89f;

        const int MaxGroundHits = 16;
        const float ForwardSprintThreshold = 0.5f;
        const float ProbeRadiusRatio = 0.9f;
        const float ProbeClearanceRatio = 0.1f;
        const float MaximumPenetrationCorrectionRatio = 2f;
        const float MaximumSubmergedFractionForJump = 0.35f;

        [Header("Profile")]
        [SerializeField] FirstPersonMotorProfile profile;

        [Header("Input")]
        [SerializeField] KeyboardFirstPersonInput inputSource;

        [Header("View")]
        [SerializeField] Transform viewReference;

        [Header("Gravity")]
        [SerializeField] bool applyCelestialGravity = true;

        [Header("Runtime Movement")]
        [SerializeField] bool grounded;
        [SerializeField] bool walkableGround;
        [SerializeField] float groundSlopeAngle;
        [SerializeField] float surfaceSpeed;
        [SerializeField] Vector3 surfaceVelocity;
        [SerializeField] float verticalSpeed;
        [SerializeField] bool touchingWater;
        [SerializeField] bool underwater;
        [SerializeField] float waterDepth;
        [SerializeField] float waterSubmergedFraction;
        [SerializeField] Vector3 localUp = Vector3.up;
        [SerializeField] Vector3 groundNormal = Vector3.up;
        [SerializeField] float viewPitchDegrees;

        readonly RaycastHit[] groundHits = new RaycastHit[MaxGroundHits];
        Rigidbody cachedRigidbody;
        CapsuleCollider cachedCapsule;
        CelestialActorProbe actorProbe;
        RigidbodyFirstPersonPhysicsBody offlinePhysicsBody;
        KeyboardFirstPersonInput resolvedInput;
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
        bool externalSimulation;
        float simulationTime;

        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();
        public CapsuleCollider Capsule => cachedCapsule != null ? cachedCapsule : cachedCapsule = GetComponent<CapsuleCollider>();
        public CelestialActorProbe ActorProbe => actorProbe != null ? actorProbe : actorProbe = GetComponent<CelestialActorProbe>();
        public FirstPersonMotorProfile Profile => profile;
        public bool Grounded => grounded;
        public bool WalkableGround => walkableGround;
        public Vector3 LocalUp => localUp;
        public Vector3 GroundNormal => groundNormal;
        public Vector3 SurfaceVelocity => surfaceVelocity;
        public float YawDegreesPerMouseUnit =>
            profile != null ? profile.YawDegreesPerMouseUnit : 0f;
        public float ViewPitchDegrees => viewPitchDegrees;

        public void SetViewPitchDegrees(float degrees)
        {
            viewPitchDegrees = Mathf.Clamp(
                float.IsFinite(degrees) ? degrees : 0f,
                -MaximumViewPitchDegrees,
                MaximumViewPitchDegrees);
        }

        void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            cachedCapsule = GetComponent<CapsuleCollider>();
            actorProbe = GetComponent<CelestialActorProbe>();
            offlinePhysicsBody = new RigidbodyFirstPersonPhysicsBody(cachedRigidbody);
            ConfigureRigidbody();
            ResolveInputSource();
        }

        void OnValidate()
        {
        }

        void Update()
        {
            if (externalSimulation)
            {
                return;
            }

            ResolveInputSource();
            currentInput = resolvedInput?.CurrentInput ?? FirstPersonInputState.None;
            if (profile != null)
            {
                pendingYawDegrees += currentInput.Look.x * profile.YawDegreesPerMouseUnit;
            }

            if (currentInput.Jump && !previousJumpHeld)
            {
                jumpQueued = true;
            }

            previousJumpHeld = currentInput.Jump;
        }

        void FixedUpdate()
        {
            if (externalSimulation)
            {
                return;
            }

            FirstPersonMotorInput input = new(
                currentInput.Movement,
                pendingYawDegrees,
                jumpQueued,
                currentInput.Sprint,
                currentInput.Jump);
            pendingYawDegrees = 0f;
            Simulate(input, Time.fixedDeltaTime, offlinePhysicsBody);
        }

        public void Simulate(
            FirstPersonMotorInput input,
            float deltaTime,
            IFirstPersonPhysicsBody physicsBody)
        {
            if (physicsBody == null || deltaTime <= 0f)
            {
                return;
            }

            simulationTime += deltaTime;
            currentInput = new FirstPersonInputState(
                input.Movement,
                Vector2.zero,
                input.Jump,
                input.Sprint,
                false);
            jumpQueued = input.Jump;
            if (jumpQueued)
            {
                lastJumpRequestTime = simulationTime;
            }

            CelestialFrameSample celestialFrame = ActorProbe.CurrentSample;
            bool hasArtificialGravity = artificialGravitySource != null;
            Vector3 gravityAcceleration = hasArtificialGravity
                ? artificialGravitySource.GravityAcceleration
                : celestialFrame.GravityAcceleration;
            Vector3 referenceVelocity = hasArtificialGravity
                ? artificialGravitySource.ReferenceVelocityAt(physicsBody.Position)
                : (celestialFrame.HasBody ? celestialFrame.BodyPointVelocity : Vector3.zero);
            localUp = hasArtificialGravity
                ? artificialGravitySource.Up
                : (celestialFrame.HasBody ? celestialFrame.LocalUp : transform.up);
            CelestialFrameSample environmentFrame = hasArtificialGravity
                ? CelestialFrameSample.Empty(physicsBody.Position, physicsBody.LinearVelocity)
                : celestialFrame;
            RefreshWaterState(environmentFrame);

            RefreshGrounding(
                localUp,
                physicsBody,
                celestialFrame,
                hasArtificialGravity,
                deltaTime);
            ResolveSurfacePenetration(
                physicsBody,
                celestialFrame,
                referenceVelocity,
                hasArtificialGravity,
                localUp,
                deltaTime);
            ApplyGravity(physicsBody, gravityAcceleration, hasArtificialGravity || (applyCelestialGravity && celestialFrame.HasBody));
            bool jumpLaunching = ShouldLaunchJump();
            StabilizeGroundContact(physicsBody, gravityAcceleration, referenceVelocity, localUp, jumpLaunching, deltaTime);
            ApplyMovement(physicsBody, referenceVelocity, localUp, input.Movement, input.Sprint, deltaTime);
            ApplyStandAnchor(
                physicsBody,
                celestialFrame,
                hasArtificialGravity,
                localUp,
                input.Movement,
                jumpLaunching,
                deltaTime);
            ApplySteepSlopeSlide(physicsBody, gravityAcceleration);
            ApplyWaterForces(physicsBody, environmentFrame, localUp, input.SwimAscend);
            ApplyJump(physicsBody, gravityAcceleration, referenceVelocity, celestialFrame, hasArtificialGravity, localUp, jumpLaunching);
            ApplyOrientation(physicsBody, localUp, input.YawDegrees, deltaTime);
            physicsBody.Commit();
        }

        public void SetInputSource(KeyboardFirstPersonInput source)
        {
            resolvedInput = source;
            inputSource = source;
        }

        public void SetViewReference(Transform reference)
        {
            viewReference = reference;
        }

        public void SetExternalSimulation(bool enabled)
        {
            externalSimulation = enabled;
            pendingYawDegrees = 0f;
            jumpQueued = false;
            previousJumpHeld = false;
        }

        const float StandAnchorReleaseDistance = 1.5f;
        const float StandAnchorRecoveryMetersPerSecond = 4f;

        bool hasStandAnchor;
        int standAnchorBodyId;
        Vector3 standAnchorLocalPosition;

        void ApplyStandAnchor(
            IFirstPersonPhysicsBody physicsBody,
            in CelestialFrameSample frame,
            bool hasArtificialGravity,
            Vector3 up,
            Vector2 movement,
            bool jumpLaunching,
            float deltaTime)
        {
            bool wantsAnchor = !hasArtificialGravity
                && frame.HasBody
                && grounded
                && walkableGround
                && !jumpLaunching
                && !jumpQueued
                && movement.sqrMagnitude <= 0.0001f
                && waterSubmergedFraction < 0.5f;
            if (!wantsAnchor)
            {
                hasStandAnchor = false;
                return;
            }

            Transform bodyTransform = frame.Body.transform;
            Vector3 position = physicsBody.Position;
            if (!hasStandAnchor || standAnchorBodyId != frame.Body.StableId)
            {
                hasStandAnchor = true;
                standAnchorBodyId = frame.Body.StableId;
                standAnchorLocalPosition = bodyTransform.InverseTransformPoint(position);
                return;
            }

            Vector3 anchorPosition = bodyTransform.TransformPoint(standAnchorLocalPosition);
            Vector3 tangentialDelta = Vector3.ProjectOnPlane(anchorPosition - position, up);
            float drift = tangentialDelta.magnitude;
            if (drift > StandAnchorReleaseDistance)
            {
                standAnchorLocalPosition = bodyTransform.InverseTransformPoint(position);
                return;
            }

            if (drift <= 0.0001f)
            {
                return;
            }

            float correction = Mathf.Min(drift, StandAnchorRecoveryMetersPerSecond * deltaTime);
            physicsBody.SetPosition(position + tangentialDelta / drift * correction);
        }

        public FirstPersonMotorState CaptureState()
        {
            return new FirstPersonMotorState
            {
                SimulationTime = simulationTime,
                LastJumpTime = lastJumpTime,
                LastJumpRequestTime = lastJumpRequestTime,
                LastWalkableGroundTime = lastWalkableGroundTime,
                JumpQueued = jumpQueued,
                Grounded = grounded,
                WalkableGround = walkableGround,
                GroundSlopeAngle = groundSlopeAngle,
                SurfaceSpeed = surfaceSpeed,
                SurfaceVelocity = surfaceVelocity,
                VerticalSpeed = verticalSpeed,
                TouchingWater = touchingWater,
                Underwater = underwater,
                WaterDepth = waterDepth,
                WaterSubmergedFraction = waterSubmergedFraction,
                LocalUp = localUp,
                GroundNormal = groundNormal,
                SmoothedGroundNormal = smoothedGroundNormal,
                HasSmoothedGroundNormal = hasSmoothedGroundNormal,
                HasStandAnchor = hasStandAnchor,
                StandAnchorBodyId = standAnchorBodyId,
                StandAnchorLocalPosition = standAnchorLocalPosition
            };
        }

        public void RestoreState(FirstPersonMotorState state)
        {
            simulationTime = state.SimulationTime;
            lastJumpTime = state.LastJumpTime;
            lastJumpRequestTime = state.LastJumpRequestTime;
            lastWalkableGroundTime = state.LastWalkableGroundTime;
            jumpQueued = state.JumpQueued;
            grounded = state.Grounded;
            walkableGround = state.WalkableGround;
            groundSlopeAngle = state.GroundSlopeAngle;
            surfaceSpeed = state.SurfaceSpeed;
            surfaceVelocity = state.SurfaceVelocity;
            verticalSpeed = state.VerticalSpeed;
            touchingWater = state.TouchingWater;
            underwater = state.Underwater;
            waterDepth = state.WaterDepth;
            waterSubmergedFraction = state.WaterSubmergedFraction;
            localUp = state.LocalUp;
            groundNormal = state.GroundNormal;
            smoothedGroundNormal = state.SmoothedGroundNormal;
            hasSmoothedGroundNormal = state.HasSmoothedGroundNormal;
            hasStandAnchor = state.HasStandAnchor;
            standAnchorBodyId = state.StandAnchorBodyId;
            standAnchorLocalPosition = state.StandAnchorLocalPosition;
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
            lastJumpTime = float.NegativeInfinity;
            simulationTime = 0f;
            grounded = false;
            walkableGround = false;
            surfaceSpeed = 0f;
            surfaceVelocity = Vector3.zero;
            verticalSpeed = 0f;
            touchingWater = false;
            underwater = false;
            waterDepth = 0f;
            waterSubmergedFraction = 0f;
            viewPitchDegrees = 0f;
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
            if (inputSource != null)
            {
                resolvedInput = inputSource;
                return;
            }

            resolvedInput ??= GetComponent<KeyboardFirstPersonInput>();
        }

        void RefreshGrounding(
            Vector3 up,
            IFirstPersonPhysicsBody physicsBody,
            CelestialFrameSample frame,
            bool hasArtificialGravity,
            float deltaTime)
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

            if (simulationTime - lastJumpTime <= profile.PostJumpGroundingSuppressionTime)
            {
                hasSmoothedGroundNormal = false;
                return;
            }

            float capsuleHalfHeight = Mathf.Max(Capsule.height * 0.5f, Capsule.radius);
            float probeRadius = Mathf.Min(
                profile.GroundProbeRadius,
                Capsule.radius * ProbeRadiusRatio);
            float probeClearance = probeRadius + Capsule.radius * ProbeClearanceRatio;
            float contactDistance =
                capsuleHalfHeight * 2f + probeClearance - probeRadius;
            Vector3 castOrigin =
                physicsBody.Position + up * (capsuleHalfHeight + probeClearance);
            float castDistance = contactDistance + profile.GroundProbeDistance;
            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
            int hitCount = physicsScene.SphereCast(
                castOrigin,
                probeRadius,
                -up,
                groundHits,
                castDistance,
                FarionLayers.GroundMask,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null ||
                    hit.collider.attachedRigidbody == Rigidbody ||
                    hit.distance <= 0f ||
                    hit.normal.sqrMagnitude <= 0.0001f ||
                    hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                detectedGroundNormal = hit.normal.normalized;
            }

            bool physicalGroundHitDetected = closestDistance < float.PositiveInfinity;
            float closestGap = physicalGroundHitDetected
                ? closestDistance - contactDistance
                : float.PositiveInfinity;

            if (!hasArtificialGravity &&
                !physicalGroundHitDetected &&
                TrySampleSurfaceContact(
                    physicsBody.Position,
                    frame,
                    up,
                    out float surfaceGap,
                    out Vector3 surfaceNormal) &&
                surfaceGap < closestGap)
            {
                closestGap = surfaceGap;
                detectedGroundNormal = surfaceNormal;
            }

            grounded = closestGap <= profile.GroundProbeDistance;
            if (!grounded)
            {
                hasSmoothedGroundNormal = false;
                return;
            }

            groundNormal = SmoothGroundNormal(detectedGroundNormal, up, deltaTime);
            groundSlopeAngle = Vector3.Angle(up, groundNormal);
            walkableGround = groundSlopeAngle <= profile.MaxWalkableSlopeAngle;
            if (walkableGround)
            {
                lastWalkableGroundTime = simulationTime;
            }
        }

        bool TrySampleSurfaceContact(
            Vector3 position,
            CelestialFrameSample frame,
            Vector3 up,
            out float gap,
            out Vector3 normal)
        {
            gap = float.PositiveInfinity;
            normal = up;
            if (!frame.HasBody)
            {
                return false;
            }

            normal = frame.SurfaceNormal.sqrMagnitude > 0.0001f
                ? frame.SurfaceNormal.normalized
                : up;
            gap = Vector3.Dot(position - frame.SurfacePoint, normal) -
                CalculateCapsuleSupportOffset(up, normal);
            return true;
        }

        float CalculateCapsuleSupportOffset(Vector3 up, Vector3 normal)
        {
            float radius = Capsule.radius;
            float cylinderHalfHeight = Mathf.Max(0f, Capsule.height * 0.5f - radius);
            return cylinderHalfHeight * Mathf.Abs(Vector3.Dot(up, normal)) + radius;
        }

        void ResolveSurfacePenetration(
            IFirstPersonPhysicsBody physicsBody,
            CelestialFrameSample frame,
            Vector3 referenceVelocity,
            bool hasArtificialGravity,
            Vector3 up,
            float deltaTime)
        {
            float slop = profile != null ? profile.SurfacePenetrationSlop : 0f;
            if (hasArtificialGravity ||
                !TrySampleSurfaceContact(
                    physicsBody.Position,
                    frame,
                    up,
                    out float gap,
                    out Vector3 normal) ||
                gap >= -slop)
            {
                return;
            }

            float maximumCorrection = Capsule.radius * MaximumPenetrationCorrectionRatio;
            if (-gap - slop > maximumCorrection)
            {
                return;
            }

            float recoveryRate = profile != null
                ? profile.SurfacePenetrationRecoverySpeed
                : 0f;
            float correction = recoveryRate > 0f
                ? Mathf.Min(-gap - slop, recoveryRate * deltaTime)
                : -gap - slop;
            physicsBody.SetPosition(physicsBody.Position + normal * correction);

            float approachSpeed = Vector3.Dot(
                physicsBody.LinearVelocity - referenceVelocity,
                normal);
            if (approachSpeed < 0f)
            {
                physicsBody.LinearVelocity =
                    physicsBody.LinearVelocity - normal * approachSpeed;
            }
        }

        void ApplyGravity(IFirstPersonPhysicsBody physicsBody, Vector3 gravityAcceleration, bool shouldApply)
        {
            if (!shouldApply)
            {
                return;
            }

            float gravityScale = profile != null
                ? Mathf.Lerp(1f, profile.UnderwaterGravityScale, Mathf.SmoothStep(0f, 1f, waterSubmergedFraction))
                : 1f;
            physicsBody.AddForce(
                ResolveEffectiveGravity(gravityAcceleration) * gravityScale,
                ForceMode.Acceleration);
        }

        Vector3 ResolveEffectiveGravity(Vector3 gravityAcceleration)
        {
            if (!grounded ||
                !walkableGround ||
                waterSubmergedFraction >= 0.5f ||
                groundNormal.sqrMagnitude <= 0.0001f)
            {
                return gravityAcceleration;
            }

            return Vector3.Project(gravityAcceleration, groundNormal);
        }

        void ApplyMovement(
            IFirstPersonPhysicsBody physicsBody,
            Vector3 referenceVelocity,
            Vector3 up,
            Vector2 movement,
            bool sprint,
            float deltaTime)
        {
            if (profile == null)
            {
                return;
            }

            Vector3 movementPlaneNormal = ResolveMovementPlaneNormal(up);
            Vector3 desiredDirection = BuildMoveDirection(movementPlaneNormal, movement);
            float waterControl = Mathf.SmoothStep(0f, 1f, waterSubmergedFraction);
            float drySpeed = sprint && movement.y > ForwardSprintThreshold
                ? profile.SprintSpeed
                : profile.WalkSpeed;
            float speed = Mathf.Lerp(drySpeed, profile.UnderwaterMoveSpeed, waterControl);
            Vector3 desiredSurfaceVelocity = desiredDirection * speed;
            Vector3 relativeVelocity = physicsBody.LinearVelocity - referenceVelocity;
            Vector3 currentSurfaceVelocity = Vector3.ProjectOnPlane(relativeVelocity, movementPlaneNormal);
            verticalSpeed = Vector3.Dot(relativeVelocity, up);
            surfaceVelocity = currentSurfaceVelocity;
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
                velocityDelta / Mathf.Max(deltaTime, 0.0001f),
                acceleration);

            physicsBody.AddForce(accelerationVector, ForceMode.Acceleration);
        }

        void ApplySteepSlopeSlide(IFirstPersonPhysicsBody physicsBody, Vector3 gravityAcceleration)
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

            physicsBody.AddForce(
                slideDirection.normalized * profile.SteepSlopeSlideAcceleration,
                ForceMode.Acceleration);
        }

        void StabilizeGroundContact(
            IFirstPersonPhysicsBody physicsBody,
            Vector3 gravityAcceleration,
            Vector3 referenceVelocity,
            Vector3 up,
            bool suppressStick,
            float deltaTime)
        {
            if (profile == null || !grounded || !walkableGround || waterSubmergedFraction >= 0.5f)
            {
                return;
            }

            Vector3 contactNormal = groundNormal.sqrMagnitude > 0.0001f
                ? groundNormal
                : up;
            Vector3 relativeVelocity = physicsBody.LinearVelocity - referenceVelocity;
            verticalSpeed = Vector3.Dot(relativeVelocity, up);
            float normalSpeed = Vector3.Dot(relativeVelocity, contactNormal);

            if (!suppressStick && profile.GroundedVerticalDamping > 0f && Mathf.Abs(normalSpeed) > 0.001f)
            {
                float damping = FarionMath.SmoothFactor(profile.GroundedVerticalDamping, deltaTime);
                physicsBody.AddForce(
                    -contactNormal * (normalSpeed * damping),
                    ForceMode.VelocityChange);
            }

            float stickAcceleration = CalculateGroundStickAcceleration(gravityAcceleration);
            if (!suppressStick && stickAcceleration > 0f)
            {
                physicsBody.AddForce(
                    -contactNormal * stickAcceleration,
                    ForceMode.Acceleration);
            }
        }

        bool ShouldLaunchJump()
        {
            if (profile == null ||
                waterSubmergedFraction >= MaximumSubmergedFractionForJump)
            {
                return false;
            }

            bool jumpRequested = jumpQueued ||
                simulationTime - lastJumpRequestTime <= profile.JumpBufferTime;
            return jumpRequested &&
                simulationTime - lastWalkableGroundTime <= profile.CoyoteTime &&
                simulationTime - lastJumpTime >= profile.JumpCooldown;
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
            IFirstPersonPhysicsBody physicsBody,
            Vector3 gravityAcceleration,
            Vector3 referenceVelocity,
            CelestialFrameSample celestialFrame,
            bool hasArtificialGravity,
            Vector3 up,
            bool launch)
        {
            jumpQueued = false;
            if (launch)
            {
                lastJumpRequestTime = float.NegativeInfinity;
                LaunchJump(
                    physicsBody,
                    gravityAcceleration,
                    referenceVelocity,
                    celestialFrame,
                    hasArtificialGravity,
                    up);
                return;
            }

            if (profile == null ||
                waterSubmergedFraction >= MaximumSubmergedFractionForJump)
            {
                lastJumpRequestTime = float.NegativeInfinity;
            }
        }

        void LaunchJump(
            IFirstPersonPhysicsBody physicsBody,
            Vector3 gravityAcceleration,
            Vector3 referenceVelocity,
            CelestialFrameSample celestialFrame,
            bool hasArtificialGravity,
            Vector3 up)
        {
            Vector3 relativeVelocity = physicsBody.LinearVelocity - referenceVelocity;
            Vector3 tangentialVelocity = Vector3.ProjectOnPlane(relativeVelocity, up);
            float jumpSpeed = CalculateJumpSpeed(
                gravityAcceleration,
                celestialFrame,
                hasArtificialGravity);
            float upwardSpeed = Mathf.Max(Vector3.Dot(relativeVelocity, up), jumpSpeed);
            physicsBody.LinearVelocity = referenceVelocity + tangentialVelocity + up * upwardSpeed;
            lastJumpTime = simulationTime;
            verticalSpeed = upwardSpeed;
            grounded = false;
            walkableGround = false;
            hasSmoothedGroundNormal = false;
        }

        float CalculateJumpSpeed(
            Vector3 gravityAcceleration,
            CelestialFrameSample celestialFrame,
            bool hasArtificialGravity)
        {
            float gravityMagnitude = gravityAcceleration.magnitude;
            float speed = profile.JumpHeight > 0f
                ? Mathf.Sqrt(2f * profile.JumpReferenceGravity * profile.JumpHeight)
                : profile.MaximumJumpSpeed;

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

        Vector3 SmoothGroundNormal(
            Vector3 targetNormal,
            Vector3 fallbackUp,
            float deltaTime)
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

            float t = FarionMath.SmoothFactor(profile.GroundNormalResponsiveness, deltaTime);
            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, targetNormal, t).normalized;
            return smoothedGroundNormal.sqrMagnitude > 0.0001f ? smoothedGroundNormal : targetNormal;
        }

        void ApplyOrientation(
            IFirstPersonPhysicsBody physicsBody,
            Vector3 up,
            float yawDegrees,
            float deltaTime)
        {
            Vector3 forward = Vector3.ProjectOnPlane(physicsBody.Rotation * Vector3.forward, up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            if (Mathf.Abs(yawDegrees) > 0.0001f)
            {
                forward = Quaternion.AngleAxis(yawDegrees, up) * forward;
            }

            Quaternion targetRotation = Quaternion.LookRotation(forward, up);
            if (profile == null || profile.UprightResponsiveness <= 0f)
            {
                physicsBody.MoveRotation(targetRotation);
                return;
            }

            float t = FarionMath.SmoothFactor(profile.UprightResponsiveness, deltaTime);
            physicsBody.MoveRotation(Quaternion.Slerp(physicsBody.Rotation, targetRotation, t));
        }

        Vector3 BuildMoveDirection(Vector3 up, Vector2 movement)
        {
            Vector3 forwardSource = viewReference != null ? viewReference.forward : transform.forward;
            Vector3 forward = Vector3.ProjectOnPlane(forwardSource, up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 move = right * movement.x + forward * movement.y;
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

        void ApplyWaterForces(
            IFirstPersonPhysicsBody physicsBody,
            CelestialFrameSample frame,
            Vector3 up,
            bool swimAscend)
        {
            if (profile == null || waterSubmergedFraction <= 0f)
            {
                return;
            }

            float waterControl = Mathf.SmoothStep(0f, 1f, waterSubmergedFraction);
            Vector3 relativeVelocity = physicsBody.LinearVelocity - frame.WaterPointVelocity;

            if (profile.UnderwaterLinearDrag > 0f)
            {
                physicsBody.AddForce(-relativeVelocity * (profile.UnderwaterLinearDrag * waterControl), ForceMode.Acceleration);
            }

            if (swimAscend && profile.UnderwaterAscendAcceleration > 0f)
            {
                physicsBody.AddForce(up * (profile.UnderwaterAscendAcceleration * waterControl), ForceMode.Acceleration);
            }
        }

    }
}
