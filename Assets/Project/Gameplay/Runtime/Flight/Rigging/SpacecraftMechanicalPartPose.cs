using System;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [Serializable]
    public sealed class SpacecraftMechanicalPartPose
    {
        [SerializeField] string partName;
        [SerializeField] string parentPartName;
        [SerializeField] Transform part;
        [SerializeField] bool createVirtualPivot;
        [SerializeField] Vector3 virtualPivotLocalPosition;
        [SerializeField] Vector3 targetLocalPositionOffset;
        [SerializeField] Vector3 targetLocalEulerOffset;
        [Range(0f, 1f)]
        [SerializeField] float startAmount;
        [Range(0.01f, 1f)]
        [SerializeField] float durationAmount = 1f;
        [SerializeField] bool smoothStep = true;

        Vector3 initialLocalPosition;
        Quaternion initialLocalRotation;
        Transform drivenTransform;
        bool capturedInitialPose;
        bool attemptedBinding;

        internal string PartName => partName;
        internal string ParentPartName => parentPartName;
        internal Transform DrivenTransform => drivenTransform;

        public void Validate()
        {
            startAmount = Mathf.Clamp01(startAmount);
            durationAmount = Mathf.Clamp(durationAmount, 0.01f, 1f);
            if (startAmount + durationAmount > 1f)
            {
                durationAmount = Mathf.Max(0.01f, 1f - startAmount);
            }
        }

        internal bool PrepareDrivenTransform(Transform root)
        {
            BindPart(root);
            if (part == null)
            {
                return false;
            }

            if (drivenTransform != null)
            {
                return true;
            }

            drivenTransform = part;
            if (!createVirtualPivot)
            {
                return true;
            }

            GameObject pivotObject = new($"{partName} Pivot");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(part.parent, false);
            pivot.position = part.TransformPoint(virtualPivotLocalPosition);
            pivot.rotation = part.rotation;
            part.SetParent(pivot, true);
            drivenTransform = pivot;
            return true;
        }

        internal bool AttachTo(SpacecraftMechanicalPartPose parentPose)
        {
            if (string.IsNullOrWhiteSpace(parentPartName))
            {
                return true;
            }

            Transform parentTransform = parentPose?.DrivenTransform;
            if (drivenTransform == null ||
                parentTransform == null ||
                drivenTransform == parentTransform ||
                parentTransform.IsChildOf(drivenTransform))
            {
                return false;
            }

            drivenTransform.SetParent(parentTransform, true);
            return true;
        }

        public void CaptureInitialPose(Transform root)
        {
            if (!PrepareDrivenTransform(root))
            {
                return;
            }

            initialLocalPosition = drivenTransform.localPosition;
            initialLocalRotation = drivenTransform.localRotation;
            capturedInitialPose = true;
        }

        public void Apply(float amount)
        {
            if (!capturedInitialPose)
            {
                return;
            }

            if (drivenTransform == null)
            {
                return;
            }

            float partAmount = EvaluateAmount(amount);
            Vector3 targetPosition = initialLocalPosition + targetLocalPositionOffset;
            Quaternion targetRotation = initialLocalRotation * Quaternion.Euler(targetLocalEulerOffset);
            drivenTransform.localPosition = Vector3.Lerp(initialLocalPosition, targetPosition, partAmount);
            drivenTransform.localRotation = Quaternion.Slerp(initialLocalRotation, targetRotation, partAmount);
        }

        float EvaluateAmount(float amount)
        {
            Validate();
            float normalized = Mathf.Clamp01((amount - startAmount) / durationAmount);
            return smoothStep ? normalized * normalized * (3f - 2f * normalized) : normalized;
        }

        void BindPart(Transform root)
        {
            if (attemptedBinding || part != null || root == null || string.IsNullOrWhiteSpace(partName))
            {
                return;
            }

            attemptedBinding = true;
            part = SpacecraftRigTransformResolver.FindChild(root, partName);
        }
    }
}
