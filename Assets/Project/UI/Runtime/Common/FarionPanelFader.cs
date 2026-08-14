using Farion.UI.Foundation;
using Farion.UI.Styling;
using DG.Tweening;
using UnityEngine;

namespace Farion.UI.Common
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FarionPanelFader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] UiTheme theme;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] RectTransform motionRoot;

        [Header("Motion")]
        [SerializeField] Ease showEase = Ease.OutQuart;
        [SerializeField] Ease hideEase = Ease.OutCubic;
        [SerializeField] bool fadeOnly;

        bool capturedPosition;
        Vector2 shownPosition;
        Sequence sequence;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        public bool FadeOnly => fadeOnly;

        // Durations and offset are theme-owned; nothing here is per-instance authored.
        Vector2 HiddenOffset => Theme.PanelHiddenOffset;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            CapturePosition();
        }

        void OnDisable()
        {
            sequence?.Kill();
            sequence = null;
        }

        public void SetVisible(bool visible, bool animated)
        {
            ResolveReferences();
            CapturePosition();

            if (!animated || !Application.isPlaying || IsReducedMotionEnabled())
            {
                sequence?.Kill();
                sequence = null;
                ApplyImmediate(visible);
                return;
            }

            PlayTransition(visible);
        }

        void PlayTransition(bool visible)
        {
            sequence?.Kill();
            gameObject.SetActive(true);

            if (canvasGroup == null)
            {
                return;
            }

            float duration = visible ? Theme.StateEnterDuration : Theme.StateExitDuration;
            Ease ease = visible ? showEase : hideEase;
            Vector2 targetPosition = visible ? shownPosition : shownPosition + HiddenOffset;

            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this);
            sequence.Join(
                DOTween.To(
                        () => canvasGroup.alpha,
                        value => canvasGroup.alpha = value,
                        visible ? 1f : 0f,
                        duration)
                    .SetEase(ease));

            if (!fadeOnly && motionRoot != null)
            {
                sequence.Join(
                    DOTween.To(
                            () => motionRoot.anchoredPosition,
                            value => motionRoot.anchoredPosition = value,
                            targetPosition,
                            duration)
                        .SetEase(ease));
            }

            if (!visible)
            {
                sequence.OnComplete(() => gameObject.SetActive(false));
            }
        }

        void ApplyImmediate(bool visible)
        {
            gameObject.SetActive(visible);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (!fadeOnly && motionRoot != null)
            {
                motionRoot.anchoredPosition = visible ? shownPosition : shownPosition + HiddenOffset;
            }
        }

        void ResolveReferences()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (motionRoot == null)
            {
                motionRoot = transform as RectTransform;
            }

            if (theme == null)
            {
                UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
                theme = UiTheme.Resolve(root != null ? root.Theme : null);
            }
        }

        void CapturePosition()
        {
            if (capturedPosition || motionRoot == null)
            {
                return;
            }

            shownPosition = motionRoot.anchoredPosition;
            capturedPosition = true;
        }

        bool IsReducedMotionEnabled()
        {
            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            return root != null && root.ReducedMotion;
        }
    }
}
