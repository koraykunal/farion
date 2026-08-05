using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    public interface ICelestialSurfaceProvider
    {
        bool TrySampleSurface(CelestialBody body, Vector3 position, out CelestialSurfaceSample sample);
    }
}
