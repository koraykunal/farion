using Farion.Gameplay.Commands;
using Farion.Gameplay.Ships;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ShuttleCargoHatchInteractable : MonoBehaviour, IInteractable
    {
        const string DefaultPrompt = "Load cargo";

        [SerializeField] ShuttleCargoInventory cargo;
        [SerializeField] string prompt = DefaultPrompt;

        public string InteractionPrompt =>
            string.IsNullOrWhiteSpace(prompt) ? DefaultPrompt : prompt.Trim();
        public bool HasValidAuthoring => cargo != null;

        void Reset()
        {
            ResolveCargo();
            prompt = DefaultPrompt;
        }

        void OnValidate()
        {
            ResolveCargo();
            prompt = InteractionPrompt;
        }

        public bool CanInteract(InteractionContext context)
        {
            return context.Commands != null &&
                   context.Commands.CanLoadAssignedShuttleCargo(cargo) ==
                   CargoTransferResult.Succeeded;
        }

        public void Interact(InteractionContext context)
        {
            context.Commands?.TryLoadAssignedShuttleCargo(cargo);
        }

        void ResolveCargo()
        {
            cargo ??= GetComponentInParent<ShuttleCargoInventory>();
        }
    }
}
