using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(75)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SpacecraftSurfaceContactProbe))]
    public sealed class SpacecraftSurfaceContactStabilizer : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] SpacecraftSurfaceContactProbe surfaceContactProbe;

        [Header("Velocity Correction")]
        [SerializeField] bool removeLowSpeedClosingVelocity = true;
        [Min(0f)]
        [SerializeField] float maxCorrectedNormalSpeed = 4f;
        [Min(0f)]
        [SerializeField] float maxStabilizedTangentialSpeed = 6f;
        [Min(0f)]
        [SerializeField] float tangentialDamping = 3f;
        [Min(0f)]
        [SerializeField] float angularDamping = 6f;

        [Header("Penetration Recovery")]
        [SerializeField] bool recoverSmallPenetration = true;
        [Min(0f)]
        [SerializeField] float penetrationSlop = 0.02f;
        [Min(0f)]
        [SerializeField] float maxRecoveryDistance = 0.35f;

        [Header("Runtime")]
        [SerializeField] bool stabilizingContact;
        [SerializeField] float correctedNormalSpeed;
        [SerializeField] float recoveredDistance;

        Rigidbody cachedRigidbody;

        public bool StabilizingContact => stabilizingContact;
        public float CorrectedNormalSpeed => correctedNormalSpeed;
        public float RecoveredDistance => recoveredDistance;

        Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();

        void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            ResolveComponents();
            ConfigureRigidbody();
        }

        void OnValidate()
        {
            maxCorrectedNormalSpeed = Mathf.Max(0f, maxCorrectedNormalSpeed);
            maxStabilizedTangentialSpeed = Mathf.Max(0f, maxStabilizedTangentialSpeed);
            tangentialDamping = Mathf.Max(0f, tangentialDamping);
            angularDamping = Mathf.Max(0f, angularDamping);
            penetrationSlop = Mathf.Max(0f, penetrationSlop);
            maxRecoveryDistance = Mathf.Max(0f, maxRecoveryDistance);
            ResolveComponents();
        }

        void FixedUpdate()
        {
            stabilizingContact = false;
            correctedNormalSpeed = 0f;
            recoveredDistance = 0f;

            ResolveComponents();
            if (surfaceContactProbe == null || !surfaceContactProbe.HasContact)
            {
                return;
            }

            SpacecraftSurfaceContactSample contact = surfaceContactProbe.CurrentContact;
            if (!contact.HasContact)
            {
                return;
            }

            Vector3 bodyVelocity = contact.Body != null ? contact.Body.GetVelocityAtPoint(contact.Point) : Vector3.zero;
            Vector3 relativeVelocity = Rigidbody.linearVelocity - bodyVelocity;
            Vector3 normal = contact.Normal;
            float closingSpeed = Mathf.Max(0f, Vector3.Dot(relativeVelocity, -normal));
            bool canStabilize = closingSpeed <= maxCorrectedNormalSpeed &&
                contact.TangentialSpeed <= maxStabilizedTangentialSpeed;
            if (!canStabilize)
            {
                return;
            }

            if (removeLowSpeedClosingVelocity && closingSpeed > 0f)
            {
                relativeVelocity += normal * closingSpeed;
                correctedNormalSpeed = closingSpeed;
                stabilizingContact = true;
            }

            if (tangentialDamping > 0f)
            {
                Vector3 normalVelocity = Vector3.Project(relativeVelocity, normal);
                Vector3 tangentialVelocity = relativeVelocity - normalVelocity;
                float damping = 1f - Mathf.Exp(-tangentialDamping * Time.fixedDeltaTime);
                relativeVelocity = normalVelocity + Vector3.Lerp(tangentialVelocity, Vector3.zero, damping);
                stabilizingContact |= tangentialVelocity.sqrMagnitude > 0.000001f;
            }

            Rigidbody.linearVelocity = bodyVelocity + relativeVelocity;

            if (angularDamping > 0f)
            {
                float damping = 1f - Mathf.Exp(-angularDamping * Time.fixedDeltaTime);
                Rigidbody.angularVelocity = Vector3.Lerp(Rigidbody.angularVelocity, Vector3.zero, damping);
                stabilizingContact |= Rigidbody.angularVelocity.sqrMagnitude > 0.000001f;
            }

            if (recoverSmallPenetration && contact.Separation < -penetrationSlop)
            {
                float recovery = Mathf.Min(-contact.Separation + penetrationSlop, maxRecoveryDistance);
                Rigidbody.MovePosition(Rigidbody.position + normal * recovery);
                recoveredDistance = recovery;
                stabilizingContact = true;
            }
        }

        void ResolveComponents()
        {
            if (surfaceContactProbe == null)
            {
                surfaceContactProbe = GetComponent<SpacecraftSurfaceContactProbe>();
            }
        }

        void ConfigureRigidbody()
        {
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }
}
