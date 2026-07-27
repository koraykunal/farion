using System.Collections.Generic;
using Farion.Gameplay.Resources;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    static class PlayerPossessionTracking
    {
        public static Transform ResolveSpacecraftTarget(Rigidbody spacecraftRigidbody, Transform spacecraftRoot)
        {
            if (spacecraftRigidbody != null)
            {
                return spacecraftRigidbody.transform;
            }

            return spacecraftRoot;
        }

        public static Transform ResolveExplorerTarget(Rigidbody explorerRigidbody, GameObject explorerRoot)
        {
            if (explorerRigidbody != null)
            {
                return explorerRigidbody.transform;
            }

            return explorerRoot != null ? explorerRoot.transform : null;
        }

        public static void ApplyTargets(
            bool updateOriginTrackingTarget,
            WorldOriginRebaser originRebaser,
            bool updateResourceStreamingTarget,
            IReadOnlyList<ResourceDepositRuntimeSpawner> resourceStreamers,
            Transform target)
        {
            if (updateOriginTrackingTarget && originRebaser != null)
            {
                originRebaser.SetTrackingTarget(target);
            }

            if (!updateResourceStreamingTarget || resourceStreamers == null)
            {
                return;
            }

            for (int i = 0; i < resourceStreamers.Count; i++)
            {
                ResourceDepositRuntimeSpawner streamer = resourceStreamers[i];
                if (streamer != null)
                {
                    streamer.SetTrackingTarget(target);
                }
            }
        }
    }
}
