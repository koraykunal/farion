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
    public sealed class ExplorerMotor : MonoBehaviour
    {
        const int MaxGroundHits = 16;
        const float SprintStickThreshold = 0.5f;
        const float ProbeRadiusRatio = 0.9f;
        const float ProbeClearanceRatio = 0.1f;
        const float MaximumPenetrationCorrectionRatio = 2f;
        const float MaximumSubmergedFractionForJump = 0.35f;
        const float SwimEnterFraction = 0.5f;
        const float SwimExitFraction = 0.3f;

        [Header("Profile")]
        [SerializeField] ExplorerMotorProfile profile;

        [Header("Input")]
        [SerializeField] ExplorerInput inputSource;

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
        [SerializeField] bool swimming;
        [SerializeField] float waterDepth;
        [SerializeField] float waterSubmergedFraction;
        [SerializeField] Vector3 localUp = Vector3.up;
        [SerializeField] Vector3 groundNormal = Vector3.up;
        [SerializeField] Vector3 lookDirection;
        [SerializeField] bool aiming;

        readonly RaycastHit[] groundHits = new RaycastHit[MaxGroundHits];
        Rigidbody cachedRigidbody;
        CapsuleCollider cachedCapsule;
        CelestialActorProbe actorProbe;
        RigidbodyExplorerPhysicsBody offlinePhysicsBody;
        ExplorerInput resolvedInput;
        ExplorerInputState currentInput;
        bool jumpQueued;
        bool previousJumpHeld;
        float lastJumpTime = float.NegativeInfinity;
        float lastJumpRequestTime = float.NegativeInfinity;
        float lastWalkableGroundTime = float.NegativeInfinity;
        Vector3 smoothedGroundNormal = Vector3.up;
        bool hasSmoothedGroundNormal;
        float groundGap = float.PositiveInfinity;
        ArtificialGravityVolume artificialGravitySource;
        ArtificialWaterVolume artificialWaterSource;
        bool externalSimulation;
        float simulationTime;
        int groundLayer = -1;

        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();
        public CapsuleCollider Capsule => cachedCapsule != null ? cachedCapsule : cachedCapsule = GetComponent<CapsuleCollider>();
        public CelestialActorProbe ActorProbe => actorProbe != null ? actorProbe : actorProbe = GetComponent<CelestialActorProbe>();
        public ExplorerMotorProfile Profile => profile;
        public bool Grounded => grounded;
        public bool WalkableGround => walkableGround;
        public Vector3 LocalUp => localUp;
        public Vector3 GroundNormal => groundNormal;
        public Vector3 SurfaceVelocity => surfaceVelocity;
        public int GroundLayer => groundLayer;
        public Transform ViewReference => viewReference;
        public Vector3 LookDirection => lookDirection.sqrMagnitude > 0.0001f ? lookDirection : transform.forward;
        public bool Aiming => aiming;
        public float ViewPitchDegrees =>
            -Mathf.Asin(Mathf.Clamp(Vector3.Dot(LookDirection, localUp.normalized), -1f, 1f)) * Mathf.Rad2Deg;

        public ExplorerMotorInput BuildInput(
            Vector2 stick,
            bool aim,
            bool jump,
            bool sprint,
            bool swimAscend,
            bool swimDescend = false)
        {
            Vector3 up = localUp.sqrMagnitude > 0.0001f ? localUp : transform.up;
            Vector3 forwardSource = viewReference != null ? viewReference.forward : transform.forward;
            Vector3 forward = Vector3.ProjectOnPlane(forwardSource, up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            return new ExplorerMotorInput(
                right * stick.x + forward * stick.y,
                forwardSource,
                aim,
                jump,
                sprint,
                swimAscend,
                swimDescend);
        }

        void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            cachedCapsule = GetComponent<CapsuleCollider>();
            actorProbe = GetComponent<CelestialActorProbe>();
            offlinePhysicsBody = new RigidbodyExplorerPhysicsBody(cachedRigidbody);
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
            currentInput = resolvedInput?.CurrentInput ?? ExplorerInputState.None;

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

            Simulate(
                BuildInput(currentInput.Movement, currentInput.Aim, jumpQueued, currentInput.Sprint, currentInput.Jump, currentInput.Dive),
                Time.fixedDeltaTime,
                offlinePhysicsBody);
        }

        public void Simulate(
            ExplorerMotorInput input,
            float deltaTime,
            IExplorerPhysicsBody physicsBody)
        {
            if (physicsBody == null || deltaTime <= 0f)
            {
                return;
            }

            simulationTime += deltaTime;
            if (input.LookDirection.sqrMagnitude > 0.0001f)
            {
                lookDirection = input.LookDirection;
            }

            aiming = input.Aim;
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
            swimming = touchingWater &&
                (swimming
                    ? waterSubmergedFraction > SwimExitFraction
                    : waterSubmergedFraction >= SwimEnterFraction);

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
            bool jumpLaunching = ShouldLaunchJump();
            if (ShouldRest(physicsBody, referenceVelocity, input.Movement, jumpLaunching))
            {
                Rest(physicsBody, referenceVelocity);
            }
            else
            {
                ApplyGravity(physicsBody, gravityAcceleration, hasArtificialGravity || (applyCelestialGravity && celestialFrame.HasBody));
                StabilizeGroundContact(physicsBody, gravityAcceleration, referenceVelocity, localUp, jumpLaunching, deltaTime);
                ApplyMovement(physicsBody, referenceVelocity, localUp, input.Movement, input.Sprint, deltaTime);
                ApplySteepSlopeSlide(physicsBody, gravityAcceleration);
                ApplyWaterForces(physicsBody, environmentFrame, localUp, gravityAcceleration, input.SwimAscend, input.SwimDescend, deltaTime);
                ApplyJump(physicsBody, gravityAcceleration, referenceVelocity, celestialFrame, hasArtificialGravity, localUp, jumpLaunching);
            }

            ApplyOrientation(
                physicsBody,
                localUp,
                input.Aim ? Vector3.ProjectOnPlane(LookDirection, localUp) : input.Movement,
                deltaTime);
            physicsBody.Commit();
        }

        const float RestSpeed = 0.15f;
        const float RestContactGap = 0.03f;

        bool ShouldRest(
            IExplorerPhysicsBody physicsBody,
            Vector3 referenceVelocity,
            Vector3 movement,
            bool jumpLaunching)
        {
            return grounded
                && walkableGround
                && groundGap <= RestContactGap
                && !jumpLaunching
                && !jumpQueued
                && !swimming
                && movement.sqrMagnitude <= 0.0001f
                && (physicsBody.LinearVelocity - referenceVelocity).sqrMagnitude <= RestSpeed * RestSpeed;
        }

        void Rest(IExplorerPhysicsBody physicsBody, Vector3 referenceVelocity)
        {
            if (physicsBody.LinearVelocity != referenceVelocity)
            {
                physicsBody.LinearVelocity = referenceVelocity;
            }

            if (physicsBody.AngularVelocity != Vector3.zero)
            {
                physicsBody.AngularVelocity = Vector3.zero;
            }

            surfaceVelocity = Vector3.zero;
            surfaceSpeed = 0f;
            verticalSpeed = 0f;
        }

        public void SetInputSource(ExplorerInput source)
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
            jumpQueued = false;
            previousJumpHeld = false;
        }

        const float UprightSettledDegrees = 0.01f;

        public ExplorerMotorState CaptureState()
        {
            return new ExplorerMotorState
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
                Swimming = swimming,
                Underwater = underwater,
                WaterDepth = waterDepth,
                WaterSubmergedFraction = waterSubmergedFraction,
                LocalUp = localUp,
                GroundNormal = groundNormal,
                SmoothedGroundNormal = smoothedGroundNormal,
                HasSmoothedGroundNormal = hasSmoothedGroundNormal
            };
        }

        public void RestoreState(ExplorerMotorState state)
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
            swimming = state.Swimming;
            underwater = state.Underwater;
            waterDepth = state.WaterDepth;
            waterSubmergedFraction = state.WaterSubmergedFraction;
            localUp = state.LocalUp;
            groundNormal = state.GroundNormal;
            smoothedGroundNormal = state.SmoothedGroundNormal;
            hasSmoothedGroundNormal = state.HasSmoothedGroundNormal;
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

        public void SetArtificialWaterSource(ArtificialWaterVolume source)
        {
            artificialWaterSource = source;
        }

        public void ClearArtificialWaterSource(ArtificialWaterVolume source)
        {
            if (artificialWaterSource == source)
            {
                artificialWaterSource = null;
            }
        }

        public void ResetMotorState()
        {
            currentInput = ExplorerInputState.None;
            jumpQueued = false;
            previousJumpHeld = false;
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
            swimming = false;
            waterDepth = 0f;
            waterSubmergedFraction = 0f;
            lookDirection = Vector3.zero;
            aiming = false;
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

            resolvedInput ??= GetComponent<ExplorerInput>();
        }

        void RefreshGrounding(
            Vector3 up,
            IExplorerPhysicsBody physicsBody,
            CelestialFrameSample frame,
            bool hasArtificialGravity,
            float deltaTime)
        {
            grounded = false;
            walkableGround = false;
            groundSlopeAngle = 0f;
            groundNormal = up;
            groundLayer = -1;
            groundGap = float.PositiveInfinity;
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
                groundLayer = hit.collider.gameObject.layer;
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
                groundLayer = FarionLayers.CelestialSurface;
            }

            groundGap = closestGap;
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
            IExplorerPhysicsBody physicsBody,
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

        void ApplyGravity(IExplorerPhysicsBody physicsBody, Vector3 gravityAcceleration, bool shouldApply)
        {
            if (!shouldApply)
            {
                return;
            }

            physicsBody.AddForce(
                ResolveEffectiveGravity(gravityAcceleration),
                ForceMode.Acceleration);
        }

        Vector3 ResolveEffectiveGravity(Vector3 gravityAcceleration)
        {
            if (!grounded ||
                !walkableGround ||
                swimming ||
                groundNormal.sqrMagnitude <= 0.0001f)
            {
                return gravityAcceleration;
            }

            return Vector3.Project(gravityAcceleration, groundNormal);
        }

        void ApplyMovement(
            IExplorerPhysicsBody physicsBody,
            Vector3 referenceVelocity,
            Vector3 up,
            Vector3 movement,
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
            float drySpeed = sprint && movement.sqrMagnitude > SprintStickThreshold * SprintStickThreshold
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

        void ApplySteepSlopeSlide(IExplorerPhysicsBody physicsBody, Vector3 gravityAcceleration)
        {
            if (profile == null || !grounded || walkableGround || swimming)
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
            IExplorerPhysicsBody physicsBody,
            Vector3 gravityAcceleration,
            Vector3 referenceVelocity,
            Vector3 up,
            bool suppressStick,
            float deltaTime)
        {
            if (profile == null || !grounded || !walkableGround || swimming)
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
                swimming ||
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
            IExplorerPhysicsBody physicsBody,
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
                swimming ||
                waterSubmergedFraction >= MaximumSubmergedFractionForJump)
            {
                lastJumpRequestTime = float.NegativeInfinity;
            }
        }

        void LaunchJump(
            IExplorerPhysicsBody physicsBody,
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
            IExplorerPhysicsBody physicsBody,
            Vector3 up,
            Vector3 faceDirection,
            float deltaTime)
        {
            Vector3 face = Vector3.ProjectOnPlane(faceDirection, up);
            bool turning = face.sqrMagnitude > 0.0001f;
            Vector3 forward = turning ? face : Vector3.ProjectOnPlane(physicsBody.Rotation * Vector3.forward, up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Quaternion targetRotation = Quaternion.LookRotation(forward, up);
            if (Quaternion.Angle(physicsBody.Rotation, targetRotation) <= UprightSettledDegrees)
            {
                return;
            }

            if (profile == null)
            {
                physicsBody.MoveRotation(targetRotation);
                return;
            }

            if (turning && profile.TurnDegreesPerSecond > 0f)
            {
                physicsBody.MoveRotation(Quaternion.RotateTowards(
                    physicsBody.Rotation,
                    targetRotation,
                    profile.TurnDegreesPerSecond * deltaTime));
                return;
            }

            if (profile.UprightResponsiveness <= 0f)
            {
                physicsBody.MoveRotation(targetRotation);
                return;
            }

            float t = FarionMath.SmoothFactor(profile.UprightResponsiveness, deltaTime);
            physicsBody.MoveRotation(Quaternion.Slerp(physicsBody.Rotation, targetRotation, t));
        }

        static Vector3 BuildMoveDirection(Vector3 up, Vector3 movement)
        {
            Vector3 move = Vector3.ProjectOnPlane(movement, up);
            return move.sqrMagnitude > 0.0001f ? Vector3.ClampMagnitude(move, 1f) : Vector3.zero;
        }

        Vector3 ResolveMovementPlaneNormal(Vector3 up)
        {
            if (grounded && !swimming && groundNormal.sqrMagnitude > 0.0001f)
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

            if (profile == null || (artificialWaterSource == null && !frame.HasOcean))
            {
                return;
            }

            float surfaceAltitude = artificialWaterSource != null
                ? artificialWaterSource.SignedDistanceToSurface(frame.Position)
                : frame.OceanAltitude;
            float capsuleHalfHeight = Mathf.Max(Capsule.height * 0.5f, Capsule.radius);
            float immersionDepth = Mathf.Clamp(capsuleHalfHeight - surfaceAltitude, 0f, capsuleHalfHeight * 2f);
            waterSubmergedFraction = Mathf.Clamp01(immersionDepth / (capsuleHalfHeight * 2f));
            touchingWater = waterSubmergedFraction > 0f;
            underwater = surfaceAltitude < 0f;
            waterDepth = Mathf.Max(0f, -surfaceAltitude);
        }

        void ApplyWaterForces(
            IExplorerPhysicsBody physicsBody,
            CelestialFrameSample frame,
            Vector3 up,
            Vector3 gravityAcceleration,
            bool swimAscend,
            bool swimDescend,
            float deltaTime)
        {
            if (profile == null || waterSubmergedFraction <= 0f)
            {
                return;
            }

            float swimBlend = Mathf.Clamp01(surfaceSpeed / Mathf.Max(0.01f, profile.UnderwaterMoveSpeed));
            float floatFraction = Mathf.Lerp(
                profile.SwimFloatSubmergedFraction,
                profile.SwimForwardSubmergedFraction,
                swimBlend);
            float waterControl = Mathf.SmoothStep(0f, 1f, waterSubmergedFraction);
            float swimControl = swimming ? 1f : 0f;
            Vector3 waterVelocity = artificialWaterSource != null
                ? artificialWaterSource.ReferenceVelocityAt(frame.Position)
                : frame.WaterPointVelocity;
            Vector3 relativeVelocity = physicsBody.LinearVelocity - waterVelocity;
            float upwardSpeed = Vector3.Dot(relativeVelocity, up);

            if (profile.UnderwaterLinearDrag > 0f)
            {
                physicsBody.AddForce(-relativeVelocity * (profile.UnderwaterLinearDrag * waterControl), ForceMode.Acceleration);
            }

            if (swimControl <= 0f)
            {
                return;
            }

            if (swimDescend)
            {
                PushVertical(physicsBody, up, upwardSpeed, -profile.UnderwaterMoveSpeed, swimControl, deltaTime);
                return;
            }

            float buoyancy = gravityAcceleration.magnitude * (waterSubmergedFraction / floatFraction);
            physicsBody.AddForce(up * (buoyancy * swimControl), ForceMode.Acceleration);
            physicsBody.AddForce(-up * (upwardSpeed * profile.SwimVerticalDamping * swimControl), ForceMode.Acceleration);

            if (swimAscend)
            {
                float ascendControl = Mathf.InverseLerp(floatFraction, floatFraction + 0.1f, waterSubmergedFraction);
                if (ascendControl > 0f)
                {
                    PushVertical(physicsBody, up, upwardSpeed, profile.UnderwaterMoveSpeed * ascendControl, swimControl, deltaTime);
                }
            }
        }

        void PushVertical(
            IExplorerPhysicsBody physicsBody,
            Vector3 up,
            float upwardSpeed,
            float desiredUpwardSpeed,
            float control,
            float deltaTime)
        {
            if (profile.UnderwaterAscendAcceleration <= 0f)
            {
                return;
            }

            float acceleration = Mathf.Clamp(
                (desiredUpwardSpeed - upwardSpeed) / Mathf.Max(deltaTime, 0.0001f),
                -profile.UnderwaterAscendAcceleration,
                profile.UnderwaterAscendAcceleration);
            physicsBody.AddForce(up * (acceleration * control), ForceMode.Acceleration);
        }

    }
}
