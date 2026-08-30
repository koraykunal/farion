using Farion.Core.Numerics;
using Farion.Core.Persistence;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Simulation.Physics
{
    [MovedFrom(true, "Farion.Core.Physics", "Farion.Core.Runtime")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PersistentObjectId))]
    public sealed class CelestialBody : MonoBehaviour
    {
        [SerializeField] string bodyName = "Unnamed Body";
        [SerializeField] CelestialBodyType bodyType = CelestialBodyType.Planet;
        [Min(0.01f)]
        [SerializeField] float radius = 50f;
        [Min(0f)]
        [SerializeField] float surfaceGravity = 9.81f;
        [SerializeField] Vector3 initialVelocity;
        [SerializeField] Vector3 initialAngularVelocityDegreesPerSecond;
        [SerializeField] bool deriveMassFromSurfaceGravity = true;
        [Min(0f)]
        [SerializeField] float explicitMass = 1000000000f;
        [SerializeField] bool participatesInNBody = true;
        [SerializeField] CelestialBodyMotionMode motionMode = CelestialBodyMotionMode.KinematicOrbit;
        [SerializeField] CelestialBody orbitAttractor;

        Rigidbody cachedRigidbody;
        PersistentObjectId cachedPersistentObjectId;
        Vector3 simulatedVelocity;
        Vector3 simulatedAngularVelocity;
        Vector3 physicsReferenceFrameVelocity;
        Vector3 frameRelativeVelocity;
        Vector3 frameRelativeAngularVelocity;
        bool hasResolvedFrameMotion;
        float mass;
        OrbitalElements orbit;
        bool hasOrbit;
        Quaternion referenceRotation = Quaternion.identity;
        bool hasAnalyticReference;
        Vector3 spinAxis = Vector3.up;
        float spinDegreesPerSecond;
        Vector3 systemPosition;
        Vector3 systemVelocity;
        Vector3 localOffsetAtEpoch;
        Vector3 driftVelocity;
        Vector3 epochSystemOrigin;
        double analyticEpochSeconds;

        public string BodyName => bodyName;
        public int StableId
        {
            get
            {
                string persistentId = PersistentId;
                return ResolveStableId(
                    string.IsNullOrEmpty(persistentId) ? bodyName : persistentId);
            }
        }
        public string PersistentId
        {
            get
            {
                if (cachedPersistentObjectId == null)
                {
                    cachedPersistentObjectId = GetComponent<PersistentObjectId>();
                }

                return cachedPersistentObjectId != null
                    ? cachedPersistentObjectId.Id
                    : string.Empty;
            }
        }
        public CelestialBodyType BodyType => bodyType;
        public float Radius => radius;
        public float SurfaceGravity => surfaceGravity;
        public Vector3 InitialVelocity => initialVelocity;
        public Vector3 InitialAngularVelocityDegreesPerSecond => initialAngularVelocityDegreesPerSecond;
        public Vector3 InertialVelocity => simulatedVelocity;
        public Vector3 InertialAngularVelocity => hasAnalyticReference
            ? referenceRotation * simulatedAngularVelocity
            : transform.rotation * simulatedAngularVelocity;
        public Vector3 Velocity => hasResolvedFrameMotion
            ? frameRelativeVelocity
            : ResolvedInertialVelocity - physicsReferenceFrameVelocity;
        public Vector3 AngularVelocity => hasResolvedFrameMotion
            ? frameRelativeAngularVelocity
            : InertialAngularVelocity;
        public float Mass => mass;
        public bool ParticipatesInNBody => participatesInNBody;
        public CelestialBodyMotionMode MotionMode => motionMode;
        public CelestialBody OrbitAttractor => orbitAttractor;
        public bool IsKinematicBody => motionMode != CelestialBodyMotionMode.DynamicNBody;
        public bool IntegratesOrbit => motionMode != CelestialBodyMotionMode.Static;
        public bool SupportsNonConvexSurfaceCollider => IsKinematicBody;
        public bool UsesAnalyticMotion => motionMode != CelestialBodyMotionMode.DynamicNBody;
        public Vector3 SystemPosition => systemPosition;
        public Vector3 SystemVelocity => systemVelocity;
        public Vector3 EpochLocalOffset => localOffsetAtEpoch;
        public Vector3 AnalyticEpochOrigin => epochSystemOrigin;
        public bool HasAnalyticReference => hasAnalyticReference;
        public Vector3 Position => Rigidbody.position;

        public Rigidbody Rigidbody
        {
            get
            {
                if (cachedRigidbody == null)
                {
                    cachedRigidbody = GetComponent<Rigidbody>();
                }

                return cachedRigidbody;
            }
        }

        public void ApplyDefinition(
            CelestialBodyDefinition definition,
            float gravitationalConstant = GravitySimulation.DefaultGravitationalConstant)
        {
            if (definition == null)
            {
                return;
            }

            bodyName = definition.BodyName;
            bodyType = definition.BodyType;
            radius = Mathf.Max(0.01f, definition.Radius);
            surfaceGravity = Mathf.Max(0f, definition.SurfaceGravity);
            initialVelocity = definition.InitialVelocity;
            initialAngularVelocityDegreesPerSecond = definition.InitialAngularVelocityDegreesPerSecond;
            deriveMassFromSurfaceGravity = definition.DeriveMassFromSurfaceGravity;
            explicitMass = Mathf.Max(0f, definition.ExplicitMass);
            participatesInNBody = definition.ParticipatesInNBody;
            motionMode = definition.MotionMode;

            if (!string.IsNullOrWhiteSpace(bodyName))
            {
                gameObject.name = bodyName;
            }

            if (TryGetComponent(out Rigidbody rb))
            {
                cachedRigidbody = rb;
            }

            RecalculateMass(gravitationalConstant > 0f
                ? gravitationalConstant
                : GravitySimulation.DefaultGravitationalConstant);
            ConfigureRigidbody();
        }

        void Awake()
        {
            RecalculateMass(GravitySimulation.DefaultGravitationalConstant);
            ConfigureRigidbody();
            ResetSimulationState();
        }

        void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            surfaceGravity = Mathf.Max(0f, surfaceGravity);
            explicitMass = Mathf.Max(0f, explicitMass);
            if (orbitAttractor == this)
            {
                orbitAttractor = null;
            }

            if (!string.IsNullOrWhiteSpace(bodyName))
            {
                gameObject.name = bodyName;
            }

            if (TryGetComponent(out Rigidbody rb))
            {
                cachedRigidbody = rb;
                RecalculateMass(GravitySimulation.DefaultGravitationalConstant);
            }
        }

        public void ConfigureRigidbody()
        {
            Rigidbody rb = Rigidbody;
            if (rb == null)
            {
                return;
            }

            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.isKinematic = IsKinematicBody;
            rb.mass = Mathf.Max(0.0001f, mass);
        }

        public void ResetSimulationState()
        {
            simulatedVelocity = initialVelocity;
            simulatedAngularVelocity = initialAngularVelocityDegreesPerSecond * Mathf.Deg2Rad;
            physicsReferenceFrameVelocity = Vector3.zero;
            frameRelativeVelocity = Vector3.zero;
            frameRelativeAngularVelocity = Vector3.zero;
            hasResolvedFrameMotion = false;
        }

        public void CaptureAnalyticReference(
            Vector3 attractorSystemOrigin,
            Vector3 attractorSystemVelocity,
            float gravitationalConstant,
            double epochSeconds)
        {
            referenceRotation = Rigidbody != null ? Rigidbody.rotation : transform.rotation;
            hasAnalyticReference = true;
            epochSystemOrigin = attractorSystemOrigin;
            analyticEpochSeconds = epochSeconds;
            spinDegreesPerSecond = initialAngularVelocityDegreesPerSecond.magnitude;
            spinAxis = spinDegreesPerSecond > 0.0001f
                ? initialAngularVelocityDegreesPerSecond / spinDegreesPerSecond
                : Vector3.up;

            hasOrbit = false;
            orbit = default;

            CelestialBody attractor = orbitAttractor;
            localOffsetAtEpoch = attractor != null ? Position - attractor.Position : Vector3.zero;
            driftVelocity = motionMode == CelestialBodyMotionMode.KinematicOrbit
                ? initialVelocity - attractorSystemVelocity
                : Vector3.zero;
            systemPosition = attractorSystemOrigin + localOffsetAtEpoch;
            systemVelocity = attractorSystemVelocity + driftVelocity;

            if (motionMode != CelestialBodyMotionMode.KinematicOrbit ||
                attractor == null ||
                gravitationalConstant <= 0f)
            {
                return;
            }

            double gravitationalParameter = (double)gravitationalConstant * attractor.Mass;
            hasOrbit = OrbitalElements.TryCreate(
                localOffsetAtEpoch,
                driftVelocity,
                gravitationalParameter,
                out orbit);
            if (!hasOrbit)
            {
                Debug.LogWarning(
                    $"Celestial body '{bodyName}' could not fit a Kepler orbit around '{attractor.BodyName}' (offset {localOffsetAtEpoch}, velocity {driftVelocity}); it will drift linearly without gravity.",
                    this);
            }
        }

        public void EvaluateAnalyticMotion(double timeSeconds, Vector3 attractorSystemPosition, Vector3 attractorSystemVelocity)
        {
            double localTime = timeSeconds - analyticEpochSeconds;
            if (hasOrbit)
            {
                orbit.Evaluate(localTime, out Vector3 localPosition, out Vector3 localVelocity);
                systemPosition = attractorSystemPosition + localPosition;
                systemVelocity = attractorSystemVelocity + localVelocity;
            }
            else
            {
                systemPosition = attractorSystemPosition +
                    localOffsetAtEpoch +
                    driftVelocity * (float)localTime;
                systemVelocity = attractorSystemVelocity + driftVelocity;
            }

            simulatedVelocity = systemVelocity;
        }

        public Quaternion EvaluateAnalyticRotation(double timeSeconds)
        {
            if (!hasAnalyticReference)
            {
                return Rigidbody != null ? Rigidbody.rotation : transform.rotation;
            }

            if (spinDegreesPerSecond <= 0.0001f)
            {
                return referenceRotation;
            }

            double totalDegrees = spinDegreesPerSecond * (timeSeconds - analyticEpochSeconds);
            float wrapped = (float)(totalDegrees - 360d * System.Math.Floor(totalDegrees / 360d));
            return referenceRotation * Quaternion.AngleAxis(wrapped, spinAxis);
        }

        public void ApplyAnalyticPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            Rigidbody body = Rigidbody;
            if (body == null)
            {
                return;
            }

            bool stepped = Application.isPlaying && body.isKinematic;
            if ((body.position - worldPosition).sqrMagnitude > 1e-10f)
            {
                if (stepped)
                {
                    body.MovePosition(worldPosition);
                }
                else
                {
                    body.position = worldPosition;
                }
            }

            if (!FarionMath.IsSameRotation(body.rotation, worldRotation))
            {
                if (stepped)
                {
                    body.MoveRotation(worldRotation);
                }
                else
                {
                    body.rotation = worldRotation;
                }
            }
        }

        public void SetOrbitAttractor(CelestialBody attractor)
        {
            orbitAttractor = attractor != this ? attractor : null;
        }

        public void RecalculateMass(float gravitationalConstant)
        {
            if (deriveMassFromSurfaceGravity && gravitationalConstant > 0f)
            {
                mass = surfaceGravity * radius * radius / gravitationalConstant;
            }
            else
            {
                mass = explicitMass;
            }

            if (cachedRigidbody != null)
            {
                cachedRigidbody.mass = Mathf.Max(0.0001f, mass);
            }
        }

        public CelestialSurfaceSample SampleSurface(Vector3 point)
        {
            Vector3 centerToPoint = point - Position;
            float centerDistance = centerToPoint.magnitude;
            Vector3 normal = centerDistance > 0.0001f ? centerToPoint / centerDistance : transform.up;
            Vector3 surfacePoint = Position + normal * radius;
            return new CelestialSurfaceSample(this, surfacePoint, normal, centerDistance, centerDistance - radius);
        }

        public void IntegrateVelocity(Vector3 acceleration, float deltaTime)
        {
            if (!IntegratesOrbit)
            {
                simulatedVelocity = Vector3.zero;
                return;
            }

            simulatedVelocity += acceleration * deltaTime;
        }

        internal void SetPhysicsReferenceFrameVelocity(Vector3 frameVelocity)
        {
            physicsReferenceFrameVelocity = frameVelocity;
            hasResolvedFrameMotion = false;
        }

        internal void SetPhysicsFrameMotion(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            frameRelativeVelocity = linearVelocity;
            frameRelativeAngularVelocity = angularVelocity;
            hasResolvedFrameMotion = true;
        }

        public void IntegratePosition(float deltaTime, Vector3 referenceFrameVelocity)
        {
            physicsReferenceFrameVelocity = referenceFrameVelocity;
            Vector3 frameRelativeVelocity = Velocity;
            if (frameRelativeVelocity.sqrMagnitude > 0.00000001f)
            {
                Rigidbody.MovePosition(Rigidbody.position + frameRelativeVelocity * deltaTime);
            }

            if (IntegratesOrbit && simulatedAngularVelocity.sqrMagnitude > 0.000001f)
            {
                Quaternion deltaRotation = Quaternion.Euler(simulatedAngularVelocity * Mathf.Rad2Deg * deltaTime);
                Rigidbody.MoveRotation(Rigidbody.rotation * deltaRotation);
            }
        }

        public Vector3 GetVelocityAtPoint(Vector3 point)
        {
            return Velocity + Vector3.Cross(AngularVelocity, point - Position);
        }

        public CelestialBodySnapshot CaptureSnapshot()
        {
            return new CelestialBodySnapshot(
                PersistentId,
                BodyName,
                new TransformPoseSnapshot(
                    Rigidbody.position,
                    Rigidbody.rotation,
                    simulatedVelocity,
                    simulatedAngularVelocity));
        }

        public bool ApplySnapshot(CelestialBodySnapshot snapshot)
        {
            if (!SnapshotMatches(snapshot))
            {
                return false;
            }

            TransformPoseSnapshot pose = snapshot.Pose;
            Rigidbody.position = pose.Position;
            Rigidbody.rotation = pose.Rotation;
            simulatedVelocity = pose.LinearVelocity;
            simulatedAngularVelocity = pose.AngularVelocity;
            return true;
        }

        public bool SnapshotMatches(CelestialBodySnapshot snapshot)
        {
            if (!snapshot.IsValid)
            {
                return false;
            }

            if (snapshot.HasPersistentId)
            {
                return !string.IsNullOrEmpty(PersistentId) &&
                       string.Equals(snapshot.PersistentId, PersistentId, System.StringComparison.Ordinal);
            }

            return string.Equals(snapshot.BodyName, BodyName, System.StringComparison.Ordinal);
        }

        static int ResolveStableId(string identity)
        {
            if (string.IsNullOrEmpty(identity))
            {
                return 0;
            }

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < identity.Length; i++)
                {
                    hash ^= identity[i];
                    hash *= 16777619u;
                }

                return hash == 0u ? 1 : (int)hash;
            }
        }

        Vector3 ResolvedInertialVelocity =>
            ParticipatesInNBody && IntegratesOrbit
                ? simulatedVelocity
                : Vector3.zero;
    }
}
