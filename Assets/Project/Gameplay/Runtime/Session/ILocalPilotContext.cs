using System;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;

namespace Farion.Gameplay.Session
{
    public interface ILocalPilotContext
    {
        PlayerPossessionMode CurrentMode { get; }
        SpacecraftPilotCameraView CurrentPilotCameraView { get; }
        SpacecraftMotor PilotedSpacecraftMotor { get; }
        event Action<PlayerPossessionMode> ModeChanged;
    }
}
