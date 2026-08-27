using FishNet.Broadcast;
using UnityEngine;

namespace Farion.Multiplayer.World
{
    public struct WorldOriginBroadcast : IBroadcast
    {
        public uint Sequence;
        public Vector3 AccumulatedOrigin;
        public int ReferenceBodyId;
        public uint ApplyTick;

        public WorldOriginBroadcast(
            uint sequence,
            Vector3 accumulatedOrigin,
            int referenceBodyId,
            uint applyTick)
        {
            Sequence = sequence;
            AccumulatedOrigin = accumulatedOrigin;
            ReferenceBodyId = referenceBodyId;
            ApplyTick = applyTick;
        }
    }
}
