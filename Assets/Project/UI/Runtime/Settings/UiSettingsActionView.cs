using Farion.UI.Common;
using Farion.UI.Foundation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Settings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UiSettingsActionView :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text labelText;
        [SerializeField] UiTheme theme;
        [SerializeField] Image background;
        [SerializeField] Image selectionFrame;
        [SerializeField] bool destructive;

        UiPointerFocusState focusState;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        void Awake()
        {
            ResolveReferences();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
            }

            if (Application.isPlaying)
            {
                RefreshVisual();
            }
        }

        void OnEnable()
        {
            ResolveReferences();
            if (Application.isPlaying)
            {
                RefreshVisual();
            }
        }

        void OnDisable()
        {
            focusState.Reset();
        }

        public void SetAvailable(bool available)
        {
            ResolveReferences();
            if (button != null)
            {
                button.interactable = available;
            }

            RefreshVisual();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            focusState.PointerEnter();
            RefreshVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            focusState.PointerExit();
            RefreshVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            focusState.PointerClick();
            RefreshVisual();
        }

        public void OnSelect(BaseEventData eventData)
        {
            focusState.Select();
            RefreshVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            focusState.Deselect();
            RefreshVisual();
        }

        void ResolveReferences()
        {
            button ??= GetComponent<Button>();
            background ??= GetComponent<Image>();

            if (theme == null)
            {
                UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
                theme = UiTheme.Resolve(root != null ? root.Theme : null);
            }

            if (labelText != null && Theme.InterfaceMediumFont != null)
            {
                labelText.font = theme.InterfaceMediumFont;
                labelText.fontWeight = FontWeight.Medium;
            }
        }

        void RefreshVisual()
        {
            bool available = button == null || button.interactable;
            bool focused = available && focusState.IsFocused;

            Color normalSurface = Theme.ButtonSurface;
            Color raisedSurface = Theme.ButtonSurfaceHighlighted;
            Color primary = Theme.PrimaryText;
            Color secondary = Theme.SecondaryText;
            Color focus = Theme.Focus;
            Color critical = Theme.Critical;

            if (!available)
            {
                normalSurface.a *= 0.42f;
                secondary.a *= 0.3f;
            }

            if (background != null)
            {
                background.color = focused ? raisedSurface : normalSurface;
                background.raycastTarget = true;
            }

            if (labelText != null)
            {
                labelText.color = focused ? primary : secondary;
            }

            if (selectionFrame != null)
            {
                Color signal = destructive ? critical : focus;
                signal.a *= focused ? 0.9f : available ? 0.16f : 0.05f;
                selectionFrame.color = signal;
                selectionFrame.raycastTarget = false;
            }
        }
    }
}
