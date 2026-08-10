using Farion.Core.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.App.Flow
{
    public static class GameFlowService
    {
        public static AsyncOperation LoadGameplaySceneAsync(
            string sceneName,
            SaveGameStartupMode startupMode,
            string requestedSlotName = null)
        {
            string normalizedSceneName = NormalizeSceneName(sceneName);
            if (!CanLoadScene(normalizedSceneName))
            {
                return null;
            }

            switch (startupMode)
            {
                case SaveGameStartupMode.LoadGame:
                    SaveGameStartupRequest.RequestLoad(requestedSlotName);
                    break;
                case SaveGameStartupMode.NewGame:
                    SaveGameStartupRequest.RequestNewGame();
                    break;
            }

            return SceneManager.LoadSceneAsync(normalizedSceneName, LoadSceneMode.Single);
        }

        public static AsyncOperation LoadMainMenuAsync(GameFlowSettings settings)
        {
            return settings != null
                ? LoadSceneAsync(settings.MainMenuSceneName)
                : null;
        }

        public static AsyncOperation LoadSceneAsync(string sceneName)
        {
            string normalizedSceneName = NormalizeSceneName(sceneName);
            return CanLoadScene(normalizedSceneName)
                ? SceneManager.LoadSceneAsync(normalizedSceneName, LoadSceneMode.Single)
                : null;
        }

        public static void Quit()
        {
            UnityEngine.Application.Quit();
        }

        static bool CanLoadScene(string sceneName)
        {
            if (!string.IsNullOrEmpty(sceneName) &&
                UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
            {
                return true;
            }

            Debug.LogError($"Cannot load scene '{sceneName}'. Add it to the active build profile.");
            return false;
        }

        static string NormalizeSceneName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
        }
    }
}
