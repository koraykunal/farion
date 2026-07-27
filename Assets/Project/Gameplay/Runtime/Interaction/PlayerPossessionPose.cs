using Farion.Core.Persistence;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    static class PlayerPossessionPose
    {
        public static void Apply(Rigidbody rigidbody, Transform transformTarget, TransformPoseSnapshot snapshot)
        {
            if (rigidbody != null)
            {
                snapshot.ApplyTo(rigidbody);
                return;
            }

            snapshot.ApplyTo(transformTarget);
        }
    }
}
