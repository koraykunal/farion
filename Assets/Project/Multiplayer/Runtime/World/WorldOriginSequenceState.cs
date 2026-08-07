using UnityEngine;

namespace Farion.Multiplayer.World
{
    public sealed class WorldOriginSequenceState
    {
        public uint Sequence { get; private set; }
        public Vector3 AccumulatedOrigin { get; private set; }

        public void RecordServerShift(Vector3 accumulatedOrigin)
        {
            Sequence++;
            AccumulatedOrigin = accumulatedOrigin;
        }

        public bool TryAccept(
            uint sequence,
            Vector3 accumulatedOrigin,
            out Vector3 delta)
        {
            delta = Vector3.zero;
            if (sequence <= Sequence)
            {
                return false;
            }

            delta = accumulatedOrigin - AccumulatedOrigin;
            Sequence = sequence;
            AccumulatedOrigin = accumulatedOrigin;
            return true;
        }

        public void Reset()
        {
            Sequence = 0;
            AccumulatedOrigin = Vector3.zero;
        }
    }
}
