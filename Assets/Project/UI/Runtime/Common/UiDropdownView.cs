using System;
using System.Collections.Generic;
using DG.Tweening;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using Farion.UI.Styling;
using TMPro;
using UiNavigation = UnityEngine.UI.Navigation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Common
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class UiDropdownView :
        Selectable,
        IUiValueControl,
        IUiCancelConsumer,
        ISubmitHandler,
        ICancelHandler,
        IPointerClickHandler
    {
        [Header("Content")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text valueText;

        [Header("Visuals")]
        [SerializeField] UiTheme theme;
        [SerializeField] Image background;
        [SerializeField] Image selectionFrame;
        [SerializeField] RectTransform caret;
        [SerializeField] Image caretGraphic;

        [Header("Popup")]
        [SerializeField] RectTransform popupRoot;
        [SerializeField] CanvasGroup popupGroup;
        [SerializeField] ScrollRect popupScroll;
        [SerializeField] RectTransform popupViewport;
        [SerializeField] RectTransform popupContent;
        [SerializeField] Image popupBackground;
        [SerializeField] Image popupFrame;
        [SerializeField] UiDropdownItemView itemPrefab;

        [Header("Layout")]
        [SerializeField] float itemHeight = 38f;
        [SerializeField] int maxVisibleItems = 7;
        [SerializeField] float popupPadding = 6f;
        [SerializeField] float popupGap = 4f;

        readonly List<UiDropdownItemView> items = new();
        readonly List<string> options = new();
        readonly Vector3[] rowCorners = new Vector3[4];
        UiPointerFocusState focusState;
        UiScreenRouter router;
        RectTransform overlayLayer;
        Transform popupHome;
        int popupHomeSiblingIndex;
        Button overlayBlocker;
        string displayTitle = string.Empty;
        string displayDescription = string.Empty;
        bool pending;
        bool expanded;
        int selectedIndex;
        float viewportHeight;
        Sequence popupTween;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        public event Action<IUiValueControl> Focused;
        public event Action<UiDropdownView, int> SelectionChanged;

        public string DisplayTitle => displayTitle;
        public string DisplayDescription => displayDescription;
        public string DisplayValue =>
            selectedIndex >= 0 && selectedIndex < options.Count
                ? options[selectedIndex]
                : string.Empty;
        public Selectable Selectable => this;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            transition = Transition.None;
            ApplyExpandedState(false, immediate: true);
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
            Collapse(immediate: true);
            focusState.Reset();
            base.OnDisable();
        }

        void LateUpdate()
        {
            if (!expanded || !Application.isPlaying)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null
                ? eventSystem.currentSelectedGameObject
                : null;
            bool insideDropdown = selected != null &&
                (selected.transform.IsChildOf(transform) ||
                 (popupRoot != null && selected.transform.IsChildOf(popupRoot)));
            if (!insideDropdown)
            {
                Collapse(immediate: false);
                return;
            }

            LayoutPopup();
        }

        public void ConfigureContent(string title, string description)
        {
            displayTitle = title ?? string.Empty;
            displayDescription = description ?? string.Empty;
            if (titleText != null)
            {
                titleText.text = UiLocalization.ToDisplayUpper(displayTitle);
            }
        }

        public void SetOptions(IReadOnlyList<string> values, int index)
        {
            options.Clear();
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    options.Add(values[i] ?? string.Empty);
                }
            }

            selectedIndex = options.Count > 0
                ? Mathf.Clamp(index, 0, options.Count - 1)
                : 0;
            if (expanded)
            {
                BuildItems();
            }

            RefreshValueText();
            RefreshVisual();
        }

        public void SetSelectedIndex(int index)
        {
            selectedIndex = options.Count > 0
                ? Mathf.Clamp(index, 0, options.Count - 1)
                : 0;
            RefreshValueText();
            RefreshVisual();
        }

        public void SetAvailable(bool available)
        {
            interactable = available;
            if (!available)
            {
                Collapse(immediate: true);
            }

            RefreshVisual();
        }

        public void SetPending(bool value)
        {
            if (pending == value)
            {
                return;
            }

            pending = value;
            RefreshVisual();
        }

        public bool TryConsumeCancel()
        {
            if (!expanded)
            {
                return false;
            }

            Collapse(immediate: false);
            return true;
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (expanded)
            {
                return;
            }

            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    Step(-1);
                    eventData.Use();
                    return;
                case MoveDirection.Right:
                    Step(1);
                    eventData.Use();
                    return;
                default:
                    base.OnMove(eventData);
                    break;
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Toggle();
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (expanded)
            {
                Collapse(immediate: false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            Select();
            Toggle();
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

        void Toggle()
        {
            if (!IsActive() || !IsInteractable() || options.Count == 0)
            {
                return;
            }

            if (expanded)
            {
                Collapse(immediate: false);
                return;
            }

            Expand();
        }

        void Step(int direction)
        {
            if (!IsActive() || !IsInteractable() || options.Count < 2)
            {
                return;
            }

            int delta = direction < 0 ? -1 : 1;
            Commit((selectedIndex + delta + options.Count) % options.Count);
        }

        void Commit(int index)
        {
            int normalized = Mathf.Clamp(index, 0, options.Count - 1);
            if (normalized == selectedIndex)
            {
                return;
            }

            selectedIndex = normalized;
            RefreshValueText();
            RefreshVisual();
            SelectionChanged?.Invoke(this, selectedIndex);
        }

        void Expand()
        {
            if (popupRoot == null || itemPrefab == null)
            {
                return;
            }

            expanded = true;
            MoveToOverlay();
            BuildItems();
            LayoutPopup();
            ApplyExpandedState(true, IsReducedMotionEnabled());
            ResolveRouter()?.RegisterCancelConsumer(this);
            RefreshVisual();

            if (popupScroll != null)
            {
                popupScroll.verticalNormalizedPosition = 1f;
            }

            UiDropdownItemView current = ResolveItem(selectedIndex);
            if (current != null)
            {
                current.Select();
                ScrollTo(selectedIndex);
            }
        }

        void Collapse(bool immediate)
        {
            if (!expanded)
            {
                return;
            }

            expanded = false;
            ResolveRouter()?.UnregisterCancelConsumer(this);
            ReleaseBlocker();
            ApplyExpandedState(false, immediate || IsReducedMotionEnabled());
            RefreshVisual();

            if (IsActive() && IsInteractable() && gameObject.activeInHierarchy)
            {
                Select();
            }
        }

        void BuildItems()
        {
            if (popupContent == null || itemPrefab == null)
            {
                return;
            }

            for (int i = items.Count; i < options.Count; i++)
            {
                UiDropdownItemView item = Instantiate(itemPrefab, popupContent);
                item.name = itemPrefab.name + "_" + i;
                item.Chosen += HandleItemChosen;
                item.Highlighted += HandleItemHighlighted;
                items.Add(item);
            }

            for (int i = 0; i < items.Count; i++)
            {
                UiDropdownItemView item = items[i];
                if (item == null)
                {
                    continue;
                }

                bool used = i < options.Count;
                item.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                RectTransform itemRect = (RectTransform)item.transform;
                itemRect.anchorMin = new Vector2(0f, 1f);
                itemRect.anchorMax = new Vector2(1f, 1f);
                itemRect.pivot = new Vector2(0.5f, 1f);
                itemRect.sizeDelta = new Vector2(0f, itemHeight);
                itemRect.anchoredPosition = new Vector2(0f, -i * itemHeight);
                item.Configure(i, options[i], i == selectedIndex);
            }

            ConfigureItemNavigation();
            popupContent.sizeDelta = new Vector2(0f, options.Count * itemHeight);
        }

        void ConfigureItemNavigation()
        {
            int last = Mathf.Min(options.Count, items.Count) - 1;
            for (int i = 0; i <= last; i++)
            {
                UiDropdownItemView item = items[i];
                if (item == null)
                {
                    continue;
                }

                item.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = i == 0 ? items[last] : items[i - 1],
                    selectOnDown = i == last ? items[0] : items[i + 1]
                };
            }
        }

        void LayoutPopup()
        {
            RectTransform surface = overlayLayer != null
                ? overlayLayer
                : ResolveCanvasRect();
            int desired = Mathf.Clamp(
                options.Count,
                1,
                Mathf.Max(1, maxVisibleItems));

            if (surface == null)
            {
                ApplyPopupSize(desired * itemHeight);
                return;
            }

            ((RectTransform)transform).GetWorldCorners(rowCorners);
            Vector2 rowBottomLeft = surface.InverseTransformPoint(rowCorners[0]);
            Vector2 rowTopLeft = surface.InverseTransformPoint(rowCorners[1]);
            Vector2 rowTopRight = surface.InverseTransformPoint(rowCorners[2]);
            float width = Mathf.Abs(rowTopRight.x - rowTopLeft.x);
            Rect bounds = surface.rect;

            float roomBelow = rowBottomLeft.y - bounds.yMin - popupGap;
            float roomAbove = bounds.yMax - rowTopLeft.y - popupGap;
            float desiredHeight = desired * itemHeight + popupPadding * 2f;
            bool openDown = roomBelow >= desiredHeight || roomBelow >= roomAbove;
            float room = openDown ? roomBelow : roomAbove;
            int fits = Mathf.FloorToInt((room - popupPadding * 2f) / itemHeight);
            int visible = Mathf.Clamp(fits, 1, desired);

            float height = ApplyPopupSize(visible * itemHeight);
            popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
            popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
            popupRoot.pivot = new Vector2(0f, 1f);
            popupRoot.sizeDelta = new Vector2(width, height);
            popupRoot.anchoredPosition = new Vector2(
                Mathf.Clamp(
                    rowTopLeft.x,
                    bounds.xMin,
                    Mathf.Max(bounds.xMin, bounds.xMax - width)),
                Mathf.Clamp(
                    openDown
                        ? rowBottomLeft.y - popupGap
                        : rowTopLeft.y + popupGap + height,
                    Mathf.Min(bounds.yMin + height, bounds.yMax),
                    bounds.yMax));
        }

        float ApplyPopupSize(float viewHeight)
        {
            viewportHeight = viewHeight;
            float height = viewHeight + popupPadding * 2f;

            if (popupRoot != null)
            {
                popupRoot.sizeDelta = new Vector2(popupRoot.sizeDelta.x, height);
            }

            if (popupViewport != null)
            {
                popupViewport.anchorMin = new Vector2(0f, 1f);
                popupViewport.anchorMax = new Vector2(1f, 1f);
                popupViewport.pivot = new Vector2(0.5f, 1f);
                popupViewport.anchoredPosition = new Vector2(0f, -popupPadding);
                popupViewport.sizeDelta = new Vector2(
                    -popupPadding * 2f,
                    viewHeight);
            }

            if (popupContent != null)
            {
                popupContent.anchorMin = new Vector2(0f, 1f);
                popupContent.anchorMax = new Vector2(1f, 1f);
                popupContent.pivot = new Vector2(0.5f, 1f);
                popupContent.sizeDelta = new Vector2(
                    0f,
                    options.Count * itemHeight);
            }

            return height;
        }

        void MoveToOverlay()
        {
            overlayLayer = UiOverlayLayer.Resolve(this);
            if (overlayLayer == null || popupRoot.parent == overlayLayer)
            {
                return;
            }

            popupHome = popupRoot.parent;
            popupHomeSiblingIndex = popupRoot.GetSiblingIndex();
            popupRoot.SetParent(overlayLayer, false);
            popupRoot.localScale = Vector3.one;

            overlayBlocker = UiOverlayLayer.ResolveBlocker(overlayLayer);
            if (overlayBlocker != null)
            {
                overlayBlocker.onClick.RemoveAllListeners();
                overlayBlocker.onClick.AddListener(CollapseFromBlocker);
                overlayBlocker.gameObject.SetActive(true);
                overlayBlocker.transform.SetAsFirstSibling();
            }

            popupRoot.SetAsLastSibling();
        }

        void ReleaseBlocker()
        {
            if (overlayBlocker == null)
            {
                return;
            }

            overlayBlocker.onClick.RemoveListener(CollapseFromBlocker);
            overlayBlocker.gameObject.SetActive(false);
            overlayBlocker = null;
        }

        void RestoreFromOverlay()
        {
            ReleaseBlocker();
            if (popupHome == null || popupRoot == null)
            {
                return;
            }

            popupRoot.SetParent(popupHome, false);
            popupRoot.SetSiblingIndex(popupHomeSiblingIndex);
            popupHome = null;
            overlayLayer = null;
        }

        void CollapseFromBlocker()
        {
            Collapse(immediate: false);
        }

        RectTransform ResolveCanvasRect()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            return root != null ? root.transform as RectTransform : null;
        }

        void ScrollTo(int index)
        {
            if (popupScroll == null || popupContent == null || popupViewport == null)
            {
                return;
            }

            float contentHeight = options.Count * itemHeight;
            float viewHeight = viewportHeight;
            float scrollable = contentHeight - viewHeight;
            if (scrollable <= 0f)
            {
                popupScroll.verticalNormalizedPosition = 1f;
                return;
            }

            float itemTop = index * itemHeight;
            float current = (1f - popupScroll.verticalNormalizedPosition) * scrollable;
            float offset = Mathf.Clamp(
                current,
                itemTop + itemHeight - viewHeight,
                itemTop);
            popupScroll.verticalNormalizedPosition =
                1f - Mathf.Clamp01(offset / scrollable);
        }

        void HandleItemChosen(UiDropdownItemView item)
        {
            if (item == null)
            {
                return;
            }

            Commit(item.Index);
            Collapse(immediate: false);
        }

        void HandleItemHighlighted(UiDropdownItemView item)
        {
            if (item != null && expanded)
            {
                ScrollTo(item.Index);
            }
        }

        UiDropdownItemView ResolveItem(int index)
        {
            return index >= 0 && index < items.Count ? items[index] : null;
        }

        void ApplyExpandedState(bool visible, bool immediate)
        {
            popupTween?.Kill();
            popupTween = null;

            if (popupRoot == null)
            {
                return;
            }

            float caretAngle = visible ? 180f : 0f;

            if (immediate || !Application.isPlaying)
            {
                if (!visible)
                {
                    RestoreFromOverlay();
                }

                popupRoot.gameObject.SetActive(visible);
                if (popupGroup != null)
                {
                    popupGroup.alpha = visible ? 1f : 0f;
                    popupGroup.interactable = visible;
                    popupGroup.blocksRaycasts = visible;
                }

                if (caret != null)
                {
                    caret.localEulerAngles = new Vector3(0f, 0f, caretAngle);
                }

                return;
            }

            popupRoot.gameObject.SetActive(true);
            if (popupGroup != null)
            {
                popupGroup.interactable = visible;
                popupGroup.blocksRaycasts = visible;
            }

            float duration = visible
                ? Theme.StateEnterDuration
                : Theme.StateExitDuration;
            popupTween = DOTween.Sequence().SetUpdate(true).SetTarget(this);

            if (popupGroup != null)
            {
                popupTween.Join(
                    DOTween.To(
                            () => popupGroup.alpha,
                            value => popupGroup.alpha = value,
                            visible ? 1f : 0f,
                            duration)
                        .SetEase(visible ? Ease.OutQuart : Ease.OutCubic));
            }

            if (caret != null)
            {
                popupTween.Join(
                    DOTween.To(
                            () => caret.localEulerAngles.z,
                            value => caret.localEulerAngles = new Vector3(0f, 0f, value),
                            caretAngle,
                            duration)
                        .SetEase(Ease.OutQuart));
            }

            if (!visible)
            {
                popupTween.OnComplete(() =>
                {
                    RestoreFromOverlay();
                    popupRoot.gameObject.SetActive(false);
                });
            }
        }

        void RefreshValueText()
        {
            if (valueText != null)
            {
                valueText.text = UiLocalization.ToDisplayUpper(DisplayValue);
            }
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

            if (titleText != null && Theme.InterfaceMediumFont != null)
            {
                titleText.font = theme.InterfaceMediumFont;
                titleText.fontWeight = FontWeight.Medium;
            }

            if (valueText != null && Theme.InstrumentFont != null)
            {
                valueText.font = theme.InstrumentFont;
                valueText.fontWeight = FontWeight.Medium;
            }
        }

        UiScreenRouter ResolveRouter()
        {
            if (router != null)
            {
                return router;
            }

            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            router = root != null ? root.ScreenRouter : null;
            return router;
        }

        bool IsReducedMotionEnabled()
        {
            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            return root != null && root.ReducedMotion;
        }

        void RefreshVisual()
        {
            bool available = IsInteractable();
            bool focused = available && (focusState.IsFocused || expanded);

            Color panelNormal = Theme.ButtonSurface;
            Color panelFocused = Theme.ButtonSurfaceHighlighted;
            Color primary = Theme.PrimaryText;
            Color supporting = Theme.SupportingText;
            Color focus = Theme.Focus;

            if (!available)
            {
                panelNormal.a *= 0.42f;
                supporting.a = 0.24f;
            }

            if (background != null)
            {
                background.color = focused ? panelFocused : panelNormal;
                background.raycastTarget = true;
            }

            if (titleText != null)
            {
                titleText.color = focused
                    ? primary
                    : new Color(primary.r, primary.g, primary.b, 0.82f);
            }

            Color valueColor = pending && available
                ? Theme.Caution
                : focused
                    ? focus
                    : supporting;

            if (valueText != null)
            {
                valueText.color = valueColor;
            }

            if (caretGraphic != null)
            {
                caretGraphic.color = valueColor;
                caretGraphic.raycastTarget = false;
            }

            if (popupBackground != null)
            {
                Color surface = Theme.RaisedSurface;
                surface.a = 1f;
                popupBackground.color = surface;
                popupBackground.raycastTarget = true;
            }

            if (popupFrame != null)
            {
                Color frame = Theme.Focus;
                frame.a *= 0.28f;
                popupFrame.color = frame;
                popupFrame.raycastTarget = false;
            }

            if (selectionFrame != null)
            {
                Color signal = focus;
                signal.a *= focused ? 0.9f : available ? 0.12f : 0.05f;
                selectionFrame.color = signal;
                selectionFrame.raycastTarget = false;
            }
        }
    }
}
