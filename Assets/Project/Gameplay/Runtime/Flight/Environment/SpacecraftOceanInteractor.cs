using Farion.Gameplay.Actors;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(46)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CelestialActorProbe))]
    public sealed class SpacecraftOceanInteractor : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] SpacecraftOceanInteractionProfile profile;

        [Header("Source")]
        [SerializeField] CelestialActorProbe celestialProbe;

        [Header("Forces")]
        [SerializeField] bool applyBuoyancy = true;
        [SerializeField] bool applyDrag = true;
        [SerializeField] bool dampAngularVelocity = true;

        [Header("Runtime Ocean")]
        [SerializeField] bool hasOcean;
        [SerializeField] bool touchingWater;
        [SerializeField] bool centerBelowWater;
        [SerializeField] float submergedFraction;
        [SerializeField] float waterDepth;
        [SerializeField] float waterEntrySpeed;
        [SerializeField] float buoyancyAcceleration;
        [SerializeField] float dragAcceleration;
        [SerializeField] float pressureStress;
        [SerializeField] bool unsafeWaterEntry;
        [SerializeField] bool crushingDepth;

        Rigidbody cachedRigidbody;
        ISpacecraftPhysicsBody offlinePhysicsBody;
        SpacecraftOceanInteractionSample currentInteraction;
        bool externalSimulation;

        public SpacecraftOceanInteractionSample CurrentInteraction => currentInteraction;
        public bool IsTouchingWater => currentInteraction.IsTouchingWater;
        public bool IsCenterBelowWater => currentInteraction.IsCenterBelowWater;
        public bool UnsafeWaterEntry => currentInteraction.UnsafeWaterEntry;
        public bool CrushingDepth => currentInteraction.CrushingDepth;
        public Rigidbody Rigidbody => cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();

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
            if (physicsBody == null || deltaTime <= 0f)
            {
                return;
            }

            RefreshInteraction();
            ApplyInteractionForces(deltaTime, physicsBody);
        }

        [ContextMenu("Refresh Ocean Interaction")]
        public void RefreshInteraction()
        {
            ResolveComponents();

            if (profile == null || celestialProbe == null || !celestialProbe.HasSample)
            {
                currentInteraction = SpacecraftOceanInteractionSample.Empty(
                    celestialProbe != null
                        ? celestialProbe.CurrentSample
                        : default);
                ApplyRuntimeState();
                return;
            }

            currentInteraction = profile.Evaluate(celestialProbe.CurrentSample);
            ApplyRuntimeState();
        }

        void ApplyInteractionForces(
            float deltaTime,
            ISpacecraftPhysicsBody physicsBody)
        {
            if (!currentInteraction.IsTouchingWater)
            {
                return;
            }

            if (applyBuoyancy && currentInteraction.BuoyancyAcceleration.sqrMagnitude > 0.0001f)
            {
                physicsBody.AddForce(
                    currentInteraction.BuoyancyAcceleration,
                    ForceMode.Acceleration);
            }

            if (applyDrag && currentInteraction.DragAcceleration.sqrMagnitude > 0.0001f)
            {
                physicsBody.AddForce(
                    currentInteraction.DragAcceleration,
                    ForceMode.Acceleration);
            }

            if (dampAngularVelocity && profile != null && profile.AngularDamping > 0f)
            {
                float damping = 1f - Mathf.Exp(
                    -profile.AngularDamping *
                    currentInteraction.SubmergedFraction *
                    deltaTime);
                physicsBody.SetAngularVelocity(Vector3.Lerp(
                    physicsBody.AngularVelocity,
                    Vector3.zero,
                    damping));
            }
        }

        void ApplyRuntimeState()
        {
            hasOcean = currentInteraction.HasOcean;
            touchingWater = currentInteraction.IsTouchingWater;
            centerBelowWater = currentInteraction.IsCenterBelowWater;
            submergedFraction = currentInteraction.SubmergedFraction;
            waterDepth = currentInteraction.WaterDepth;
            waterEntrySpeed = currentInteraction.WaterEntrySpeed;
            buoyancyAcceleration = currentInteraction.BuoyancyAcceleration.magnitude;
            dragAcceleration = currentInteraction.DragAcceleration.magnitude;
            pressureStress = currentInteraction.PressureStress;
            unsafeWaterEntry = currentInteraction.UnsafeWaterEntry;
            crushingDepth = currentInteraction.CrushingDepth;
        }

        void ResolveComponents()
        {
            if (celestialProbe == null)
            {
                celestialProbe = GetComponent<CelestialActorProbe>();
            }
        }
    }
}

