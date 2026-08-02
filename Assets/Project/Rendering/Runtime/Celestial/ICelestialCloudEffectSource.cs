namespace Farion.Rendering.Celestial
{
    public interface ICelestialCloudEffectSource
    {
        bool TryGetCloudEffectData(out CelestialCloudEffectData data);
    }
}
