using Farion.Gameplay.Actors;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialActorProbe))]
    public sealed class SpacecraftLandingComputer : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] SpacecraftLandingProfile profile;

        [Header("Source")]
        [SerializeField] CelestialActorProbe celestialProbe;
        [SerializeField] SpacecraftSurfaceContactProbe surfaceContactProbe;

        [Header("Runtime Assessment")]
        [SerializeField] SpacecraftApproachPhase phase = SpacecraftApproachPhase.NoFrame;
        [SerializeField] SpacecraftLandingRiskFlags risks = SpacecraftLandingRiskFlags.NoFrame;
        [SerializeField] float normalizedStress;
        [SerializeField] float verticalSpeedLimit;
        [SerializeField] float tangentialSpeedLimit;
        [SerializeField] float surfaceSlopeAngle;
        [SerializeField] float surfaceSlopeLimit;
        [SerializeField] bool safeTouchdownWindow;
        [SerializeField] bool impactRisk;
        [SerializeField] bool hasSurfaceContact;
        [SerializeField] bool touchdownConfirmed;
        [SerializeField] bool unsafeSurfaceContact;

        SpacecraftLandingAssessment currentAssessment;

        public SpacecraftLandingAssessment CurrentAssessment => currentAssessment;
        public SpacecraftApproachPhase Phase => phase;
        public SpacecraftLandingRiskFlags Risks => risks;
        public float NormalizedStress => normalizedStress;
        public bool SafeTouchdownWindow => safeTouchdownWindow;
        public bool ImpactRisk => impactRisk;
        public bool TouchdownConfirmed => touchdownConfirmed;
        public bool UnsafeSurfaceContact => unsafeSurfaceContact;

        void Awake()
        {
            ResolveComponents();
        }

        void OnValidate()
        {
            ResolveComponents();
        }

        void FixedUpdate()
        {
            RefreshAssessment();
        }

        [ContextMenu("Refresh Landing Assessment")]
        public void RefreshAssessment()
        {
            ResolveComponents();

            if (profile == null || celestialProbe == null)
            {
                currentAssessment = default;
                ApplyDebugFields();
                return;
            }

            currentAssessment = profile.Evaluate(celestialProbe.CurrentSample);
            ApplyDebugFields();
        }

        void ResolveComponents()
        {
            if (celestialProbe == null)
            {
                celestialProbe = GetComponent<CelestialActorProbe>();
            }

            if (surfaceContactProbe == null)
            {
                surfaceContactProbe = GetComponent<SpacecraftSurfaceContactProbe>();
            }
        }

        void ApplyDebugFields()
        {
            SpacecraftSurfaceContactSample contact = surfaceContactProbe != null
                ? surfaceContactProbe.CurrentContact
                : SpacecraftSurfaceContactSample.Empty;
            hasSurfaceContact = contact.HasContact;

            if (!currentAssessment.HasFrame)
            {
                phase = SpacecraftApproachPhase.NoFrame;
                risks = SpacecraftLandingRiskFlags.NoFrame;
                normalizedStress = 0f;
                verticalSpeedLimit = 0f;
                tangentialSpeedLimit = 0f;
                surfaceSlopeAngle = 0f;
                surfaceSlopeLimit = 0f;
                safeTouchdownWindow = false;
                impactRisk = false;
                touchdownConfirmed = false;
                unsafeSurfaceContact = hasSurfaceContact;
                return;
            }

            phase = currentAssessment.Phase;
            risks = currentAssessment.Risks;
            normalizedStress = currentAssessment.NormalizedStress;
            verticalSpeedLimit = currentAssessment.VerticalSpeedLimit;
            tangentialSpeedLimit = currentAssessment.TangentialSpeedLimit;
            surfaceSlopeAngle = currentAssessment.Frame.SurfaceSlopeAngleDegrees;
            surfaceSlopeLimit = currentAssessment.SurfaceSlopeLimit;
            safeTouchdownWindow = currentAssessment.IsSafeTouchdownWindow;
            impactRisk = currentAssessment.HasImpactRisk;
            touchdownConfirmed = hasSurfaceContact &&
                safeTouchdownWindow &&
                contact.NormalSpeed <= verticalSpeedLimit &&
                contact.TangentialSpeed <= tangentialSpeedLimit;
            unsafeSurfaceContact = hasSurfaceContact && !touchdownConfirmed;
        }
    }
}
