using Farion.Gameplay.Commands;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Processing;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class FleetProcessingInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] ProcessingRecipeDefinition recipe;
        [SerializeField] FleetRuntime fleet;

        public string InteractionPrompt => recipe != null
            ? InteractionPromptKeys.WithArgument(
                InteractionPromptKeys.ProcessRecipe,
                recipe.DisplayName)
            : InteractionPromptKeys.ProcessMaterials;

        void Awake()
        {
            InteractableLayerBinding.Apply(this);
            ResolveFleet();
        }

        void OnValidate()
        {
            ResolveFleet();
        }

        public bool CanInteract(InteractionContext context)
        {
            return context.Commands != null &&
                   TryBuildRequest(out FleetProcessingRequest request) &&
                   context.Commands.CanProcessFleetRecipe(request) ==
                   FleetProcessingResult.Succeeded;
        }

        public void Interact(InteractionContext context)
        {
            if (context.Commands != null &&
                TryBuildRequest(out FleetProcessingRequest request))
            {
                context.Commands.TryProcessFleetRecipe(request);
            }
        }

        void ResolveFleet()
        {
            fleet ??= GetComponentInParent<FleetRuntime>();
        }

        bool TryBuildRequest(out FleetProcessingRequest request)
        {
            request = default;
            ResolveFleet();
            FleetStorageInventory storage = fleet != null ? fleet.Storage : null;
            if (recipe == null ||
                storage == null ||
                !DefinitionId.TryCreate(recipe.RecipeId, out DefinitionId recipeId))
            {
                return false;
            }

            request = new FleetProcessingRequest(
                recipeId,
                storage.ContainerId,
                storage.Revision);
            return true;
        }
    }
}
