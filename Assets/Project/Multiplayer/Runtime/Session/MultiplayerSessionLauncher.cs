using System;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    public static class MultiplayerSessionLauncher
    {
        const string SessionPrefabPath = "Multiplayer/PF_NetworkSessionRoot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void StartFromCommandLine()
        {
            if (!MultiplayerCommandLine.TryParse(
                    Environment.GetCommandLineArgs(),
                    out MultiplayerLaunchMode mode,
                    out MultiplayerEndpoint endpoint) ||
                !TryCreateSession(out MultiplayerSessionController controller))
            {
                return;
            }

            if (mode == MultiplayerLaunchMode.Host)
            {
                controller.StartHost(endpoint.Port);
            }
            else
            {
                controller.StartClient(endpoint);
            }
        }

        public static bool TryCreateSession(
            out MultiplayerSessionController controller)
        {
            MultiplayerSessionController active = MultiplayerSessionController.Active;
            if (active != null)
            {
                controller = active.CanStartSession ? active : null;
                return controller != null;
            }

            controller = null;
            GameObject prefab = Resources.Load<GameObject>(SessionPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing Resources/{SessionPrefabPath}.prefab.");
                return false;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            controller = instance.GetComponent<MultiplayerSessionController>();
            if (controller != null)
            {
                return true;
            }

            Debug.LogError(
                $"{prefab.name} has no {nameof(MultiplayerSessionController)}.");
            UnityEngine.Object.Destroy(instance);
            return false;
        }
    }
}
