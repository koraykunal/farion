using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CargoTransferTerminalInteractable :
        MonoBehaviour,
        IInteractable
    {
        [SerializeField] string prompt = "Open Cargo Transfer";
        [SerializeField] InventoryContainerComponent sourceInventory;
        [SerializeField] InventoryContainerComponent destinationInventory;
        [SerializeField] CargoTransferPanelPresenter presenter;

        public string InteractionPrompt => string.IsNullOrWhiteSpace(prompt)
            ? TerminalUiText.Get(
                "interaction.open_cargo",
                "Open Cargo Transfer")
            : prompt.Trim();

        void Reset()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        public bool CanInteract(InteractionContext context)
        {
            ResolveReferences();
            return presenter != null;
        }

        public void Interact(InteractionContext context)
        {
            ResolveReferences();
            presenter?.Open(sourceInventory, destinationInventory);
        }

        void ResolveReferences()
        {
            sourceInventory ??= GetComponent<InventoryContainerComponent>();
            sourceInventory ??= GetComponentInParent<InventoryContainerComponent>();
        }
    }
}
