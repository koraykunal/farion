using Farion.Gameplay.Interaction;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftRampController : MonoBehaviour, IInteractable
    {
        [Header("Ramp")]
        [SerializeField] Transform rampPivot;
        [SerializeField] bool captureInitialPoseAsClosed = true;
        [SerializeField] Vector3 closedLocalEulerAngles;
        [SerializeField] Vector3 openLocalEulerAngles = new(-70f, 0f, 0f);
        [SerializeField] bool startOpen;

        [Header("Motion")]
        [Min(0.01f)]
        [SerializeField] float openCloseSeconds = 1.2f;

        [Header("Runtime")]
        [SerializeField] bool isOpen;
        [SerializeField] float normalizedOpen;

        float targetOpen;
        IInteractable host;

        public bool IsOpen => isOpen;
        public float NormalizedOpen => normalizedOpen;
        public bool IsCommandedOpen => targetOpen >= 0.5f;
        public string InteractionPrompt => InteractionPromptKeys.ToggleRamp;

        public void Bind(IInteractable interactable)
        {
            host = interactable;
        }

        void Awake()
        {
            if (captureInitialPoseAsClosed && rampPivot != null)
            {
                closedLocalEulerAngles = rampPivot.localEulerAngles;
            }

            targetOpen = startOpen ? 1f : 0f;
            normalizedOpen = targetOpen;
            ApplyPose();
            isOpen = normalizedOpen >= 0.999f;
        }

        void OnValidate()
        {
            openCloseSeconds = Mathf.Max(0.01f, openCloseSeconds);
        }

        void Update()
        {
            float speed = 1f / Mathf.Max(0.01f, openCloseSeconds);
            normalizedOpen = Mathf.MoveTowards(normalizedOpen, targetOpen, speed * Time.deltaTime);
            ApplyPose();
            isOpen = normalizedOpen >= 0.999f;
        }

        [ContextMenu("Open")]
        public void Open()
        {
            targetOpen = 1f;
        }

        [ContextMenu("Close")]
        public void Close()
        {
            targetOpen = 0f;
        }

        public void SetOpen(bool open)
        {
            targetOpen = open ? 1f : 0f;
        }

        public void Toggle()
        {
            SetOpen(targetOpen < 0.5f);
        }

        public bool CanInteract(InteractionContext context)
        {
            return host == null || host.CanInteract(context);
        }

        public void Interact(InteractionContext context)
        {
            if (host != null)
            {
                host.Interact(context);
                return;
            }

            Toggle();
        }

        [ContextMenu("Capture Closed Pose")]
        public void CaptureClosedPose()
        {
            if (rampPivot != null)
            {
                closedLocalEulerAngles = rampPivot.localEulerAngles;
            }
        }

        void ApplyPose()
        {
            if (rampPivot == null)
            {
                return;
            }

            Quaternion closedRotation = Quaternion.Euler(closedLocalEulerAngles);
            Quaternion openRotation = Quaternion.Euler(openLocalEulerAngles);
            rampPivot.localRotation = Quaternion.Slerp(closedRotation, openRotation, normalizedOpen);
        }
    }
}
