using Farion.Gameplay.Flight;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    static class PlayerPossessionCameraTargets
    {
        public static Transform ResolveExterior(Transform explicitTarget, SpacecraftRig spacecraftRig, Transform spacecraftRoot)
        {
            if (explicitTarget != null)
            {
                return explicitTarget;
            }

            if (spacecraftRig != null && spacecraftRig.ChaseCameraTarget != null)
            {
                return spacecraftRig.ChaseCameraTarget;
            }

            return spacecraftRoot;
        }

        public static Transform ResolveCockpit(SpacecraftRig spacecraftRig)
        {
            if (spacecraftRig != null && spacecraftRig.CockpitCameraTarget != null)
            {
                return spacecraftRig.CockpitCameraTarget;
            }

            if (spacecraftRig != null && spacecraftRig.PilotSeatPoint != null)
            {
                return spacecraftRig.PilotSeatPoint;
            }

            return null;
        }
    }
}
