using UnityEngine;

namespace Farion.UI.MainMenu
{
    [CreateAssetMenu(menuName = "Farion/UI/Game Flow Settings", fileName = "SO_GameFlowSettings")]
    public sealed class GameFlowSettings : ScriptableObject
    {
        const string DefaultGameplaySceneName = "SC_PhysicsSandbox";

        [Header("Scenes")]
        [SerializeField] string newGameSceneName = DefaultGameplaySceneName;
        [SerializeField] string continueSceneName = DefaultGameplaySceneName;
        [SerializeField] string loadGameSceneName = DefaultGameplaySceneName;

        [Header("Save State")]
        [SerializeField] bool hasSaveGame;

        public bool HasSaveGame => hasSaveGame;
        public string NewGameSceneName => ResolveSceneName(newGameSceneName);
        public string ContinueSceneName => ResolveSceneName(continueSceneName);
        public string LoadGameSceneName => ResolveSceneName(loadGameSceneName);

        void OnValidate()
        {
            newGameSceneName = ResolveSceneName(newGameSceneName);
            continueSceneName = ResolveSceneName(continueSceneName);
            loadGameSceneName = ResolveSceneName(loadGameSceneName);
        }

        static string ResolveSceneName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? DefaultGameplaySceneName : sceneName.Trim();
        }
    }
}
