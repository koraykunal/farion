namespace Farion.Rendering.Celestial
{
    public interface ICelestialOceanLevelProvider
    {
        bool TryGetOceanLevel(out float oceanLevel);
    }
}
