using UnityEngine;

namespace Farion.Gameplay.Character
{
    public interface IFirstPersonPhysicsBody
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }
        Vector3 LinearVelocity { get; set; }
        Vector3 AngularVelocity { get; set; }
        void AddForce(Vector3 force, ForceMode mode);
        void MoveRotation(Quaternion rotation);
        void SetPosition(Vector3 position);
        void Commit();
    }
}
