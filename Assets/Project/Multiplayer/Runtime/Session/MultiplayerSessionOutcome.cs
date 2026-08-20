using UnityEngine;

namespace Farion.Multiplayer.Session
{
    public static class MultiplayerSessionOutcome
    {
        public static MultiplayerFailureReason PendingFailure { get; private set; } =
            MultiplayerFailureReason.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            PendingFailure = MultiplayerFailureReason.None;
        }

        public static void Report(MultiplayerFailureReason reason)
        {
            if (reason != MultiplayerFailureReason.None)
            {
                PendingFailure = reason;
            }
        }

        public static bool TryConsume(out MultiplayerFailureReason reason)
        {
            reason = PendingFailure;
            PendingFailure = MultiplayerFailureReason.None;
            return reason != MultiplayerFailureReason.None;
        }
    }
}
