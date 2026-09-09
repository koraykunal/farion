using Farion.Gameplay.Character;
using FishNet.Object.Prediction;
using UnityEngine;

namespace Farion.Multiplayer.Player
{
    public sealed class PredictionRigidbodyExplorerPhysicsBody :
        IExplorerPhysicsBody
    {
        readonly PredictionRigidbody predictionRigidbody;

        public PredictionRigidbodyExplorerPhysicsBody(
            PredictionRigidbody predictionRigidbody)
        {
            this.predictionRigidbody = predictionRigidbody;
        }

        Rigidbody Rigidbody => predictionRigidbody.Rigidbody;

        public Vector3 Position => Rigidbody.position;
        public Quaternion Rotation => Rigidbody.rotation;
        public Vector3 LinearVelocity
        {
            get => Rigidbody.linearVelocity;
            set => predictionRigidbody.Velocity(value);
        }
        public Vector3 AngularVelocity
        {
            get => Rigidbody.angularVelocity;
            set => predictionRigidbody.AngularVelocity(value);
        }

        public void AddForce(Vector3 force, ForceMode mode) =>
            predictionRigidbody.AddForce(force, mode);

        public void MoveRotation(Quaternion rotation) =>
            predictionRigidbody.MoveRotation(rotation);

        public void SetPosition(Vector3 position) =>
            Rigidbody.position = position;

        public void Commit() => predictionRigidbody.Simulate();
    }
}
