using FishNet.Broadcast;
using UnityEngine;

namespace Farion.Multiplayer.World
{
    public struct WorldOriginBroadcast : IBroadcast
    {
        public uint Sequence;
        public Vector3 ShiftDelta;
        public Vector3 AccumulatedOrigin;

        public WorldOriginBroadcast(
            uint sequence,
            Vector3 shiftDelta,
            Vector3 accumulatedOrigin)
        {
            Sequence = sequence;
            ShiftDelta = shiftDelta;
            AccumulatedOrigin = accumulatedOrigin;
        }
    }
}
