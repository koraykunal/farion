using System.Collections.Generic;
using UnityEngine;

namespace Farion.Gameplay.Ships
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ShuttleDockingBoundary : MonoBehaviour
    {
        readonly Dictionary<ShuttleRuntimeBinding, HashSet<Collider>> occupants = new();
        readonly List<ShuttleRuntimeBinding> dockedShuttles = new();

        public IReadOnlyList<ShuttleRuntimeBinding> DockedShuttles =>
            dockedShuttles;
        public bool HasValidAuthoring =>
            TryGetComponent(out BoxCollider boundary) && boundary.isTrigger;

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnDisable()
        {
            occupants.Clear();
            dockedShuttles.Clear();
        }

        void OnTriggerEnter(Collider other)
        {
            ShuttleRuntimeBinding shuttle =
                other != null
                    ? other.GetComponentInParent<ShuttleRuntimeBinding>()
                    : null;
            if (shuttle == null)
            {
                return;
            }

            if (!occupants.TryGetValue(
                    shuttle,
                    out HashSet<Collider> shuttleColliders))
            {
                shuttleColliders = new HashSet<Collider>();
                occupants.Add(shuttle, shuttleColliders);
                dockedShuttles.Add(shuttle);
            }

            shuttleColliders.Add(other);
        }

        void OnTriggerExit(Collider other)
        {
            ShuttleRuntimeBinding shuttle =
                other != null
                    ? other.GetComponentInParent<ShuttleRuntimeBinding>()
                    : null;
            if (shuttle == null ||
                !occupants.TryGetValue(
                    shuttle,
                    out HashSet<Collider> shuttleColliders))
            {
                return;
            }

            shuttleColliders.Remove(other);
            if (shuttleColliders.Count > 0)
            {
                return;
            }

            occupants.Remove(shuttle);
            dockedShuttles.Remove(shuttle);
        }
    }
}
