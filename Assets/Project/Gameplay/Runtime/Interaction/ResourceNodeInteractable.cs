using System;
using Farion.Gameplay.Commands;
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
        long revision;

        public event Action<ResourceNodeInteractable, int> Harvested;
        public event Action<ResourceNodeInteractable> Depleted;

        public string InteractionPrompt => ResolvePrompt();
        public ResourceNodeDefinition Definition => definition;
        public GeneratedEntityId DepositId => depositId;
        public int InitialQuantity => initialQuantity;
        public int RemainingQuantity => remainingQuantity;
        public bool IsDepleted => depleted;
        public bool HasRuntimeDeposit => depositId.IsValid && deltaStore != null;
        public long Revision => revision;

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
            revision = 0L;
        }

        public bool CanInteract(InteractionContext context)
        {
            IInventoryContainer inventory = ResolveInventory(context);
            return context.Commands != null &&
                   context.Commands.CanHarvest(this, inventory) ==
                   ResourceHarvestResult.Succeeded;
        }

        public void Interact(InteractionContext context)
        {
            IInventoryContainer inventory = ResolveInventory(context);
            context.Commands?.TryHarvest(this, inventory);
        }

        public void RefreshRuntimeState()
        {
            if (deltaStore == null || !depositId.IsValid)
            {
                return;
            }

            int nextQuantity = Mathf.Max(
                0,
                initialQuantity - deltaStore.GetExtractedAmount(depositId));
            if (nextQuantity != remainingQuantity)
            {
                remainingQuantity = nextQuantity;
                IncrementRevision();
            }

            SetDepleted(remainingQuantity <= 0);
        }

        internal bool TryGetHarvestOffer(
            out InventoryItemDefinition item,
            out int amount)
        {
            item = ResolveYieldedItem();
            amount = ResolveHarvestAmount();
            return !depleted && item != null && amount > 0;
        }

        internal bool TryCommitHarvest(int amount, long expectedRevision)
        {
            if (expectedRevision != revision ||
                amount <= 0 ||
                !TryGetHarvestOffer(out _, out int offeredAmount) ||
                amount > offeredAmount)
            {
                return false;
            }

            remainingQuantity = Mathf.Max(0, remainingQuantity - amount);
            IncrementRevision();
            deltaStore?.RecordExtraction(DepositId, amount, initialQuantity);
            Harvested?.Invoke(this, amount);
            SetDepleted(remainingQuantity <= 0);
            return true;
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
            revision = 0L;
        }

        void IncrementRevision()
        {
            if (revision == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "Resource node revision capacity was exhausted.");
            }

            revision++;
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

        static IInventoryContainer ResolveInventory(InteractionContext context)
        {
            if (context.Actor == null)
            {
                return null;
            }

            InventoryContainerComponent inventory =
                context.Actor.GetComponent<InventoryContainerComponent>();
            if (inventory != null)
            {
                return inventory;
            }

            inventory =
                context.Actor.GetComponentInParent<InventoryContainerComponent>();
            if (inventory != null)
            {
                return inventory;
            }

            return context.Actor.GetComponentInChildren<InventoryContainerComponent>();
        }
    }
}
