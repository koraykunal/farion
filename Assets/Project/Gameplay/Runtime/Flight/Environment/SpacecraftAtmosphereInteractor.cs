using Farion.Gameplay.Actors;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(47)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CelestialActorProbe))]
    public sealed class SpacecraftAtmosphereInteractor : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] SpacecraftAtmosphereInteractionProfile profile;

        [Header("Source")]
        [SerializeField] CelestialActorProbe celestialProbe;

        [Header("Forces")]
        [SerializeField] bool applyDrag = true;

        [Header("Runtime Atmosphere")]
        [SerializeField] bool insideAtmosphere;
        [SerializeField, Range(0f, 1f)] float atmosphereDensity;
        [SerializeField] float relativeSpeed;
        [SerializeField] float dynamicPressure;
        [SerializeField, Range(0f, 1f)] float dynamicPressureLoad;
        [SerializeField] float heatingRate;
        [SerializeField, Range(0f, 1f)] float heatLoad;
        [SerializeField] float dragAcceleration;

        Rigidbody cachedRigidbody;
        ISpacecraftPhysicsBody offlinePhysicsBody;
        SpacecraftAtmosphereInteractionSample currentInteraction;
        bool externalSimulation;

        public SpacecraftAtmosphereInteractionSample CurrentInteraction => currentInteraction;
        public Rigidbody Rigidbody =>
            cachedRigidbody != null ? cachedRigidbody : cachedRigidbody = GetComponent<Rigidbody>();

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
            ApplyInteractionForce(physicsBody);
        }

        void OnDisable()
        {
            currentInteraction = SpacecraftAtmosphereInteractionSample.Empty(
                celestialProbe != null ? celestialProbe.CurrentSample : default);
            ApplyRuntimeState();
        }

        [ContextMenu("Refresh Atmosphere Interaction")]
        public void RefreshInteraction()
        {
            ResolveComponents();

            if (profile == null || celestialProbe == null || !celestialProbe.HasSample)
            {
                currentInteraction = SpacecraftAtmosphereInteractionSample.Empty(
                    celestialProbe != null ? celestialProbe.CurrentSample : default);
                ApplyRuntimeState();
                return;
            }

            currentInteraction = profile.Evaluate(celestialProbe.CurrentSample);
            ApplyRuntimeState();
        }

        void ApplyInteractionForce(ISpacecraftPhysicsBody physicsBody)
        {
            if (!applyDrag || currentInteraction.DragAcceleration.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            physicsBody.AddForce(
                currentInteraction.DragAcceleration,
                ForceMode.Acceleration);
        }

        void ApplyRuntimeState()
        {
            insideAtmosphere = currentInteraction.IsInsideAtmosphere;
            atmosphereDensity = currentInteraction.AtmosphereDensity;
            relativeSpeed = currentInteraction.RelativeSpeed;
            dynamicPressure = currentInteraction.DynamicPressure;
            dynamicPressureLoad = currentInteraction.DynamicPressureLoad;
            heatingRate = currentInteraction.HeatingRate;
            heatLoad = currentInteraction.HeatLoad;
            dragAcceleration = currentInteraction.DragAcceleration.magnitude;
        }

        void ResolveComponents()
        {
            celestialProbe ??= GetComponent<CelestialActorProbe>();
        }
    }
}
