using System;
using UnityEngine;

namespace Farion.Gameplay.Input
{
    [DisallowMultipleComponent]
    public sealed class PlayerControlLock : MonoBehaviour
    {
        [SerializeField] PlayerControlLockReason activeReasons;

        public event Action<PlayerControlLockReason> Changed;
        public PlayerControlLockReason ActiveReasons => activeReasons;
        public bool IsGameplayInputLocked => activeReasons != PlayerControlLockReason.None;

        public bool HasLock(PlayerControlLockReason reason)
        {
            return reason != PlayerControlLockReason.None && (activeReasons & reason) != 0;
        }

        public void SetLocked(PlayerControlLockReason reason, bool locked)
        {
            if (reason == PlayerControlLockReason.None)
            {
                return;
            }

            PlayerControlLockReason nextReasons = locked
                ? activeReasons | reason
                : activeReasons & ~reason;

            if (nextReasons == activeReasons)
            {
                return;
            }

            activeReasons = nextReasons;
            Changed?.Invoke(activeReasons);
        }

        public void ClearAllLocks()
        {
            if (activeReasons == PlayerControlLockReason.None)
            {
                return;
            }

            activeReasons = PlayerControlLockReason.None;
            Changed?.Invoke(activeReasons);
        }
    }
}
