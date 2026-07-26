namespace Farion.Gameplay.Flight
{
    public enum SpacecraftLandingGuidanceCommand
    {
        None = 0,
        EstablishOrbit = 10,
        PlanRetroBurn = 20,
        ReduceVerticalSpeed = 30,
        ReduceTangentialSpeed = 40,
        SeekLevelSurface = 45,
        HoldAttitude = 50,
        CommitTouchdown = 60,
        AbortLanding = 70
    }
}
