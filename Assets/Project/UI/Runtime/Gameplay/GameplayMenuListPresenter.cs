using System;
using System.Collections.Generic;
using Farion.UI.Common;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayMenuListPresenter : MonoBehaviour
    {
        [Serializable]
        sealed class MenuEntry
        {
            [SerializeField] GameplayMenuAction action;
            [SerializeField] string title;
            [SerializeField] Sprite icon;
            [SerializeField] bool available = true;

            public GameplayMenuAction Action => action;
            public string Title => title;
            public Sprite Icon => icon;
            public bool Available => available;
        }

        [Header("References")]
        [SerializeField] GameplayUiController controller;
        [SerializeField] UiScreenView screenView;
        [SerializeField] Transform buttonContainer;
        [SerializeField] Transform sessionButtonContainer;
        [SerializeField] MenuButtonView buttonPrefab;

        [Header("Entries")]
        [SerializeField] List<MenuEntry> entries = new();

        readonly List<MenuButtonView> generatedButtons = new();
        readonly List<MenuEntry> generatedEntries = new();
        bool built;

        public MenuButtonView FirstAvailableButton { get; private set; }

        void Awake()
        {
            ResolveReferences();
            Build();
        }

        void OnEnable()
        {
            ResolveReferences();
            if (Application.isPlaying)
            {
                LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
                RefreshLocalizedContent();
            }

            AssignInitialSelection();
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        public void Rebuild()
        {
            built = false;
            ClearGeneratedButtons();
            Build();
            screenView?.SetFirstSelection(FirstAvailableButton?.Button);
        }

        void Build()
        {
            if (built || buttonPrefab == null)
            {
                return;
            }

            Transform parent = buttonContainer != null ? buttonContainer : transform;
            for (int i = 0; i < entries.Count; i++)
            {
                MenuEntry entry = entries[i];
                if (entry == null ||
                    !entry.Available ||
                    controller != null &&
                    !controller.IsActionAvailable(entry.Action))
                {
                    continue;
                }

                Transform entryParent = IsSessionAction(entry.Action) &&
                                        sessionButtonContainer != null
                    ? sessionButtonContainer
                    : parent;
                MenuButtonView button = Instantiate(buttonPrefab, entryParent);
                button.name = $"UI_MenuButton_{entry.Action}";
                button.ConfigureContent(ResolveTitle(entry), string.Empty, entry.Icon);
                button.SetAvailable(true);

                GameplayMenuAction action = entry.Action;
                button.Clicked += () => Handle(action);
                generatedButtons.Add(button);
                generatedEntries.Add(entry);
                if (FirstAvailableButton == null)
                {
                    FirstAvailableButton = button;
                }
            }

            built = true;
            AssignInitialSelection();
        }

        void ClearGeneratedButtons()
        {
            if (screenView != null)
            {
                screenView.SetFirstSelection(null);
            }

            for (int i = generatedButtons.Count - 1; i >= 0; i--)
            {
                MenuButtonView button = generatedButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    button.gameObject.SetActive(false);
                    Destroy(button.gameObject);
                }
                else
                {
                    DestroyImmediate(button.gameObject);
                }
            }

            generatedButtons.Clear();
            generatedEntries.Clear();
            FirstAvailableButton = null;
        }

        void Handle(GameplayMenuAction action)
        {
            ResolveReferences();
            controller?.Handle(action);
        }

        void ResolveReferences()
        {
            if (buttonContainer == null)
            {
                buttonContainer = transform;
            }

            if (controller == null)
            {
                controller = GetComponentInParent<GameplayUiController>();
            }

            screenView ??= GetComponentInParent<UiScreenView>();
        }

        void AssignInitialSelection()
        {
            if (screenView == null ||
                screenView.HasExplicitFirstSelection ||
                FirstAvailableButton == null)
            {
                return;
            }

            screenView.SetFirstSelection(FirstAvailableButton.Button);
        }

        void HandleLocaleChanged(Locale _)
        {
            RefreshLocalizedContent();
        }

        void RefreshLocalizedContent()
        {
            int count = Mathf.Min(generatedButtons.Count, generatedEntries.Count);
            for (int i = 0; i < count; i++)
            {
                if (generatedButtons[i] != null && generatedEntries[i] != null)
                {
                    generatedButtons[i].SetTitle(ResolveTitle(generatedEntries[i]));
                }
            }
        }

        static string ResolveTitle(MenuEntry entry)
        {
            string key = entry.Action switch
            {
                GameplayMenuAction.Resume => UiTextKeys.PauseResume,
                GameplayMenuAction.Inventory => UiTextKeys.PauseInventory,
                GameplayMenuAction.Blueprints => UiTextKeys.PauseBlueprints,
                GameplayMenuAction.Journal => UiTextKeys.PauseJournal,
                GameplayMenuAction.Ship => UiTextKeys.PauseShip,
                GameplayMenuAction.Map => UiTextKeys.PauseMap,
                GameplayMenuAction.Options => UiTextKeys.PauseOptions,
                GameplayMenuAction.Save => UiTextKeys.PauseSave,
                GameplayMenuAction.ExitToMainMenu => UiTextKeys.PauseMainMenu,
                GameplayMenuAction.QuitGame => UiTextKeys.PauseQuit,
                _ => string.Empty
            };
            return UiLocalization.Get(key);
        }

        static bool IsSessionAction(GameplayMenuAction action)
        {
            return action is GameplayMenuAction.ExitToMainMenu or
                GameplayMenuAction.QuitGame;
        }
    }
}
