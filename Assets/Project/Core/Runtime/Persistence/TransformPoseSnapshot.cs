using System;
using UnityEngine;

namespace Farion.Core.Persistence
{
    [Serializable]
    public struct TransformPoseSnapshot
    {
        [SerializeField] Vector3 position;
        [SerializeField] Quaternion rotation;
        [SerializeField] Vector3 linearVelocity;
        [SerializeField] Vector3 angularVelocity;

        public TransformPoseSnapshot(
            Vector3 position,
            Quaternion rotation,
            Vector3 linearVelocity,
            Vector3 angularVelocity)
        {
            this.position = position;
            this.rotation = NormalizeRotation(rotation);
            this.linearVelocity = linearVelocity;
            this.angularVelocity = angularVelocity;
        }

        public Vector3 Position => position;
        public Quaternion Rotation => NormalizeRotation(rotation);
        public Vector3 LinearVelocity => linearVelocity;
        public Vector3 AngularVelocity => angularVelocity;

        public static TransformPoseSnapshot Capture(Transform target)
        {
            return target != null
                ? new TransformPoseSnapshot(target.position, target.rotation, Vector3.zero, Vector3.zero)
                : new TransformPoseSnapshot(Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero);
        }

        public static TransformPoseSnapshot Capture(Rigidbody target, Transform transformTarget = null)
        {
            if (target != null)
            {
                return new TransformPoseSnapshot(
                    target.position,
                    target.rotation,
                    target.linearVelocity,
                    target.angularVelocity);
            }

            return Capture(transformTarget);
        }

        public void ApplyTo(Transform target)
        {
            if (target != null)
            {
                target.SetPositionAndRotation(Position, Rotation);
            }
        }

        public void ApplyTo(Rigidbody target)
        {
            if (target == null)
            {
                return;
            }

            target.position = Position;
            target.rotation = Rotation;
            target.linearVelocity = LinearVelocity;
            target.angularVelocity = AngularVelocity;
        }

        static Quaternion NormalizeRotation(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x +
                value.y * value.y +
                value.z * value.z +
                value.w * value.w);
            if (magnitude <= 0.00001f)
            {
                return Quaternion.identity;
            }

            float inverseMagnitude = 1f / magnitude;
            return new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
        }
    }
}
