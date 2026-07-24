using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Common
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Interaction")]
        [SerializeField] Button button;
        [SerializeField] bool available = true;

        [Header("Content")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] Image iconImage;

        [Header("Visuals")]
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
        [SerializeField] Color normalPanel = new(0.004f, 0.018f, 0.017f, 0.48f);
        [SerializeField] Color highlightedPanel = new(0.004f, 0.018f, 0.017f, 0.68f);
        [SerializeField] Color normalTitle = new(0.62f, 0.65f, 0.59f, 0.88f);
        [SerializeField] Color highlightedTitle = new(0.94f, 0.91f, 0.83f, 1f);
        [SerializeField] Color normalSubtitle = new(0.38f, 0.48f, 0.58f, 1f);
        [SerializeField] Color highlightedSubtitle = new(0.55f, 0.72f, 0.88f, 1f);
        [SerializeField] Color accentColor = new(0.78f, 0.9f, 0.7f, 0.95f);

        [Header("Motion")]
        [SerializeField] Vector2 normalContentOffset;
        [SerializeField] Vector2 highlightedContentOffset;
        [SerializeField, Min(0.01f)] float hoverInDuration = 0.14f;
        [SerializeField, Min(0.01f)] float hoverOutDuration = 0.2f;
        [SerializeField] Ease hoverInEase = Ease.OutQuart;
        [SerializeField] Ease hoverOutEase = Ease.OutCubic;
        [SerializeField] Vector3 normalIconScale = Vector3.one;
        [SerializeField] Vector3 highlightedIconScale = Vector3.one;

        bool pointerInside;
        bool selected;
        bool capturedContentRootPosition;
        Vector2 contentRootBasePosition;
        float visualAmount;
        bool visualStateInitialized;
        bool currentHighlighted;
        bool currentInteractable;
        Sequence visualTween;

        public event Action Clicked;
        public bool Available => available;

        void Reset()
        {
            ResolveReferences();
            ConfigureButtonTransition();
        }

        void Awake()
        {
            ResolveReferences();
            ConfigureButtonTransition();
            ApplyIconState(iconImage != null ? iconImage.sprite : null);
            RefreshVisualState(immediate: true);
        }

        void OnEnable()
        {
            ResolveReferences();
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
        }

        public void SetAvailable(bool isAvailable)
        {
            available = isAvailable;
            ApplyAvailability();
        }

        public void ConfigureContent(string title, string subtitle, Sprite icon)
        {
            if (titleText != null)
            {
                titleText.text = ToMenuLabel(title);
            }

            if (subtitleText != null)
            {
                subtitleText.text = ToMenuLabel(subtitle);
            }

            ApplyIconState(icon);
            RefreshVisualState(immediate: true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            RefreshVisualState(immediate: false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
                selected = false;
            }

            RefreshVisualState(immediate: false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            RefreshVisualState(immediate: false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
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

            bool isInteractable = available && button != null && button.interactable;
            bool highlighted = isInteractable && (pointerInside || selected);
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
    }
}
