using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Farion.Tests.EditMode
{
    public sealed class SurfaceDecorationTests
    {
        const string ProfilePath =
            "Assets/Project/Design/Rendering/Celestial/SO_TerrestrialSurfaceDecoration.asset";

        [Test]
        public void SurfaceCellPlacement_IsDeterministicAndSeeded()
        {
            int resolution = SurfaceScatterPlacement.CalculateResolution(1600f, 11f);
            SurfaceScatterCell cell = SurfaceScatterPlacement.CellFromDirection(
                new Vector3(0.63f, 0.71f, -0.31f),
                resolution);

            Vector3 first = SurfaceScatterPlacement.CandidateDirection(cell, 4065, "surfaceflora.birch");
            Vector3 repeated = SurfaceScatterPlacement.CandidateDirection(cell, 4065, "surfaceflora.birch");
            Vector3 otherSeed = SurfaceScatterPlacement.CandidateDirection(cell, 4066, "surfaceflora.birch");

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(Vector3.Distance(first, otherSeed), Is.GreaterThan(0.000001f));
            Assert.That(first.magnitude, Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void PlacementUp_BlendsFromLocalGravityToTerrainNormal()
        {
            Vector3 radialUp = Vector3.right;
            Vector3 surfaceNormal = new Vector3(1f, 1f, 0f).normalized;

            Assert.That(
                SurfaceScatterPlacement.ResolvePlacementUp(radialUp, surfaceNormal, 0f),
                Is.EqualTo(radialUp).Using(Vector3EqualityComparer.Instance));
            Assert.That(
                SurfaceScatterPlacement.ResolvePlacementUp(radialUp, surfaceNormal, 1f),
                Is.EqualTo(surfaceNormal).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void TerrestrialPlacementStaysActiveDuringLowAltitudeFlight()
        {
            SurfaceDecorationProfile profile = LoadProfile();

            Assert.That(SurfaceDecorationRenderer.ShouldSuspendPlacement(0f, 0f, profile), Is.False);
            Assert.That(
                SurfaceDecorationRenderer.ShouldSuspendPlacement(120f, 0f, profile),
                Is.False);
            Assert.That(
                SurfaceDecorationRenderer.ShouldSuspendPlacement(0f, profile.PlacementPauseAltitude, profile),
                Is.True);
        }

        [Test]
        public void VisibilityAndLodTransitionsHaveStableBands()
        {
            Assert.That(
                SurfaceDecorationRenderer.ResolveVisibilityScale(100f, 100f, 120f),
                Is.EqualTo(1f));
            Assert.That(
                SurfaceDecorationRenderer.ResolveVisibilityScale(120f, 100f, 120f),
                Is.EqualTo(0f));
            Assert.That(
                SurfaceDecorationRenderer.ResolveVisibilityScale(110f, 100f, 120f),
                Is.InRange(0f, 1f));

            Assert.That(
                SurfaceDecorationRenderer.ResolveFarLod(false, 105f, 100f),
                Is.False);
            Assert.That(
                SurfaceDecorationRenderer.ResolveFarLod(false, 111f, 100f),
                Is.True);
            Assert.That(
                SurfaceDecorationRenderer.ResolveFarLod(true, 95f, 100f),
                Is.True);
            Assert.That(
                SurfaceDecorationRenderer.ResolveFarLod(true, 89f, 100f),
                Is.False);
        }

        [Test]
        public void CellsInsideReleaseDistanceAreNeverReclaimed()
        {
            Vector3 anchor = Vector3.up * 1600f;
            Vector3 scanCenter = anchor + Vector3.forward * 80f;

            Assert.That(
                SurfaceDecorationRenderer.IsCellReleased(
                    anchor + Vector3.forward * 100f,
                    anchor,
                    scanCenter,
                    150f),
                Is.False);
            Assert.That(
                SurfaceDecorationRenderer.IsCellReleased(
                    anchor + Vector3.forward * 220f,
                    anchor,
                    scanCenter,
                    150f),
                Is.False);
            Assert.That(
                SurfaceDecorationRenderer.IsCellReleased(
                    anchor - Vector3.forward * 200f,
                    anchor,
                    scanCenter,
                    150f),
                Is.True);
        }

        [Test]
        public void MeshGroundingPlacesTheRotatedBoundsOnTheSurface()
        {
            Bounds bounds = new(new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 2f));

            Assert.That(
                SurfaceDecorationRenderer.ResolveMeshGroundingOffset(
                    bounds,
                    Quaternion.identity,
                    Vector3.up,
                    2f),
                Is.EqualTo(0f).Within(0.000001f));
            Assert.That(
                SurfaceDecorationRenderer.ResolveMeshGroundingOffset(
                    bounds,
                    Quaternion.Euler(0f, 0f, 90f),
                    Vector3.up,
                    2f),
                Is.EqualTo(2f).Within(0.000001f));
        }

        [Test]
        public void RestingRotationRollsTallSlabsOntoTheirSideAndLeavesSquatRocksAlone()
        {
            Bounds slab = new(Vector3.zero, new Vector3(0.6f, 3f, 0.6f));
            Bounds boulder = new(Vector3.zero, new Vector3(2f, 2.2f, 1.8f));
            Quaternion upright = Quaternion.AngleAxis(37f, Vector3.up);
            Vector3 slopeUp = new Vector3(0.2f, 1f, 0.1f).normalized;

            Quaternion rested = SurfaceDecorationRenderer.ResolveRestingRotation(slab, upright, slopeUp);
            Assert.That(Mathf.Abs(Vector3.Dot(rested * Vector3.up, slopeUp)), Is.LessThan(0.01f));
            Assert.That(
                SurfaceDecorationRenderer.ResolveRestingRotation(slab, rested, slopeUp),
                Is.EqualTo(rested));
            Assert.That(
                SurfaceDecorationRenderer.ResolveRestingRotation(boulder, upright, slopeUp),
                Is.EqualTo(upright));
        }

        [Test]
        public void FootprintSeatTiltsToTheTerrainUnderTheWholeFootprintAndSinksToItsLowestEdge()
        {
            const float radius = 2000f;
            Vector3 direction = Vector3.up;
            Vector3 slopeAxis = Vector3.right;
            float rise = Mathf.Tan(20f * Mathf.Deg2Rad);
            float Sloped(Vector3 d) => radius + Vector3.Dot(d, slopeAxis) * radius * rise;
            float Flat(Vector3 d) => radius;

            bool resolved = SurfaceScatterPlacement.TryResolveFootprintSeat(
                Sloped, direction, radius, 3f, 1f,
                out Vector3 normal, out Vector3 up, out float drop);
            Assert.That(resolved, Is.True);
            Assert.That(Vector3.Angle(normal, direction), Is.EqualTo(20f).Within(0.5f));
            Assert.That(Vector3.Dot(normal, slopeAxis), Is.LessThan(0f));
            Assert.That(up, Is.EqualTo(normal));
            Assert.That(drop, Is.EqualTo(0f).Within(0.02f));

            SurfaceScatterPlacement.TryResolveFootprintSeat(
                Sloped, direction, radius, 3f, 0f,
                out _, out Vector3 radialUp, out float radialDrop);
            Assert.That(radialUp, Is.EqualTo(direction));
            Assert.That(radialDrop, Is.EqualTo(-3f * rise).Within(0.05f));

            SurfaceScatterPlacement.TryResolveFootprintSeat(
                Flat, direction, radius, 3f, 1f,
                out Vector3 flatNormal, out _, out float flatDrop);
            Assert.That(Vector3.Angle(flatNormal, direction), Is.LessThan(0.1f));
            Assert.That(flatDrop, Is.EqualTo(0f).Within(0.01f));
            Assert.That(
                SurfaceScatterPlacement.TryResolveFootprintSeat(
                    Flat, direction, radius, 0f, 1f, out _, out _, out _),
                Is.False);
        }

        [Test]
        public void SurfaceAnchorOnlyMorphsWhenItsPatchDoes()
        {
            CelestialSurfaceAnchor pinned = new(6, 2000f, 2003f, Vector4.zero);
            Assert.That(pinned.ResolveRadius(500f), Is.EqualTo(2000f));

            CelestialSurfaceAnchor morphing = new(6, 2000f, 2004f, new Vector4(50f, 100f, 0f, 0f));
            Assert.That(morphing.ResolveRadius(10f), Is.EqualTo(2000f));
            Assert.That(morphing.ResolveRadius(75f), Is.EqualTo(2002f).Within(0.001f));
            Assert.That(morphing.ResolveRadius(500f), Is.EqualTo(2004f));
        }

        [Test]
        public void GlobalLodMeshesCanShareOneSurfaceSamplingFootprint()
        {
            FootprintShapeProfile shape = ScriptableObject.CreateInstance<FootprintShapeProfile>();
            float footprint = CelestialSphereMeshBuilder.CalculateAngularSampleFootprint(80);
            Mesh coarse = null;
            Mesh detailed = null;
            try
            {
                coarse = CelestialSphereMeshBuilder.Build(
                    120f,
                    20,
                    "Coarse LOD",
                    shape,
                    angularSampleFootprint: footprint);
                detailed = CelestialSphereMeshBuilder.Build(
                    120f,
                    80,
                    "Detailed LOD",
                    shape,
                    angularSampleFootprint: footprint);

                Assert.That(
                    coarse.vertices[0].magnitude,
                    Is.EqualTo(detailed.vertices[0].magnitude).Within(0.000001f));
                Assert.That(
                    coarse.vertices[0].magnitude,
                    Is.EqualTo(120f + footprint).Within(0.000001f));
            }
            finally
            {
                Object.DestroyImmediate(coarse);
                Object.DestroyImmediate(detailed);
                Object.DestroyImmediate(shape);
            }
        }

        [Test]
        [Category("Content")]
        public void TerrestrialProfile_HasUniqueRuleIdsAndInstancedVariants()
        {
            SurfaceDecorationProfile profile = LoadProfile();
            System.Collections.Generic.HashSet<string> ids = new();

            foreach (SurfaceDecorationRule rule in profile.Rules)
            {
                Assert.That(rule, Is.Not.Null);
                Assert.That(ids.Add(rule.StableId), Is.True, $"Duplicate rule id '{rule.StableId}'.");
                Assert.That(rule.Variants.Count, Is.GreaterThan(0));
                foreach (SurfaceDecorationVariant variant in rule.Variants)
                {
                    Assert.That(variant.NearMesh, Is.Not.Null);
                    Assert.That(variant.FarMesh, Is.Not.Null);
                    Assert.That(variant.Materials.Count, Is.GreaterThan(0));
                    foreach (Material material in variant.Materials)
                    {
                        Assert.That(material, Is.Not.Null);
                        Assert.That(material.enableInstancing, Is.True);
                    }
                }
            }
        }

        [Test]
        public void TemperateBirchSuitability_RejectsOceanCoveredSurface()
        {
            SurfaceDecorationRule birch = LoadProfile().Rules[0];
            GameObject owner = new("Surface Decoration Suitability Test");
            try
            {
                CelestialBody body = owner.AddComponent<CelestialBody>();
                PlanetGenerationContext context = new(
                    4065,
                    1600f,
                    9.81f,
                    PlanetType.Rocky,
                    1f,
                    0.05f);
                PlanetClimateSample climate = new(
                    context,
                    Vector3.up,
                    90f,
                    10f,
                    10f,
                    15f,
                    0.7f,
                    0.7f,
                    0.3f,
                    0.1f);
                CelestialSurfaceSample surface = new(
                    body,
                    Vector3.up * 1610f,
                    Vector3.up,
                    1610f,
                    0f,
                    10f);
                PlanetSurfaceSample sample = new(
                    context,
                    surface,
                    Vector3.up,
                    1610f,
                    climate,
                    new BiomeSample(null, 1f),
                    default,
                    new PlanetSurfaceStateSample(0f, 0f, 0.7f),
                    default);

                Assert.That(birch.Suitability.Evaluate(sample, 1f, false, 0f), Is.GreaterThan(0f));
                Assert.That(birch.Suitability.Evaluate(sample, 1f, true, 1610f), Is.EqualTo(0f));
                Assert.That(birch.Suitability.Evaluate(sample, 0f, false, 0f), Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        static SurfaceDecorationProfile LoadProfile()
        {
            SurfaceDecorationProfile profile =
                AssetDatabase.LoadAssetAtPath<SurfaceDecorationProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null, ProfilePath);
            return profile;
        }

        sealed class FootprintShapeProfile : CelestialShapeProfile
        {
            public override float EvaluateDisplacement(
                float baseRadius,
                Vector3 unitDirection) => 0f;

            public override CelestialShapeSample EvaluateSample(
                float baseRadius,
                Vector3 unitDirection,
                float angularSampleFootprint)
            {
                return new CelestialShapeSample(
                    baseRadius + angularSampleFootprint,
                    Vector4.zero);
            }
        }
    }
}
