using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class VehicleBoardingPoint : MonoBehaviour, IInteractable
    {
        [Header("Exit Pose")]
        [SerializeField] Transform exitPoint;

        IInteractable host;

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public bool HasExplicitExitPoint => exitPoint != null;
        public string InteractionPrompt =>
            host?.InteractionPrompt ?? InteractionPromptKeys.EnterShip;
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
