using System;

namespace Farion.Gameplay.Flight
{
    [Flags]
    public enum SpacecraftLandingRiskFlags
    {
        None = 0,
        NoFrame = 1 << 0,
        Descending = 1 << 1,
        InsideAtmosphere = 1 << 2,
        BelowOceanLevel = 1 << 3,
        ExcessiveVerticalSpeed = 1 << 4,
        ExcessiveTangentialSpeed = 1 << 5,
        ImpactRisk = 1 << 6,
        TouchdownCandidate = 1 << 7,
        ExcessiveSurfaceSlope = 1 << 8
    }
}
