using Farion.Gameplay.Crafting;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CraftingStationTerminalInteractable :
        MonoBehaviour,
        IInteractable
    {
        [SerializeField] string prompt = "Open Fabricator";
        [SerializeField] CraftingStationRuntime station;
        [SerializeField] InventoryContainerComponent inventory;
        [SerializeField] CraftingStationPanelPresenter presenter;

        public string InteractionPrompt => string.IsNullOrWhiteSpace(prompt)
            ? TerminalUiText.Get(
                "interaction.open_crafting",
                "Open Crafting Terminal")
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
            return station != null && presenter != null;
        }

        public void Interact(InteractionContext context)
        {
            ResolveReferences();
            presenter?.Open(station, inventory);
        }

        void ResolveReferences()
        {
            station ??= GetComponent<CraftingStationRuntime>();
            station ??= GetComponentInParent<CraftingStationRuntime>();
            inventory ??= GetComponent<InventoryContainerComponent>();
            inventory ??= GetComponentInParent<InventoryContainerComponent>();
        }
    }
}
