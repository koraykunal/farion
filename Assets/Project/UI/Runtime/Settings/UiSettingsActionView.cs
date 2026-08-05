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
                theme = root != null ? root.Theme : null;
            }

            if (labelText != null && theme != null && theme.InterfaceMediumFont != null)
            {
                labelText.font = theme.InterfaceMediumFont;
                labelText.fontWeight = FontWeight.Medium;
            }
        }

        void RefreshVisual()
        {
            bool available = button == null || button.interactable;
            bool focused = available && focusState.IsFocused;

            Color normalSurface = theme != null
                ? theme.ButtonSurface
                : new Color(0.024f, 0.037f, 0.052f, 0.72f);
            Color raisedSurface = theme != null
                ? theme.ButtonSurfaceHighlighted
                : new Color(0.055f, 0.086f, 0.118f, 0.92f);
            Color primary = theme != null
                ? theme.PrimaryText
                : new Color(0.88f, 0.91f, 0.93f, 1f);
            Color secondary = theme != null
                ? theme.SecondaryText
                : new Color(0.54f, 0.6f, 0.65f, 0.9f);
            Color focus = theme != null
                ? theme.Focus
                : new Color(0.56f, 0.68f, 0.76f, 1f);
            Color critical = theme != null
                ? theme.Critical
                : new Color(1f, 0.26f, 0.2f, 1f);

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
