using System;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class VehicleBoardingPoint : MonoBehaviour, IInteractable
    {

        void Awake()
        {
            InteractableLayerBinding.Apply(this);
        }
        const string DefaultPrompt = "Enter ship";
        const string LegacyCockpitPrompt = "Enter cockpit";

        [Header("Exit Pose")]
        [SerializeField] Transform exitPoint;

        [Header("Boarding")]
        [SerializeField] PlayerPossessionController possessionController;
        [SerializeField] string prompt = DefaultPrompt;

        IInteractable runtimeInteractable;

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public bool HasExplicitExitPoint => exitPoint != null;
        public string InteractionPrompt => runtimeInteractable?.InteractionPrompt ?? ResolvePrompt();
        public bool IsBound => runtimeInteractable != null || possessionController != null;

        void Reset()
        {
            possessionController = GetComponentInParent<PlayerPossessionController>();
            prompt = DefaultPrompt;
        }

        void OnValidate()
        {
            prompt = ResolvePrompt();
        }

        public void Bind(PlayerPossessionController controller)
        {
            runtimeInteractable = null;
            possessionController = controller;
        }

        public void Bind(IInteractable interactable)
        {
            possessionController = null;
            runtimeInteractable = interactable;
        }

        public bool CanInteract(InteractionContext context)
        {
            return runtimeInteractable?.CanInteract(context) ??
                   possessionController != null &&
                   possessionController.IsOnFoot &&
                   possessionController.CanEnterShipInterior;
        }

        public void Interact(InteractionContext context)
        {
            if (runtimeInteractable != null)
            {
                runtimeInteractable.Interact(context);
                return;
            }

            possessionController?.EnterShipInterior();
        }

        string ResolvePrompt()
        {
            return string.IsNullOrWhiteSpace(prompt) ||
                   string.Equals(
                       prompt.Trim(),
                       LegacyCockpitPrompt,
                       StringComparison.OrdinalIgnoreCase)
                ? DefaultPrompt
                : prompt.Trim();
        }
    }
}
