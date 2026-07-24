using System;
using Farion.Gameplay.Interaction;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftRampController : MonoBehaviour, IInteractable
    {
        [Header("Ramp")]
        [SerializeField] Transform rampPivot;
        [SerializeField] string interactionPrompt = "Toggle ramp";
        [SerializeField] bool captureInitialPoseAsClosed = true;
        [SerializeField] Vector3 closedLocalEulerAngles;
        [SerializeField] Vector3 openLocalEulerAngles = new(-70f, 0f, 0f);
        [SerializeField] bool startOpen;

        [Header("Auto Resolve")]
        [SerializeField] bool resolvePivotByName = true;
        [SerializeField] string pivotName = "RampPivot";
        [SerializeField] Transform pivotSearchRoot;

        [Header("Motion")]
        [Min(0.01f)]
        [SerializeField] float openCloseSeconds = 1.2f;

        [Header("Runtime")]
        [SerializeField] bool isOpen;
        [SerializeField] float normalizedOpen;

        float targetOpen;

        public bool IsOpen => isOpen;
        public float NormalizedOpen => normalizedOpen;
        public string InteractionPrompt => interactionPrompt;

        void Awake()
        {
            ResolvePivot();
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
            ResolvePivot();
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
            return true;
        }

        public void Interact(InteractionContext context)
        {
            Toggle();
        }

        [ContextMenu("Capture Closed Pose")]
        public void CaptureClosedPose()
        {
            ResolvePivot();
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

        void ResolvePivot()
        {
            if (resolvePivotByName && !string.IsNullOrWhiteSpace(pivotName) && (rampPivot == null || rampPivot == transform))
            {
                Transform searchRoot = ResolvePivotSearchRoot();
                Transform namedPivot = FindChildByName(searchRoot, pivotName);
                if (namedPivot != null)
                {
                    rampPivot = namedPivot;
                    return;
                }
            }

            if (rampPivot == null)
            {
                rampPivot = transform;
            }
        }

        Transform ResolvePivotSearchRoot()
        {
            if (pivotSearchRoot != null)
            {
                return pivotSearchRoot;
            }

            SpacecraftRig rig = GetComponentInParent<SpacecraftRig>(true);
            if (rig != null)
            {
                return rig.VisualRoot != null ? rig.VisualRoot : rig.transform;
            }

            return transform.parent != null ? transform.parent : transform;
        }

        static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
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
    }
}
