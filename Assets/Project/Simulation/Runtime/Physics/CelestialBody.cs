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
        float mass;

        public string BodyName => bodyName;
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
        public Vector3 Velocity => ResolvedInertialVelocity - physicsReferenceFrameVelocity;
        public Vector3 AngularVelocity => simulatedAngularVelocity;
        public float Mass => mass;
        public bool ParticipatesInNBody => participatesInNBody;
        public CelestialBodyMotionMode MotionMode => motionMode;
        public CelestialBody OrbitAttractor => orbitAttractor;
        public bool IsKinematicBody => motionMode != CelestialBodyMotionMode.DynamicNBody;
        public bool IntegratesOrbit => motionMode != CelestialBodyMotionMode.Static;
        public bool SupportsNonConvexSurfaceCollider => IsKinematicBody;
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

        public void ApplyDefinition(CelestialBodyDefinition definition)
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

            RecalculateMass(GravitySimulation.DefaultGravitationalConstant);
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
            return Velocity + Vector3.Cross(simulatedAngularVelocity, point - Position);
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

        Vector3 ResolvedInertialVelocity =>
            ParticipatesInNBody && IntegratesOrbit
                ? simulatedVelocity
                : Vector3.zero;
    }
}
