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
        RigidbodyFirstPersonPhysicsBody offlinePhysicsBody;
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
        bool externalSimulation;
        float simulationTime;

        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();
        public CapsuleCollider Capsule => cachedCapsule != null ? cachedCapsule : cachedCapsule = GetComponent<CapsuleCollider>();
        public CelestialActorProbe ActorProbe => actorProbe != null ? actorProbe : actorProbe = GetComponent<CelestialActorProbe>();
        public bool Grounded => grounded;
        public bool WalkableGround => walkableGround;
        public Vector3 LocalUp => localUp;
        public Vector3 GroundNormal => groundNormal;
        public float YawDegreesPerMouseUnit =>
            profile != null ? profile.YawDegreesPerMouseUnit : 0f;

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
                lastJumpRequestTime = simulationTime;
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
                currentInput.Sprint);
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

            RefreshGrounding(localUp, physicsBody, deltaTime);
            ApplyGravity(physicsBody, gravityAcceleration, hasArtificialGravity || (applyCelestialGravity && celestialFrame.HasBody));
            StabilizeGroundContact(physicsBody, gravityAcceleration, referenceVelocity, localUp, deltaTime);
            ApplyMovement(physicsBody, referenceVelocity, localUp, input.Movement, input.Sprint, deltaTime);
            ApplySteepSlopeSlide(physicsBody, gravityAcceleration);
            ApplyWaterForces(physicsBody, environmentFrame, localUp);
            ApplyJump(physicsBody, gravityAcceleration, referenceVelocity, celestialFrame, hasArtificialGravity, localUp);
            ApplyOrientation(physicsBody, localUp, input.YawDegrees, deltaTime);
            physicsBody.Commit();
        }

        public void SetInputSource(IFirstPersonInputSource source)
        {
            resolvedInput = source;
            inputSource = source as MonoBehaviour;
        }

        public void SetViewReference(Transform reference)
        {
            viewReference = reference;
        }

        public void SetExternalSimulation(bool enabled)
        {
            externalSimulation = enabled;
            if (enabled)
            {
                pendingYawDegrees = 0f;
                jumpQueued = false;
            }
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
                VerticalSpeed = verticalSpeed,
                TouchingWater = touchingWater,
                Underwater = underwater,
                WaterDepth = waterDepth,
                WaterSubmergedFraction = waterSubmergedFraction,
                LocalUp = localUp,
                GroundNormal = groundNormal,
                SmoothedGroundNormal = smoothedGroundNormal,
                HasSmoothedGroundNormal = hasSmoothedGroundNormal
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
            verticalSpeed = state.VerticalSpeed;
            touchingWater = state.TouchingWater;
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

        void RefreshGrounding(
            Vector3 up,
            IFirstPersonPhysicsBody physicsBody,
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
            Vector3 castOrigin = physicsBody.Position + up * Mathf.Max(0.02f, Capsule.radius * 0.25f);
            float castDistance = capsuleHalfHeight + profile.GroundProbeDistance;
            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
            int hitCount = physicsScene.SphereCast(
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

            groundNormal = SmoothGroundNormal(detectedGroundNormal, up, deltaTime);
            groundSlopeAngle = Vector3.Angle(up, groundNormal);
            walkableGround = groundSlopeAngle <= profile.MaxWalkableSlopeAngle;
            if (walkableGround)
            {
                lastWalkableGroundTime = simulationTime;
            }
        }

        void ApplyGravity(IFirstPersonPhysicsBody physicsBody, Vector3 gravityAcceleration, bool shouldApply)
        {
            if (!shouldApply)
            {
                return;
            }

            float gravityScale = profile != null
                ? Mathf.Lerp(1f, profile.UnderwaterGravityScale, Smooth01(waterSubmergedFraction))
                : 1f;
            physicsBody.AddForce(gravityAcceleration * gravityScale, ForceMode.Acceleration);
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
            float waterControl = Smooth01(waterSubmergedFraction);
            float drySpeed = sprint ? profile.SprintSpeed : profile.WalkSpeed;
            float speed = Mathf.Lerp(drySpeed, profile.UnderwaterMoveSpeed, waterControl);
            Vector3 desiredSurfaceVelocity = desiredDirection * speed;
            Vector3 relativeVelocity = physicsBody.LinearVelocity - referenceVelocity;
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
            float deltaTime)
        {
            if (profile == null || !grounded || !walkableGround || waterSubmergedFraction >= 0.5f)
            {
                return;
            }

            Vector3 relativeVelocity = physicsBody.LinearVelocity - referenceVelocity;
            float currentVerticalSpeed = Vector3.Dot(relativeVelocity, up);
            verticalSpeed = currentVerticalSpeed;

            if (!jumpQueued && profile.GroundedVerticalDamping > 0f && Mathf.Abs(currentVerticalSpeed) > 0.001f)
            {
                float damping = 1f - Mathf.Exp(-profile.GroundedVerticalDamping * deltaTime);
                physicsBody.AddForce(-up * (currentVerticalSpeed * damping), ForceMode.VelocityChange);
            }

            float stickAcceleration = CalculateGroundStickAcceleration(gravityAcceleration);
            if (!jumpQueued && stickAcceleration > 0f)
            {
                physicsBody.AddForce(-up * stickAcceleration, ForceMode.Acceleration);
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
            IFirstPersonPhysicsBody physicsBody,
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

            if (!jumpQueued || simulationTime - lastJumpRequestTime > profile.JumpBufferTime)
            {
                jumpQueued = false;
                return;
            }

            if (waterSubmergedFraction >= 0.35f)
            {
                jumpQueued = false;
                return;
            }

            bool canJump = simulationTime - lastWalkableGroundTime <= profile.CoyoteTime &&
                simulationTime - lastJumpTime >= profile.JumpCooldown;

            if (canJump)
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

            float t = 1f - Mathf.Exp(-profile.GroundNormalResponsiveness * deltaTime);
            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, targetNormal, t).normalized;
            return smoothedGroundNormal.sqrMagnitude > 0.0001f ? smoothedGroundNormal : targetNormal;
        }

        void ApplyOrientation(
            IFirstPersonPhysicsBody physicsBody,
            Vector3 up,
            float yawDegrees,
            float deltaTime)
        {
            physicsBody.AngularVelocity = Vector3.zero;
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

            float t = 1f - Mathf.Exp(-profile.UprightResponsiveness * deltaTime);
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

        void ApplyWaterForces(IFirstPersonPhysicsBody physicsBody, CelestialFrameSample frame, Vector3 up)
        {
            if (profile == null || waterSubmergedFraction <= 0f)
            {
                return;
            }

            float waterControl = Smooth01(waterSubmergedFraction);
            Vector3 bodyVelocity = frame.HasBody ? frame.BodyPointVelocity : Vector3.zero;
            Vector3 relativeVelocity = physicsBody.LinearVelocity - bodyVelocity;

            if (profile.UnderwaterLinearDrag > 0f)
            {
                physicsBody.AddForce(-relativeVelocity * (profile.UnderwaterLinearDrag * waterControl), ForceMode.Acceleration);
            }

            if (currentInput.Jump && profile.UnderwaterAscendAcceleration > 0f)
            {
                physicsBody.AddForce(up * (profile.UnderwaterAscendAcceleration * waterControl), ForceMode.Acceleration);
            }
        }

        static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
