namespace Farion.Gameplay.Flight
{
    public interface ISpacecraftInputSource
    {
        SpacecraftInputState CurrentInput { get; }
    }
}
