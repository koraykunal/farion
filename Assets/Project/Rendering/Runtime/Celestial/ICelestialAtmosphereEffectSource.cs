namespace Farion.Rendering.Celestial
{
    public interface ICelestialAtmosphereEffectSource
    {
        bool TryGetAtmosphereEffectData(out CelestialAtmosphereEffectData data);
    }
}
