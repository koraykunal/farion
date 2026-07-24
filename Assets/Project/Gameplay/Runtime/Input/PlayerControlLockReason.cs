using System;

namespace Farion.Gameplay.Input
{
    [Flags]
    public enum PlayerControlLockReason
    {
        None = 0,
        UserInterface = 1 << 0,
        Dialogue = 1 << 1,
        Interaction = 1 << 2,
        Cinematic = 1 << 3
    }
}
