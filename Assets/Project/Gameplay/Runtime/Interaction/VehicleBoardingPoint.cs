using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class VehicleBoardingPoint : MonoBehaviour, IInteractable
    {
        [Header("Exit Pose")]
        [SerializeField] Transform exitPoint;

        [Header("Boarding")]
        [SerializeField] PlayerPossessionController possessionController;
        [SerializeField] string prompt = "Enter cockpit";

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public bool HasExplicitExitPoint => exitPoint != null;
        public string InteractionPrompt => prompt;
        public bool IsBound => possessionController != null;

        void Reset()
        {
            possessionController = GetComponentInParent<PlayerPossessionController>();
        }

        public void Bind(PlayerPossessionController controller)
        {
            possessionController = controller;
        }

        public bool CanInteract(InteractionContext context)
        {
            return possessionController != null && possessionController.CanEnterSpacecraft;
        }

        public void Interact(InteractionContext context)
        {
            possessionController?.EnterPilotSeat();
        }

    }
}
