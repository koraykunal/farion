using Farion.Core.Identity;
using System;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.ResourceNodes
{
    [Serializable]
    public struct ResourceDepositDeltaSnapshot
    {
        [SerializeField] ulong depositId;
        [SerializeField] int extractedAmount;

        public ResourceDepositDeltaSnapshot(GeneratedEntityId depositId, int extractedAmount)
        {
            this.depositId = depositId.IsValid ? depositId.Value : 0UL;
            this.extractedAmount = Mathf.Max(0, extractedAmount);
        }

        public GeneratedEntityId DepositId => depositId != 0UL ? new GeneratedEntityId(depositId) : GeneratedEntityId.None;
        public int ExtractedAmount => Mathf.Max(0, extractedAmount);
        public bool IsValid => DepositId.IsValid && ExtractedAmount > 0;
    }
}
