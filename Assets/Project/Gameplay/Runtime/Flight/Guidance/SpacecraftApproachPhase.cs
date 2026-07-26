namespace Farion.Gameplay.Flight
{
    public enum SpacecraftApproachPhase
    {
        NoFrame = 0,
        BodyProximity = 10,
        Orbit = 20,
        Deorbiting = 30,
        AtmosphericFlight = 40,
        AtmosphericDescent = 50,
        HighDescent = 60,
        LowApproach = 70,
        TouchdownWindow = 80,
        UnsafeTouchdown = 90,
        Submerged = 100
    }
}
