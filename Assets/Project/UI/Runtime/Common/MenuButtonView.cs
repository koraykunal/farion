using System;
using DG.Tweening;
using Farion.UI.Foundation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Common
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonView :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [Header("Interaction")]
        [SerializeField] Button button;
        [SerializeField] bool available = true;
        [SerializeField] bool destructive;

        [Header("Content")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] Image iconImage;

        [Header("Visuals")]
        [SerializeField] UiTheme theme;
        [SerializeField] Image panelBackground;
        [SerializeField] Image accentBar;
        [SerializeField] Image selectionFrame;
        [SerializeField] RectTransform contentRoot;

        [Header("Layout")]
        [SerializeField] Vector2 contentOffsetWithIcon = new(76f, 0f);
        [SerializeField] Vector2 contentOffsetWithoutIcon = new(32f, 0f);
        [SerializeField] Vector2 contentSizeWithIcon = new(-96f, 0f);
        [SerializeField] Vector2 contentSizeWithoutIcon = new(-52f, 0f);

        [Header("Colors")]
        [SerializeField] Color normalPanel = new(0.024f, 0.037f, 0.052f, 0.72f);
        [SerializeField] Color highlightedPanel = new(0.055f, 0.086f, 0.118f, 0.92f);
        [SerializeField] Color normalTitle = new(0.54f, 0.6f, 0.65f, 0.9f);
        [SerializeField] Color highlightedTitle = new(0.88f, 0.91f, 0.93f, 1f);
        [SerializeField] Color normalSubtitle = new(0.54f, 0.6f, 0.65f, 0.9f);
        [SerializeField] Color highlightedSubtitle = new(0.68f, 0.76f, 0.81f, 1f);
        [SerializeField] Color accentColor = new(0.56f, 0.68f, 0.76f, 1f);

        [Header("Motion")]
        [SerializeField] Vector2 normalContentOffset;
        [SerializeField] Vector2 highlightedContentOffset;
        [SerializeField, Min(0.01f)] float hoverInDuration = 0.16f;
        [SerializeField, Min(0.01f)] float hoverOutDuration = 0.1f;
        [SerializeField] Ease hoverInEase = Ease.OutQuart;
        [SerializeField] Ease hoverOutEase = Ease.OutCubic;
        [SerializeField] Vector3 normalIconScale = Vector3.one;
        [SerializeField] Vector3 highlightedIconScale = Vector3.one;

        UiPointerFocusState focusState;
        bool capturedContentRootPosition;
        Vector2 contentRootBasePosition;
        float visualAmount;
        bool visualStateInitialized;
        bool currentHighlighted;
        bool currentInteractable;
        Sequence visualTween;

        public event Action Clicked;
        public bool Available => available;
        public Button Button
        {
            get
            {
                ResolveReferences();
                return button;
            }
        }

        void Reset()
        {
            ResolveReferences();
            ApplyTheme();
            ConfigureButtonTransition();
        }

        void Awake()
        {
            ResolveReferences();
            ApplyTheme();
            ConfigureButtonTransition();
            ApplyIconState(iconImage != null ? iconImage.sprite : null);
            RefreshVisualState(immediate: true);
        }

        void OnEnable()
        {
            ResolveReferences();
            ApplyTheme();
            CaptureContentRootPosition();
            if (button != null)
            {
                button.onClick.RemoveListener(InvokeClicked);
                button.onClick.AddListener(InvokeClicked);
            }

            ApplyAvailability();
        }

        void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(InvokeClicked);
            }

            visualTween?.Kill();
            visualTween = null;
            focusState.Reset();
        }

        public void SetAvailable(bool isAvailable)
        {
            available = isAvailable;
            ApplyAvailability();
        }

        public void ConfigureContent(string title, string subtitle, Sprite icon)
        {
            SetTitle(title);

            if (subtitleText != null)
            {
                subtitleText.text = ToMenuLabel(subtitle);
            }

            ApplyIconState(icon);
            RefreshVisualState(immediate: true);
        }

        public void SetTitle(string title)
        {
            if (titleText != null)
            {
                titleText.text = ToMenuLabel(title);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            focusState.PointerEnter();
            RefreshVisualState(immediate: false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            focusState.PointerExit();
            RefreshVisualState(immediate: false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            focusState.PointerClick();
            RefreshVisualState(immediate: false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            focusState.Select();
            RefreshVisualState(immediate: false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            focusState.Deselect();
            RefreshVisualState(immediate: false);
        }

        void InvokeClicked()
        {
            if (!available)
            {
                return;
            }

            Clicked?.Invoke();
        }

        void ResolveReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (theme == null)
            {
                UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
                theme = root != null ? root.Theme : null;
            }
        }

        void ApplyTheme()
        {
            if (theme == null)
            {
                return;
            }

            normalPanel = theme.ButtonSurface;
            highlightedPanel = theme.ButtonSurfaceHighlighted;
            normalTitle = theme.SecondaryText;
            highlightedTitle = theme.PrimaryText;
            normalSubtitle = theme.SecondaryText;
            highlightedSubtitle = theme.SupportingText;
            accentColor = destructive ? theme.Critical : theme.Focus;
            hoverInDuration = theme.StateEnterDuration;
            hoverOutDuration = theme.StateExitDuration;

            if (titleText != null && theme.InterfaceMediumFont != null)
            {
                titleText.font = theme.InterfaceMediumFont;
                titleText.fontWeight = FontWeight.Medium;
            }

            if (subtitleText != null && theme.InterfaceFont != null)
            {
                subtitleText.font = theme.InterfaceFont;
                subtitleText.fontWeight = FontWeight.Regular;
            }
        }

        void ApplyAvailability()
        {
            if (button != null)
            {
                button.interactable = available;
            }

            RefreshVisualState(immediate: true);
        }

        void ConfigureButtonTransition()
        {
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
            }
        }

        void RefreshVisualState(bool immediate)
        {
            CaptureContentRootPosition();
            immediate |= IsReducedMotionEnabled();

            bool isInteractable = available && button != null && button.interactable;
            bool highlighted = isInteractable && focusState.IsFocused;
            float targetHighlight = highlighted ? 1f : 0f;

            if (immediate || !Application.isPlaying || !visualStateInitialized)
            {
                visualTween?.Kill();
                visualTween = null;
                visualAmount = targetHighlight;
                ApplyVisualAmount(visualAmount);

                if (!isInteractable)
                {
                    ApplyDisabledState();
                }

                currentHighlighted = highlighted;
                currentInteractable = isInteractable;
                visualStateInitialized = true;
                return;
            }

            if (currentHighlighted == highlighted && currentInteractable == isInteractable)
            {
                return;
            }

            visualTween?.Kill();

            float duration = highlighted ? hoverInDuration : hoverOutDuration;
            Ease ease = highlighted ? hoverInEase : hoverOutEase;
            visualTween = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this);

            visualTween.Append(
                DOTween.To(
                        () => visualAmount,
                        ApplyVisualAmount,
                        targetHighlight,
                        duration)
                    .SetEase(ease));

            if (!isInteractable)
            {
                visualTween.OnComplete(ApplyDisabledState);
            }

            currentHighlighted = highlighted;
            currentInteractable = isInteractable;
        }

        void ApplyDisabledState()
        {
            if (panelBackground != null)
            {
                panelBackground.color = WithAlpha(normalPanel, normalPanel.a * 0.45f);
            }

            if (titleText != null)
            {
                titleText.color = WithAlpha(normalTitle, 0.35f);
            }

            if (subtitleText != null)
            {
                subtitleText.color = WithAlpha(normalSubtitle, 0.25f);
            }

            if (iconImage != null)
            {
                iconImage.color = WithAlpha(accentColor, 0.3f);
            }

            SetAccentVisible(accentBar, false);
            SetAccentVisible(selectionFrame, false);
        }

        void ApplyVisualAmount(float amount)
        {
            visualAmount = Mathf.Clamp01(amount);

            if (panelBackground != null)
            {
                panelBackground.color = Color.Lerp(normalPanel, highlightedPanel, visualAmount);
                panelBackground.raycastTarget = false;
            }

            if (titleText != null)
            {
                titleText.color = Color.Lerp(normalTitle, highlightedTitle, visualAmount);
            }

            if (subtitleText != null)
            {
                subtitleText.color = Color.Lerp(normalSubtitle, highlightedSubtitle, visualAmount);
            }

            if (iconImage != null)
            {
                float iconAlpha = Mathf.Lerp(0.72f, accentColor.a, visualAmount);
                iconImage.color = WithAlpha(accentColor, iconAlpha);
                iconImage.rectTransform.localScale = Vector3.Lerp(normalIconScale, highlightedIconScale, visualAmount);
            }

            SetAccentVisible(accentBar, visualAmount);
            SetAccentVisible(selectionFrame, visualAmount * 0.95f);

            if (contentRoot != null)
            {
                contentRoot.anchoredPosition = contentRootBasePosition + Vector2.Lerp(normalContentOffset, highlightedContentOffset, visualAmount);
            }
        }

        void SetAccentVisible(Image image, float amount)
        {
            if (image == null)
            {
                return;
            }

            image.color = WithAlpha(accentColor, accentColor.a * Mathf.Clamp01(amount));
            image.raycastTarget = false;
        }

        void SetAccentVisible(Image image, bool visible)
        {
            SetAccentVisible(image, visible ? 1f : 0f);
        }

        void CaptureContentRootPosition()
        {
            if (capturedContentRootPosition || contentRoot == null)
            {
                return;
            }

            contentRootBasePosition = contentRoot.anchoredPosition;
            capturedContentRootPosition = true;
        }

        void ApplyIconLayout(bool hasIcon)
        {
            if (contentRoot == null)
            {
                return;
            }

            contentRoot.anchoredPosition = hasIcon ? contentOffsetWithIcon : contentOffsetWithoutIcon;
            contentRoot.sizeDelta = hasIcon ? contentSizeWithIcon : contentSizeWithoutIcon;
            contentRootBasePosition = contentRoot.anchoredPosition;
            capturedContentRootPosition = true;
        }

        void ApplyIconState(Sprite icon)
        {
            bool hasIcon = iconImage != null && icon != null;
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = hasIcon;
            }

            ApplyIconLayout(hasIcon);
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        static string ToMenuLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.ToUpperInvariant();
        }

        bool IsReducedMotionEnabled()
        {
            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            return root != null && root.ReducedMotion;
        }
    }
}
