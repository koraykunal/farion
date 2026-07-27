using System;
using UnityEngine;

namespace Farion.Simulation.World
{
    [Serializable]
    public struct WorldOriginSnapshot
    {
        const int CurrentVersion = 1;

        [SerializeField] int version;
        [SerializeField] Vector3 accumulatedOffset;
        [SerializeField] int shiftCount;

        public WorldOriginSnapshot(Vector3 accumulatedOffset, int shiftCount)
        {
            version = CurrentVersion;
            this.accumulatedOffset = accumulatedOffset;
            this.shiftCount = Mathf.Max(0, shiftCount);
        }

        public Vector3 AccumulatedOffset => accumulatedOffset;
        public int ShiftCount => Mathf.Max(0, shiftCount);
        public bool IsSupported => version == CurrentVersion;
    }
}
