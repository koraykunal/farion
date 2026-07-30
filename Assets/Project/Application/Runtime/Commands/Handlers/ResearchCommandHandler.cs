using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;
using Farion.Gameplay.Session;

namespace Farion.App.Commands.Handlers
{
    internal sealed class ResearchCommandHandler
    {
        readonly GameplaySessionRuntime session;
        readonly SessionInventoryAccess access;

        public ResearchCommandHandler(
            GameplaySessionRuntime session,
            SessionInventoryAccess access)
        {
            this.session = session;
            this.access = access;
        }

        public bool HasCompleted(ResearchDefinition research)
        {
            return session?.FleetProgression != null &&
                   session.FleetProgression.HasCompletedResearch(research);
        }

        public ResearchUnlockResult CanComplete(
            ResearchTerminalRuntime terminal,
            ResearchDefinition research,
            IInventoryContainer inventory)
        {
            ResearchUnlockResult authorization =
                ValidateRequest(terminal, research, inventory);
            return authorization == ResearchUnlockResult.Succeeded
                ? session.FleetProgression.CanCompleteResearch(
                    research,
                    inventory)
                : authorization;
        }

        public ResearchUnlockResult TryComplete(
            ResearchTerminalRuntime terminal,
            ResearchDefinition research,
            IInventoryContainer inventory)
        {
            ResearchUnlockResult authorization =
                ValidateRequest(terminal, research, inventory);
            return authorization == ResearchUnlockResult.Succeeded
                ? session.FleetProgression.TryCompleteResearch(
                    research,
                    inventory)
                : authorization;
        }

        ResearchUnlockResult ValidateRequest(
            ResearchTerminalRuntime terminal,
            ResearchDefinition research,
            IInventoryContainer inventory)
        {
            if (!access.Owns(inventory))
            {
                return inventory == null
                    ? ResearchUnlockResult.MissingInventory
                    : ResearchUnlockResult.UnauthorizedInventory;
            }

            if (session?.FleetProgression == null)
            {
                return ResearchUnlockResult.MissingKnowledge;
            }

            return terminal != null && terminal.CanOffer(research)
                ? ResearchUnlockResult.Succeeded
                : ResearchUnlockResult.ResearchNotOffered;
        }
    }
}
