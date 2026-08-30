using Farion.UI.Common;
using Farion.UI.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiMenuButtonView))]
    public sealed class UiMainMenuButton : MonoBehaviour
    {
        [Header("Action")]
        [SerializeField] UiMainMenuAction action;
        [SerializeField] UiMainMenuController controller;
        [SerializeField] UiMenuButtonView view;

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

        void ResolveReferences()
        {
            if (view == null)
            {
                view = GetComponent<UiMenuButtonView>();
            }

            if (controller == null)
            {
                controller = GetComponentInParent<UiMainMenuController>();
            }

            if (controller == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"{nameof(UiMainMenuButton)} on {name} requires a parent or explicit {nameof(UiMainMenuController)} reference.", this);
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
                UiMainMenuAction.Continue => UiTextKeys.MainMenuContinue,
                UiMainMenuAction.NewGame => UiTextKeys.MainMenuNewGame,
                UiMainMenuAction.LoadGame => UiTextKeys.MainMenuLoadGame,
                UiMainMenuAction.Settings => UiTextKeys.MainMenuSettings,
                UiMainMenuAction.Credits => UiTextKeys.MainMenuCredits,
                UiMainMenuAction.Exit => UiTextKeys.MainMenuExit,
                UiMainMenuAction.Back => UiTextKeys.CommonBack,
                UiMainMenuAction.ConfirmExit => UiTextKeys.CommonQuit,
                UiMainMenuAction.CancelExit => UiTextKeys.CommonCancel,
                UiMainMenuAction.JoinCoop => UiTextKeys.MainMenuJoinCoop,
                _ => string.Empty
            };
            view.SetTitle(UiLocalization.Get(key));
        }
    }
}
