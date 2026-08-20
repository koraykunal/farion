using Farion.UI.Common;
using Farion.UI.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace Farion.UI.Navigation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiScreenView : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] UiScreenId screenId;
        [SerializeField] UiScreenLayer layer = UiScreenLayer.Screen;

        [Header("Navigation")]
        [SerializeField] Selectable firstSelection;
        [SerializeField] bool closeOnCancel = true;
        [SerializeField] bool locksGameplayInput = true;
        [SerializeField] bool visibleOnAwake;
        [SerializeField] bool blocksRaycastsWhenVisible = true;

        [Header("Presentation")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] UiPanelFader panelFader;

        public UiScreenId ScreenId => screenId;
        public UiScreenLayer Layer => layer;
        public bool CloseOnCancel => closeOnCancel;
        public bool LocksGameplayInput => locksGameplayInput;
        public bool VisibleOnAwake => visibleOnAwake;
        public bool BlocksRaycastsWhenVisible => blocksRaycastsWhenVisible;
        public Selectable FirstSelection => firstSelection;
        public bool HasExplicitFirstSelection => firstSelection != null;
        public bool IsVisible { get; private set; }

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        public void Configure(
            UiScreenId id,
            UiScreenLayer screenLayer,
            Selectable initialSelection,
            bool canCloseOnCancel,
            bool shouldLockGameplayInput,
            bool shouldBeVisibleOnAwake,
            bool shouldBlockRaycastsWhenVisible = true)
        {
            screenId = id;
            layer = screenLayer;
            firstSelection = initialSelection;
            closeOnCancel = canCloseOnCancel;
            locksGameplayInput = shouldLockGameplayInput;
            visibleOnAwake = shouldBeVisibleOnAwake;
            blocksRaycastsWhenVisible = shouldBlockRaycastsWhenVisible;
        }

        public void SetVisible(bool visible, bool animated)
        {
            ResolveReferences();
            IsVisible = visible;

            if (panelFader != null)
            {
                panelFader.SetVisible(visible, animated);
                return;
            }

            gameObject.SetActive(visible);
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible && blocksRaycastsWhenVisible;
            canvasGroup.blocksRaycasts = visible && blocksRaycastsWhenVisible;
        }

        public void SetFirstSelection(Selectable selection)
        {
            firstSelection = selection;
        }

        public Selectable ResolveFirstSelection()
        {
            if (IsConfiguredSelectable(firstSelection))
            {
                return firstSelection;
            }

            Selectable[] candidates = GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsSelectable(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return null;
        }

        void ResolveReferences()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            panelFader ??= GetComponent<UiPanelFader>();
        }

        static bool IsSelectable(Selectable candidate)
        {
            return candidate != null &&
                   candidate.gameObject.activeInHierarchy &&
                   candidate.IsActive() &&
                   candidate.IsInteractable();
        }

        static bool IsConfiguredSelectable(Selectable candidate)
        {
            return candidate != null &&
                   candidate.enabled &&
                   candidate.interactable;
        }
    }
}
