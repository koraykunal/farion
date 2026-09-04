using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PilotSeatInteractable : MonoBehaviour, IInteractable
    {
        IInteractable host;

        public string InteractionPrompt => InteractionPromptKeys.PilotSeat;
        public bool IsBound => host != null;

        void Awake()
        {
            InteractableLayerBinding.Apply(this);
        }

        public void Bind(IInteractable interactable)
        {
            host = interactable;
        }

        public bool CanInteract(InteractionContext context)
        {
            return host != null && host.CanInteract(context);
        }

        public void Interact(InteractionContext context)
        {
            host?.Interact(context);
        }
    }
}
