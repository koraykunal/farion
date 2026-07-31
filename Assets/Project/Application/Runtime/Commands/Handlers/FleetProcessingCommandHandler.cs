using Farion.Gameplay.Commands;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Processing;

namespace Farion.App.Commands.Handlers
{
    internal sealed class FleetProcessingCommandHandler
    {
        readonly SessionInventoryAccess access;

        public FleetProcessingCommandHandler(SessionInventoryAccess access)
        {
            this.access = access;
        }

        public FleetProcessingResult CanExecute(
            ProcessingRecipeDefinition recipe)
        {
            if (recipe == null)
            {
                return FleetProcessingResult.MissingRecipe;
            }

            if (!recipe.IsValid)
            {
                return FleetProcessingResult.InvalidRecipe;
            }

            FleetStorageInventory storage = access.FleetStorage;
            if (storage == null)
            {
                return FleetProcessingResult.MissingStorage;
            }

            if (!access.OwnsFleetStorage(storage))
            {
                return FleetProcessingResult.UnauthorizedStorage;
            }

            return storage.CanExchange(recipe.Inputs, recipe.Outputs)
                ? FleetProcessingResult.Succeeded
                : FleetProcessingResult.Rejected;
        }

        public FleetProcessingResult TryExecute(
            ProcessingRecipeDefinition recipe)
        {
            FleetProcessingResult validation = CanExecute(recipe);
            if (validation != FleetProcessingResult.Succeeded)
            {
                return validation;
            }

            return access.FleetStorage.TryExchange(recipe.Inputs, recipe.Outputs)
                ? FleetProcessingResult.Succeeded
                : FleetProcessingResult.Rejected;
        }
    }
}
