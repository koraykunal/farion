using Farion.Core.Physics;
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
        float lastContactTime = float.NegativeInfinity;

        public SpacecraftSurfaceContactSample CurrentContact => currentContact;
        public bool HasContact => hasContact;
        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();

        void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            timeSinceLastContact = lastContactTime > 0f ? Time.time - lastContactTime : float.PositiveInfinity;
            if (Time.fixedTime - lastContactTime > Time.fixedDeltaTime * 1.5f)
            {
                ClearContact();
            }
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
            CelestialBody body = ResolveBody(collision.collider);
            if (body != null && currentContact.Body == body)
            {
                ClearContact();
            }
        }

        void CaptureContact(Collision collision)
        {
            CelestialBody body = ResolveBody(collision.collider);
            if (body == null || collision.contactCount <= 0)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            Vector3 bodyVelocity = body.GetVelocityAtPoint(contact.point);
            Vector3 relativeVelocity = Rigidbody.linearVelocity - bodyVelocity;
            currentContact = new SpacecraftSurfaceContactSample(
                body,
                contact.point,
                contact.normal,
                relativeVelocity,
                contact.separation,
                collision.contactCount,
                Time.time);

            lastContactTime = Time.fixedTime;
            ApplyDebugFields();
        }

        void ClearContact()
        {
            currentContact = SpacecraftSurfaceContactSample.Empty;
            ApplyDebugFields();
        }

        void ApplyDebugFields()
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
