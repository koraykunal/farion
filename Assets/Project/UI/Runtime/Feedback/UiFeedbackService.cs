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
            public Message(string text, UiFeedbackSeverity severity, float duration)
            {
                Text = text;
                Severity = severity;
                Duration = duration;
            }

            public string Text { get; }
            public UiFeedbackSeverity Severity { get; }
            public float Duration { get; }
        }

        [Header("View")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] TMP_Text messageText;
        [SerializeField] Image signalImage;

        [Header("Design")]
        [SerializeField] UiTheme theme;
        [Min(0.1f)]
        [SerializeField] float defaultDuration = 2.4f;
        [Range(1, 16)]
        [SerializeField] int queueCapacity = 6;
        [SerializeField] bool animate = true;

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
            ResolveReferences();
        }

        public void Show(
            string message,
            UiFeedbackSeverity severity = UiFeedbackSeverity.Information,
            float duration = -1f)
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
                    duration > 0f ? duration : defaultDuration));
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
            if (messageText != null)
            {
                messageText.text = FormatMessage(
                    message.Text,
                    message.Severity,
                    includeSeverityLabel);
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
                    _ => new Color(0.52f, 0.94f, 0.9f, 0.96f)
                };
            }

            return severity switch
            {
                UiFeedbackSeverity.Success => theme.Focus,
                UiFeedbackSeverity.Caution => theme.Caution,
                UiFeedbackSeverity.Error => theme.Critical,
                _ => theme.Nominal
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

        void ResolveReferences()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (theme == null)
            {
                UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
                theme = root != null ? root.Theme : null;
            }
        }

        bool IsReducedMotionEnabled()
        {
            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            return root != null && root.ReducedMotion;
        }
    }
}
