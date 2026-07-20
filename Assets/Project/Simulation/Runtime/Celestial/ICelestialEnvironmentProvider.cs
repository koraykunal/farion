using Farion.Core.Physics;

namespace Farion.Simulation.Celestial
{
    public interface ICelestialEnvironmentProvider
    {
        bool TryGetEnvironment(CelestialBody body, out CelestialEnvironmentSample sample);
    }
}
