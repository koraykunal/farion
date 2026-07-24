using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MainMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Action")]
        [SerializeField] MainMenuAction action;
        [SerializeField] MainMenuController controller;

        [Header("Interaction")]
        [SerializeField] Button button;
        [SerializeField] bool available = true;

        [Header("Content")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] Image iconImage;

        [Header("Visuals")]
        [SerializeField] Image background;
        [SerializeField] Image accentBar;
        [SerializeField] Image selectionFrame;
        [SerializeField] RectTransform contentRoot;

        [Header("Colors")]
        [SerializeField] Color normalBackground = new(0.02f, 0.055f, 0.09f, 0.82f);
        [SerializeField] Color highlightedBackground = new(0.04f, 0.1f, 0.16f, 0.92f);
        [SerializeField] Color normalTitle = new(0.78f, 0.82f, 0.86f, 1f);
        [SerializeField] Color highlightedTitle = new(0.93f, 0.97f, 1f, 1f);
        [SerializeField] Color normalSubtitle = new(0.38f, 0.48f, 0.58f, 1f);
        [SerializeField] Color highlightedSubtitle = new(0.55f, 0.72f, 0.88f, 1f);
        [SerializeField] Color accentColor = new(0.15f, 0.62f, 1f, 0.72f);

        [Header("Motion")]
        [SerializeField] Vector2 normalContentOffset;
        [SerializeField] Vector2 highlightedContentOffset = new(6f, 0f);
        [SerializeField, Min(0.01f)] float hoverInDuration = 0.14f;
        [SerializeField, Min(0.01f)] float hoverOutDuration = 0.2f;
        [SerializeField] Ease hoverInEase = Ease.OutQuart;
        [SerializeField] Ease hoverOutEase = Ease.OutCubic;
        [SerializeField] Vector3 normalIconScale = Vector3.one;
        [SerializeField] Vector3 highlightedIconScale = new(1.08f, 1.08f, 1f);

        bool pointerInside;
        bool selected;
        bool capturedContentRootPosition;
        Vector2 contentRootBasePosition;
        float visualAmount;
        bool visualStateInitialized;
        bool currentHighlighted;
        bool currentInteractable;
        Sequence visualTween;

        public MainMenuAction Action => action;

        void Reset()
        {
            ResolveReferences();
            ConfigureButtonTransition();
        }

        void Awake()
        {
            ResolveReferences();
            ConfigureButtonTransition();
            CaptureContentRootPosition();
            RefreshVisualState(immediate: true);
        }

        void OnEnable()
        {
            ResolveReferences();
            CaptureContentRootPosition();
            if (button != null)
            {
                button.onClick.RemoveListener(InvokeAction);
                button.onClick.AddListener(InvokeAction);
            }

            ApplyAvailability();
        }

        void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(InvokeAction);
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
                titleText.text = title;
            }

            if (subtitleText != null)
            {
                subtitleText.text = subtitle;
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
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

        void InvokeAction()
        {
            if (!available)
            {
                return;
            }

            controller?.Handle(action);
        }

        void ResolveReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (controller == null)
            {
                controller = GetComponentInParent<MainMenuController>();
            }

            if (controller == null)
            {
                Debug.LogError($"{nameof(MainMenuButton)} on {name} requires a parent or explicit {nameof(MainMenuController)} reference.", this);
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
            if (background != null)
            {
                background.color = WithAlpha(normalBackground, normalBackground.a * 0.45f);
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
                iconImage.color = WithAlpha(normalTitle, 0.3f);
            }

            SetAccentVisible(accentBar, false);
            SetAccentVisible(selectionFrame, false);
        }

        void ApplyVisualAmount(float amount)
        {
            visualAmount = Mathf.Clamp01(amount);

            if (background != null)
            {
                background.color = Color.Lerp(normalBackground, highlightedBackground, visualAmount);
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
                iconImage.color = Color.Lerp(normalTitle, highlightedTitle, visualAmount);
                iconImage.rectTransform.localScale = Vector3.Lerp(normalIconScale, highlightedIconScale, visualAmount);
            }

            SetAccentVisible(accentBar, visualAmount);
            SetAccentVisible(selectionFrame, visualAmount * 0.55f);

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

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

    }
}
