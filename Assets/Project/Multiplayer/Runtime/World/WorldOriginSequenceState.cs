using System.Collections.Generic;
using UnityEngine;

namespace Farion.Multiplayer.World
{
    public sealed class WorldOriginSequenceState
    {
        const int HistoryCapacity = 32;

        readonly Queue<SequenceRecord> history = new();

        public uint Sequence { get; private set; }
        public Vector3 AccumulatedOrigin { get; private set; }
        public int ReferenceBodyId { get; private set; }

        public bool CanAccept(uint sequence)
        {
            return sequence > Sequence;
        }

        public void RecordServerShift(Vector3 accumulatedOrigin, int referenceBodyId)
        {
            Sequence++;
            AccumulatedOrigin = accumulatedOrigin;
            ReferenceBodyId = referenceBodyId;
            RecordHistory();
        }

        public bool TryAccept(
            uint sequence,
            Vector3 accumulatedOrigin,
            int referenceBodyId,
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
            ReferenceBodyId = referenceBodyId;
            RecordHistory();
            return true;
        }

        public bool TryGetReferenceBody(uint sequence, out int referenceBodyId)
        {
            if (sequence == Sequence)
            {
                referenceBodyId = ReferenceBodyId;
                return true;
            }

            foreach (SequenceRecord record in history)
            {
                if (record.Sequence == sequence)
                {
                    referenceBodyId = record.ReferenceBodyId;
                    return true;
                }
            }

            referenceBodyId = 0;
            return false;
        }

        public void Reset()
        {
            Sequence = 0;
            AccumulatedOrigin = Vector3.zero;
            ReferenceBodyId = 0;
            history.Clear();
        }

        void RecordHistory()
        {
            history.Enqueue(new SequenceRecord(Sequence, ReferenceBodyId));
            while (history.Count > HistoryCapacity)
            {
                history.Dequeue();
            }
        }

        readonly struct SequenceRecord
        {
            public SequenceRecord(uint sequence, int referenceBodyId)
            {
                Sequence = sequence;
                ReferenceBodyId = referenceBodyId;
            }

            public uint Sequence { get; }
            public int ReferenceBodyId { get; }
        }
    }
}
