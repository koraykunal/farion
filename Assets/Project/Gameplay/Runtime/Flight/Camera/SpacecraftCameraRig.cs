using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class SpacecraftCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Transform target;
        [SerializeField] SpacecraftMotor motor;

        [Header("Camera Payload")]
        [SerializeField] Transform cameraTransform;
        [SerializeField] Camera payloadCamera;
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

        [Header("Flight Feedback")]
        [Min(0f)]
        [SerializeField] float velocityOffsetScale = 0.008f;
        [Min(0f)]
        [SerializeField] float accelerationOffsetScale = 0.018f;
        [Min(0f)]
        [SerializeField] float maxFlightOffset = 0.85f;
        [Min(0f)]
        [SerializeField] float flightOffsetResponsiveness = 4f;
        [Min(0f)]
        [SerializeField] float fovResponsiveness = 6f;
        [Min(0f)]
        [SerializeField] float boostFovIncrease = 7f;

        bool snapNextFrame = true;
        float baseFieldOfView = 60f;
        Vector3 smoothedFlightOffset;

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
            velocityOffsetScale = Mathf.Max(0f, velocityOffsetScale);
            accelerationOffsetScale = Mathf.Max(0f, accelerationOffsetScale);
            maxFlightOffset = Mathf.Max(0f, maxFlightOffset);
            flightOffsetResponsiveness = Mathf.Max(0f, flightOffsetResponsiveness);
            fovResponsiveness = Mathf.Max(0f, fovResponsiveness);
            boostFovIncrease = Mathf.Max(0f, boostFovIncrease);
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

            ResolveMotor();
            ApplyCameraFov();

            Vector3 desiredPosition = target.TransformPoint(GetScaledLocalOffset() + UpdateFlightOffset());
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
                if (payloadCamera == null)
                {
                    payloadCamera = cameraTransform.GetComponent<Camera>();
                }

                CacheBaseFov();
                return;
            }

            if (TryGetComponent(out Camera localCamera))
            {
                cameraTransform = localCamera.transform;
                payloadCamera = localCamera;
                CacheBaseFov();
                return;
            }

            Camera childCamera = GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
                payloadCamera = childCamera;
                CacheBaseFov();
            }
        }

        void ResolveMotor()
        {
            if (motor != null)
            {
                return;
            }

            if (target != null)
            {
                motor = target.GetComponentInParent<SpacecraftMotor>();
            }
        }

        Vector3 UpdateFlightOffset()
        {
            Vector3 targetOffset = CalculateFlightOffset();
            smoothedFlightOffset = Vector3.Lerp(
                smoothedFlightOffset,
                targetOffset,
                ResponsivenessToLerp(flightOffsetResponsiveness));

            if (smoothedFlightOffset.sqrMagnitude < 0.000001f)
            {
                smoothedFlightOffset = Vector3.zero;
            }

            return smoothedFlightOffset;
        }

        Vector3 CalculateFlightOffset()
        {
            if (motor == null)
            {
                return Vector3.zero;
            }

            SpacecraftMovementTelemetry sample = motor.Telemetry;
            Vector3 localVelocityOffset = new(
                -sample.LocalRelativeVelocity.x * velocityOffsetScale,
                -sample.LocalRelativeVelocity.y * velocityOffsetScale,
                -sample.LocalLinearAcceleration.z * accelerationOffsetScale);

            return Vector3.ClampMagnitude(localVelocityOffset, maxFlightOffset);
        }

        void ApplyCameraFov()
        {
            if (payloadCamera == null)
            {
                return;
            }

            float targetFov = baseFieldOfView + (motor != null ? motor.Telemetry.BoostBlend * boostFovIncrease : 0f);
            payloadCamera.fieldOfView = Mathf.Lerp(
                payloadCamera.fieldOfView,
                targetFov,
                ResponsivenessToLerp(fovResponsiveness));
        }

        void CacheBaseFov()
        {
            if (payloadCamera != null && baseFieldOfView <= 0f)
            {
                baseFieldOfView = payloadCamera.fieldOfView;
            }

            if (payloadCamera != null && Mathf.Approximately(baseFieldOfView, 60f))
            {
                baseFieldOfView = payloadCamera.fieldOfView;
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
