using System;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Input;
using Farion.Gameplay.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerControlLock))]
    public sealed class GameplayUiController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] Key pauseKey = Key.Escape;
        [SerializeField] Key inventoryKey = Key.I;

        [Header("State")]
        [SerializeField] GameplayScreenState currentScreen = GameplayScreenState.None;
        [SerializeField] string mainMenuSceneName = "MainMenu";

        [Header("References")]
        [SerializeField] PlayerControlLock controlLock;
        [SerializeField] GameplayPanelSwitcher panelSwitcher;
        [SerializeField] InventoryPanelPresenter inventoryPanel;
        [SerializeField] PlayerInventory playerInventory;

        [Header("HUD")]
        [SerializeField] PlayerInteractionRaycaster interactionRaycaster;
        [SerializeField] TMP_Text interactionPromptText;
        [SerializeField] string interactionPromptPrefix = "E";

        [Header("Cursor")]
        [SerializeField] bool lockCursorDuringGameplay = true;

        public event Action<GameplayScreenState> ScreenChanged;
        public GameplayScreenState CurrentScreen => currentScreen;
        public bool IsUiFocused => currentScreen != GameplayScreenState.None;

        void Awake()
        {
            ResolveReferences();
            ApplyScreenState();
        }

        void OnEnable()
        {
            ResolveReferences();
            ApplyScreenState();
        }

        void OnDisable()
        {
            if (controlLock != null)
            {
                controlLock.SetLocked(PlayerControlLockReason.UserInterface, false);
            }
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (WasPressedThisFrame(keyboard, pauseKey))
                {
                    HandlePausePressed();
                    return;
                }

                if (WasPressedThisFrame(keyboard, inventoryKey))
                {
                    ToggleInventory();
                }
            }
        }

        void LateUpdate()
        {
            RefreshHud();
        }

        public void ShowPauseMenu()
        {
            Show(GameplayScreenState.PauseMenu);
        }

        public void ShowInventory()
        {
            Show(GameplayScreenState.Inventory);
        }

        public void CloseActiveScreen()
        {
            Show(GameplayScreenState.None);
        }

        public void ToggleInventory()
        {
            Show(currentScreen == GameplayScreenState.Inventory
                ? GameplayScreenState.None
                : GameplayScreenState.Inventory);
        }

        public void SetPlayerInventory(PlayerInventory inventory)
        {
            playerInventory = inventory;
            if (inventoryPanel != null)
            {
                inventoryPanel.SetInventory(playerInventory);
            }
        }

        public void Handle(GameplayMenuAction action)
        {
            switch (action)
            {
                case GameplayMenuAction.Resume:
                    CloseActiveScreen();
                    break;
                case GameplayMenuAction.Inventory:
                    ShowInventory();
                    break;
                case GameplayMenuAction.ExitToMainMenu:
                    LoadMainMenu();
                    break;
                case GameplayMenuAction.QuitGame:
                    Application.Quit();
                    break;
                case GameplayMenuAction.Blueprints:
                case GameplayMenuAction.Journal:
                case GameplayMenuAction.Ship:
                case GameplayMenuAction.Map:
                case GameplayMenuAction.Options:
                case GameplayMenuAction.Save:
                    ShowPauseMenu();
                    break;
            }
        }

        void HandlePausePressed()
        {
            if (currentScreen == GameplayScreenState.None)
            {
                ShowPauseMenu();
                return;
            }

            CloseActiveScreen();
        }

        void LoadMainMenu()
        {
            if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                return;
            }

            SceneManager.LoadScene(mainMenuSceneName.Trim(), LoadSceneMode.Single);
        }

        void Show(GameplayScreenState nextScreen)
        {
            if (nextScreen == currentScreen)
            {
                return;
            }

            currentScreen = nextScreen;
            ApplyScreenState();
            ScreenChanged?.Invoke(currentScreen);
        }

        void ApplyScreenState()
        {
            ResolveReferences();
            panelSwitcher?.Show(currentScreen);
            inventoryPanel?.SetInventory(playerInventory);

            bool uiFocused = currentScreen != GameplayScreenState.None;
            if (controlLock != null)
            {
                controlLock.SetLocked(PlayerControlLockReason.UserInterface, uiFocused);
            }

            ApplyCursorState(uiFocused);
            RefreshHud();
        }

        void ApplyCursorState(bool uiFocused)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (uiFocused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (lockCursorDuringGameplay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void ResolveReferences()
        {
            if (controlLock == null)
            {
                controlLock = GetComponent<PlayerControlLock>();
            }

            if (panelSwitcher == null)
            {
                panelSwitcher = GetComponent<GameplayPanelSwitcher>();
            }

            if (inventoryPanel == null)
            {
                inventoryPanel = GetComponentInChildren<InventoryPanelPresenter>(true);
            }
        }

        void RefreshHud()
        {
            if (interactionPromptText == null)
            {
                return;
            }

            bool showPrompt = currentScreen == GameplayScreenState.None &&
                interactionRaycaster != null &&
                interactionRaycaster.HasTarget &&
                !string.IsNullOrWhiteSpace(interactionRaycaster.CurrentPrompt);

            interactionPromptText.gameObject.SetActive(showPrompt);
            if (!showPrompt)
            {
                interactionPromptText.text = string.Empty;
                return;
            }

            string prompt = interactionRaycaster.CurrentPrompt.Trim();
            interactionPromptText.text = string.IsNullOrWhiteSpace(interactionPromptPrefix)
                ? prompt
                : $"{interactionPromptPrefix.Trim()} - {prompt}";
        }

        static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            KeyControl control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }
    }
}
