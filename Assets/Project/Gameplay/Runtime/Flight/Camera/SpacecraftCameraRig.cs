using Farion.Core.Physics;
using Farion.Gameplay.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class SpacecraftCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [FormerlySerializedAs("target")]
        [SerializeField] Transform exteriorTarget;
        [SerializeField] Transform cockpitTarget;
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] SpacecraftPilotCameraView view = SpacecraftPilotCameraView.Exterior;

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

        [Header("Collision")]
        [SerializeField] bool avoidObstacles = true;
        [SerializeField] LayerMask obstacleLayers = FarionLayers.CameraObstacleMask;
        [Min(0.01f)]
        [SerializeField] float collisionRadius = 0.35f;
        [Min(0f)]
        [SerializeField] float collisionPadding = 0.15f;
        [Min(0f)]
        [SerializeField] float minimumTargetDistance = 1f;

        [Header("Flight Feedback")]
        [Min(0f)]
        [SerializeField] float velocityOffsetScale = 0.008f;
        [Min(0f)]
        [SerializeField] float accelerationOffsetScale = 0.06f;
        [Min(0f)]
        [SerializeField] float maxFlightOffset = 2.5f;
        [Min(0f)]
        [SerializeField] float flightOffsetResponsiveness = 4f;
        [Min(0f)]
        [SerializeField] float fovResponsiveness = 6f;
        [Min(0f)]
        [SerializeField] float boostFovIncrease = 7f;
        [Tooltip("Extra field of view at the reference relative speed.")]
        [Min(0f)]
        [SerializeField] float speedFovIncrease = 12f;
        [Tooltip("Relative speed at which the speed field of view reaches its full increase.")]
        [Min(1f)]
        [SerializeField] float speedFovReferenceSpeed = 200f;
        [Tooltip("One-shot field of view kick applied during the boost onset surge.")]
        [Min(0f)]
        [SerializeField] float boostSurgeFovKick = 6f;

        bool snapNextFrame = true;
        float baseFieldOfView = 60f;
        Vector3 smoothedFlightOffset;
        readonly RaycastHit[] collisionHits = new RaycastHit[32];
        Transform cachedBoundsTarget;
        float cachedTargetBoundsRadius;
        bool targetBoundsDirty = true;
        Camera cachedFovCamera;
        Vector3 lastExteriorTargetPosition;
        bool hasExteriorTargetPosition;
        public SpacecraftPilotCameraView View => view;

        void OnEnable()
        {
            ResolveCameraTransform();
            ResetCameraPayloadPose();
            targetBoundsDirty = true;
            snapNextFrame = true;
        }

        void OnValidate()
        {
            positionResponsiveness = Mathf.Max(0f, positionResponsiveness);
            rotationResponsiveness = Mathf.Max(0f, rotationResponsiveness);
            snapDistance = Mathf.Max(0f, snapDistance);
            maxPositionLag = Mathf.Max(0f, maxPositionLag);
            collisionRadius = Mathf.Max(0.01f, collisionRadius);
            collisionPadding = Mathf.Max(0f, collisionPadding);
            minimumTargetDistance = Mathf.Max(0f, minimumTargetDistance);
            referenceTargetRadius = Mathf.Max(0.01f, referenceTargetRadius);
            minimumOffsetScale = Mathf.Max(0.1f, minimumOffsetScale);
            maximumOffsetScale = Mathf.Max(minimumOffsetScale, maximumOffsetScale);
            velocityOffsetScale = Mathf.Max(0f, velocityOffsetScale);
            accelerationOffsetScale = Mathf.Max(0f, accelerationOffsetScale);
            maxFlightOffset = Mathf.Max(0f, maxFlightOffset);
            flightOffsetResponsiveness = Mathf.Max(0f, flightOffsetResponsiveness);
            fovResponsiveness = Mathf.Max(0f, fovResponsiveness);
            boostFovIncrease = Mathf.Max(0f, boostFovIncrease);
            speedFovIncrease = Mathf.Max(0f, speedFovIncrease);
            speedFovReferenceSpeed = Mathf.Max(1f, speedFovReferenceSpeed);
            boostSurgeFovKick = Mathf.Max(0f, boostSurgeFovKick);
            targetBoundsDirty = true;
        }

        void LateUpdate()
        {
            ApplyCamera(forceSnap: false);
        }

        public void SetTarget(Transform newTarget)
        {
            SetExteriorTarget(newTarget);
        }

        public void SetExteriorTarget(Transform newTarget)
        {
            if (exteriorTarget != newTarget)
            {
                targetBoundsDirty = true;
            }

            exteriorTarget = newTarget;
            hasExteriorTargetPosition = false;
            snapNextFrame = true;
        }

        public void SetCockpitTarget(Transform newTarget)
        {
            cockpitTarget = newTarget;
            snapNextFrame = true;
        }

        public void SetMotor(SpacecraftMotor newMotor)
        {
            motor = newMotor;
        }

        public void SetView(SpacecraftPilotCameraView nextView)
        {
            view = nextView;
            snapNextFrame = true;
            smoothedFlightOffset = Vector3.zero;
        }

        public void SnapToTarget()
        {
            ApplyCamera(forceSnap: true);
        }

        public void RefreshTargetBounds()
        {
            targetBoundsDirty = true;
        }

        void ApplyCamera(bool forceSnap)
        {
            if (view == SpacecraftPilotCameraView.Cockpit && cockpitTarget != null)
            {
                ApplyCockpitCamera();
                return;
            }

            if (exteriorTarget == null)
            {
                return;
            }

            ResolveCameraTransform();
            ResetCameraPayloadPose();
            ResolveMotor();
            ApplyCameraFov();

            Vector3 targetPosition = exteriorTarget.position;
            if (hasExteriorTargetPosition)
            {
                transform.position += targetPosition - lastExteriorTargetPosition;
            }

            lastExteriorTargetPosition = targetPosition;
            hasExteriorTargetPosition = true;

            Vector3 desiredPosition = exteriorTarget.TransformPoint(GetScaledLocalOffset() + UpdateFlightOffset());
            desiredPosition = ResolveCollisionAdjustedPosition(desiredPosition);
            Vector3 viewDirection = exteriorTarget.position - desiredPosition;
            if (viewDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(viewDirection, exteriorTarget.up);

            if (snapToTarget || forceSnap || snapNextFrame || Vector3.Distance(transform.position, desiredPosition) > snapDistance)
            {
                transform.SetPositionAndRotation(desiredPosition, desiredRotation);
                snapNextFrame = false;
                return;
            }

            float positionT = ResponsivenessToLerp(positionResponsiveness);
            float rotationT = ResponsivenessToLerp(rotationResponsiveness);

            Vector3 currentOffset = transform.position - targetPosition;
            Vector3 desiredOffset = desiredPosition - targetPosition;
            if (currentOffset.sqrMagnitude > 0.0001f &&
                desiredOffset.sqrMagnitude > 0.0001f)
            {
                Vector3 direction = Vector3.Slerp(
                    currentOffset.normalized,
                    desiredOffset.normalized,
                    positionT).normalized;
                float distance = Mathf.Lerp(
                    currentOffset.magnitude,
                    desiredOffset.magnitude,
                    positionT);
                transform.position = targetPosition + direction * distance;
            }
            else
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    desiredPosition,
                    positionT);
            }
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

        Vector3 ResolveCollisionAdjustedPosition(Vector3 desiredPosition)
        {
            if (!avoidObstacles || exteriorTarget == null)
            {
                return desiredPosition;
            }

            Vector3 origin = exteriorTarget.position;
            Vector3 offset = desiredPosition - origin;
            float desiredDistance = offset.magnitude;
            if (desiredDistance <= 0.0001f)
            {
                return desiredPosition;
            }

            Vector3 direction = offset / desiredDistance;
            PhysicsScene physicsScene =
                exteriorTarget.gameObject.scene.GetPhysicsScene();
            int hitCount = physicsScene.IsValid()
                ? physicsScene.SphereCast(
                    origin,
                    collisionRadius,
                    direction,
                    collisionHits,
                    desiredDistance,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore)
                : Physics.SphereCastNonAlloc(
                    origin,
                    collisionRadius,
                    direction,
                    collisionHits,
                    desiredDistance,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore);

            float nearestDistance = desiredDistance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = collisionHits[i];
                if (hit.collider == null || IsExteriorTargetCollider(hit.collider))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, hit.distance);
            }

            if (nearestDistance >= desiredDistance)
            {
                return desiredPosition;
            }

            float adjustedDistance = Mathf.Clamp(
                nearestDistance - collisionPadding,
                minimumTargetDistance,
                desiredDistance);
            return origin + direction * adjustedDistance;
        }

        bool IsExteriorTargetCollider(Collider candidate)
        {
            Transform candidateTransform = candidate.transform;
            Transform shipRoot = motor != null ? motor.transform : exteriorTarget;
            return candidateTransform == shipRoot ||
                candidateTransform.IsChildOf(shipRoot);
        }

        Vector3 GetScaledLocalOffset()
        {
            if (!scaleOffsetByTargetBounds)
            {
                return localOffset;
            }

            float targetRadius = GetTargetBoundsRadius();
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

        float GetTargetBoundsRadius()
        {
            if (exteriorTarget == null)
            {
                return 0f;
            }

            if (!targetBoundsDirty && cachedBoundsTarget == exteriorTarget)
            {
                return cachedTargetBoundsRadius;
            }

            Renderer[] renderers = exteriorTarget.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
            {
                cachedBoundsTarget = exteriorTarget;
                cachedTargetBoundsRadius = 0f;
                targetBoundsDirty = false;
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

            cachedBoundsTarget = exteriorTarget;
            cachedTargetBoundsRadius = hasBounds ? bounds.extents.magnitude : 0f;
            targetBoundsDirty = false;
            return cachedTargetBoundsRadius;
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

            if (exteriorTarget != null)
            {
                motor = exteriorTarget.GetComponentInParent<SpacecraftMotor>();
            }
        }

        void ApplyCockpitCamera()
        {
            ResolveCameraTransform();
            ResetCameraPayloadPose();
            ResolveMotor();
            ApplyCameraFov();

            transform.SetPositionAndRotation(cockpitTarget.position, cockpitTarget.rotation);
            snapNextFrame = false;
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

            float targetFov = FarionViewPreferences.ResolveFieldOfView(baseFieldOfView);
            if (motor != null)
            {
                SpacecraftMovementTelemetry telemetry = motor.Telemetry;
                float speed01 = speedFovReferenceSpeed > 0f
                    ? Mathf.Clamp01(telemetry.RelativeSpeed / speedFovReferenceSpeed)
                    : 0f;
                targetFov += speed01 * speed01 * speedFovIncrease;
                targetFov += telemetry.BoostBlend * boostFovIncrease;
                targetFov += telemetry.BoostSurge * boostSurgeFovKick;
            }

            payloadCamera.fieldOfView = Mathf.Lerp(
                payloadCamera.fieldOfView,
                targetFov,
                ResponsivenessToLerp(fovResponsiveness));
        }

        void CacheBaseFov()
        {
            if (payloadCamera == null || cachedFovCamera == payloadCamera)
            {
                return;
            }

            cachedFovCamera = payloadCamera;
            baseFieldOfView = payloadCamera.fieldOfView;
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
