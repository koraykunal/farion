using Farion.Gameplay.Commands;
using Farion.Gameplay.Ships;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ShuttleCargoHatchInteractable : MonoBehaviour, IInteractable
    {

        [SerializeField] ShuttleCargoInventory cargo;

        public string InteractionPrompt => InteractionPromptKeys.LoadCargo;
        public bool HasValidAuthoring => cargo != null;

        void Awake()
        {
            InteractableLayerBinding.Apply(this);
        }

        void Reset()
        {
            ResolveCargo();
        }

        void OnValidate()
        {
            ResolveCargo();
        }

        public bool CanInteract(InteractionContext context)
        {
            return context.Commands != null &&
                   cargo != null &&
                   CanAttempt(
                       context.Commands.CanLoadAssignedShuttleCargo(BuildRequest()));
        }

        public void Interact(InteractionContext context)
        {
            if (cargo != null)
            {
                context.Commands?.TryLoadAssignedShuttleCargo(BuildRequest());
            }
        }

        CargoTransferRequest BuildRequest()
        {
            return new CargoTransferRequest(cargo.ContainerId, cargo.Revision);
        }

        void ResolveCargo()
        {
            cargo ??= GetComponentInParent<ShuttleCargoInventory>();
        }

        static bool CanAttempt(CargoTransferResult result)
        {
            return result != CargoTransferResult.MissingSource &&
                   result != CargoTransferResult.MissingDestination &&
                   result != CargoTransferResult.UnauthorizedSource &&
                   result != CargoTransferResult.UnauthorizedDestination;
        }
    }
}
