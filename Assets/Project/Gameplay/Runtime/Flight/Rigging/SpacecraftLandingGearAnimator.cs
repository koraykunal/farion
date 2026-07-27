using System;
using Farion.Gameplay.Actors;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftLandingGearAnimator : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] Transform visualRoot;
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] CelestialActorProbe celestialProbe;
        [SerializeField] SpacecraftSurfaceContactProbe surfaceContactProbe;
        [SerializeField] SpacecraftRampController rampController;
        [SerializeField] MonoBehaviour inputSource;

        [Header("Deployment")]
        [SerializeField] bool landingGearStartsDeployed = true;
        [SerializeField] bool allowManualToggle = true;
        [SerializeField] bool deployOnSurfaceContact = true;
        [SerializeField] bool deployWhenRampOpen = true;
        [SerializeField] bool deployNearSurface;
        [Min(0f)]
        [SerializeField] float deployAltitude = 8f;
        [Min(0f)]
        [SerializeField] float retractAltitude = 18f;
        [Min(0f)]
        [SerializeField] float retractSpeed = 6f;
        [Min(0f)]
        [SerializeField] float decisionHoldSeconds = 0.2f;
        [Min(0.01f)]
        [SerializeField] float transitionSeconds = 1.25f;
        [SerializeField] SpacecraftMechanicalPartPose[] landingGearParts = Array.Empty<SpacecraftMechanicalPartPose>();

        [Header("Collision")]
        [SerializeField] Collider[] landingGearColliders = Array.Empty<Collider>();
        [Range(0f, 1f)]
        [SerializeField] float colliderEnableThreshold = 0.72f;

        [Header("Runtime")]
        [Range(0f, 1f)]
        [SerializeField] float deployedAmount;

        ISpacecraftInputSource resolvedInput;
        bool commandedDeployed;
        bool deployDecision;
        float decisionTimer;

        public float DeployedAmount => deployedAmount;
        public bool IsDeployed => deployedAmount >= 0.999f;
        public bool IsCommandedDeployed => commandedDeployed;
        public bool IsTransitioning => deployedAmount > 0.001f && deployedAmount < 0.999f;

        public void SetCommandedDeployed(bool shouldDeploy)
        {
            commandedDeployed = shouldDeploy;
        }

        public void ToggleCommandedDeployment()
        {
            commandedDeployed = !commandedDeployed;
        }

        void Reset()
        {
            AutoAssignReferences();
        }

        void Awake()
        {
            AutoAssignReferences();
            InitializeParts();
        }

        void OnValidate()
        {
            deployAltitude = Mathf.Max(0f, deployAltitude);
            retractAltitude = Mathf.Max(deployAltitude, retractAltitude);
            retractSpeed = Mathf.Max(0f, retractSpeed);
            decisionHoldSeconds = Mathf.Max(0f, decisionHoldSeconds);
            transitionSeconds = Mathf.Max(0.01f, transitionSeconds);
            colliderEnableThreshold = Mathf.Clamp01(colliderEnableThreshold);
            ValidateParts();
            AutoAssignReferences();
        }

        void Update()
        {
            UpdateManualCommand();
            float target = UpdateDeployDecision(Time.deltaTime) ? 1f : 0f;
            float speed = 1f / Mathf.Max(0.01f, transitionSeconds);
            deployedAmount = Mathf.MoveTowards(deployedAmount, target, speed * Time.deltaTime);
            ApplyParts();
            ApplyLandingGearColliders();
        }

        void AutoAssignReferences()
        {
            SpacecraftRig rig = GetComponent<SpacecraftRig>();
            if (rig != null)
            {
                visualRoot ??= rig.VisualRoot;
                rampController ??= rig.RampController;
            }

            motor ??= GetComponent<SpacecraftMotor>();
            celestialProbe ??= GetComponent<CelestialActorProbe>();
            surfaceContactProbe ??= GetComponent<SpacecraftSurfaceContactProbe>();
            if (inputSource is ISpacecraftInputSource explicitInput)
            {
                resolvedInput = explicitInput;
            }
            else
            {
                resolvedInput ??= GetComponent<ISpacecraftInputSource>();
                inputSource = resolvedInput as MonoBehaviour;
            }

            if (landingGearColliders == null || landingGearColliders.Length == 0)
            {
                Transform footprint = SpacecraftRigTransformResolver.FindChild(
                    transform,
                    "COL_Landing_Footprint");
                Collider footprintCollider = footprint != null
                    ? footprint.GetComponent<Collider>()
                    : null;
                landingGearColliders = footprintCollider != null
                    ? new[] { footprintCollider }
                    : Array.Empty<Collider>();
            }
        }

        void InitializeParts()
        {
            deployedAmount = landingGearStartsDeployed ? 1f : 0f;
            commandedDeployed = landingGearStartsDeployed;
            deployDecision = landingGearStartsDeployed;
            decisionTimer = 0f;

            if (landingGearParts == null)
            {
                return;
            }

            for (int i = 0; i < landingGearParts.Length; i++)
            {
                SpacecraftMechanicalPartPose partPose = landingGearParts[i];
                if (partPose != null && !partPose.PrepareDrivenTransform(visualRoot))
                {
                    Debug.LogWarning(
                        $"{nameof(SpacecraftLandingGearAnimator)} could not bind landing gear part " +
                        $"'{partPose.PartName}'.",
                        this);
                }
            }

            for (int i = 0; i < landingGearParts.Length; i++)
            {
                SpacecraftMechanicalPartPose partPose = landingGearParts[i];
                if (partPose == null || string.IsNullOrWhiteSpace(partPose.ParentPartName))
                {
                    continue;
                }

                SpacecraftMechanicalPartPose parentPose = FindPartPose(partPose.ParentPartName);
                if (parentPose == null || !partPose.AttachTo(parentPose))
                {
                    Debug.LogWarning(
                        $"{nameof(SpacecraftLandingGearAnimator)} could not attach '{partPose.PartName}' " +
                        $"to '{partPose.ParentPartName}'.",
                        this);
                }
            }

            for (int i = 0; i < landingGearParts.Length; i++)
            {
                landingGearParts[i]?.CaptureInitialPose(visualRoot);
            }

            ApplyParts();
            ApplyLandingGearColliders();
        }

        SpacecraftMechanicalPartPose FindPartPose(string partName)
        {
            for (int i = 0; i < landingGearParts.Length; i++)
            {
                SpacecraftMechanicalPartPose candidate = landingGearParts[i];
                if (candidate != null &&
                    string.Equals(candidate.PartName, partName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        void UpdateManualCommand()
        {
            if (resolvedInput == null)
            {
                AutoAssignReferences();
            }

            if (allowManualToggle &&
                resolvedInput != null &&
                resolvedInput.CurrentInput.ToggleLandingGear)
            {
                commandedDeployed = !commandedDeployed;
            }
        }

        void ApplyLandingGearColliders()
        {
            if (landingGearColliders == null)
            {
                return;
            }

            bool shouldEnable = deployedAmount >= colliderEnableThreshold;
            for (int i = 0; i < landingGearColliders.Length; i++)
            {
                Collider gearCollider = landingGearColliders[i];
                if (gearCollider != null && gearCollider.enabled != shouldEnable)
                {
                    gearCollider.enabled = shouldEnable;
                }
            }
        }

        void ValidateParts()
        {
            if (landingGearParts == null)
            {
                return;
            }

            for (int i = 0; i < landingGearParts.Length; i++)
            {
                landingGearParts[i]?.Validate();
            }
        }

        void ApplyParts()
        {
            if (landingGearParts == null)
            {
                return;
            }

            float retractedAmount = 1f - Mathf.Clamp01(deployedAmount);
            for (int i = 0; i < landingGearParts.Length; i++)
            {
                landingGearParts[i]?.Apply(retractedAmount);
            }
        }

        bool UpdateDeployDecision(float deltaTime)
        {
            bool desiredDeploy = EvaluateDeployDecision();
            if (desiredDeploy == deployDecision)
            {
                decisionTimer = 0f;
                return deployDecision;
            }

            decisionTimer += Mathf.Max(0f, deltaTime);
            if (decisionTimer >= decisionHoldSeconds)
            {
                deployDecision = desiredDeploy;
                decisionTimer = 0f;
            }

            return deployDecision;
        }

        bool EvaluateDeployDecision()
        {
            if (deployWhenRampOpen && rampController != null && rampController.IsOpen)
            {
                return true;
            }

            if (deployOnSurfaceContact && surfaceContactProbe != null && surfaceContactProbe.HasContact)
            {
                return true;
            }

            if (commandedDeployed)
            {
                return true;
            }

            if (motor == null)
            {
                return false;
            }

            if (deployNearSurface && celestialProbe != null && celestialProbe.HasSample)
            {
                return EvaluateSurfaceFrame(celestialProbe.CurrentSample.SurfaceAltitude);
            }

            return false;
        }

        bool EvaluateSurfaceFrame(float surfaceAltitude)
        {
            bool currentlyDeployedOrDeploying = deployDecision || deployedAmount > 0.5f;
            if (currentlyDeployedOrDeploying)
            {
                bool highEnoughToRetract = surfaceAltitude > retractAltitude;
                bool committedToFlight = motor == null || motor.RelativeSpeed > retractSpeed;
                return !highEnoughToRetract || !committedToFlight;
            }

            return surfaceAltitude <= deployAltitude;
        }
    }
}
