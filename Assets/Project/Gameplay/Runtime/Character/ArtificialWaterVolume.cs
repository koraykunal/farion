using System.Collections.Generic;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ArtificialWaterVolume : MonoBehaviour
    {
        [SerializeField] Rigidbody referenceBody;

        readonly HashSet<ExplorerMotor> occupants = new();
        BoxCollider cachedVolume;

        BoxCollider Volume => cachedVolume != null
            ? cachedVolume
            : cachedVolume = GetComponent<BoxCollider>();

        public float SignedDistanceToSurface(Vector3 worldPosition)
        {
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            Vector3 localSurface = localPosition;
            localSurface.y = Volume.center.y + Volume.size.y * 0.5f;
            Vector3 worldSurface = transform.TransformPoint(localSurface);
            return Vector3.Dot(worldPosition - worldSurface, transform.up);
        }

        public Vector3 ReferenceVelocityAt(Vector3 worldPosition)
        {
            return referenceBody != null
                ? referenceBody.GetPointVelocity(worldPosition)
                : Vector3.zero;
        }

        void Reset()
        {
            Volume.isTrigger = true;
            referenceBody = GetComponentInParent<Rigidbody>();
        }

        void OnValidate()
        {
            if (TryGetComponent(out BoxCollider volume))
            {
                volume.isTrigger = true;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            ExplorerMotor motor = other.GetComponentInParent<ExplorerMotor>();
            if (motor != null && occupants.Add(motor))
            {
                motor.SetArtificialWaterSource(this);
            }
        }

        void OnTriggerExit(Collider other)
        {
            ExplorerMotor motor = other.GetComponentInParent<ExplorerMotor>();
            if (motor != null && occupants.Remove(motor))
            {
                motor.ClearArtificialWaterSource(this);
            }
        }

        void OnDisable()
        {
            foreach (ExplorerMotor motor in occupants)
            {
                if (motor != null)
                {
                    motor.ClearArtificialWaterSource(this);
                }
            }

            occupants.Clear();
        }
    }
}
