using Farion.Audio;
using Farion.UI.Foundation;
using UnityEngine;

namespace Farion.UI.Feedback
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiSystemRoot))]
    public sealed class UiAudioBridge : MonoBehaviour
    {
        UiSystemRoot systemRoot;
        UiFeedbackService feedbackService;
        bool subscribed;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
            AudioDirector.Current?.SetPaused(false);
        }

        void Subscribe()
        {
            if (subscribed || systemRoot == null)
            {
                return;
            }

            systemRoot.ScreenRouter.TopScreenChanged += HandleTopScreenChanged;
            if (feedbackService != null)
            {
                feedbackService.MessageShown += HandleMessageShown;
            }

            subscribed = true;
            HandleTopScreenChanged(systemRoot.ScreenRouter.TopScreenId);
        }

        void Unsubscribe()
        {
            if (!subscribed || systemRoot == null)
            {
                return;
            }

            systemRoot.ScreenRouter.TopScreenChanged -= HandleTopScreenChanged;
            if (feedbackService != null)
            {
                feedbackService.MessageShown -= HandleMessageShown;
            }

            subscribed = false;
        }

        void HandleTopScreenChanged(UiScreenId screenId)
        {
            AudioDirector.Current?.SetPaused(screenId == UiScreenId.PauseMenu);
        }

        static void HandleMessageShown(string _, UiFeedbackSeverity severity)
        {
            switch (severity)
            {
                case UiFeedbackSeverity.Success:
                    AudioDirector.Current?.PlayUi(UiAudioCue.Success);
                    break;
                case UiFeedbackSeverity.Caution:
                case UiFeedbackSeverity.Error:
                    AudioDirector.Current?.PlayUi(UiAudioCue.Error);
                    break;
            }
        }

        void ResolveReferences()
        {
            systemRoot ??= GetComponent<UiSystemRoot>();
            feedbackService ??= systemRoot != null ? systemRoot.FeedbackService : null;
        }
    }
}
