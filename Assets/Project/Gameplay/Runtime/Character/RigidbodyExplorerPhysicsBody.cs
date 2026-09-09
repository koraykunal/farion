using UnityEngine;

namespace Farion.Gameplay.Character
{
    public sealed class RigidbodyExplorerPhysicsBody : IExplorerPhysicsBody
    {
        readonly Rigidbody rigidbody;

        public RigidbodyExplorerPhysicsBody(Rigidbody rigidbody)
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

        public void SetPosition(Vector3 position) =>
            rigidbody.position = position;

        public void Commit()
        {
        }
    }
}
