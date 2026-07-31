using Farion.Gameplay.Commands;
using Farion.Gameplay.Processing;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class FleetProcessingInteractable : MonoBehaviour, IInteractable
    {
        const string DefaultPrompt = "Process materials";

        [SerializeField] ProcessingRecipeDefinition recipe;

        public string InteractionPrompt => recipe != null
            ? $"Process {recipe.DisplayName}"
            : DefaultPrompt;

        public bool CanInteract(InteractionContext context)
        {
            return context.Commands?.CanProcessFleetRecipe(recipe) ==
                   FleetProcessingResult.Succeeded;
        }

        public void Interact(InteractionContext context)
        {
            context.Commands?.TryProcessFleetRecipe(recipe);
        }
    }
}
