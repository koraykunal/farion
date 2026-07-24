using UnityEngine;

namespace Farion.Simulation.Celestial
{
    public readonly struct CelestialShapeSample
    {
        public CelestialShapeSample(float radius, Vector4 shadingData)
        {
            Radius = radius;
            ShadingData = shadingData;
        }

        public float Radius { get; }
        public Vector4 ShadingData { get; }
    }
}
