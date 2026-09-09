using System.Collections.Generic;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Ships;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class FleetCargoUnloadInteractable : MonoBehaviour, IInteractable
    {

        [SerializeField] ShuttleDockingBoundary dockingBoundary;

        public string InteractionPrompt => InteractionPromptKeys.UnloadCargo;
        public bool HasValidAuthoring => dockingBoundary != null;

        void Awake()
        {
            InteractableLayerBinding.Apply(this);
        }

        void Reset()
        {
            ResolveBoundary();
        }

        void OnValidate()
        {
            ResolveBoundary();
        }

        public bool CanInteract(InteractionContext context)
        {
            return TryResolveAuthorizedCargo(
                context.Commands,
                out _);
        }

        public void Interact(InteractionContext context)
        {
            if (TryResolveAuthorizedCargo(
                    context.Commands,
                    out CargoTransferRequest request))
            {
                context.Commands.TryUnloadAssignedShuttleCargo(request);
            }
        }

        void ResolveBoundary()
        {
            dockingBoundary ??=
                GetComponentInParent<ShuttleDockingBoundary>();
        }

        bool TryResolveAuthorizedCargo(
            IGameplayCommandGateway commands,
            out CargoTransferRequest request)
        {
            request = default;
            if (commands == null || dockingBoundary == null)
            {
                return false;
            }

            IReadOnlyList<ShuttleRuntimeBinding> dockedShuttles =
                dockingBoundary.DockedShuttles;
            for (int i = 0; i < dockedShuttles.Count; i++)
            {
                ShuttleRuntimeBinding shuttle = dockedShuttles[i];
                ShuttleCargoInventory candidate = shuttle != null
                    ? shuttle.Cargo
                    : null;
                if (candidate == null)
                {
                    continue;
                }

                if (commands.IsAssignedShuttleCargo(candidate.ContainerId))
                {
                    request = new CargoTransferRequest(
                        candidate.ContainerId,
                        candidate.Revision);
                    return true;
                }
            }

            return false;
        }
    }
}
