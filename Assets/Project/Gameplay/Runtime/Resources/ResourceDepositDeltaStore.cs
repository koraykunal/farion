using Farion.Core.Identity;
using System.Collections.Generic;
using Farion.Simulation.World;

namespace Farion.Gameplay.Resources
{
    public sealed class ResourceDepositDeltaStore
    {
        readonly Dictionary<GeneratedEntityId, int> extractedAmountByDeposit = new();

        public int Count => extractedAmountByDeposit.Count;

        public int GetExtractedAmount(GeneratedEntityId depositId)
        {
            return depositId.IsValid && extractedAmountByDeposit.TryGetValue(depositId, out int extractedAmount)
                ? extractedAmount
                : 0;
        }

        public int CalculateRemainingQuantity(ResourceDepositData deposit)
        {
            if (!deposit.IsValid)
            {
                return 0;
            }

            return UnityEngine.Mathf.Max(0, deposit.InitialReserve - GetExtractedAmount(deposit.DepositId));
        }

        public void RecordExtraction(GeneratedEntityId depositId, int amount)
        {
            if (!depositId.IsValid || amount <= 0)
            {
                return;
            }

            int current = GetExtractedAmount(depositId);
            extractedAmountByDeposit[depositId] = current + amount;
        }

        public void RecordExtraction(GeneratedEntityId depositId, int amount, int maxExtractedAmount)
        {
            if (!depositId.IsValid || amount <= 0)
            {
                return;
            }

            int current = GetExtractedAmount(depositId);
            int next = current + amount;
            if (maxExtractedAmount > 0)
            {
                next = System.Math.Min(next, maxExtractedAmount);
            }

            extractedAmountByDeposit[depositId] = next;
        }

        public void Apply(ResourceDepositDelta delta)
        {
            if (delta.DepositId.IsValid && !delta.IsEmpty)
            {
                extractedAmountByDeposit[delta.DepositId] = delta.ExtractedAmount;
            }
        }

        public void Apply(IEnumerable<ResourceDepositDelta> deltas)
        {
            if (deltas == null)
            {
                return;
            }

            foreach (ResourceDepositDelta delta in deltas)
            {
                Apply(delta);
            }
        }

        public void CopyDeltas(List<ResourceDepositDelta> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            foreach (KeyValuePair<GeneratedEntityId, int> pair in extractedAmountByDeposit)
            {
                if (pair.Value > 0)
                {
                    results.Add(new ResourceDepositDelta(pair.Key, pair.Value));
                }
            }

            results.Sort((a, b) => a.DepositId.Value.CompareTo(b.DepositId.Value));
        }

        public void CaptureSnapshot(List<ResourceDepositDeltaSnapshot> results)
        {
            if (results == null)
            {
                return;
            }

            foreach (KeyValuePair<GeneratedEntityId, int> pair in extractedAmountByDeposit)
            {
                if (pair.Value > 0)
                {
                    results.Add(new ResourceDepositDeltaSnapshot(pair.Key, pair.Value));
                }
            }

            results.Sort((a, b) => a.DepositId.Value.CompareTo(b.DepositId.Value));
        }

        public void ApplySnapshot(IEnumerable<ResourceDepositDeltaSnapshot> snapshots)
        {
            extractedAmountByDeposit.Clear();
            if (snapshots == null)
            {
                return;
            }

            foreach (ResourceDepositDeltaSnapshot snapshot in snapshots)
            {
                if (snapshot.IsValid)
                {
                    extractedAmountByDeposit[snapshot.DepositId] = snapshot.ExtractedAmount;
                }
            }
        }

        public void ApplySnapshot(ResourceDepositDeltaSnapshot snapshot)
        {
            if (snapshot.IsValid)
            {
                extractedAmountByDeposit[snapshot.DepositId] = snapshot.ExtractedAmount;
            }
        }

        public bool TryGetDelta(GeneratedEntityId depositId, out ResourceDepositDelta delta)
        {
            int extractedAmount = GetExtractedAmount(depositId);
            if (!depositId.IsValid || extractedAmount <= 0)
            {
                delta = default;
                return false;
            }

            delta = new ResourceDepositDelta(depositId, extractedAmount);
            return true;
        }

        public void Clear()
        {
            extractedAmountByDeposit.Clear();
        }
    }
}
