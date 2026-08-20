using System;
using Farion.Core.Persistence;
using Farion.UI.Common;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.SaveLoad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class UiSaveSlotView :
        Selectable,
        ISubmitHandler,
        IPointerClickHandler
    {
        [Header("Content")]
        [SerializeField] TMP_Text indexText;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text timestampText;
        [SerializeField] TMP_Text stateText;

        [Header("Visuals")]
        [SerializeField] UiTheme theme;
        [SerializeField] Image background;
        [SerializeField] Image selectionFrame;
        [SerializeField] Image stateMarker;

        UiPointerFocusState focusState;
        bool current;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        public event Action<UiSaveSlotView> Focused;
        public event Action<UiSaveSlotView> Submitted;
        public SaveGameSlotSummary Summary { get; private set; }
        public int DisplayIndex { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            transition = Transition.None;
            if (Application.isPlaying)
            {
                RefreshVisual();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveReferences();
            if (Application.isPlaying)
            {
                RefreshVisual();
            }
        }

        protected override void OnDisable()
        {
            focusState.Reset();
            current = false;
            base.OnDisable();
        }

        public void Configure(
            SaveGameSlotSummary summary,
            int displayIndex,
            string title,
            string timestamp,
            string state)
        {
            Summary = summary;
            DisplayIndex = displayIndex;
            SetText(indexText, displayIndex.ToString("00"));
            SetText(titleText, title);
            SetText(timestampText, timestamp);
            SetText(stateText, state);
            interactable = true;
            RefreshVisual();
        }

        public void SetCurrent(bool value)
        {
            current = value;
            RefreshVisual();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            Submitted?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            focusState.PointerClick();
            RefreshVisual();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            focusState.PointerEnter();
            RefreshVisual();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            focusState.PointerExit();
            RefreshVisual();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            focusState.Select();
            RefreshVisual();
            Focused?.Invoke(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            focusState.Deselect();
            RefreshVisual();
        }

        void ResolveReferences()
        {
            background ??= GetComponent<Image>();
            targetGraphic = background;

            if (theme == null)
            {
                UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
                theme = UiTheme.Resolve(root != null ? root.Theme : null);
            }


            SetFont(indexText, theme.InstrumentFont, FontWeight.Medium);
            SetFont(titleText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(timestampText, theme.InstrumentFont, FontWeight.Medium);
            SetFont(stateText, theme.InstrumentFont, FontWeight.Medium);
        }

        void RefreshVisual()
        {
            bool focused = IsInteractable() && focusState.IsFocused;
            bool emphasized = IsInteractable() && (current || focused);
            Color normalSurface = Theme.ButtonSurface;
            Color focusedSurface = Theme.ButtonSurfaceHighlighted;
            Color primary = Theme.PrimaryText;
            Color secondary = Theme.SecondaryText;
            Color supporting = Theme.SupportingText;
            Color focus = Theme.Focus;

            if (background != null)
            {
                background.color = emphasized ? focusedSurface : normalSurface;
                background.raycastTarget = true;
            }

            if (indexText != null)
            {
                indexText.color = emphasized
                    ? focus
                    : secondary;
            }

            if (titleText != null)
            {
                titleText.color = emphasized
                    ? primary
                    : Summary.HasData
                        ? new Color(primary.r, primary.g, primary.b, 0.82f)
                        : secondary;
            }

            if (timestampText != null)
            {
                timestampText.color = emphasized ? supporting : secondary;
            }

            if (stateText != null)
            {
                stateText.color = ResolveStateColor(emphasized);
            }

            if (stateMarker != null)
            {
                Color markerColor = ResolveStateColor(emphasized);
                markerColor.a = Summary.HasData ? 0.9f : 0.24f;
                stateMarker.color = markerColor;
                stateMarker.raycastTarget = false;
            }

            if (selectionFrame != null)
            {
                focus.a *= current
                    ? 0.94f
                    : focused
                        ? 0.58f
                        : Summary.HasData
                            ? 0.15f
                            : 0.08f;
                selectionFrame.color = focus;
                selectionFrame.raycastTarget = false;
            }
        }

        Color ResolveStateColor(bool focused)
        {
            Color secondary = Theme.SecondaryText;
            Color supporting = Theme.SupportingText;

            return Summary.State switch
            {
                SaveGameSlotState.Unsupported => Theme.Caution,
                SaveGameSlotState.Invalid => Theme.Critical,
                _ => focused ? supporting : secondary
            };
        }

        static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        static void SetFont(TMP_Text target, TMP_FontAsset font, FontWeight weight)
        {
            if (target != null && font != null)
            {
                target.font = font;
                target.fontWeight = weight;
            }
        }
    }
}
