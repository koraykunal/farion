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

            string key = action switch
            {
                MainMenuAction.Continue => UiTextKeys.MainMenuContinue,
                MainMenuAction.NewGame => UiTextKeys.MainMenuNewGame,
                MainMenuAction.LoadGame => UiTextKeys.MainMenuLoadGame,
                MainMenuAction.Settings => UiTextKeys.MainMenuSettings,
                MainMenuAction.Credits => UiTextKeys.MainMenuCredits,
                MainMenuAction.Exit => UiTextKeys.MainMenuExit,
                MainMenuAction.Back => UiTextKeys.CommonBack,
                MainMenuAction.ConfirmExit => UiTextKeys.CommonQuit,
                MainMenuAction.CancelExit => UiTextKeys.CommonCancel,
                MainMenuAction.JoinCoop => UiTextKeys.MainMenuJoinCoop,
                _ => string.Empty
            };
            view.SetTitle(UiLocalization.Get(key));
        }
    }
}
