using System.Collections.Generic;
using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Farion.Tests.EditMode
{
    public sealed class SurfaceFormationTests
    {
        const string FormationProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_TerrestrialSurfaceFormation.asset";
        const string DecorationProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_TerrestrialSurfaceDecoration.asset";

        [Test]
        public void VisibilityDistance_IsDeterministicAndStaysInsideItsLadder()
        {
            SurfaceScatterDistribution distribution = LoadFormationProfile()
                .Rules[0]
                .Distribution;
            int resolution = SurfaceScatterPlacement.CalculateResolution(
                1600f,
                distribution.SpacingMeters);

            HashSet<float> distinct = new();
            for (int i = 0; i < 64; i++)
            {
                SurfaceScatterCell cell = SurfaceScatterPlacement.CellFromDirection(
                    new Vector3(Mathf.Cos(i * 0.37f), Mathf.Sin(i * 0.71f), Mathf.Sin(i * 0.29f)),
                    resolution);
                float first = SurfaceScatterPlacement.ResolveVisibilityDistance(
                    distribution,
                    4065,
                    "surfaceformation.cliffwall",
                    cell);
                float repeated = SurfaceScatterPlacement.ResolveVisibilityDistance(
                    distribution,
                    4065,
                    "surfaceformation.cliffwall",
                    cell);

                Assert.That(repeated, Is.EqualTo(first));
                Assert.That(first, Is.GreaterThanOrEqualTo(distribution.NearVisibilityDistance));
                Assert.That(first, Is.LessThanOrEqualTo(distribution.FarVisibilityDistance));
                distinct.Add(first);
            }

            Assert.That(
                distinct.Count,
                Is.GreaterThan(8),
                "The visibility ladder must spread cells across the near-to-far range.");
        }

        [Test]
        public void VisibilityDistance_ChangesWithSeedAndRule()
        {
            SurfaceScatterDistribution distribution = LoadFormationProfile()
                .Rules[0]
                .Distribution;
            SurfaceScatterCell cell = SurfaceScatterPlacement.CellFromDirection(
                new Vector3(0.41f, 0.62f, -0.67f),
                SurfaceScatterPlacement.CalculateResolution(1600f, distribution.SpacingMeters));

            float baseline = SurfaceScatterPlacement.ResolveVisibilityDistance(
                distribution, 4065, "surfaceformation.cliffwall", cell);
            float otherSeed = SurfaceScatterPlacement.ResolveVisibilityDistance(
                distribution, 4066, "surfaceformation.cliffwall", cell);
            float otherRule = SurfaceScatterPlacement.ResolveVisibilityDistance(
                distribution, 4065, "surfaceformation.rockoutcrop", cell);

            Assert.That(baseline, Is.Not.EqualTo(otherSeed));
            Assert.That(baseline, Is.Not.EqualTo(otherRule));
        }

        [Test]
        public void GeologyWeight_RisesWithBreakStrength()
        {
            SurfaceFormationRule rule = LoadFormationProfile().Rules[0];

            float previous = -1f;
            foreach (float strength in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                float weight = rule.EvaluateGeologyWeight(
                    new CelestialGeologySample(
                        strength,
                        18f,
                        Vector3.up,
                        rule.PreferredConvexity));
                Assert.That(
                    weight,
                    Is.GreaterThan(previous),
                    $"Weight must rise monotonically; broke at strength {strength}.");
                previous = weight;
            }

            Assert.That(
                rule.EvaluateGeologyWeight(CelestialGeologySample.None),
                Is.LessThanOrEqualTo(1f - rule.GeologyRequirement + 0.0001f));
        }

        [Test]
        public void GeologyWeight_PrefersTheConfiguredConvexity()
        {
            SurfaceFormationRule rule = LoadFormationProfile().Rules[0];
            if (rule.ConvexityBias <= 0f)
            {
                Assert.Ignore("Rule does not use convexity.");
            }

            float aligned = rule.EvaluateGeologyWeight(
                new CelestialGeologySample(0.8f, 18f, Vector3.up, rule.PreferredConvexity));
            float opposed = rule.EvaluateGeologyWeight(
                new CelestialGeologySample(0.8f, 18f, Vector3.up, -1f));

            Assert.That(aligned, Is.GreaterThan(opposed));
        }

        static void AssertLadder(string displayName, SurfaceScatterDistribution distribution)
        {
            Assert.That(
                distribution.NearVisibilityDistance,
                Is.LessThanOrEqualTo(distribution.FarVisibilityDistance),
                $"'{displayName}' has an inverted visibility ladder.");
            Assert.That(
                distribution.ResolveReleaseDistance(distribution.FarVisibilityDistance),
                Is.GreaterThan(distribution.FarVisibilityDistance),
                $"'{displayName}' releases before it leaves view.");
            Assert.That(distribution.SpawnChance, Is.GreaterThan(0f), displayName);
        }

        static SurfaceFormationProfile LoadFormationProfile()
        {
            SurfaceFormationProfile profile =
                AssetDatabase.LoadAssetAtPath<SurfaceFormationProfile>(FormationProfilePath);
            Assert.That(profile, Is.Not.Null, FormationProfilePath);
            return profile;
        }
    }
}
