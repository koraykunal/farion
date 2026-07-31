using System.Collections.Generic;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Ships;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class FleetCargoUnloadInteractable : MonoBehaviour, IInteractable
    {
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
                    out ShuttleCargoInventory cargo))
            {
                context.Commands.TryUnloadAssignedShuttleCargo(cargo);
            }
        }

        void ResolveBoundary()
        {
            dockingBoundary ??=
                GetComponentInParent<ShuttleDockingBoundary>();
        }

        bool TryResolveAuthorizedCargo(
            IGameplayCommandGateway commands,
            out ShuttleCargoInventory cargo)
        {
            cargo = null;
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
                if (candidate != null &&
                    commands.CanUnloadAssignedShuttleCargo(candidate) ==
                    CargoTransferResult.Succeeded)
                {
                    cargo = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
