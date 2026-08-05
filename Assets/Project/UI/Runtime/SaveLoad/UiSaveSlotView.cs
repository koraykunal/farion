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
            Focused?.Invoke(this);
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
                theme = root != null ? root.Theme : null;
            }

            if (theme == null)
            {
                return;
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
            Color normalSurface = theme != null
                ? theme.ButtonSurface
                : new Color(0.024f, 0.037f, 0.052f, 0.82f);
            Color focusedSurface = theme != null
                ? theme.ButtonSurfaceHighlighted
                : new Color(0.055f, 0.086f, 0.118f, 0.96f);
            Color primary = theme != null
                ? theme.PrimaryText
                : new Color(0.88f, 0.91f, 0.93f, 1f);
            Color secondary = theme != null
                ? theme.SecondaryText
                : new Color(0.54f, 0.6f, 0.65f, 0.9f);
            Color supporting = theme != null
                ? theme.SupportingText
                : new Color(0.68f, 0.76f, 0.81f, 1f);
            Color focus = theme != null
                ? theme.Focus
                : new Color(0.56f, 0.68f, 0.76f, 1f);

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
                focus.a *= focused
                    ? 0.94f
                    : current
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
            Color secondary = theme != null
                ? theme.SecondaryText
                : new Color(0.54f, 0.6f, 0.65f, 0.9f);
            Color supporting = theme != null
                ? theme.SupportingText
                : new Color(0.68f, 0.76f, 0.81f, 1f);

            return Summary.State switch
            {
                SaveGameSlotState.Unsupported => theme != null
                    ? theme.Caution
                    : new Color(1f, 0.72f, 0.24f, 0.98f),
                SaveGameSlotState.Invalid => theme != null
                    ? theme.Critical
                    : new Color(1f, 0.26f, 0.2f, 1f),
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
