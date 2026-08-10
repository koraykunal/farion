using System.Collections.Generic;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Ships;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class FleetCargoUnloadInteractable : MonoBehaviour, IInteractable
    {

        void Awake()
        {
            InteractableLayerBinding.Apply(this);
        }
        const string DefaultPrompt = "Unload shuttle cargo";

        [SerializeField] ShuttleDockingBoundary dockingBoundary;
        [SerializeField] string prompt = DefaultPrompt;

        public string InteractionPrompt =>
            string.IsNullOrWhiteSpace(prompt) ? DefaultPrompt : prompt.Trim();
        public bool HasValidAuthoring => dockingBoundary != null;

        void Reset()
        {
            ResolveBoundary();
            prompt = DefaultPrompt;
        }

        void OnValidate()
        {
            ResolveBoundary();
            prompt = InteractionPrompt;
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

                CargoTransferRequest candidateRequest = new(
                    candidate.ContainerId,
                    candidate.Revision);
                if (CanAttempt(
                        commands.CanUnloadAssignedShuttleCargo(candidateRequest)))
                {
                    request = candidateRequest;
                    return true;
                }
            }

            return false;
        }

        static bool CanAttempt(CargoTransferResult result)
        {
            return result != CargoTransferResult.MissingSource &&
                   result != CargoTransferResult.MissingDestination &&
                   result != CargoTransferResult.UnauthorizedSource &&
                   result != CargoTransferResult.UnauthorizedDestination;
        }
    }
}
