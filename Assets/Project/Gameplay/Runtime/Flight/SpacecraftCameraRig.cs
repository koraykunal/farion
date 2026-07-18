using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class SpacecraftCameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 localOffset = new(0f, 4f, -14f);
        [SerializeField] bool snapToTarget = true;
        [Min(0f)]
        [SerializeField] float positionResponsiveness = 8f;
        [Min(0f)]
        [SerializeField] float rotationResponsiveness = 10f;

        void LateUpdate()
        {
            ApplyCamera(forceSnap: false);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        void ApplyCamera(bool forceSnap)
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.TransformPoint(localOffset);
            Vector3 viewDirection = target.position - desiredPosition;
            if (viewDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(viewDirection, target.up);

            if (snapToTarget || forceSnap)
            {
                transform.SetPositionAndRotation(desiredPosition, desiredRotation);
                return;
            }

            float positionT = ResponsivenessToLerp(positionResponsiveness);
            float rotationT = ResponsivenessToLerp(rotationResponsiveness);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
        }

        static float ResponsivenessToLerp(float responsiveness)
        {
            return responsiveness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-responsiveness * UnityEngine.Time.deltaTime);
        }
    }
}
