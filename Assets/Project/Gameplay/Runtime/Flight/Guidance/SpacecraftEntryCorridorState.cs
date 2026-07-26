namespace Farion.Gameplay.Flight
{
    public enum SpacecraftEntryCorridorState
    {
        NoFrame = 0,
        NoAtmosphere = 10,
        OutsideAtmosphere = 20,
        SafeEntry = 30,
        ShallowEntry = 40,
        SteepEntry = 50,
        Overspeed = 60,
        Impacting = 70,
        Escaping = 80
    }
}
