namespace Farion.Rendering.Celestial
{
    public interface ICelestialOceanEffectSource
    {
        bool TryGetOceanEffectData(out CelestialOceanEffectData data);
    }
}
