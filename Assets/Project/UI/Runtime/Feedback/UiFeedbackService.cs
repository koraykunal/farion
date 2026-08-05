using System;
using System.Collections;
using System.Collections.Generic;
using Farion.UI.Foundation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Farion.UI.Feedback
{
    public enum UiFeedbackSeverity
    {
        Information = 0,
        Success = 10,
        Caution = 20,
        Error = 30
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiFeedbackService : MonoBehaviour
    {
        readonly struct Message
        {
            public Message(
                string text,
                UiFeedbackSeverity severity,
                float duration,
                Sprite icon,
                bool includeSeverityLabel,
                bool isItem)
            {
                Text = text;
                Severity = severity;
                Duration = duration;
                Icon = icon;
                IncludeSeverityLabel = includeSeverityLabel;
                IsItem = isItem;
            }

            public string Text { get; }
            public UiFeedbackSeverity Severity { get; }
            public float Duration { get; }
            public Sprite Icon { get; }
            public bool IncludeSeverityLabel { get; }
            public bool IsItem { get; }
        }

        [Header("View")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] TMP_Text messageText;
        [SerializeField] Image signalImage;
        [SerializeField] Image iconImage;
        [SerializeField] LayoutElement messageLayout;

        [Header("Design")]
        [SerializeField] UiTheme theme;
        [Min(0.1f)]
        [SerializeField] float defaultDuration = 2.4f;
        [Range(1, 16)]
        [SerializeField] int queueCapacity = 6;
        [SerializeField] bool animate = true;
        [Min(120f)]
        [SerializeField] float maxMessageWidth = 440f;

        [Header("Accessibility")]
        [Tooltip("Prefixes each message with a semantic severity label so state never depends on color alone.")]
        [SerializeField] bool includeSeverityLabel = true;

        readonly Queue<Message> messages = new();
        Coroutine routine;

        public event Action<string, UiFeedbackSeverity> MessageShown;
        public bool IsPresenting => routine != null;
        public bool IncludesSeverityLabel => includeSeverityLabel;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            ApplyTypography(isItem: false);
            SetIcon(null);
            ApplyAlpha(0f);
        }

        void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            messages.Clear();
            ApplyAlpha(0f);
        }

        void OnValidate()
        {
            defaultDuration = Mathf.Max(0.1f, defaultDuration);
            queueCapacity = Mathf.Clamp(queueCapacity, 1, 16);
            maxMessageWidth = Mathf.Max(120f, maxMessageWidth);
            ResolveReferences();
        }

        public void Show(
            string message,
            UiFeedbackSeverity severity = UiFeedbackSeverity.Information,
            float duration = -1f)
        {
            Enqueue(
                message,
                severity,
                duration,
                null,
                includeSeverityLabel,
                isItem: false);
        }

        public void ShowItem(
            string itemName,
            int amount,
            Sprite icon,
            float duration = -1f)
        {
            Enqueue(
                FormatItemMessage(itemName, amount),
                UiFeedbackSeverity.Success,
                duration,
                icon,
                includeLabel: false,
                isItem: true);
        }

        void Enqueue(
            string message,
            UiFeedbackSeverity severity,
            float duration,
            Sprite icon,
            bool includeLabel,
            bool isItem)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            while (messages.Count >= queueCapacity)
            {
                messages.Dequeue();
            }

            messages.Enqueue(
                new Message(
                    message.Trim(),
                    severity,
                    duration > 0f ? duration : defaultDuration,
                    icon,
                    includeLabel,
                    isItem));
            if (routine == null && isActiveAndEnabled)
            {
                routine = StartCoroutine(PresentQueue());
            }
        }

        public void Clear()
        {
            messages.Clear();
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            ApplyAlpha(0f);
        }

        IEnumerator PresentQueue()
        {
            while (messages.Count > 0)
            {
                Message message = messages.Dequeue();
                ApplyMessage(message);
                MessageShown?.Invoke(message.Text, message.Severity);

                bool shouldAnimate = animate && !IsReducedMotionEnabled();
                float enterDuration = shouldAnimate && theme != null
                    ? theme.StateEnterDuration
                    : shouldAnimate
                        ? 0.16f
                        : 0f;
                float exitDuration = shouldAnimate && theme != null
                    ? theme.StateExitDuration
                    : shouldAnimate
                        ? 0.1f
                        : 0f;

                yield return Fade(0f, 1f, enterDuration);
                yield return new WaitForSecondsRealtime(message.Duration);
                yield return Fade(1f, 0f, exitDuration);
            }

            routine = null;
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                ApplyAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 4f);
                ApplyAlpha(Mathf.LerpUnclamped(from, to, eased));
                yield return null;
            }

            ApplyAlpha(to);
        }

        void ApplyMessage(Message message)
        {
            SetIcon(message.Icon);
            if (messageText != null)
            {
                ApplyTypography(message.IsItem);
                messageText.text = FormatMessage(
                    message.Text,
                    message.Severity,
                    message.IncludeSeverityLabel);
                ResizeToMessage();
            }

            if (signalImage != null)
            {
                signalImage.color = ResolveSeverityColor(message.Severity);
            }
        }

        Color ResolveSeverityColor(UiFeedbackSeverity severity)
        {
            if (theme == null)
            {
                return severity switch
                {
                    UiFeedbackSeverity.Success => new Color(0.78f, 0.9f, 0.7f, 0.95f),
                    UiFeedbackSeverity.Caution => new Color(1f, 0.72f, 0.24f, 0.98f),
                    UiFeedbackSeverity.Error => new Color(1f, 0.26f, 0.2f, 1f),
                    _ => new Color(0.68f, 0.76f, 0.81f, 1f)
                };
            }

            return severity switch
            {
                UiFeedbackSeverity.Success => theme.Nominal,
                UiFeedbackSeverity.Caution => theme.Caution,
                UiFeedbackSeverity.Error => theme.Critical,
                _ => theme.SupportingText
            };
        }

        public static string FormatMessage(
            string message,
            UiFeedbackSeverity severity,
            bool includeLabel = true)
        {
            string normalizedMessage = message?.Trim() ?? string.Empty;
            if (!includeLabel || normalizedMessage.Length == 0)
            {
                return normalizedMessage;
            }

            return $"{GetSeverityLabel(severity)} · {normalizedMessage}";
        }

        public static string FormatItemMessage(string itemName, int amount)
        {
            return string.IsNullOrWhiteSpace(itemName) || amount <= 0
                ? string.Empty
                : $"{itemName.Trim()} ×{amount}";
        }

        public static string GetSeverityLabel(UiFeedbackSeverity severity)
        {
            return severity switch
            {
                UiFeedbackSeverity.Success => "SUCCESS",
                UiFeedbackSeverity.Caution => "CAUTION",
                UiFeedbackSeverity.Error => "ERROR",
                _ => "INFO"
            };
        }

        void ApplyAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        void ResizeToMessage()
        {
            if (messageText == null || messageLayout == null)
            {
                return;
            }

            Vector2 preferred = messageText.GetPreferredValues(
                messageText.text,
                maxMessageWidth,
                0f);
            messageLayout.preferredWidth = Mathf.Min(
                maxMessageWidth,
                Mathf.Ceil(preferred.x));
            messageLayout.preferredHeight = Mathf.Ceil(preferred.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        void SetIcon(Sprite icon)
        {
            if (iconImage == null)
            {
                return;
            }

            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }

        void ResolveReferences()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            messageLayout ??= messageText != null
                ? messageText.GetComponent<LayoutElement>()
                : null;
            if (theme == null)
            {
                UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
                theme = root != null ? root.Theme : null;
            }
        }

        void ApplyTypography(bool isItem)
        {
            if (messageText == null || theme == null)
            {
                return;
            }

            TMP_FontAsset font = isItem && theme.InterfaceMediumFont != null
                ? theme.InterfaceMediumFont
                : theme.InterfaceFont;
            if (font != null)
            {
                messageText.font = font;
            }
            messageText.fontWeight = isItem
                ? FontWeight.Medium
                : FontWeight.Regular;
            messageText.fontSize = isItem ? 17f : 14f;
            messageText.color = theme.PrimaryText;
        }

        bool IsReducedMotionEnabled()
        {
            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            return root != null && root.ReducedMotion;
        }
    }
}
