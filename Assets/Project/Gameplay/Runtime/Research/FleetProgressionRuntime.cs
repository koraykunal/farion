using System.Collections.Generic;
using Farion.Gameplay.Crafting;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Fleet;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.Gameplay.Research
{
    [DisallowMultipleComponent]
    public sealed class FleetProgressionRuntime : MonoBehaviour
    {
        FleetKnowledgeState knowledge = new();

        public FleetKnowledgeState Knowledge => knowledge ??= new FleetKnowledgeState();

        public bool HasCompletedResearch(ResearchDefinition research)
        {
            return research != null &&
                   DefinitionId.TryCreate(
                       research.ResearchId,
                       out DefinitionId researchId) &&
                   Knowledge.HasDiscovery(researchId);
        }

        public bool HasUnlockedRecipe(RecipeDefinition recipe)
        {
            return recipe != null &&
                   DefinitionId.TryCreate(
                       recipe.RecipeId,
                       out DefinitionId recipeId) &&
                   Knowledge.HasBlueprint(recipeId);
        }

        public ResearchUnlockResult CanCompleteResearch(
            ResearchDefinition research,
            IInventoryContainer inventory)
        {
            return ResearchUnlockTransaction.CanExecute(
                research,
                inventory,
                Knowledge);
        }

        public ResearchUnlockResult TryCompleteResearch(
            ResearchDefinition research,
            IInventoryContainer inventory)
        {
            return ResearchUnlockTransaction.TryExecute(
                research,
                inventory,
                Knowledge);
        }

        public FleetKnowledgeSnapshot CaptureSnapshot()
        {
            return new FleetKnowledgeSnapshot(
                ToStrings(Knowledge.Capabilities),
                ToStrings(Knowledge.Blueprints),
                ToStrings(Knowledge.Discoveries));
        }

        public bool CanApplySnapshot(
            FleetKnowledgeSnapshot snapshot,
            GameplayDefinitionRegistry definitions)
        {
            if (snapshot == null ||
                !snapshot.IsValid ||
                definitions == null)
            {
                return false;
            }

            for (int i = 0; i < snapshot.Blueprints.Count; i++)
            {
                if (!definitions.TryGetRecipe(
                        snapshot.Blueprints[i],
                        out _))
                {
                    return false;
                }
            }

            for (int i = 0; i < snapshot.Discoveries.Count; i++)
            {
                if (!definitions.TryGetResearch(
                        snapshot.Discoveries[i],
                        out _))
                {
                    return false;
                }
            }

            return true;
        }

        public bool ApplySnapshot(
            FleetKnowledgeSnapshot snapshot,
            GameplayDefinitionRegistry definitions)
        {
            if (!CanApplySnapshot(snapshot, definitions))
            {
                return false;
            }

            FleetKnowledgeState restored = new();
            if (snapshot != null)
            {
                if (!ApplyIds(snapshot.Capabilities, restored.TryUnlockCapability) ||
                    !ApplyIds(snapshot.Blueprints, restored.TryUnlockBlueprint) ||
                    !ApplyIds(snapshot.Discoveries, restored.TryRecordDiscovery))
                {
                    return false;
                }
            }

            knowledge = restored;
            return true;
        }

        static List<string> ToStrings(IEnumerable<DefinitionId> ids)
        {
            List<string> values = new();
            if (ids == null)
            {
                return values;
            }

            foreach (DefinitionId id in ids)
            {
                if (id.IsValid)
                {
                    values.Add(id.Value);
                }
            }

            values.Sort(System.StringComparer.Ordinal);
            return values;
        }

        static bool ApplyIds(
            IReadOnlyList<string> ids,
            System.Func<DefinitionId, long, FleetOperationResult> apply)
        {
            if (ids == null)
            {
                return true;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (!DefinitionId.TryCreate(ids[i], out DefinitionId id))
                {
                    return false;
                }

                FleetOperationResult result = apply(id, -1L);
                if (result != FleetOperationResult.Succeeded &&
                    result != FleetOperationResult.AlreadyExists)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
