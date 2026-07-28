using System.Collections.Generic;

namespace Farion.Simulation.Planetary
{
    public readonly struct SurfaceMaterialSample
    {
        public SurfaceMaterialSample(SurfaceMaterialDefinition material, float suitability)
        {
            Material = material;
            Suitability = suitability;
        }

        public SurfaceMaterialDefinition Material { get; }
        public float Suitability { get; }
    }

    public readonly struct SurfaceMaterialWeight
    {
        public SurfaceMaterialWeight(SurfaceMaterialDefinition material, float weight)
        {
            Material = material;
            Weight = weight;
        }

        public SurfaceMaterialDefinition Material { get; }
        public float Weight { get; }
    }

    public static class SurfaceMaterialWeightUtility
    {
        public static void Normalize(List<SurfaceMaterialWeight> weights, float total)
        {
            if (weights == null || total <= 0f)
            {
                return;
            }

            for (int i = 0; i < weights.Count; i++)
            {
                SurfaceMaterialWeight weight = weights[i];
                weights[i] = new SurfaceMaterialWeight(weight.Material, weight.Weight / total);
            }
        }
    }
}
