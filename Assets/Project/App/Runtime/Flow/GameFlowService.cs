using Farion.Core.Identity;
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
            string normalizedSceneName = IdentifierText.Normalize(sceneName);
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

        public static AsyncOperation LoadSceneAsync(string sceneName)
        {
            string normalizedSceneName = IdentifierText.Normalize(sceneName);
            return CanLoadScene(normalizedSceneName)
                ? SceneManager.LoadSceneAsync(normalizedSceneName, LoadSceneMode.Single)
                : null;
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
    }
}
