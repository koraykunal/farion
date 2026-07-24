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
        [SerializeField] string prompt = "Enter ship";

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public bool HasExplicitExitPoint => exitPoint != null;
        public string InteractionPrompt => prompt;

        void Reset()
        {
            possessionController = GetComponentInParent<PlayerPossessionController>();
        }

        void Awake()
        {
            if (possessionController == null)
            {
                Debug.LogError($"{nameof(VehicleBoardingPoint)} on {name} requires an explicit {nameof(PlayerPossessionController)} reference.", this);
            }
        }

        public bool CanInteract(InteractionContext context)
        {
            return possessionController != null && possessionController.IsOnFoot;
        }

        public void Interact(InteractionContext context)
        {
            possessionController?.EnterShipInterior();
        }

    }
}
