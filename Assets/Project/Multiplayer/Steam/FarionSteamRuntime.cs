using Steamworks;
using UnityEngine;

namespace Farion.Multiplayer.Steam
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class FarionSteamRuntime : MonoBehaviour
    {
        static FarionSteamRuntime instance;

        SteamAPIWarningMessageHook_t warningHook;

        public static bool IsReady => instance != null && instance.initialized;

        bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject host = new(nameof(FarionSteamRuntime));
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<FarionSteamRuntime>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        void Initialize()
        {
            if (!Packsize.Test() || !DllCheck.Test())
            {
                Debug.LogError(
                    "Steamworks binaries do not match this platform; Steam features are disabled.");
                return;
            }

            try
            {
                initialized = SteamAPI.Init();
            }
            catch (System.DllNotFoundException exception)
            {
                Debug.LogError($"Steam library could not be loaded: {exception.Message}");
                return;
            }

            if (!initialized)
            {
                Debug.LogWarning(
                    "Steam is not running or the app id is unavailable; co-op falls back to direct connections.");
                return;
            }

            warningHook = OnSteamWarning;
            SteamClient.SetWarningMessageHook(warningHook);
            gameObject.AddComponent<SteamLobbyService>();
        }

        void Update()
        {
            if (initialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            if (initialized)
            {
                SteamAPI.Shutdown();
                initialized = false;
            }

            instance = null;
        }

        static void OnSteamWarning(int severity, System.Text.StringBuilder message)
        {
            Debug.LogWarning($"[Steam] {message}");
        }
    }
}
