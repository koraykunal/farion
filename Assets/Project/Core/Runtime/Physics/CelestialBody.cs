using UnityEngine;

namespace Farion.Core.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CelestialBody : MonoBehaviour
    {
        [SerializeField] string bodyName = "Unnamed Body";
        [SerializeField] CelestialBodyType bodyType = CelestialBodyType.Planet;
        [Min(0.01f)]
        [SerializeField] float radius = 50f;
        [Min(0f)]
        [SerializeField] float surfaceGravity = 9.81f;
        [SerializeField] Vector3 initialVelocity;
        [SerializeField] bool deriveMassFromSurfaceGravity = true;
        [Min(0f)]
        [SerializeField] float explicitMass = 1000000000f;
        [SerializeField] bool participatesInNBody = true;
        [SerializeField] bool lockPosition;

        Rigidbody cachedRigidbody;
        Vector3 simulatedVelocity;
        float mass;

        public string BodyName => bodyName;
        public CelestialBodyType BodyType => bodyType;
        public float Radius => radius;
        public float SurfaceGravity => surfaceGravity;
        public Vector3 InitialVelocity => initialVelocity;
        public Vector3 Velocity => simulatedVelocity;
        public float Mass => mass;
        public bool ParticipatesInNBody => participatesInNBody;
        public bool LockPosition => lockPosition;
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
            deriveMassFromSurfaceGravity = definition.DeriveMassFromSurfaceGravity;
            explicitMass = Mathf.Max(0f, definition.ExplicitMass);
            participatesInNBody = definition.ParticipatesInNBody;
            lockPosition = definition.LockPosition;

            if (!string.IsNullOrWhiteSpace(bodyName))
            {
                gameObject.name = bodyName;
            }

            if (TryGetComponent(out Rigidbody rb))
            {
                cachedRigidbody = rb;
            }

            RecalculateMass(GravitySimulation.Active != null
                ? GravitySimulation.Active.GravitationalConstant
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
            rb.isKinematic = lockPosition;
            rb.mass = Mathf.Max(0.0001f, mass);
        }

        public void ResetSimulationState()
        {
            simulatedVelocity = initialVelocity;
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
            if (lockPosition)
            {
                simulatedVelocity = Vector3.zero;
                return;
            }

            simulatedVelocity += acceleration * deltaTime;
        }

        public void IntegratePosition(float deltaTime)
        {
            if (lockPosition)
            {
                return;
            }

            Rigidbody.MovePosition(Rigidbody.position + simulatedVelocity * deltaTime);
        }
    }
}
