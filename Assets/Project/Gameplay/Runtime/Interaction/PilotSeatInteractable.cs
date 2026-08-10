using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PilotSeatInteractable : MonoBehaviour, IInteractable
    {

        void Awake()
        {
            InteractableLayerBinding.Apply(this);
        }
        [SerializeField] PlayerPossessionController possessionController;
        [SerializeField] string prompt = "Pilot seat";

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
            return possessionController != null &&
                   possessionController.IsInShipInterior &&
                   possessionController.CanEnterSpacecraft;
        }

        public void Interact(InteractionContext context)
        {
            possessionController?.EnterPilotSeat();
        }
    }
}
