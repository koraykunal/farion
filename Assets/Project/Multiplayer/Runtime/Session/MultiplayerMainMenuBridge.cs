using Farion.UI.MainMenu;
using Farion.UI.Feedback;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MainMenuController))]
    public sealed class MultiplayerMainMenuBridge : MonoBehaviour
    {
        [SerializeField] MainMenuController mainMenu;
        MultiplayerSessionController session;

        void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mainMenu ??= GetComponent<MainMenuController>();
            mainMenu.CoopActionRequested += Handle;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (mainMenu != null)
            {
                mainMenu.CoopActionRequested -= Handle;
            }

            UnbindSession();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Handle(MainMenuAction action)
        {
            if (session != null)
            {
                mainMenu.ShowFeedback(
                    "A local co-op session is already starting.",
                    UiFeedbackSeverity.Information);
                return;
            }

            if (!MultiplayerDevelopmentRunner.TryCreateSession(out session))
            {
                mainMenu.ShowFeedback(
                    "The local co-op session could not be created.",
                    UiFeedbackSeverity.Error);
                return;
            }

            session.StateChanged += HandleStateChanged;
            mainMenu.ShowFeedback(
                action == MainMenuAction.HostGame
                    ? "Starting local co-op host..."
                    : "Connecting to localhost...",
                UiFeedbackSeverity.Information);

            if (action == MainMenuAction.HostGame)
            {
                session.StartHost();
            }
            else
            {
                session.StartClient("127.0.0.1");
            }
        }

        void HandleStateChanged(MultiplayerSessionState state)
        {
            if (state == MultiplayerSessionState.Failed)
            {
                mainMenu.ShowFeedback(
                    "The local co-op session could not connect.",
                    UiFeedbackSeverity.Error);
            }
            else if (state == MultiplayerSessionState.Idle)
            {
                UnbindSession();
            }
        }

        void UnbindSession()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }

            session = null;
        }
#endif
    }
}
