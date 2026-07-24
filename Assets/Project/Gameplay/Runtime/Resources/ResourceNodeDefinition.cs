using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.Gameplay.Resources
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Resources/Resource Node Definition", fileName = "SO_ResourceNode")]
    public sealed class ResourceNodeDefinition : ScriptableObject
    {
        [SerializeField] string nodeId = "resource.node";
        [SerializeField] string displayName = "Resource Deposit";
        [SerializeField] string promptVerb = "Collect";

        [Header("Yield")]
        [SerializeField] InventoryItemDefinition yieldedItem;
        [Min(1)]
        [SerializeField] int minReserve = 1;
        [Min(1)]
        [SerializeField] int maxReserve = 1;
        [Min(1)]
        [SerializeField] int amountPerHarvest = 1;

        [Header("Requirements")]
        [Min(0)]
        [SerializeField] int requiredToolTier;
        [Min(0f)]
        [SerializeField] float harvestDurationSeconds;
        [SerializeField] bool requiresScan;

        [Header("Presentation")]
        [SerializeField] GameObject visualPrefab;

        public string NodeId => string.IsNullOrWhiteSpace(nodeId) ? name : nodeId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? NodeId : displayName.Trim();
        public string PromptVerb => string.IsNullOrWhiteSpace(promptVerb) ? "Collect" : promptVerb.Trim();
        public InventoryItemDefinition YieldedItem => yieldedItem;
        public int MinReserve => Mathf.Max(1, minReserve);
        public int MaxReserve => Mathf.Max(MinReserve, maxReserve);
        public int AmountPerHarvest => Mathf.Max(1, amountPerHarvest);
        public int RequiredToolTier => Mathf.Max(0, requiredToolTier);
        public float HarvestDurationSeconds => Mathf.Max(0f, harvestDurationSeconds);
        public bool RequiresScan => requiresScan;
        public GameObject VisualPrefab => visualPrefab;
        public bool HasVisualPrefab => visualPrefab != null;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                nodeId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = nodeId;
            }

            minReserve = Mathf.Max(1, minReserve);
            maxReserve = Mathf.Max(minReserve, maxReserve);
            amountPerHarvest = Mathf.Max(1, amountPerHarvest);
            requiredToolTier = Mathf.Max(0, requiredToolTier);
            harvestDurationSeconds = Mathf.Max(0f, harvestDurationSeconds);
        }

        public int EvaluateInitialReserve(int seed)
        {
            int min = MinReserve;
            int max = MaxReserve;
            if (min >= max)
            {
                return min;
            }

            uint hash = Hash((uint)seed, StableStringHash(NodeId));
            return min + (int)(hash % (uint)(max - min + 1));
        }

        static uint StableStringHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        static uint Hash(uint a, uint b)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ a) * 16777619u;
                hash = (hash ^ b) * 16777619u;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                hash *= 3266489917u;
                hash ^= hash >> 16;
                return hash;
            }
        }
    }
}
