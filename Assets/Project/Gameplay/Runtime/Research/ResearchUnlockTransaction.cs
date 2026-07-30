using System.Collections.Generic;
using Farion.Gameplay.Crafting;
using Farion.Gameplay.Domain.Fleet;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;

namespace Farion.Gameplay.Research
{
    public static class ResearchUnlockTransaction
    {
        public static ResearchUnlockResult CanExecute(
            ResearchDefinition research,
            IInventoryContainer inventory,
            FleetKnowledgeState knowledge)
        {
            ResearchUnlockResult validation =
                ValidateResearch(research, inventory, knowledge, out DefinitionId researchId);
            if (validation != ResearchUnlockResult.Succeeded)
            {
                return validation;
            }

            if (knowledge.HasDiscovery(researchId))
            {
                return ResearchUnlockResult.AlreadyCompleted;
            }

            return inventory.CanRemoveAll(research.RequiredItems)
                ? ResearchUnlockResult.Succeeded
                : ResearchUnlockResult.MissingIngredients;
        }

        public static ResearchUnlockResult TryExecute(
            ResearchDefinition research,
            IInventoryContainer inventory,
            FleetKnowledgeState knowledge)
        {
            ResearchUnlockResult validation = CanExecute(research, inventory, knowledge);
            if (validation != ResearchUnlockResult.Succeeded)
            {
                return validation;
            }

            if (!DefinitionId.TryCreate(research.ResearchId, out DefinitionId researchId))
            {
                return ResearchUnlockResult.InvalidResearch;
            }

            if (!inventory.TryRemoveAll(research.RequiredItems))
            {
                return ResearchUnlockResult.InventoryRejected;
            }

            ResearchUnlockResult unlockResult =
                ApplyKnowledgeUnlocks(research, knowledge, researchId);
            if (unlockResult == ResearchUnlockResult.Succeeded)
            {
                return ResearchUnlockResult.Succeeded;
            }

            return RollBackRequiredItems(research, inventory)
                ? unlockResult
                : ResearchUnlockResult.RollbackFailed;
        }

        static ResearchUnlockResult ValidateResearch(
            ResearchDefinition research,
            IInventoryContainer inventory,
            FleetKnowledgeState knowledge,
            out DefinitionId researchId)
        {
            researchId = default;
            if (research == null)
            {
                return ResearchUnlockResult.MissingResearch;
            }

            if (inventory == null)
            {
                return ResearchUnlockResult.MissingInventory;
            }

            if (knowledge == null)
            {
                return ResearchUnlockResult.MissingKnowledge;
            }

            if (!DefinitionId.TryCreate(research.ResearchId, out researchId))
            {
                return ResearchUnlockResult.InvalidResearch;
            }

            for (int i = 0; i < research.UnlockedRecipes.Count; i++)
            {
                RecipeDefinition recipe = research.UnlockedRecipes[i];
                if (recipe == null ||
                    !DefinitionId.TryCreate(recipe.RecipeId, out _))
                {
                    return ResearchUnlockResult.InvalidResearch;
                }
            }

            for (int i = 0; i < research.UnlockedCapabilityIds.Count; i++)
            {
                if (!DefinitionId.TryCreate(
                        research.UnlockedCapabilityIds[i],
                        out _))
                {
                    return ResearchUnlockResult.InvalidResearch;
                }
            }

            return ResearchUnlockResult.Succeeded;
        }

        static ResearchUnlockResult ApplyKnowledgeUnlocks(
            ResearchDefinition research,
            FleetKnowledgeState knowledge,
            DefinitionId researchId)
        {
            List<DefinitionId> blueprintIds =
                new(research.UnlockedRecipes.Count);
            for (int i = 0; i < research.UnlockedRecipes.Count; i++)
            {
                RecipeDefinition recipe = research.UnlockedRecipes[i];
                DefinitionId.TryCreate(recipe.RecipeId, out DefinitionId blueprintId);
                blueprintIds.Add(blueprintId);
            }

            List<DefinitionId> capabilityIds =
                new(research.UnlockedCapabilityIds.Count);
            for (int i = 0; i < research.UnlockedCapabilityIds.Count; i++)
            {
                DefinitionId.TryCreate(
                    research.UnlockedCapabilityIds[i],
                    out DefinitionId capabilityId);
                capabilityIds.Add(capabilityId);
            }

            return knowledge.TryCompleteResearch(
                       researchId,
                       blueprintIds,
                       capabilityIds) == FleetOperationResult.Succeeded
                ? ResearchUnlockResult.Succeeded
                : ResearchUnlockResult.KnowledgeRejected;
        }

        static bool RollBackRequiredItems(
            ResearchDefinition research,
            IInventoryContainer inventory)
        {
            for (int i = 0; i < research.RequiredItems.Count; i++)
            {
                ItemStackDefinition stack = research.RequiredItems[i];
                if (stack == null || !stack.IsValid)
                {
                    return false;
                }

                if (inventory.TryAdd(stack.Item, stack.Amount) != stack.Amount)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
