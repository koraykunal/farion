using UnityEngine;

namespace Farion.Gameplay.Session
{
    public static class GameplaySessionModeRequest
    {
        static bool pending;
        static GameplaySessionMode requestedMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            pending = false;
            requestedMode = GameplaySessionMode.Offline;
        }

        public static void Request(GameplaySessionMode mode)
        {
            requestedMode = mode;
            pending = true;
        }

        public static void Cancel()
        {
            pending = false;
            requestedMode = GameplaySessionMode.Offline;
        }

        public static GameplaySessionMode RequestedOrDefault =>
            pending ? requestedMode : GameplaySessionMode.Offline;

        public static GameplaySessionMode ConsumeOrDefault()
        {
            if (!pending)
            {
                return GameplaySessionMode.Offline;
            }

            pending = false;
            return requestedMode;
        }
    }
}
