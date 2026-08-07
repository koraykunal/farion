using UnityEngine;

namespace Farion.Gameplay.Character
{
    public sealed class RigidbodyFirstPersonPhysicsBody : IFirstPersonPhysicsBody
    {
        readonly Rigidbody rigidbody;

        public RigidbodyFirstPersonPhysicsBody(Rigidbody rigidbody)
        {
            this.rigidbody = rigidbody;
        }

        public Vector3 Position => rigidbody.position;
        public Quaternion Rotation => rigidbody.rotation;
        public Vector3 LinearVelocity
        {
            get => rigidbody.linearVelocity;
            set => rigidbody.linearVelocity = value;
        }
        public Vector3 AngularVelocity
        {
            get => rigidbody.angularVelocity;
            set => rigidbody.angularVelocity = value;
        }

        public void AddForce(Vector3 force, ForceMode mode) =>
            rigidbody.AddForce(force, mode);

        public void MoveRotation(Quaternion rotation) =>
            rigidbody.MoveRotation(rotation);

        public void Commit()
        {
        }
    }
}
