using Farion.Gameplay.Commands;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Processing;

namespace Farion.App.Commands
{
    internal sealed class FleetProcessingCommandHandler
    {
        readonly SessionCommandScope scope;

        public FleetProcessingCommandHandler(SessionCommandScope scope)
        {
            this.scope = scope;
        }

        public FleetProcessingResult CanExecute(FleetProcessingRequest request)
        {
            return TryResolve(
                request,
                out ProcessingRecipeDefinition recipe,
                out FleetStorageInventory storage,
                out FleetProcessingResult failure)
                ? storage.CanExchange(recipe.Inputs, recipe.Outputs)
                    ? FleetProcessingResult.Succeeded
                    : FleetProcessingResult.Rejected
                : failure;
        }

        public FleetProcessingResult TryExecute(FleetProcessingRequest request)
        {
            if (!TryResolve(
                    request,
                    out ProcessingRecipeDefinition recipe,
                    out FleetStorageInventory storage,
                    out FleetProcessingResult failure))
            {
                return failure;
            }

            return storage.TryExchange(recipe.Inputs, recipe.Outputs)
                ? FleetProcessingResult.Succeeded
                : FleetProcessingResult.Rejected;
        }

        bool TryResolve(
            FleetProcessingRequest request,
            out ProcessingRecipeDefinition recipe,
            out FleetStorageInventory storage,
            out FleetProcessingResult failure)
        {
            recipe = null;
            storage = null;

            if (!request.RecipeId.IsValid)
            {
                failure = FleetProcessingResult.MissingRecipe;
                return false;
            }

            if (!scope.TryResolveProcessingRecipe(request.RecipeId, out recipe))
            {
                failure = FleetProcessingResult.MissingRecipe;
                return false;
            }

            if (!recipe.IsValid)
            {
                failure = FleetProcessingResult.InvalidRecipe;
                return false;
            }

            if (scope.FleetStorage == null)
            {
                failure = FleetProcessingResult.MissingStorage;
                return false;
            }

            if (!scope.TryResolveOwnedFleetStorage(out storage) ||
                storage.ContainerId != request.FleetStorageId)
            {
                failure = FleetProcessingResult.UnauthorizedStorage;
                return false;
            }

            if (!SessionCommandScope.RevisionMatches(
                    storage,
                    request.ExpectedFleetStorageRevision))
            {
                failure = FleetProcessingResult.StaleStorage;
                return false;
            }

            failure = FleetProcessingResult.Succeeded;
            return true;
        }
    }
}
