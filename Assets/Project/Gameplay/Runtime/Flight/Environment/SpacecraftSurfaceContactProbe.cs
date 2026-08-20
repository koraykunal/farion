using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(45)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SpacecraftSurfaceContactProbe : MonoBehaviour
    {
        [Header("Runtime Contact")]
        [SerializeField] bool hasContact;
        [SerializeField] string contactBodyName;
        [SerializeField] Vector3 contactPoint;
        [SerializeField] Vector3 contactNormal = Vector3.up;
        [SerializeField] float normalSpeed;
        [SerializeField] float tangentialSpeed;
        [SerializeField] float separation;
        [SerializeField] int contactCount;
        [SerializeField] float timeSinceLastContact;

        Rigidbody cachedRigidbody;
        SpacecraftSurfaceContactSample currentContact = SpacecraftSurfaceContactSample.Empty;
        CelestialBody accumulatedBody;
        Vector3 accumulatedPoint;
        Vector3 accumulatedNormal;
        float accumulatedSeparation;
        int accumulatedContactCount;
        bool contactReportedSinceLastStep;
        bool externalSimulation;

        public SpacecraftSurfaceContactSample CurrentContact => currentContact;
        public bool HasContact => hasContact;
        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();

        void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (!externalSimulation)
            {
                BeginSimulationStep(Time.fixedDeltaTime);
            }
        }

        public void SetExternalSimulation(bool enabled)
        {
            externalSimulation = enabled;
        }

        public void BeginSimulationStep(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            timeSinceLastContact = contactReportedSinceLastStep
                ? 0f
                : timeSinceLastContact + deltaTime;
            if (!contactReportedSinceLastStep &&
                timeSinceLastContact > deltaTime * 1.5f)
            {
                ClearContact();
            }

            contactReportedSinceLastStep = false;
            ResetAccumulator(null);
        }

        void OnCollisionEnter(Collision collision)
        {
            CaptureContact(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            CaptureContact(collision);
        }

        void OnCollisionExit(Collision collision)
        {
            // FixedUpdate time-out clears the sample after every collider pair
            // has stopped reporting contact.
        }

        void CaptureContact(Collision collision)
        {
            CelestialBody body = ResolveBody(collision.collider);
            if (body == null || collision.contactCount <= 0)
            {
                return;
            }

            if (accumulatedBody != body)
            {
                ResetAccumulator(body);
            }

            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                accumulatedPoint += contact.point;
                accumulatedNormal += contact.normal;
                accumulatedSeparation = Mathf.Min(accumulatedSeparation, contact.separation);
                accumulatedContactCount++;
            }

            Vector3 point = accumulatedPoint / accumulatedContactCount;
            Vector3 normal = accumulatedNormal.sqrMagnitude > 0.0001f
                ? accumulatedNormal.normalized
                : Vector3.up;
            Vector3 bodyVelocity = body.GetVelocityAtPoint(point);
            Vector3 relativeVelocity = Rigidbody.GetPointVelocity(point) - bodyVelocity;
            currentContact = new SpacecraftSurfaceContactSample(
                body,
                point,
                normal,
                relativeVelocity,
                accumulatedSeparation,
                accumulatedContactCount,
                Time.time);

            contactReportedSinceLastStep = true;
            timeSinceLastContact = 0f;
            ApplyRuntimeState();
        }

        void ClearContact()
        {
            currentContact = SpacecraftSurfaceContactSample.Empty;
            accumulatedBody = null;
            accumulatedContactCount = 0;
            ApplyRuntimeState();
        }

        void ResetAccumulator(CelestialBody body)
        {
            accumulatedBody = body;
            accumulatedPoint = Vector3.zero;
            accumulatedNormal = Vector3.zero;
            accumulatedSeparation = float.PositiveInfinity;
            accumulatedContactCount = 0;
        }

        void ApplyRuntimeState()
        {
            hasContact = currentContact.HasContact;
            contactBodyName = hasContact ? currentContact.Body.BodyName : string.Empty;
            contactPoint = currentContact.Point;
            contactNormal = currentContact.Normal;
            normalSpeed = hasContact ? currentContact.NormalSpeed : 0f;
            tangentialSpeed = hasContact ? currentContact.TangentialSpeed : 0f;
            separation = hasContact ? currentContact.Separation : 0f;
            contactCount = hasContact ? currentContact.ContactCount : 0;
        }

        static CelestialBody ResolveBody(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            if (collider.TryGetComponent(out CelestialBody body))
            {
                return body;
            }

            return collider.GetComponentInParent<CelestialBody>();
        }
    }
}

