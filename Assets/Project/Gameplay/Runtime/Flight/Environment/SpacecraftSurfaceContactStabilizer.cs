using Farion.Core.Numerics;
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
        [SerializeField] SpacecraftMotor motor;

        [Header("Velocity Correction")]
        [SerializeField] bool removeLowSpeedClosingVelocity = true;
        [Min(0f)]
        [SerializeField] float maxCorrectedNormalSpeed = 4f;
        [Min(0f)]
        [SerializeField] float maxStabilizedTangentialSpeed = 6f;
        [Min(0f)]
        [SerializeField] float tangentialDamping = 3f;
        [Tooltip("Rate at which residual spin is bled off while resting on a surface. Suspended entirely while the pilot commands rotation so the ship can still raise its nose for takeoff.")]
        [Min(0f)]
        [SerializeField] float angularDamping = 2f;

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
        ISpacecraftPhysicsBody offlinePhysicsBody;
        bool externalSimulation;

        public bool StabilizingContact => stabilizingContact;
        public float CorrectedNormalSpeed => correctedNormalSpeed;
        public float RecoveredDistance => recoveredDistance;

        Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();

        void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            offlinePhysicsBody = new RigidbodySpacecraftPhysicsBody(cachedRigidbody);
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
            if (!externalSimulation)
            {
                Simulate(Time.fixedDeltaTime, offlinePhysicsBody);
            }
        }

        public void SetExternalSimulation(bool enabled)
        {
            externalSimulation = enabled;
        }

        public void Simulate(float deltaTime, ISpacecraftPhysicsBody physicsBody)
        {
            stabilizingContact = false;
            correctedNormalSpeed = 0f;
            recoveredDistance = 0f;

            ResolveComponents();
            if (physicsBody == null ||
                deltaTime <= 0f ||
                surfaceContactProbe == null ||
                !surfaceContactProbe.HasContact)
            {
                return;
            }

            SpacecraftSurfaceContactSample contact = surfaceContactProbe.CurrentContact;
            if (!contact.HasContact)
            {
                return;
            }

            Vector3 bodyVelocity = contact.Body != null ? contact.Body.GetVelocityAtPoint(contact.Point) : Vector3.zero;
            Vector3 relativeVelocity = physicsBody.LinearVelocity - bodyVelocity;
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
                float damping = FarionMath.SmoothFactor(tangentialDamping, deltaTime);
                relativeVelocity = normalVelocity + Vector3.Lerp(tangentialVelocity, Vector3.zero, damping);
                stabilizingContact |= tangentialVelocity.sqrMagnitude > 0.000001f;
            }

            physicsBody.SetLinearVelocity(bodyVelocity + relativeVelocity);

            if (angularDamping > 0f && !RotationCommanded)
            {
                float damping = FarionMath.SmoothFactor(angularDamping, deltaTime);
                Vector3 angularVelocity = Vector3.Lerp(
                    physicsBody.AngularVelocity,
                    Vector3.zero,
                    damping);
                physicsBody.SetAngularVelocity(angularVelocity);
                stabilizingContact |= angularVelocity.sqrMagnitude > 0.000001f;
            }

            if (recoverSmallPenetration && contact.Separation < -penetrationSlop)
            {
                float recovery = Mathf.Min(-contact.Separation + penetrationSlop, maxRecoveryDistance);
                physicsBody.MovePosition(physicsBody.Position + normal * recovery);
                recoveredDistance = recovery;
                stabilizingContact = true;
            }
        }

        bool RotationCommanded =>
            motor != null && motor.CurrentCommand.Rotation.sqrMagnitude > 0.0001f;

        void ResolveComponents()
        {
            if (surfaceContactProbe == null)
            {
                surfaceContactProbe = GetComponent<SpacecraftSurfaceContactProbe>();
            }

            if (motor == null)
            {
                motor = GetComponent<SpacecraftMotor>();
            }
        }

        void ConfigureRigidbody()
        {
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }
}
