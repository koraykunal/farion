using System.Collections.Generic;
using Farion.Gameplay.Flight;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    sealed class SpacecraftExteriorCollisionGate
    {
        readonly List<Collider> ignoredExteriorColliders = new();

        public void SetIgnored(CapsuleCollider explorerCapsule, Transform spacecraftRoot, bool ignore)
        {
            if (explorerCapsule == null)
            {
                ignoredExteriorColliders.Clear();
                return;
            }

            if (!ignore)
            {
                Restore(explorerCapsule);
                return;
            }

            if (ignoredExteriorColliders.Count > 0 || spacecraftRoot == null)
            {
                return;
            }

            Collider[] spacecraftColliders = spacecraftRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < spacecraftColliders.Length; i++)
            {
                Collider spacecraftCollider = spacecraftColliders[i];
                if (ShouldIgnoreCandidate(spacecraftCollider, explorerCapsule))
                {
                    continue;
                }

                Physics.IgnoreCollision(explorerCapsule, spacecraftCollider, true);
                ignoredExteriorColliders.Add(spacecraftCollider);
            }
        }

        void Restore(CapsuleCollider explorerCapsule)
        {
            for (int i = 0; i < ignoredExteriorColliders.Count; i++)
            {
                Collider spacecraftCollider = ignoredExteriorColliders[i];
                if (spacecraftCollider != null)
                {
                    Physics.IgnoreCollision(explorerCapsule, spacecraftCollider, false);
                }
            }

            ignoredExteriorColliders.Clear();
        }

        static bool ShouldIgnoreCandidate(Collider spacecraftCollider, CapsuleCollider explorerCapsule)
        {
            return spacecraftCollider == null ||
                spacecraftCollider == explorerCapsule ||
                spacecraftCollider.isTrigger ||
                spacecraftCollider.GetComponentInParent<SpacecraftInteriorCollider>() != null;
        }
    }
}
