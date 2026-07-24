using System;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Resources;
using Farion.Simulation.World.Identity;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ResourceNodeInteractable : MonoBehaviour, IInteractable
    {
        [Header("Resource")]
        [SerializeField] ResourceNodeDefinition definition;
        [SerializeField] bool initializeReserveOnAwake = true;
        [SerializeField] int authoredSeed;
        [SerializeField] bool consumeOnDepleted = true;

        [Header("Prompt")]
        [SerializeField] string promptOverride;

        [Header("Runtime")]
        [Min(0)]
        [SerializeField] int remainingQuantity;
        [SerializeField] bool depleted;

        ResourceDepositDeltaStore deltaStore;
        GeneratedEntityId depositId = GeneratedEntityId.None;
        int initialQuantity;

        public event Action<ResourceNodeInteractable, int> Harvested;
        public event Action<ResourceNodeInteractable> Depleted;

        public string InteractionPrompt => ResolvePrompt();
        public ResourceNodeDefinition Definition => definition;
        public GeneratedEntityId DepositId => depositId;
        public int InitialQuantity => initialQuantity;
        public int RemainingQuantity => remainingQuantity;
        public bool IsDepleted => depleted;
        public bool HasRuntimeDeposit => depositId.IsValid && deltaStore != null;

        void Awake()
        {
            if (initializeReserveOnAwake && !depleted && remainingQuantity <= 0)
            {
                InitializeReserve();
            }
        }

        void OnValidate()
        {
            remainingQuantity = Mathf.Max(0, remainingQuantity);
        }

        public void Configure(ResourceNodeDefinition nodeDefinition, int initialQuantity, int seed)
        {
            Configure(nodeDefinition, initialQuantity, seed, GeneratedEntityId.None, null);
        }

        public void Configure(
            ResourceNodeDefinition nodeDefinition,
            int initialQuantity,
            int seed,
            GeneratedEntityId depositId,
            ResourceDepositDeltaStore resourceDeltaStore)
        {
            definition = nodeDefinition;
            authoredSeed = seed;
            this.depositId = depositId;
            deltaStore = resourceDeltaStore;
            this.initialQuantity = Mathf.Max(0, initialQuantity);
            int extractedAmount = deltaStore != null ? deltaStore.GetExtractedAmount(depositId) : 0;
            remainingQuantity = Mathf.Max(0, this.initialQuantity - extractedAmount);
            depleted = remainingQuantity <= 0;
        }

        public bool CanInteract(InteractionContext context)
        {
            InventoryItemDefinition yieldedItem = ResolveYieldedItem();
            int harvestAmount = ResolveHarvestAmount();
            if (depleted || yieldedItem == null || harvestAmount <= 0)
            {
                return false;
            }

            PlayerInventory inventory = ResolveInventory(context);
            return inventory != null && inventory.CanAdd(yieldedItem, harvestAmount);
        }

        public void Interact(InteractionContext context)
        {
            InventoryItemDefinition yieldedItem = ResolveYieldedItem();
            int harvestAmount = ResolveHarvestAmount();
            if (depleted || yieldedItem == null || harvestAmount <= 0)
            {
                return;
            }

            PlayerInventory inventory = ResolveInventory(context);
            if (inventory == null)
            {
                return;
            }

            if (!inventory.CanAdd(yieldedItem, harvestAmount))
            {
                return;
            }

            int added = inventory.TryAdd(yieldedItem, harvestAmount);
            if (added <= 0)
            {
                return;
            }

            remainingQuantity = Mathf.Max(0, remainingQuantity - added);
            deltaStore?.RecordExtraction(DepositId, added, initialQuantity);
            Harvested?.Invoke(this, added);
            SetDepleted(remainingQuantity <= 0);
        }

        public void RefreshRuntimeState()
        {
            if (deltaStore == null || !depositId.IsValid)
            {
                return;
            }

            remainingQuantity = Mathf.Max(0, initialQuantity - deltaStore.GetExtractedAmount(depositId));
            SetDepleted(remainingQuantity <= 0);
        }

        void SetDepleted(bool value)
        {
            if (!value)
            {
                depleted = false;
                return;
            }

            if (depleted)
            {
                if (consumeOnDepleted)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            depleted = true;
            Depleted?.Invoke(this);
            if (depleted && consumeOnDepleted)
            {
                gameObject.SetActive(false);
            }
        }

        void InitializeReserve()
        {
            initialQuantity = definition != null
                ? definition.EvaluateInitialReserve(authoredSeed)
                : 0;
            remainingQuantity = initialQuantity;
            depleted = remainingQuantity <= 0;
        }

        InventoryItemDefinition ResolveYieldedItem()
        {
            return definition != null ? definition.YieldedItem : null;
        }

        int ResolveHarvestAmount()
        {
            if (definition == null || remainingQuantity <= 0)
            {
                return 0;
            }

            return Mathf.Min(remainingQuantity, definition.AmountPerHarvest);
        }

        string ResolvePrompt()
        {
            if (depleted)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(promptOverride))
            {
                return promptOverride.Trim();
            }

            return definition != null
                ? $"{definition.PromptVerb} {definition.DisplayName}"
                : string.Empty;
        }

        static PlayerInventory ResolveInventory(InteractionContext context)
        {
            if (context.Actor == null)
            {
                return null;
            }

            PlayerInventory inventory = context.Actor.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            inventory = context.Actor.GetComponentInParent<PlayerInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            return context.Actor.GetComponentInChildren<PlayerInventory>();
        }
    }
}
