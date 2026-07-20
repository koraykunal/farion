using Farion.Core.Physics;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SpacecraftMotor : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] MonoBehaviour inputSource;

        [Header("Gravity")]
        [SerializeField] GravitySimulation simulation;
        [SerializeField] bool applyGravity = true;

        [Header("Translation")]
        [Min(0f)]
        [SerializeField] float thrustAcceleration = 18f;
        [Min(1f)]
        [SerializeField] float boostMultiplier = 3f;

        [Header("Rotation")]
        [Min(0f)]
        [SerializeField] float yawDegreesPerSecond = 90f;
        [Min(0f)]
        [SerializeField] float pitchDegreesPerSecond = 90f;
        [Min(0f)]
        [SerializeField] float rollDegreesPerSecond = 120f;
        [Min(0f)]
        [SerializeField] float rotationResponsiveness = 12f;

        [Header("Surface Contact")]
        [SerializeField] SpacecraftSurfaceContactProbe surfaceContactProbe;
        [SerializeField] bool suspendRotationWhileInSurfaceContact = true;

        Rigidbody cachedRigidbody;
        ISpacecraftInputSource resolvedInput;
        SpacecraftInputState currentInput;
        Quaternion targetRotation;
        Vector3 lastGravityAcceleration;
        Vector3 lastThrustAcceleration;

        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();
        public Vector3 LastGravityAcceleration => lastGravityAcceleration;
        public Vector3 LastThrustAcceleration => lastThrustAcceleration;
        public Vector3 Velocity => Rigidbody.linearVelocity;
        public float Speed => Velocity.magnitude;
        public bool RotationSuspendedByContact => suspendRotationWhileInSurfaceContact &&
            surfaceContactProbe != null &&
            surfaceContactProbe.HasContact;

        void Awake()
        {
            ConfigureRigidbody();
            ResolveInputSource();
            ResolveContactProbe();
            targetRotation = Rigidbody.rotation;
        }

        void OnValidate()
        {
            thrustAcceleration = Mathf.Max(0f, thrustAcceleration);
            boostMultiplier = Mathf.Max(1f, boostMultiplier);
            yawDegreesPerSecond = Mathf.Max(0f, yawDegreesPerSecond);
            pitchDegreesPerSecond = Mathf.Max(0f, pitchDegreesPerSecond);
            rollDegreesPerSecond = Mathf.Max(0f, rollDegreesPerSecond);
            rotationResponsiveness = Mathf.Max(0f, rotationResponsiveness);

            if (inputSource != null && inputSource is not ISpacecraftInputSource)
            {
                inputSource = null;
            }

            ResolveContactProbe();
        }

        void Update()
        {
            ResolveInputSource();
            ResolveContactProbe();
            currentInput = resolvedInput?.CurrentInput ?? SpacecraftInputState.None;
            IntegrateTargetRotation(UnityEngine.Time.deltaTime);
        }

        void FixedUpdate()
        {
            ApplyGravity();
            ApplyThrust();
            ApplyRotation();
        }

        public void SetInputSource(ISpacecraftInputSource source)
        {
            resolvedInput = source;
            inputSource = source as MonoBehaviour;
        }

        void ConfigureRigidbody()
        {
            Rigidbody.useGravity = false;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Rigidbody.centerOfMass = Vector3.zero;
        }

        void ResolveInputSource()
        {
            if (inputSource is ISpacecraftInputSource explicitSource)
            {
                resolvedInput = explicitSource;
                return;
            }

            resolvedInput ??= GetComponent<ISpacecraftInputSource>();
        }

        void ResolveContactProbe()
        {
            if (surfaceContactProbe == null)
            {
                surfaceContactProbe = GetComponent<SpacecraftSurfaceContactProbe>();
            }
        }

        void ApplyGravity()
        {
            lastGravityAcceleration = Vector3.zero;
            if (!applyGravity)
            {
                return;
            }

            GravitySimulation source = simulation != null ? simulation : GravitySimulation.Active;
            if (source == null)
            {
                return;
            }

            lastGravityAcceleration = source.CalculateAcceleration(Rigidbody.position);
            Rigidbody.AddForce(lastGravityAcceleration, ForceMode.Acceleration);
        }

        void ApplyThrust()
        {
            float multiplier = currentInput.Boost ? boostMultiplier : 1f;
            lastThrustAcceleration = transform.TransformDirection(currentInput.Translation) * (thrustAcceleration * multiplier);
            Rigidbody.AddForce(lastThrustAcceleration, ForceMode.Acceleration);
        }

        void IntegrateTargetRotation(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            Quaternion yaw = Quaternion.AngleAxis(currentInput.Look.x * yawDegreesPerSecond * deltaTime, transform.up);
            Quaternion pitch = Quaternion.AngleAxis(-currentInput.Look.y * pitchDegreesPerSecond * deltaTime, transform.right);
            Quaternion roll = Quaternion.AngleAxis(-currentInput.Roll * rollDegreesPerSecond * deltaTime, transform.forward);
            targetRotation = yaw * pitch * roll * targetRotation;
        }

        void ApplyRotation()
        {
            if (RotationSuspendedByContact)
            {
                targetRotation = Rigidbody.rotation;
                return;
            }

            if (rotationResponsiveness <= 0f)
            {
                Rigidbody.MoveRotation(targetRotation);
                return;
            }

            Quaternion nextRotation = Quaternion.Slerp(
                Rigidbody.rotation,
                targetRotation,
                rotationResponsiveness * UnityEngine.Time.fixedDeltaTime);

            Rigidbody.MoveRotation(nextRotation);
        }
    }
}
