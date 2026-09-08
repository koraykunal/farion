using Farion.Gameplay.Actors;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(80)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CelestialActorProbe))]
    public sealed class SpacecraftSurfaceGuard : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] CelestialActorProbe celestialProbe;
        [SerializeField] SpacecraftHull hull;

        [Header("Envelope")]
        [Tooltip("Altitude of the hull centre above the analytic terrain that the guard settles the ship at. Roughly hull half-height plus landing gear.")]
        [Min(0f)]
        [SerializeField] float surfaceClearanceMeters = 1.5f;
        [Tooltip("How far below the clearance the hull may sink before the guard engages. Leaves room for the coarser collision mesh to sit slightly under the rendered terrain without the guard fighting it.")]
        [Min(0.1f)]
        [SerializeField] float penetrationToleranceMeters = 3f;

        [Header("Runtime")]
        [SerializeField] bool engaged;
        [SerializeField] float lastCorrectionMeters;
        [SerializeField] float lastRemovedClosingSpeed;
        [SerializeField] float lastImpactSpeed;

        Rigidbody cachedRigidbody;
        ISpacecraftPhysicsBody offlinePhysicsBody;
        bool externalSimulation;

        public bool Engaged => engaged;
        public float LastCorrectionMeters => lastCorrectionMeters;
        public float LastImpactSpeed => lastImpactSpeed;
        public SpacecraftSurfaceGuardSettings Settings =>
            new(surfaceClearanceMeters, penetrationToleranceMeters);

        Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();

        void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            offlinePhysicsBody = new RigidbodySpacecraftPhysicsBody(cachedRigidbody);
            ResolveComponents();
        }

        void OnValidate()
        {
            ResolveComponents();
        }

        void FixedUpdate()
        {
            if (!externalSimulation && !Rigidbody.isKinematic)
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
            lastCorrectionMeters = 0f;
            lastRemovedClosingSpeed = 0f;
            lastImpactSpeed = 0f;

            ResolveComponents();
            CelestialFrameProvider provider = celestialProbe != null ? celestialProbe.FrameProvider : null;
            if (physicsBody == null ||
                deltaTime <= 0f ||
                provider == null ||
                !celestialProbe.HasSample)
            {
                engaged = false;
                return;
            }

            CelestialFrameSample frame = celestialProbe.CurrentSample;
            Vector3 surfaceRelativeVelocity = physicsBody.LinearVelocity - frame.BodyPointVelocity;
            CelestialSurfaceSample predicted = provider.SampleSurface(
                frame.Body,
                physicsBody.Position + surfaceRelativeVelocity * deltaTime);
            bool predictedIsDeeper = predicted.SurfaceDistance < frame.SurfaceAltitude;

            SpacecraftSurfaceGuardOutput output = SpacecraftSurfaceGuardLaw.Evaluate(
                new SpacecraftSurfaceGuardFrame(
                    frame.SurfaceAltitude,
                    predicted.SurfaceDistance,
                    predictedIsDeeper ? predicted.Normal : frame.SurfaceNormal,
                    frame.RadialUp,
                    surfaceRelativeVelocity,
                    engaged),
                Settings);
            engaged = output.Engaged;
            if (!engaged)
            {
                return;
            }

            if (output.CorrectsVelocity)
            {
                physicsBody.SetLinearVelocity(frame.BodyPointVelocity + output.SurfaceRelativeVelocity);
                lastRemovedClosingSpeed = output.RemovedClosingSpeed;
            }

            if (output.CorrectsPosition)
            {
                physicsBody.MovePosition(physicsBody.Position + output.PositionCorrection);
                lastCorrectionMeters = output.PositionCorrection.magnitude;
            }

            if (output.ImpactSpeed > 0f)
            {
                lastImpactSpeed = output.ImpactSpeed;
                hull?.RegisterImpact(output.ImpactSpeed);
            }
        }

        void ResolveComponents()
        {
            if (celestialProbe == null)
            {
                celestialProbe = GetComponent<CelestialActorProbe>();
            }

            if (hull == null)
            {
                hull = GetComponent<SpacecraftHull>();
            }
        }
    }
}
