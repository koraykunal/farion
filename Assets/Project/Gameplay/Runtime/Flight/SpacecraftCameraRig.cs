using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class SpacecraftCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Transform target;

        [Header("Camera Payload")]
        [SerializeField] Transform cameraTransform;
        [SerializeField] bool resetCameraLocalPose = true;

        [Header("Follow")]
        [SerializeField] Vector3 localOffset = new(0f, 4f, -14f);
        [SerializeField] bool scaleOffsetByTargetBounds = true;
        [Min(0.01f)]
        [SerializeField] float referenceTargetRadius = 2.5f;
        [Min(0.1f)]
        [SerializeField] float minimumOffsetScale = 1f;
        [Min(0.1f)]
        [SerializeField] float maximumOffsetScale = 4f;
        [SerializeField] bool snapToTarget = true;
        [Min(0f)]
        [SerializeField] float positionResponsiveness = 8f;
        [Min(0f)]
        [SerializeField] float rotationResponsiveness = 10f;
        [Min(0f)]
        [SerializeField] float snapDistance = 40f;
        [Min(0f)]
        [SerializeField] float maxPositionLag = 1.5f;

        bool snapNextFrame = true;

        void OnEnable()
        {
            ResolveCameraTransform();
            ResetCameraPayloadPose();
            snapNextFrame = true;
        }

        void OnValidate()
        {
            positionResponsiveness = Mathf.Max(0f, positionResponsiveness);
            rotationResponsiveness = Mathf.Max(0f, rotationResponsiveness);
            snapDistance = Mathf.Max(0f, snapDistance);
            maxPositionLag = Mathf.Max(0f, maxPositionLag);
            referenceTargetRadius = Mathf.Max(0.01f, referenceTargetRadius);
            minimumOffsetScale = Mathf.Max(0.1f, minimumOffsetScale);
            maximumOffsetScale = Mathf.Max(minimumOffsetScale, maximumOffsetScale);
        }

        void LateUpdate()
        {
            ApplyCamera(forceSnap: false);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            snapNextFrame = true;
        }

        public void SnapToTarget()
        {
            ApplyCamera(forceSnap: true);
        }

        void ApplyCamera(bool forceSnap)
        {
            if (target == null)
            {
                return;
            }

            ResolveCameraTransform();
            ResetCameraPayloadPose();

            Vector3 desiredPosition = target.TransformPoint(GetScaledLocalOffset());
            Vector3 viewDirection = target.position - desiredPosition;
            if (viewDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(viewDirection, target.up);

            if (snapToTarget || forceSnap || snapNextFrame || Vector3.Distance(transform.position, desiredPosition) > snapDistance)
            {
                transform.SetPositionAndRotation(desiredPosition, desiredRotation);
                snapNextFrame = false;
                return;
            }

            float positionT = ResponsivenessToLerp(positionResponsiveness);
            float rotationT = ResponsivenessToLerp(rotationResponsiveness);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);
            if (maxPositionLag > 0f)
            {
                Vector3 lag = transform.position - desiredPosition;
                if (lag.sqrMagnitude > maxPositionLag * maxPositionLag)
                {
                    transform.position = desiredPosition + lag.normalized * maxPositionLag;
                }
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
        }

        Vector3 GetScaledLocalOffset()
        {
            if (!scaleOffsetByTargetBounds)
            {
                return localOffset;
            }

            float targetRadius = CalculateTargetBoundsRadius();
            if (targetRadius <= 0.0001f)
            {
                return localOffset;
            }

            float scale = Mathf.Clamp(
                targetRadius / Mathf.Max(0.01f, referenceTargetRadius),
                minimumOffsetScale,
                maximumOffsetScale);
            return localOffset * scale;
        }

        float CalculateTargetBoundsRadius()
        {
            if (target == null)
            {
                return 0f;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
            {
                return 0f;
            }

            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds ? bounds.extents.magnitude : 0f;
        }

        void ResolveCameraTransform()
        {
            if (cameraTransform != null)
            {
                return;
            }

            if (TryGetComponent(out Camera localCamera))
            {
                cameraTransform = localCamera.transform;
                return;
            }

            Camera childCamera = GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
            }
        }

        void ResetCameraPayloadPose()
        {
            if (!resetCameraLocalPose || cameraTransform == null || cameraTransform == transform)
            {
                return;
            }

            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
        }

        static float ResponsivenessToLerp(float responsiveness)
        {
            return responsiveness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-responsiveness * UnityEngine.Time.deltaTime);
        }
    }
}
