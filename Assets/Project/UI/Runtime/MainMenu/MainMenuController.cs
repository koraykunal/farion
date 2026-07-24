using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        const string DefaultGameplaySceneName = "SC_PhysicsSandbox";

        [Header("Scene Flow")]
        [SerializeField] GameFlowSettings flowSettings;
        [SerializeField] string gameplaySceneName = DefaultGameplaySceneName;
        [SerializeField] bool hasSaveGame;

        [Header("Panels")]
        [SerializeField] MainMenuPanelSwitcher panelSwitcher;

        [Header("Buttons")]
        [SerializeField] MainMenuButton continueButton;
        [SerializeField] MainMenuButton loadGameButton;

        Coroutine loadRoutine;

        public bool HasSaveGame => flowSettings != null ? flowSettings.HasSaveGame : hasSaveGame;

        void Awake()
        {
            ResolveReferences();
            ApplySaveAvailability();
            panelSwitcher?.ShowMain();
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                gameplaySceneName = DefaultGameplaySceneName;
            }
        }

        public void Handle(MainMenuAction action)
        {
            switch (action)
            {
                case MainMenuAction.Continue:
                    if (HasSaveGame)
                    {
                        StartGameplayLoad(ResolveContinueSceneName());
                    }
                    break;
                case MainMenuAction.NewGame:
                    StartGameplayLoad(ResolveNewGameSceneName());
                    break;
                case MainMenuAction.LoadGame:
                    if (HasSaveGame)
                    {
                        StartGameplayLoad(ResolveLoadGameSceneName());
                    }
                    break;
                case MainMenuAction.Settings:
                    panelSwitcher?.ShowSettings();
                    break;
                case MainMenuAction.Credits:
                    panelSwitcher?.ShowCredits();
                    break;
                case MainMenuAction.Exit:
                    panelSwitcher?.ShowConfirmDialog();
                    break;
                case MainMenuAction.Back:
                case MainMenuAction.CancelExit:
                    panelSwitcher?.ShowMain();
                    break;
                case MainMenuAction.ConfirmExit:
                    Application.Quit();
                    break;
            }
        }

        void StartGameplayLoad(string sceneName)
        {
            if (loadRoutine != null)
            {
                return;
            }

            loadRoutine = StartCoroutine(LoadSceneRoutine(sceneName));
        }

        IEnumerator LoadSceneRoutine(string sceneName)
        {
            panelSwitcher?.ShowLoading();
            yield return null;

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"Main menu could not load scene '{sceneName}'. Check Build Settings and MainMenuController.", this);
                panelSwitcher?.ShowMain();
                loadRoutine = null;
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        void ResolveReferences()
        {
            if (panelSwitcher == null)
            {
                panelSwitcher = GetComponent<MainMenuPanelSwitcher>();
            }
        }

        void ApplySaveAvailability()
        {
            if (continueButton != null)
            {
                continueButton.SetAvailable(HasSaveGame);
            }

            if (loadGameButton != null)
            {
                loadGameButton.SetAvailable(HasSaveGame);
            }
        }

        string ResolveNewGameSceneName()
        {
            return flowSettings != null ? flowSettings.NewGameSceneName : ResolveFallbackSceneName();
        }

        string ResolveContinueSceneName()
        {
            return flowSettings != null ? flowSettings.ContinueSceneName : ResolveFallbackSceneName();
        }

        string ResolveLoadGameSceneName()
        {
            return flowSettings != null ? flowSettings.LoadGameSceneName : ResolveFallbackSceneName();
        }

        string ResolveFallbackSceneName()
        {
            return string.IsNullOrWhiteSpace(gameplaySceneName) ? DefaultGameplaySceneName : gameplaySceneName.Trim();
        }
    }
}
