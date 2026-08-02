using UnityEngine;

namespace Farion.App.Flow
{
    [CreateAssetMenu(menuName = "Farion/Application/Game Flow Settings", fileName = "SO_GameFlowSettings")]
    public sealed class GameFlowSettings : ScriptableObject
    {
        const string DefaultGameplaySceneName = "SC_PhysicsSandbox";
        const string DefaultMainMenuSceneName = "SC_MainMenu";

        [Header("Scenes")]
        [SerializeField] string mainMenuSceneName = DefaultMainMenuSceneName;
        [SerializeField] string newGameSceneName = DefaultGameplaySceneName;
        [SerializeField] string continueSceneName = DefaultGameplaySceneName;
        [SerializeField] string loadGameSceneName = DefaultGameplaySceneName;

        public string MainMenuSceneName => NormalizeSceneName(mainMenuSceneName);
        public string NewGameSceneName => NormalizeSceneName(newGameSceneName);
        public string ContinueSceneName => NormalizeSceneName(continueSceneName);
        public string LoadGameSceneName => NormalizeSceneName(loadGameSceneName);

        void OnValidate()
        {
            mainMenuSceneName = EnsureSceneName(mainMenuSceneName, DefaultMainMenuSceneName);
            newGameSceneName = EnsureSceneName(newGameSceneName, DefaultGameplaySceneName);
            continueSceneName = EnsureSceneName(continueSceneName, DefaultGameplaySceneName);
            loadGameSceneName = EnsureSceneName(loadGameSceneName, DefaultGameplaySceneName);
        }

        static string EnsureSceneName(string sceneName, string defaultSceneName)
        {
            string normalizedSceneName = NormalizeSceneName(sceneName);
            return string.IsNullOrEmpty(normalizedSceneName) ? defaultSceneName : normalizedSceneName;
        }

        static string NormalizeSceneName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
        }
    }
}
