using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ResearchTerminalInteractable :
        MonoBehaviour,
        IInteractable
    {
        [SerializeField] string prompt = "Open Research Terminal";
        [SerializeField] ResearchTerminalRuntime terminal;
        [SerializeField] InventoryContainerComponent inventory;
        [SerializeField] ResearchTerminalPanelPresenter presenter;

        public string InteractionPrompt => string.IsNullOrWhiteSpace(prompt)
            ? TerminalUiText.Get(
                "interaction.open_research",
                "Open Research Terminal")
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
            return terminal != null && presenter != null;
        }

        public void Interact(InteractionContext context)
        {
            ResolveReferences();
            presenter?.Open(terminal, inventory);
        }

        void ResolveReferences()
        {
            terminal ??= GetComponent<ResearchTerminalRuntime>();
            terminal ??= GetComponentInParent<ResearchTerminalRuntime>();
            inventory ??= GetComponent<InventoryContainerComponent>();
            inventory ??= GetComponentInParent<InventoryContainerComponent>();
        }
    }
}
