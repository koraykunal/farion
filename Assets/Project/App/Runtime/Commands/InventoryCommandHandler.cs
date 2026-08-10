using Farion.Gameplay.Commands;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;

namespace Farion.App.Commands
{
    internal sealed class InventoryCommandHandler
    {
        readonly SessionCommandScope scope;

        public InventoryCommandHandler(SessionCommandScope scope)
        {
            this.scope = scope;
        }

        public ResourceHarvestResult CanHarvest(ResourceHarvestRequest request)
        {
            return TryResolve(
                request,
                out ResourceNodeInteractable source,
                out IInventoryContainer destination,
                out ResourceHarvestResult failure)
                ? ResourceHarvestTransaction.CanExecute(source, destination)
                : failure;
        }

        public ResourceHarvestResult TryHarvest(ResourceHarvestRequest request)
        {
            return TryResolve(
                request,
                out ResourceNodeInteractable source,
                out IInventoryContainer destination,
                out ResourceHarvestResult failure)
                ? ResourceHarvestTransaction.TryExecute(source, destination)
                : failure;
        }

        public bool TryResolveDestination(
            ResourceHarvestRequest request,
            out IInventoryContainer destination)
        {
            return scope.TryResolveOwnedContainer(
                request.DestinationContainerId,
                out destination);
        }

        bool TryResolve(
            ResourceHarvestRequest request,
            out ResourceNodeInteractable source,
            out IInventoryContainer destination,
            out ResourceHarvestResult failure)
        {
            source = null;
            destination = null;

            if (!scope.TryResolveDeposit(request.DepositId, out source))
            {
                failure = ResourceHarvestResult.MissingSource;
                return false;
            }

            if (!request.DestinationContainerId.IsValid)
            {
                failure = ResourceHarvestResult.MissingDestination;
                return false;
            }

            if (!scope.TryResolveOwnedContainer(
                    request.DestinationContainerId,
                    out destination))
            {
                failure = ResourceHarvestResult.UnauthorizedDestination;
                return false;
            }

            if (!SessionCommandScope.RevisionMatches(
                    destination,
                    request.ExpectedDestinationRevision))
            {
                failure = ResourceHarvestResult.StaleDestination;
                return false;
            }

            failure = ResourceHarvestResult.Succeeded;
            return true;
        }
    }
}
