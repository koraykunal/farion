using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class PlayerPossessionController : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] PlayerPossessionMode initialMode = PlayerPossessionMode.Spacecraft;
        [SerializeField] PlayerPossessionMode currentMode = PlayerPossessionMode.Spacecraft;
        [SerializeField] bool applyInitialModeOnAwake = true;

        [Header("Input")]
        [SerializeField] MonoBehaviour boardingInputSource;

        [Header("Spacecraft")]
        [SerializeField] Transform spacecraftRoot;
        [SerializeField] Rigidbody spacecraftRigidbody;
        [SerializeField] SpacecraftMotor spacecraftMotor;
        [SerializeField] KeyboardSpacecraftInput spacecraftInput;
        [SerializeField] SpacecraftCameraRig spacecraftCameraRig;
        [SerializeField] Transform spacecraftCameraTarget;
        [SerializeField] VehicleBoardingPoint boardingPoint;

        [Header("Explorer")]
        [SerializeField] GameObject explorerRoot;
        [SerializeField] Rigidbody explorerRigidbody;
        [SerializeField] FirstPersonMotor explorerMotor;
        [SerializeField] KeyboardFirstPersonInput explorerInput;
        [SerializeField] FirstPersonCameraRig firstPersonCameraRig;
        [Min(0f)]
        [SerializeField] float exitPoseClearance = 0.25f;
        [SerializeField] bool ignoreExplorerSpacecraftCollisions = true;

        [Header("World Origin")]
        [SerializeField] WorldOriginRebaser originRebaser;
        [SerializeField] bool updateOriginTrackingTarget = true;

        [Header("Runtime Debug")]
        [SerializeField] bool explorerInBoardingRange;
        [SerializeField] float explorerDistanceToBoardingPoint;

        IBoardingInputSource resolvedBoardingInput;

        public PlayerPossessionMode CurrentMode => currentMode;
        public bool IsPilotingSpacecraft => currentMode == PlayerPossessionMode.Spacecraft;
        public bool IsOnFoot => currentMode == PlayerPossessionMode.OnFoot;

        void Awake()
        {
            ResolveReferences();
            ResolveInputSource();

            if (applyInitialModeOnAwake)
            {
                ApplyMode(initialMode);
            }
        }

        void OnValidate()
        {
            exitPoseClearance = Mathf.Max(0f, exitPoseClearance);
            if (boardingInputSource != null && boardingInputSource is not IBoardingInputSource)
            {
                boardingInputSource = null;
            }
        }

        void Update()
        {
            ResolveInputSource();
            RefreshBoardingDebug();

            BoardingInputState input = resolvedBoardingInput?.CurrentInput ?? BoardingInputState.None;
            if (currentMode == PlayerPossessionMode.Spacecraft)
            {
                if (input.ExitVehicle)
                {
                    ExitSpacecraft();
                }

                return;
            }

            if (input.Interact && explorerInBoardingRange)
            {
                EnterSpacecraft();
            }
        }

        [ContextMenu("Enter Spacecraft")]
        public void EnterSpacecraft()
        {
            ResolveReferences();
            ApplyMode(PlayerPossessionMode.Spacecraft);
        }

        [ContextMenu("Exit Spacecraft")]
        public void ExitSpacecraft()
        {
            ResolveReferences();
            PlaceExplorerAtExitPoint();
            ApplyMode(PlayerPossessionMode.OnFoot);
        }

        public void ApplyMode(PlayerPossessionMode nextMode)
        {
            ResolveReferences();
            currentMode = nextMode;

            bool piloting = currentMode == PlayerPossessionMode.Spacecraft;
            SetBehaviourEnabled(firstPersonCameraRig, !piloting);
            SetBehaviourEnabled(spacecraftMotor, true);
            SetBehaviourEnabled(spacecraftInput, piloting);
            SetBehaviourEnabled(spacecraftCameraRig, piloting);

            if (spacecraftCameraRig != null)
            {
                spacecraftCameraRig.SetTarget(spacecraftCameraTarget != null ? spacecraftCameraTarget : spacecraftRoot);
                if (piloting)
                {
                    spacecraftCameraRig.SnapToTarget();
                }
            }

            if (explorerRoot != null)
            {
                explorerRoot.SetActive(!piloting);
            }

            SetBehaviourEnabled(explorerMotor, !piloting);
            SetBehaviourEnabled(explorerInput, !piloting);
            SetExplorerSpacecraftCollisionIgnored(!piloting && ignoreExplorerSpacecraftCollisions);

            if (firstPersonCameraRig != null && explorerMotor != null)
            {
                firstPersonCameraRig.SetTarget(explorerMotor);
                firstPersonCameraRig.SetInputSource(explorerInput);
            }

            if (updateOriginTrackingTarget && originRebaser != null)
            {
                Transform target = piloting
                    ? GetSpacecraftTrackingTarget()
                    : GetExplorerTrackingTarget();
                originRebaser.SetTrackingTarget(target);
            }

            RefreshBoardingDebug();
        }

        void PlaceExplorerAtExitPoint()
        {
            if (explorerRoot == null)
            {
                return;
            }

            Transform exitTransform = boardingPoint != null ? boardingPoint.ExitPoint : spacecraftRoot;
            if (exitTransform == null)
            {
                return;
            }

            explorerRoot.SetActive(true);

            Vector3 up = exitTransform.up.sqrMagnitude > 0.0001f ? exitTransform.up.normalized : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(exitTransform.forward, up);
            if (forward.sqrMagnitude <= 0.0001f && spacecraftRoot != null)
            {
                forward = Vector3.ProjectOnPlane(spacecraftRoot.forward, up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.right, up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            Vector3 position = exitTransform.position + up * exitPoseClearance;

            if (explorerRigidbody != null)
            {
                explorerRigidbody.position = position;
                explorerRigidbody.rotation = rotation;
                explorerRigidbody.linearVelocity = spacecraftRigidbody != null ? spacecraftRigidbody.linearVelocity : Vector3.zero;
                explorerRigidbody.angularVelocity = Vector3.zero;
            }
            else
            {
                explorerRoot.transform.SetPositionAndRotation(position, rotation);
            }

            if (explorerMotor != null)
            {
                explorerMotor.ResetMotorState();
            }
        }

        void ResolveReferences()
        {
            if (spacecraftRoot != null)
            {
                spacecraftRigidbody ??= spacecraftRoot.GetComponent<Rigidbody>();
                spacecraftMotor ??= spacecraftRoot.GetComponent<SpacecraftMotor>();
                spacecraftInput ??= spacecraftRoot.GetComponent<KeyboardSpacecraftInput>();
                boardingPoint ??= spacecraftRoot.GetComponentInChildren<VehicleBoardingPoint>(true);
            }

            if (explorerRoot != null)
            {
                explorerRigidbody ??= explorerRoot.GetComponent<Rigidbody>();
                explorerMotor ??= explorerRoot.GetComponent<FirstPersonMotor>();
                explorerInput ??= explorerRoot.GetComponent<KeyboardFirstPersonInput>();
            }
        }

        void ResolveInputSource()
        {
            if (boardingInputSource is IBoardingInputSource explicitSource)
            {
                resolvedBoardingInput = explicitSource;
                return;
            }

            resolvedBoardingInput ??= GetComponent<IBoardingInputSource>();
        }

        void RefreshBoardingDebug()
        {
            explorerInBoardingRange = false;
            explorerDistanceToBoardingPoint = 0f;

            if (boardingPoint == null || explorerRoot == null)
            {
                return;
            }

            Vector3 explorerPosition = explorerRigidbody != null
                ? explorerRigidbody.position
                : explorerRoot.transform.position;

            explorerDistanceToBoardingPoint = Vector3.Distance(explorerPosition, boardingPoint.transform.position);
            explorerInBoardingRange = boardingPoint.IsInRange(explorerPosition);
        }

        Transform GetSpacecraftTrackingTarget()
        {
            if (spacecraftRigidbody != null)
            {
                return spacecraftRigidbody.transform;
            }

            return spacecraftRoot;
        }

        Transform GetExplorerTrackingTarget()
        {
            if (explorerRigidbody != null)
            {
                return explorerRigidbody.transform;
            }

            return explorerRoot != null ? explorerRoot.transform : null;
        }

        static void SetBehaviourEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }

        void SetExplorerSpacecraftCollisionIgnored(bool ignored)
        {
            if (explorerRoot == null || spacecraftRoot == null)
            {
                return;
            }

            Collider[] explorerColliders = explorerRoot.GetComponentsInChildren<Collider>(true);
            Collider[] spacecraftColliders = spacecraftRoot.GetComponentsInChildren<Collider>(true);
            foreach (Collider explorerCollider in explorerColliders)
            {
                if (explorerCollider == null)
                {
                    continue;
                }

                foreach (Collider spacecraftCollider in spacecraftColliders)
                {
                    if (spacecraftCollider == null || spacecraftCollider == explorerCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(explorerCollider, spacecraftCollider, ignored);
                }
            }
        }
    }
}
