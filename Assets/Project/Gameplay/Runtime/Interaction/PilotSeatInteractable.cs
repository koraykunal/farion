using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PilotSeatInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] PlayerPossessionController possessionController;
        [SerializeField] string prompt = "Pilot seat";

        public string InteractionPrompt => prompt;

        void Reset()
        {
            possessionController = GetComponentInParent<PlayerPossessionController>();
        }

        void Awake()
        {
            if (possessionController == null)
            {
                Debug.LogError($"{nameof(PilotSeatInteractable)} on {name} requires an explicit {nameof(PlayerPossessionController)} reference.", this);
            }
        }

        public bool CanInteract(InteractionContext context)
        {
            return possessionController != null && !possessionController.IsPilotingSpacecraft;
        }

        public void Interact(InteractionContext context)
        {
            possessionController?.EnterSpacecraft();
        }
    }
}
