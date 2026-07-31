using System.Collections.Generic;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ArtificialGravityVolume : MonoBehaviour
    {
        [SerializeField, Min(0f)] float acceleration = 9.81f;
        [SerializeField] Rigidbody referenceBody;

        readonly HashSet<FirstPersonMotor> occupants = new();

        public Vector3 Up => transform.up;
        public Vector3 GravityAcceleration => -Up * acceleration;

        public Vector3 ReferenceVelocityAt(Vector3 worldPosition)
        {
            return referenceBody != null
                ? referenceBody.GetPointVelocity(worldPosition)
                : Vector3.zero;
        }

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            referenceBody = GetComponentInParent<Rigidbody>();
        }

        void OnValidate()
        {
            acceleration = Mathf.Max(0f, acceleration);
            if (TryGetComponent(out BoxCollider volume))
            {
                volume.isTrigger = true;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            FirstPersonMotor motor = other.GetComponentInParent<FirstPersonMotor>();
            if (motor != null && occupants.Add(motor))
            {
                motor.SetArtificialGravitySource(this);
            }
        }

        void OnTriggerExit(Collider other)
        {
            FirstPersonMotor motor = other.GetComponentInParent<FirstPersonMotor>();
            if (motor != null && occupants.Remove(motor))
            {
                motor.ClearArtificialGravitySource(this);
            }
        }

        void OnDisable()
        {
            foreach (FirstPersonMotor motor in occupants)
            {
                if (motor != null)
                {
                    motor.ClearArtificialGravitySource(this);
                }
            }

            occupants.Clear();
        }
    }
}
