using Farion.UI.Common;
using Farion.UI.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MenuButtonView))]
    public sealed class MainMenuButton : MonoBehaviour
    {
        [Header("Action")]
        [SerializeField] MainMenuAction action;
        [SerializeField] MainMenuController controller;
        [SerializeField] MenuButtonView view;

        public MainMenuAction Action => action;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (action == MainMenuAction.HostGame ||
                action == MainMenuAction.JoinLocalhost)
            {
                gameObject.SetActive(false);
            }
#endif
        }

        void OnEnable()
        {
            ResolveReferences();
            if (view != null)
            {
                view.Clicked -= InvokeAction;
                view.Clicked += InvokeAction;
            }

            if (Application.isPlaying)
            {
                LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
                RefreshLocalizedTitle();
            }
        }

        void OnDisable()
        {
            if (view != null)
            {
                view.Clicked -= InvokeAction;
            }

            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        public void SetAvailable(bool available)
        {
            ResolveReferences();
            view?.SetAvailable(available);
        }

        public void ConfigureContent(string title, string subtitle, Sprite icon)
        {
            ResolveReferences();
            view?.ConfigureContent(title, subtitle, icon);
        }

        void ResolveReferences()
        {
            if (view == null)
            {
                view = GetComponent<MenuButtonView>();
            }

            if (controller == null)
            {
                controller = GetComponentInParent<MainMenuController>();
            }

            if (controller == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"{nameof(MainMenuButton)} on {name} requires a parent or explicit {nameof(MainMenuController)} reference.", this);
#endif
            }
        }

        void InvokeAction()
        {
            controller?.Handle(action);
        }

        void HandleLocaleChanged(Locale _)
        {
            RefreshLocalizedTitle();
        }

        void RefreshLocalizedTitle()
        {
            if (view == null)
            {
                return;
            }

            (string key, string fallback) = action switch
            {
                MainMenuAction.Continue => ("main_menu.continue", "Continue"),
                MainMenuAction.NewGame => ("main_menu.new_game", "New game"),
                MainMenuAction.LoadGame => ("main_menu.load_game", "Load game"),
                MainMenuAction.Settings => ("main_menu.settings", "Settings"),
                MainMenuAction.Credits => ("main_menu.credits", "Credits"),
                MainMenuAction.Exit => ("main_menu.exit", "Exit"),
                MainMenuAction.Back => ("common.back", "Back"),
                MainMenuAction.ConfirmExit => ("common.quit", "Quit"),
                MainMenuAction.CancelExit => ("common.cancel", "Cancel"),
                MainMenuAction.HostGame => ("main_menu.host_game", "Host game"),
                MainMenuAction.JoinLocalhost => ("main_menu.join_localhost", "Join localhost"),
                _ => (string.Empty, action.ToString())
            };
            view.SetTitle(UiLocalization.Get(key, fallback));
        }
    }
}
