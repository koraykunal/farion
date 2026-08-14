using Farion.Core.Identity;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.App.Flow
{
    [MovedFrom(true, "Farion.App.Flow", "Farion.App.Runtime")]
    [CreateAssetMenu(menuName = "Farion/Application/Game Flow Settings", fileName = "SO_GameFlowSettings")]
    public sealed class GameFlowSettings : ScriptableObject
    {
        const string DefaultGameplaySceneName = "SC_GameplayShell";
        const string DefaultMainMenuSceneName = "SC_MainMenu";

        [Header("Scenes")]
        [SerializeField] string mainMenuSceneName = DefaultMainMenuSceneName;
        [SerializeField] string gameplaySceneName = DefaultGameplaySceneName;

        public string MainMenuSceneName => IdentifierText.Normalize(mainMenuSceneName);
        public string GameplaySceneName => IdentifierText.Normalize(gameplaySceneName);

        void OnValidate()
        {
            mainMenuSceneName = EnsureSceneName(mainMenuSceneName, DefaultMainMenuSceneName);
            gameplaySceneName = EnsureSceneName(gameplaySceneName, DefaultGameplaySceneName);
        }

        static string EnsureSceneName(string sceneName, string defaultSceneName)
        {
            string normalizedSceneName = IdentifierText.Normalize(sceneName);
            return string.IsNullOrEmpty(normalizedSceneName) ? defaultSceneName : normalizedSceneName;
        }
    }
}
