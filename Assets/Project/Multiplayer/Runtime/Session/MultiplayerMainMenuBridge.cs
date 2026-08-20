using Farion.Core.Persistence;
using Farion.UI.Feedback;
using Farion.UI.Localization;
using Farion.UI.MainMenu;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MainMenuController))]
    public sealed class MultiplayerMainMenuBridge : MonoBehaviour
    {
        const int MaximumCoopPlayers = 4;

        [SerializeField] MainMenuController mainMenu;
        [SerializeField] UiCoopScreenPresenter coopScreen;

        MultiplayerSessionController session;

        void Awake()
        {
            ResolveReferences();
        }

        void Start()
        {
            if (MultiplayerSessionOutcome.TryConsume(
                    out MultiplayerFailureReason reason))
            {
                mainMenu.ShowFeedback(
                    UiLocalization.Get(ResolveFailureKey(reason)),
                    UiFeedbackSeverity.Error);
            }
        }

        void OnEnable()
        {
            ResolveReferences();
            mainMenu.CoopHostRequested -= HandleHostRequested;
            mainMenu.CoopHostRequested += HandleHostRequested;
            MultiplayerLobbyGateway.JoinRequested -= HandleLobbyJoinRequested;
            MultiplayerLobbyGateway.JoinRequested += HandleLobbyJoinRequested;
            if (coopScreen != null)
            {
                coopScreen.JoinRequested -= HandleJoinRequested;
                coopScreen.JoinRequested += HandleJoinRequested;
                coopScreen.Closed -= HandleCoopScreenClosed;
                coopScreen.Closed += HandleCoopScreenClosed;
            }
        }

        void OnDisable()
        {
            MultiplayerLobbyGateway.JoinRequested -= HandleLobbyJoinRequested;
            if (mainMenu != null)
            {
                mainMenu.CoopHostRequested -= HandleHostRequested;
            }

            if (coopScreen != null)
            {
                coopScreen.JoinRequested -= HandleJoinRequested;
                coopScreen.Closed -= HandleCoopScreenClosed;
            }

            UnbindSession();
        }

        void ResolveReferences()
        {
            mainMenu ??= GetComponent<MainMenuController>();
            coopScreen ??= GetComponentInChildren<UiCoopScreenPresenter>(true);
        }

        void HandleHostRequested()
        {
            if (!TryCreateSession())
            {
                return;
            }

            mainMenu.ShowFeedback(
                UiLocalization.Get(UiTextKeys.CoopStatusHosting),
                UiFeedbackSeverity.Information);
            if (MultiplayerLobbyGateway.IsAvailable)
            {
                MultiplayerLobbyGateway.Service.HostLobby(
                    MaximumCoopPlayers,
                    OnLobbyHosted);
                return;
            }

            session.StartHost();
        }

        void OnLobbyHosted(bool succeeded)
        {
            if (session == null)
            {
                return;
            }

            if (!succeeded)
            {
                mainMenu.ShowFeedback(
                    UiLocalization.Get(UiTextKeys.CoopErrorLobby),
                    UiFeedbackSeverity.Caution);
            }

            session.StartHost();
        }

        void HandleLobbyJoinRequested(string hostAddress)
        {
            if (!TryCreateSession())
            {
                return;
            }

            coopScreen?.SetBusy(true);
            coopScreen?.SetStatus(
                UiLocalization.Get(UiTextKeys.CoopStatusConnecting),
                UiFeedbackSeverity.Information);
            session.StartSteamClient(hostAddress);
        }

        void HandleJoinRequested(string address)
        {
            if (!TryCreateSession())
            {
                return;
            }

            coopScreen?.SetBusy(true);
            coopScreen?.SetStatus(
                UiLocalization.Get(UiTextKeys.CoopStatusConnecting),
                UiFeedbackSeverity.Information);
            session.StartClient(address);
        }

        void HandleCoopScreenClosed()
        {
            if (session == null ||
                session.State == MultiplayerSessionState.Connected)
            {
                return;
            }

            session.Stop();
            UnbindSession();
            coopScreen?.SetBusy(false);
        }

        bool TryCreateSession()
        {
            if (session != null)
            {
                ReportFailure(UiTextKeys.CoopStatusBusy);
                return false;
            }

            if (!MultiplayerSessionLauncher.TryCreateSession(out session))
            {
                ReportFailure(UiTextKeys.CoopErrorSession);
                return false;
            }

            session.StateChanged += HandleStateChanged;
            return true;
        }

        void HandleStateChanged(MultiplayerSessionState state)
        {
            if (state == MultiplayerSessionState.Failed)
            {
                ReportFailure(ResolveFailureKey());
                UnbindSession();
                return;
            }

            if (state == MultiplayerSessionState.Idle)
            {
                coopScreen?.SetBusy(false);
                UnbindSession();
            }
        }

        string ResolveFailureKey()
        {
            return ResolveFailureKey(
                session != null
                    ? session.FailureReason
                    : MultiplayerFailureReason.ConnectionFailed);
        }

        static string ResolveFailureKey(MultiplayerFailureReason reason)
        {
            return reason switch
            {
                MultiplayerFailureReason.ProtocolMismatch =>
                    UiTextKeys.CoopErrorProtocol,
                MultiplayerFailureReason.InvalidAddress =>
                    UiTextKeys.CoopErrorAddress,
                MultiplayerFailureReason.SessionSetup =>
                    UiTextKeys.CoopErrorSession,
                MultiplayerFailureReason.ConnectionLost =>
                    UiTextKeys.CoopErrorHostLeft,
                MultiplayerFailureReason.ServerFull =>
                    UiTextKeys.CoopErrorServerFull,
                _ => UiTextKeys.CoopErrorConnection
            };
        }

        void ReportFailure(string entryKey)
        {
            coopScreen?.SetBusy(false);
            string message = UiLocalization.Get(entryKey);
            if (coopScreen != null && coopScreen.isActiveAndEnabled)
            {
                coopScreen.SetStatus(message, UiFeedbackSeverity.Error);
                return;
            }

            mainMenu.ShowFeedback(message, UiFeedbackSeverity.Error);
        }

        void UnbindSession()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }

            session = null;
        }
    }
}
