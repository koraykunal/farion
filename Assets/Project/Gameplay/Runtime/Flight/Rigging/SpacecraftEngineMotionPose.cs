using System;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [Serializable]
    public sealed class SpacecraftEngineMotionPose
    {
        [SerializeField] string partName;
        [SerializeField] Transform part;
        [SerializeField] Vector3 rightInputEulerOffset;
        [SerializeField] Vector3 upInputEulerOffset;
        [SerializeField] Vector3 forwardInputEulerOffset;
        [SerializeField] Vector3 rightInputLocalPositionOffset;
        [SerializeField] Vector3 upInputLocalPositionOffset;
        [SerializeField] Vector3 forwardInputLocalPositionOffset;
        [SerializeField] Vector3 activityLocalPositionOffset;
        [SerializeField] Vector3 activityLocalEulerOffset;
        [Range(0f, 1f)]
        [SerializeField] float inputScale = 0.35f;
        [Range(0f, 1f)]
        [SerializeField] float activityScale = 1f;
        [Range(0f, 1f)]
        [SerializeField] float deadZone = 0.02f;

        Vector3 initialLocalPosition;
        Quaternion initialLocalRotation;
        bool capturedInitialPose;
        bool attemptedBinding;

        public void Validate()
        {
            inputScale = Mathf.Clamp01(inputScale);
            activityScale = Mathf.Clamp01(activityScale);
            deadZone = Mathf.Clamp01(deadZone);
        }

        public void CaptureInitialPose(Transform root)
        {
            if (!attemptedBinding && part == null && root != null && !string.IsNullOrWhiteSpace(partName))
            {
                attemptedBinding = true;
                part = SpacecraftRigTransformResolver.FindChild(root, partName);
            }

            if (part == null)
            {
                return;
            }

            initialLocalPosition = part.localPosition;
            initialLocalRotation = part.localRotation;
            capturedInitialPose = true;
        }

        public void Apply(Vector3 localInput, float activity)
        {
            if (!capturedInitialPose)
            {
                return;
            }

            if (part == null)
            {
                return;
            }

            Vector3 input = new Vector3(
                Mathf.Clamp(localInput.x, -1f, 1f),
                Mathf.Clamp(localInput.y, -1f, 1f),
                Mathf.Clamp(localInput.z, -1f, 1f)) * inputScale;
            if (input.magnitude < deadZone)
            {
                input = Vector3.zero;
            }

            float clampedActivity = Mathf.Clamp01(activity) * activityScale;
            Vector3 positionOffset =
                rightInputLocalPositionOffset * input.x +
                upInputLocalPositionOffset * input.y +
                forwardInputLocalPositionOffset * input.z +
                activityLocalPositionOffset * clampedActivity;
            Vector3 eulerOffset =
                rightInputEulerOffset * input.x +
                upInputEulerOffset * input.y +
                forwardInputEulerOffset * input.z +
                activityLocalEulerOffset * clampedActivity;

            part.localPosition = initialLocalPosition + positionOffset;
            part.localRotation = initialLocalRotation * Quaternion.Euler(eulerOffset);
        }
    }
}
