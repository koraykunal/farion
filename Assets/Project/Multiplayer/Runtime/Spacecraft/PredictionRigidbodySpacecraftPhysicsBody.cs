using Farion.Gameplay.Flight;
using FishNet.Object.Prediction;
using UnityEngine;

namespace Farion.Multiplayer.Spacecraft
{
    sealed class PredictionRigidbodySpacecraftPhysicsBody :
        ISpacecraftPhysicsBody
    {
        readonly PredictionRigidbody predictionRigidbody;

        public PredictionRigidbodySpacecraftPhysicsBody(
            PredictionRigidbody predictionRigidbody) =>
            this.predictionRigidbody = predictionRigidbody;

        Rigidbody Rigidbody => predictionRigidbody.Rigidbody;
        public Vector3 Position => Rigidbody.position;
        public Quaternion Rotation => Rigidbody.rotation;
        public Vector3 WorldCenterOfMass => Rigidbody.worldCenterOfMass;
        public Vector3 LinearVelocity => Rigidbody.linearVelocity;
        public Vector3 AngularVelocity => Rigidbody.angularVelocity;
        public void SetLinearVelocity(Vector3 velocity) =>
            predictionRigidbody.Velocity(velocity);
        public void SetAngularVelocity(Vector3 velocity) =>
            predictionRigidbody.AngularVelocity(velocity);
        public void MovePosition(Vector3 position) =>
            predictionRigidbody.MovePosition(position);
        public void AddForce(Vector3 force, ForceMode mode) =>
            predictionRigidbody.AddForce(force, mode);
        public void AddForceAtPosition(Vector3 force, Vector3 worldPosition, ForceMode mode) =>
            predictionRigidbody.AddForceAtPosition(force, worldPosition, mode);
        public void AddRelativeTorque(Vector3 torque, ForceMode mode) =>
            predictionRigidbody.AddRelativeTorque(torque, mode);
        public void Commit() => predictionRigidbody.Simulate();
    }
}
