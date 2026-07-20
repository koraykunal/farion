namespace Farion.Gameplay.Character
{
    public interface IFirstPersonInputSource
    {
        FirstPersonInputState CurrentInput { get; }
    }
}
