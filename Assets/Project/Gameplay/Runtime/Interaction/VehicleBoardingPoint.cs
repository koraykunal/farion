using System;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class VehicleBoardingPoint : MonoBehaviour, IInteractable
    {
        const string DefaultPrompt = "Enter ship";
        const string LegacyCockpitPrompt = "Enter cockpit";

        [Header("Exit Pose")]
        [SerializeField] Transform exitPoint;

        [Header("Boarding")]
        [SerializeField] PlayerPossessionController possessionController;
        [SerializeField] string prompt = DefaultPrompt;

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public bool HasExplicitExitPoint => exitPoint != null;
        public string InteractionPrompt => ResolvePrompt();
        public bool IsBound => possessionController != null;

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
            possessionController = controller;
        }

        public bool CanInteract(InteractionContext context)
        {
            return possessionController != null &&
                   possessionController.IsOnFoot &&
                   possessionController.CanEnterShipInterior;
        }

        public void Interact(InteractionContext context)
        {
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
