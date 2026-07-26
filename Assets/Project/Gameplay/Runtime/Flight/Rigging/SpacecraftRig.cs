using System;
using Farion.Gameplay.Actors;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftRig : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] Transform visualRoot;

        [Header("Boarding")]
        [SerializeField] Transform interiorSpawnPoint;
        [SerializeField] Transform exteriorExitPoint;
        [SerializeField] Transform pilotSeatPoint;
        [SerializeField] SpacecraftRampController rampController;

        [Header("Camera Targets")]
        [SerializeField] Transform chaseCameraTarget;
        [SerializeField] Transform landingCameraTarget;
        [SerializeField] Transform cockpitCameraTarget;

        [Header("Mechanical Parts")]
        [SerializeField] bool animateMechanicalParts;
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] CelestialActorProbe celestialProbe;
        [SerializeField] SpacecraftSurfaceContactProbe surfaceContactProbe;
        [SerializeField] bool landingGearStartsDeployed = true;
        [SerializeField] bool deployLandingGearOnSurfaceContact = true;
        [SerializeField] bool deployLandingGearWhenRampOpen = true;
        [SerializeField] bool deployLandingGearNearSurface = true;
        [Min(0f)]
        [SerializeField] float landingGearDeployAltitude = 8f;
        [Min(0f)]
        [SerializeField] float landingGearRetractAltitude = 18f;
        [Min(0f)]
        [SerializeField] float landingGearRetractSpeed = 6f;
        [Min(0f)]
        [SerializeField] float landingGearDecisionHoldSeconds = 0.2f;
        [Min(0.01f)]
        [SerializeField] float landingGearTransitionSeconds = 1.25f;
        [SerializeField] MechanicalPartPose[] landingGearParts = Array.Empty<MechanicalPartPose>();
        [SerializeField] bool animateEnginesFromThrust;
        [Min(0.01f)]
        [SerializeField] float thrustReferenceAcceleration = 18f;
        [Min(0f)]
        [SerializeField] float engineResponse = 8f;
        [SerializeField] MechanicalPartPose[] engineParts = Array.Empty<MechanicalPartPose>();
        [SerializeField] EngineGimbalPose[] engineGimbals = Array.Empty<EngineGimbalPose>();
        [SerializeField] DirectionalMechanicalPartPose[] engineGimbalParts = Array.Empty<DirectionalMechanicalPartPose>();

        [Header("Mechanical Runtime")]
        [Range(0f, 1f)]
        [SerializeField] float landingGearDeployedAmount;
        [Range(0f, 1f)]
        [SerializeField] float engineActivity;
        [SerializeField] Vector3 engineGimbalInput;

        bool landingGearDeployDecision;
        float landingGearDecisionTimer;

        public Transform VisualRoot => visualRoot;
        public Transform InteriorSpawnPoint => interiorSpawnPoint;
        public Transform ExteriorExitPoint => exteriorExitPoint;
        public Transform PilotSeatPoint => pilotSeatPoint;
        public SpacecraftRampController RampController => rampController;
        public Transform ChaseCameraTarget => chaseCameraTarget != null ? chaseCameraTarget : transform;
        public Transform LandingCameraTarget => landingCameraTarget != null ? landingCameraTarget : ChaseCameraTarget;
        public Transform CockpitCameraTarget => cockpitCameraTarget;
        public float LandingGearDeployedAmount => landingGearDeployedAmount;
        public float EngineActivity => engineActivity;

        void Awake()
        {
            ResolveOptionalReferences();
            InitializeMechanicalParts();
        }

        void OnValidate()
        {
            landingGearTransitionSeconds = Mathf.Max(0.01f, landingGearTransitionSeconds);
            landingGearDeployAltitude = Mathf.Max(0f, landingGearDeployAltitude);
            landingGearRetractAltitude = Mathf.Max(landingGearDeployAltitude, landingGearRetractAltitude);
            landingGearRetractSpeed = Mathf.Max(0f, landingGearRetractSpeed);
            landingGearDecisionHoldSeconds = Mathf.Max(0f, landingGearDecisionHoldSeconds);
            thrustReferenceAcceleration = Mathf.Max(0.01f, thrustReferenceAcceleration);
            engineResponse = Mathf.Max(0f, engineResponse);
            ValidateMechanicalParts(landingGearParts);
            ValidateMechanicalParts(engineParts);
            ValidateEngineGimbals(engineGimbals);
            ValidateDirectionalMechanicalParts(engineGimbalParts);
            ResolveOptionalReferences();
            ResolveMechanicalReferences();
        }

        void Reset()
        {
            ResolveOptionalReferences();
        }

        void Update()
        {
            UpdateMechanicalParts();
        }

        public void OpenRamp()
        {
            if (rampController != null)
            {
                rampController.Open();
            }
        }

        public void CloseRamp()
        {
            if (rampController != null)
            {
                rampController.Close();
            }
        }

        public void ToggleRamp()
        {
            if (rampController != null)
            {
                rampController.Toggle();
            }
        }

        void ResolveOptionalReferences()
        {
            visualRoot ??= FindChild("VisualRoot");
            rampController ??= GetComponentInChildren<SpacecraftRampController>(true);
            interiorSpawnPoint ??= FindChild("InteriorSpawnPoint");
            exteriorExitPoint ??= FindChild("ExteriorExitPoint");
            pilotSeatPoint ??= FindChild("PilotSeatPoint");
            chaseCameraTarget ??= FindChild("ChaseCameraTarget");
            landingCameraTarget ??= FindChild("LandingCameraTarget");
            cockpitCameraTarget ??= FindChild("CockpitCameraTarget");
            ResolveMechanicalReferences();
        }

        void ResolveMechanicalReferences()
        {
            motor ??= GetComponent<SpacecraftMotor>();
            celestialProbe ??= GetComponent<CelestialActorProbe>();
            surfaceContactProbe ??= GetComponent<SpacecraftSurfaceContactProbe>();
        }

        void InitializeMechanicalParts()
        {
            ResolveMechanicalReferences();
            landingGearDeployedAmount = landingGearStartsDeployed ? 1f : 0f;
            landingGearDeployDecision = landingGearStartsDeployed;
            landingGearDecisionTimer = 0f;
            engineActivity = 0f;
            CaptureMechanicalParts(landingGearParts);
            CaptureMechanicalParts(engineParts);
            CaptureEngineGimbals(engineGimbals);
            CaptureDirectionalMechanicalParts(engineGimbalParts);
            ApplyMechanicalParts(landingGearParts, 1f - landingGearDeployedAmount);
            ApplyMechanicalParts(engineParts, engineActivity);
            ApplyEngineGimbals(engineGimbals, Vector3.zero);
            if (!HasEngineGimbals())
            {
                ApplyDirectionalMechanicalParts(engineGimbalParts, Vector3.zero);
            }
        }

        void UpdateMechanicalParts()
        {
            if (!animateMechanicalParts)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            float gearTarget = UpdateLandingGearDecision(deltaTime) ? 1f : 0f;
            float gearSpeed = 1f / Mathf.Max(0.01f, landingGearTransitionSeconds);
            landingGearDeployedAmount = Mathf.MoveTowards(
                landingGearDeployedAmount,
                gearTarget,
                gearSpeed * deltaTime);
            ApplyMechanicalParts(landingGearParts, 1f - landingGearDeployedAmount);

            if (!animateEnginesFromThrust || motor == null)
            {
                engineActivity = Mathf.MoveTowards(engineActivity, 0f, engineResponse * deltaTime);
                engineGimbalInput = Vector3.MoveTowards(engineGimbalInput, Vector3.zero, engineResponse * deltaTime);
            }
            else
            {
                Vector3 targetGimbalInput = Vector3.ClampMagnitude(motor.LastLocalTranslationInput, 1f);
                float targetActivity = Mathf.Clamp01(motor.LastThrustAcceleration.magnitude / thrustReferenceAcceleration);
                float response = engineResponse <= 0f ? 1f : 1f - Mathf.Exp(-engineResponse * deltaTime);
                engineActivity = Mathf.Lerp(engineActivity, targetActivity, response);
                engineGimbalInput = Vector3.Lerp(engineGimbalInput, targetGimbalInput, response);
            }

            ApplyMechanicalParts(engineParts, engineActivity);
            if (HasEngineGimbals())
            {
                ApplyEngineGimbals(engineGimbals, engineGimbalInput);
            }
            else
            {
                ApplyDirectionalMechanicalParts(engineGimbalParts, engineGimbalInput);
            }
        }

        bool UpdateLandingGearDecision(float deltaTime)
        {
            bool desiredDeploy = EvaluateLandingGearDeployment();
            if (desiredDeploy == landingGearDeployDecision)
            {
                landingGearDecisionTimer = 0f;
                return landingGearDeployDecision;
            }

            landingGearDecisionTimer += Mathf.Max(0f, deltaTime);
            if (landingGearDecisionTimer >= landingGearDecisionHoldSeconds)
            {
                landingGearDeployDecision = desiredDeploy;
                landingGearDecisionTimer = 0f;
            }

            return landingGearDeployDecision;
        }

        bool EvaluateLandingGearDeployment()
        {
            if (deployLandingGearWhenRampOpen && rampController != null && rampController.IsOpen)
            {
                return true;
            }

            if (deployLandingGearOnSurfaceContact && surfaceContactProbe != null && surfaceContactProbe.HasContact)
            {
                return true;
            }

            if (motor == null)
            {
                return landingGearStartsDeployed;
            }

            if (deployLandingGearNearSurface && celestialProbe != null && celestialProbe.HasSample)
            {
                return EvaluateLandingGearFromSurfaceFrame(celestialProbe.CurrentSample.SurfaceAltitude);
            }

            bool activeThrust = motor.LastThrustAcceleration.sqrMagnitude > 0.25f;
            bool moving = motor.RelativeSpeed > 2f;
            return landingGearStartsDeployed && !activeThrust && !moving;
        }

        bool EvaluateLandingGearFromSurfaceFrame(float surfaceAltitude)
        {
            bool currentlyDeployedOrDeploying = landingGearDeployDecision || landingGearDeployedAmount > 0.5f;
            if (currentlyDeployedOrDeploying)
            {
                bool highEnoughToRetract = surfaceAltitude > landingGearRetractAltitude;
                bool committedToFlight = motor == null || motor.RelativeSpeed > landingGearRetractSpeed;
                return !highEnoughToRetract || !committedToFlight;
            }

            return surfaceAltitude <= landingGearDeployAltitude;
        }

        void CaptureMechanicalParts(MechanicalPartPose[] parts)
        {
            if (parts == null)
            {
                return;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i]?.CaptureInitialPose(visualRoot);
            }
        }

        void ValidateMechanicalParts(MechanicalPartPose[] parts)
        {
            if (parts == null)
            {
                return;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i]?.Validate();
            }
        }

        void CaptureDirectionalMechanicalParts(DirectionalMechanicalPartPose[] parts)
        {
            if (parts == null)
            {
                return;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i]?.CaptureInitialPose(visualRoot);
            }
        }

        void ValidateDirectionalMechanicalParts(DirectionalMechanicalPartPose[] parts)
        {
            if (parts == null)
            {
                return;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i]?.Validate();
            }
        }

        void CaptureEngineGimbals(EngineGimbalPose[] gimbals)
        {
            if (gimbals == null)
            {
                return;
            }

            for (int i = 0; i < gimbals.Length; i++)
            {
                gimbals[i]?.CaptureInitialPose(visualRoot);
            }
        }

        void ValidateEngineGimbals(EngineGimbalPose[] gimbals)
        {
            if (gimbals == null)
            {
                return;
            }

            for (int i = 0; i < gimbals.Length; i++)
            {
                gimbals[i]?.Validate();
            }
        }

        void ApplyEngineGimbals(EngineGimbalPose[] gimbals, Vector3 localInput)
        {
            if (gimbals == null)
            {
                return;
            }

            Vector3 clampedInput = Vector3.ClampMagnitude(localInput, 1f);
            for (int i = 0; i < gimbals.Length; i++)
            {
                gimbals[i]?.Apply(visualRoot, clampedInput);
            }
        }

        bool HasEngineGimbals()
        {
            return engineGimbals != null && engineGimbals.Length > 0;
        }

        void ApplyMechanicalParts(MechanicalPartPose[] parts, float amount)
        {
            if (parts == null)
            {
                return;
            }

            float clampedAmount = Mathf.Clamp01(amount);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i]?.Apply(visualRoot, clampedAmount);
            }
        }

        void ApplyDirectionalMechanicalParts(DirectionalMechanicalPartPose[] parts, Vector3 localInput)
        {
            if (parts == null)
            {
                return;
            }

            Vector3 clampedInput = Vector3.ClampMagnitude(localInput, 1f);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i]?.Apply(visualRoot, clampedInput);
            }
        }

        Transform FindChild(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        [Serializable]
        sealed class MechanicalPartPose
        {
            [SerializeField] string partName;
            [SerializeField] Transform part;
            [SerializeField] Vector3 targetLocalPositionOffset;
            [SerializeField] Vector3 targetLocalEulerOffset;
            [Range(0f, 1f)]
            [SerializeField] float startAmount;
            [Range(0.01f, 1f)]
            [SerializeField] float durationAmount = 1f;
            [SerializeField] bool smoothStep = true;

            Vector3 initialLocalPosition;
            Quaternion initialLocalRotation;
            bool capturedInitialPose;

            public void Validate()
            {
                startAmount = Mathf.Clamp01(startAmount);
                durationAmount = Mathf.Clamp(durationAmount, 0.01f, 1f);
                if (startAmount + durationAmount > 1f)
                {
                    durationAmount = Mathf.Max(0.01f, 1f - startAmount);
                }
            }

            public void CaptureInitialPose(Transform root)
            {
                Resolve(root);
                if (part == null)
                {
                    return;
                }

                initialLocalPosition = part.localPosition;
                initialLocalRotation = part.localRotation;
                capturedInitialPose = true;
            }

            public void Apply(Transform root, float amount)
            {
                if (!capturedInitialPose)
                {
                    CaptureInitialPose(root);
                }

                if (part == null)
                {
                    return;
                }

                float partAmount = EvaluateAmount(amount);
                Vector3 targetPosition = initialLocalPosition + targetLocalPositionOffset;
                Quaternion targetRotation = initialLocalRotation * Quaternion.Euler(targetLocalEulerOffset);
                part.localPosition = Vector3.Lerp(initialLocalPosition, targetPosition, partAmount);
                part.localRotation = Quaternion.Slerp(initialLocalRotation, targetRotation, partAmount);
            }

            float EvaluateAmount(float amount)
            {
                Validate();
                float normalized = Mathf.Clamp01((amount - startAmount) / durationAmount);
                return smoothStep ? normalized * normalized * (3f - 2f * normalized) : normalized;
            }

            void Resolve(Transform root)
            {
                if (part != null || root == null || string.IsNullOrWhiteSpace(partName))
                {
                    return;
                }

                Transform[] children = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child != null && string.Equals(child.name, partName, StringComparison.OrdinalIgnoreCase))
                    {
                        part = child;
                        return;
                    }
                }
            }
        }

        [Serializable]
        sealed class EngineGimbalPose
        {
            [SerializeField] string partName;
            [SerializeField] Transform part;
            [SerializeField] Vector3 lateralInputEulerOffset;
            [SerializeField] Vector3 verticalInputEulerOffset;
            [SerializeField] Vector3 forwardInputEulerOffset;
            [Range(0f, 2f)]
            [SerializeField] float inputScale = 1f;
            [Range(0f, 1f)]
            [SerializeField] float deadZone = 0.02f;

            Vector3 initialLocalPosition;
            Quaternion initialLocalRotation;
            bool capturedInitialPose;

            public void Validate()
            {
                inputScale = Mathf.Clamp(inputScale, 0f, 2f);
                deadZone = Mathf.Clamp01(deadZone);
            }

            public void CaptureInitialPose(Transform root)
            {
                Resolve(root);
                if (part == null)
                {
                    return;
                }

                initialLocalPosition = part.localPosition;
                initialLocalRotation = part.localRotation;
                capturedInitialPose = true;
            }

            public void Apply(Transform root, Vector3 localInput)
            {
                if (!capturedInitialPose)
                {
                    CaptureInitialPose(root);
                }

                if (part == null)
                {
                    return;
                }

                Vector3 input = Vector3.ClampMagnitude(localInput * inputScale, 1f);
                if (input.magnitude < deadZone)
                {
                    input = Vector3.zero;
                }

                Vector3 eulerOffset =
                    lateralInputEulerOffset * input.x +
                    verticalInputEulerOffset * input.y +
                    forwardInputEulerOffset * input.z;

                part.localPosition = initialLocalPosition;
                part.localRotation = initialLocalRotation * Quaternion.Euler(eulerOffset);
            }

            void Resolve(Transform root)
            {
                if (part != null || root == null || string.IsNullOrWhiteSpace(partName))
                {
                    return;
                }

                Transform[] children = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child != null && string.Equals(child.name, partName, StringComparison.OrdinalIgnoreCase))
                    {
                        part = child;
                        return;
                    }
                }
            }
        }

        [Serializable]
        sealed class DirectionalMechanicalPartPose
        {
            [SerializeField] string partName;
            [SerializeField] Transform part;
            [SerializeField] Vector3 rightInputEulerOffset;
            [SerializeField] Vector3 upInputEulerOffset;
            [SerializeField] Vector3 forwardInputEulerOffset;
            [Range(0f, 1f)]
            [SerializeField] float deadZone = 0.02f;

            Vector3 initialLocalPosition;
            Quaternion initialLocalRotation;
            bool capturedInitialPose;

            public void Validate()
            {
                deadZone = Mathf.Clamp01(deadZone);
            }

            public void CaptureInitialPose(Transform root)
            {
                Resolve(root);
                if (part == null)
                {
                    return;
                }

                initialLocalPosition = part.localPosition;
                initialLocalRotation = part.localRotation;
                capturedInitialPose = true;
            }

            public void Apply(Transform root, Vector3 localInput)
            {
                if (!capturedInitialPose)
                {
                    CaptureInitialPose(root);
                }

                if (part == null)
                {
                    return;
                }

                Vector3 input = Vector3.ClampMagnitude(localInput, 1f);
                if (input.magnitude < deadZone)
                {
                    input = Vector3.zero;
                }

                Vector3 eulerOffset =
                    rightInputEulerOffset * input.x +
                    upInputEulerOffset * input.y +
                    forwardInputEulerOffset * input.z;

                part.localPosition = initialLocalPosition;
                part.localRotation = initialLocalRotation * Quaternion.Euler(eulerOffset);
            }

            void Resolve(Transform root)
            {
                if (part != null || root == null || string.IsNullOrWhiteSpace(partName))
                {
                    return;
                }

                Transform[] children = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child != null && string.Equals(child.name, partName, StringComparison.OrdinalIgnoreCase))
                    {
                        part = child;
                        return;
                    }
                }
            }
        }
    }
}
