using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class VehicleBoardingPoint : MonoBehaviour
    {
        [Header("Exit Pose")]
        [SerializeField] Transform exitPoint;

        [Header("Boarding")]
        [Min(0.1f)]
        [SerializeField] float interactionRadius = 2.5f;

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public float InteractionRadius => interactionRadius;

        public bool IsInRange(Vector3 worldPosition)
        {
            float radius = Mathf.Max(0.1f, interactionRadius);
            return (worldPosition - transform.position).sqrMagnitude <= radius * radius;
        }

        void OnValidate()
        {
            interactionRadius = Mathf.Max(0.1f, interactionRadius);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, interactionRadius));

            Transform pose = ExitPoint;
            if (pose == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(pose.position, pose.forward);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(pose.position, pose.up);
        }
    }
}
