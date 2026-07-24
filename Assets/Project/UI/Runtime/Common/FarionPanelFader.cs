using DG.Tweening;
using UnityEngine;

namespace Farion.UI.Common
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FarionPanelFader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] RectTransform motionRoot;

        [Header("Motion")]
        [SerializeField, Min(0f)] float showDuration = 0.16f;
        [SerializeField, Min(0f)] float hideDuration = 0.1f;
        [SerializeField] Ease showEase = Ease.OutQuart;
        [SerializeField] Ease hideEase = Ease.OutCubic;
        [SerializeField] Vector2 hiddenOffset = new(-24f, 0f);

        bool capturedPosition;
        Vector2 shownPosition;
        Sequence sequence;

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

            if (!animated || !Application.isPlaying)
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

            float duration = visible ? showDuration : hideDuration;
            Ease ease = visible ? showEase : hideEase;
            Vector2 targetPosition = visible ? shownPosition : shownPosition + hiddenOffset;

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

            if (motionRoot != null)
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

            if (motionRoot != null)
            {
                motionRoot.anchoredPosition = visible ? shownPosition : shownPosition + hiddenOffset;
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
    }
}
