using FishNet.Broadcast;
using UnityEngine;

namespace Farion.Multiplayer.World
{
    public struct WorldOriginBroadcast : IBroadcast
    {
        public ulong ZoneId;
        public uint Sequence;
        public Vector3 AccumulatedOrigin;
        public int ReferenceBodyId;
        public uint ApplyTick;

        public WorldOriginBroadcast(
            ulong zoneId,
            uint sequence,
            Vector3 accumulatedOrigin,
            int referenceBodyId,
            uint applyTick)
        {
            ZoneId = zoneId;
            Sequence = sequence;
            AccumulatedOrigin = accumulatedOrigin;
            ReferenceBodyId = referenceBodyId;
            ApplyTick = applyTick;
        }
    }
}
