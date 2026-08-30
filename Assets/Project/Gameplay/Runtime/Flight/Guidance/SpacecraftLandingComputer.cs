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

        SpacecraftLandingAssessment currentAssessment;
        bool touchdownConfirmed;
        bool unsafeSurfaceContact;

        public SpacecraftLandingAssessment CurrentAssessment => currentAssessment;
        public SpacecraftApproachPhase Phase => currentAssessment.Phase;
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

            currentAssessment = profile != null && celestialProbe != null
                ? profile.Evaluate(celestialProbe.CurrentSample)
                : default;
            RefreshTouchdownState();
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

        void RefreshTouchdownState()
        {
            SpacecraftSurfaceContactSample contact = surfaceContactProbe != null
                ? surfaceContactProbe.CurrentContact
                : SpacecraftSurfaceContactSample.Empty;
            if (!currentAssessment.HasFrame)
            {
                touchdownConfirmed = false;
                unsafeSurfaceContact = contact.HasContact;
                return;
            }

            float gravityScale = profile.EvaluateGravitySpeedScale(currentAssessment.Frame);
            touchdownConfirmed = SpacecraftTouchdownEvaluator.IsSafe(
                contact,
                currentAssessment.Frame.SurfaceSlopeAngleDegrees,
                profile.SafeTouchdownVerticalSpeed * gravityScale,
                profile.SafeTouchdownTangentialSpeed * gravityScale,
                profile.SafeTouchdownSlopeAngle);
            unsafeSurfaceContact = contact.HasContact && !touchdownConfirmed;
        }
    }
}

