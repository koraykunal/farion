using System.Collections.Generic;
using Farion.Gameplay.ResourceNodes;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Session
{
    public static class WorldFocusTracking
    {
        public static Transform Resolve(Rigidbody body, Transform fallbackRoot)
        {
            return body != null ? body.transform : fallbackRoot;
        }

        public static Transform Resolve(Rigidbody body, GameObject fallbackRoot)
        {
            return Resolve(body, fallbackRoot != null ? fallbackRoot.transform : null);
        }

        public static void Apply(
            WorldOriginRebaser originRebaser,
            IReadOnlyList<ResourceDepositRuntimeSpawner> resourceStreamers,
            Transform target)
        {
            if (originRebaser != null)
            {
                originRebaser.SetTrackingTarget(target);
            }

            if (resourceStreamers == null)
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
