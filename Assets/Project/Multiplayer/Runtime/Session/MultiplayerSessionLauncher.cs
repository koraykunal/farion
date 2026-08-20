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
                    out MultiplayerLaunchRequest request) ||
                !TryCreateSession(out MultiplayerSessionController controller))
            {
                return;
            }

            if (request.Mode == MultiplayerLaunchMode.Host)
            {
                controller.StartHost(request.Endpoint.Port);
            }
            else
            {
                controller.StartClient(request.Endpoint);
            }
        }

        public static bool TryCreateSession(
            out MultiplayerSessionController controller)
        {
            controller = MultiplayerSessionController.Active;
            if (controller != null)
            {
                return controller.CanStartSession;
            }

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
