using Farion.Gameplay.Flight;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    sealed class ShipInteriorExitGate
    {
        float enteredAt = float.NegativeInfinity;
        float entryExitDistance;
        bool hasEntryExitDistance;

        public void Reset(float time, SpacecraftRig spacecraftRig, GameObject explorerRoot, Rigidbody explorerRigidbody)
        {
            enteredAt = time;
            hasEntryExitDistance = TryCalculateExteriorExitDistance(
                spacecraftRig,
                explorerRoot,
                explorerRigidbody,
                out entryExitDistance);
        }

        public void Clear()
        {
            enteredAt = float.NegativeInfinity;
            entryExitDistance = 0f;
            hasEntryExitDistance = false;
        }

        public bool CanTransitionOutside(
            float time,
            SpacecraftRig spacecraftRig,
            GameObject explorerRoot,
            Rigidbody explorerRigidbody,
            float transitionDistance,
            float cooldownSeconds,
            float progressDistance)
        {
            if (spacecraftRig == null ||
                spacecraftRig.ExteriorExitPoint == null ||
                spacecraftRig.RampController == null ||
                !spacecraftRig.RampController.IsOpen ||
                explorerRoot == null)
            {
                return false;
            }

            if (time < enteredAt + cooldownSeconds)
            {
                return false;
            }

            if (!TryCalculateExteriorExitDistance(spacecraftRig, explorerRoot, explorerRigidbody, out float outsideDistance))
            {
                return false;
            }

            float requiredDistance = transitionDistance;
            if (hasEntryExitDistance)
            {
                requiredDistance = Mathf.Max(requiredDistance, entryExitDistance + progressDistance);
            }

            return outsideDistance >= requiredDistance;
        }

        static bool TryCalculateExteriorExitDistance(
            SpacecraftRig spacecraftRig,
            GameObject explorerRoot,
            Rigidbody explorerRigidbody,
            out float outsideDistance)
        {
            outsideDistance = 0f;
            if (spacecraftRig == null || spacecraftRig.ExteriorExitPoint == null || explorerRoot == null)
            {
                return false;
            }

            Vector3 explorerPosition = explorerRigidbody != null
                ? explorerRigidbody.position
                : explorerRoot.transform.position;
            Transform exteriorExit = spacecraftRig.ExteriorExitPoint;
            Vector3 exitForward = exteriorExit.forward;
            if (exitForward.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            outsideDistance = Vector3.Dot(explorerPosition - exteriorExit.position, exitForward.normalized);
            return true;
        }
    }
}
