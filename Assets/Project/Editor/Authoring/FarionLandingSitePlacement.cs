using System;
using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Editor.Authoring
{
    static class FarionLandingSitePlacement
    {
        const int CandidateCount = 8192;
        const float MaximumSlopeDegrees = 12f;
        const float MinimumFreeboardMeters = 25f;

        public static float OrientToBestLandingSite(CelestialBody planet)
        {
            PlanetSurfaceModel surfaceModel = planet.GetComponent<PlanetSurfaceModel>();
            SurfaceDecorationRenderer decoration =
                planet.GetComponent<SurfaceDecorationRenderer>();
            if (surfaceModel == null || decoration == null || decoration.Profile == null)
            {
                throw new InvalidOperationException(
                    $"'{planet.BodyName}' needs a surface model and a decoration profile " +
                    "before a landing site can be chosen.");
            }

            ResolveOcean(planet, out bool hasOcean, out float oceanRadius);

            Vector3 bestDirection = Vector3.up;
            float bestRadius = planet.Radius;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < CandidateCount; i++)
            {
                Vector3 direction = FibonacciDirection(i, CandidateCount);
                if (!surfaceModel.TrySamplePlanetSurface(
                        direction,
                        out PlanetSurfaceSample sample))
                {
                    continue;
                }

                float score = ScoreCandidate(
                    sample,
                    decoration.Profile,
                    hasOcean,
                    oceanRadius);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestDirection = direction;
                bestRadius = sample.SurfaceRadius;
            }

            if (bestScore <= 0f)
            {
                throw new InvalidOperationException(
                    $"No landing site on '{planet.BodyName}' satisfies the slope, freeboard and " +
                    "decoration requirements. Retune the shape profile before placing the start.");
            }

            planet.transform.rotation = Quaternion.FromToRotation(bestDirection, Vector3.up);
            Debug.Log(
                $"[LandingSite] '{planet.BodyName}' oriented to a site at radius {bestRadius:0.0} " +
                $"(elevation {bestRadius - planet.Radius:0.0} m, score {bestScore:0.000}).");
            return bestRadius;
        }

        static float ScoreCandidate(
            in PlanetSurfaceSample sample,
            SurfaceDecorationProfile profile,
            bool hasOcean,
            float oceanRadius)
        {
            if (sample.Surface.SlopeAngleDegrees > MaximumSlopeDegrees)
            {
                return float.NegativeInfinity;
            }

            if (hasOcean && sample.SurfaceRadius < oceanRadius + MinimumFreeboardMeters)
            {
                return float.NegativeInfinity;
            }

            float decorationScore = 0f;
            int suitableRules = 0;
            foreach (SurfaceDecorationRule rule in profile.Rules)
            {
                if (!rule.Suitability.AllowsBiome(sample.Biome.Biome))
                {
                    continue;
                }

                float score = rule.Suitability.Evaluate(
                    sample,
                    sample.Biome.Suitability,
                    hasOcean,
                    oceanRadius);
                if (score <= 0f)
                {
                    continue;
                }

                suitableRules++;
                decorationScore += score;
            }

            if (suitableRules <= 0)
            {
                return float.NegativeInfinity;
            }

            return decorationScore + suitableRules;
        }

        static void ResolveOcean(
            CelestialBody planet,
            out bool hasOcean,
            out float oceanRadius)
        {
            hasOcean = false;
            oceanRadius = 0f;
            foreach (MonoBehaviour behaviour in planet.GetComponents<MonoBehaviour>())
            {
                if (behaviour is not ICelestialEnvironmentProvider provider ||
                    !provider.TryGetEnvironment(planet, out CelestialEnvironmentSample environment))
                {
                    continue;
                }

                hasOcean = environment.HasOcean;
                oceanRadius = environment.OceanRadius;
                return;
            }
        }

        static Vector3 FibonacciDirection(int index, int count)
        {
            float y = 1f - 2f * (index + 0.5f) / count;
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = index * Mathf.PI * (3f - Mathf.Sqrt(5f));
            return new Vector3(radius * Mathf.Cos(theta), y, radius * Mathf.Sin(theta));
        }
    }
}
