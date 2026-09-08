using System.Collections.Generic;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    public sealed class PlanetSurfaceModel :
        MonoBehaviour,
        ICelestialSurfaceProvider,
        ICelestialEnvironmentProvider
    {
        [Header("Source")]
        [SerializeField] CelestialBody body;
        [Tooltip("Planet class. Every layer of this body is derived from the archetype templates and the seed; nothing else is authored per planet.")]
        [SerializeField] PlanetArchetype archetype;
        [SerializeField] int seed = 1;

        [Header("Stellar Heating")]
        [SerializeField] CelestialRadiationSource primaryRadiationSource;

        [Header("Sampling")]
        [SerializeField, Range(0.25f, 64f)] float surfaceNormalSampleMeters = 2.5f;
        [SerializeField, Range(0f, 0.05f)] float surfaceSampleFootprint = 0.001534f;

        public CelestialBody Body => ResolveBody();
        public PlanetArchetype Archetype => archetype;
        public int Seed => seed;
        public PlanetaryGenerationProfile GenerationProfile => ResolveGenerationProfile();
        public CelestialShapeProfile ShapeProfile => ResolveShapeProfile();
        public event System.Action Changed;

        const int TerrainRangeSampleCount = 512;
        const float GoldenAngleRadians = 2.39996323f;

        Vector2 terrainRadiusMinMax;
        float terrainRangeBaseRadius = -1f;
        PlanetaryGenerationProfile generationProfile;
        PlanetArchetype derivedArchetype;
        int derivedSeed;
        float derivedRadius;
        float derivedGravity;
        PlanetArchetype subscribedArchetype;
        CelestialShapeProfile subscribedShapeTemplate;

        public Vector2 TerrainRadiusMinMax => ResolveTerrainRadiusRange();

        public bool TryGetEnvironment(CelestialBody body, out CelestialEnvironmentSample sample)
        {
            sample = default;
            PlanetaryGenerationProfile generationProfile = ResolveGenerationProfile();
            if (body == null || body != ResolveBody() || generationProfile == null)
            {
                return false;
            }

            float bodyRadius = Mathf.Max(0.01f, body.Radius);
            Vector2 terrainRange = ResolveTerrainRadiusRange();

            PlanetHydrosphereProfile hydrosphere = generationProfile.HydrosphereProfile;
            bool hasOcean = hydrosphere != null && hydrosphere.HasSurfaceOcean;
            float oceanRadius = hasOcean
                ? PlanetEnvironmentGeometry.GetOceanRadius(
                    bodyRadius,
                    terrainRange,
                    hydrosphere.OceanLevel,
                    hydrosphere.OceanRadiusOffset)
                : 0f;

            float atmosphereBaseRadius = hasOcean ? oceanRadius : bodyRadius;
            bool hasAtmosphere = generationProfile.HasAtmosphere;
            float atmosphereRadius = hasAtmosphere
                ? PlanetEnvironmentGeometry.GetAtmosphereRadius(
                    atmosphereBaseRadius,
                    generationProfile.AtmosphereScale,
                    generationProfile.AtmosphereRadiusOffset)
                : 0f;

            sample = new CelestialEnvironmentSample(
                body,
                hasOcean,
                oceanRadius,
                hasAtmosphere,
                atmosphereRadius,
                terrainRange,
                atmosphereBaseRadius,
                hasOcean ? hydrosphere.WaveAmplitude : 0f,
                hasOcean ? hydrosphere.WaveLength : 1f,
                hasOcean ? hydrosphere.WaveSpeed : 0f);
            return hasOcean || hasAtmosphere;
        }

        Vector2 ResolveTerrainRadiusRange()
        {
            CelestialBody body = ResolveBody();
            float baseRadius = body != null ? Mathf.Max(0.01f, body.Radius) : 0f;
            if (baseRadius <= 0f)
            {
                return Vector2.zero;
            }

            if (Mathf.Approximately(terrainRangeBaseRadius, baseRadius) &&
                terrainRadiusMinMax.x > 0f)
            {
                return terrainRadiusMinMax;
            }

            CelestialShapeProfile shape = ResolveShapeProfile();
            float min = baseRadius;
            float max = baseRadius;
            if (shape != null)
            {
                min = float.PositiveInfinity;
                max = 0f;
                for (int i = 0; i < TerrainRangeSampleCount; i++)
                {
                    float radius = shape.EvaluateRadius(
                        baseRadius,
                        FibonacciDirection(i, TerrainRangeSampleCount),
                        surfaceSampleFootprint);
                    min = Mathf.Min(min, radius);
                    max = Mathf.Max(max, radius);
                }

                min = Mathf.Min(
                    min,
                    baseRadius + shape.EstimateTroughElevationMeters());
                max = Mathf.Max(
                    max,
                    baseRadius + shape.EstimatePeakElevationMeters());
            }

            terrainRadiusMinMax = new Vector2(Mathf.Max(0.01f, min), Mathf.Max(0.01f, max));
            terrainRangeBaseRadius = baseRadius;
            return terrainRadiusMinMax;
        }

        static Vector3 FibonacciDirection(int index, int count)
        {
            float y = 1f - 2f * (index + 0.5f) / count;
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = index * GoldenAngleRadians;
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }


        void Awake()
        {
            ResolveBody();
        }

        void OnEnable()
        {
            SyncSubscriptions();
        }

        void OnDisable()
        {
            UnsubscribeAll();
            ReleaseDerived();
        }

        void OnValidate()
        {
            ResolveBody();
            surfaceNormalSampleMeters = Mathf.Clamp(surfaceNormalSampleMeters, 0.25f, 64f);
            surfaceSampleFootprint = Mathf.Clamp(surfaceSampleFootprint, 0f, 0.05f);
            Changed?.Invoke();
        }

        public void Configure(PlanetArchetype newArchetype, int newSeed)
        {
            archetype = newArchetype;
            seed = newSeed;
            Changed?.Invoke();
        }

        PlanetaryGenerationProfile ResolveGenerationProfile()
        {
            CelestialBody source = ResolveBody();
            float radius = source != null ? source.Radius : 0f;
            float gravity = source != null ? source.SurfaceGravity : 0f;
            if (generationProfile != null &&
                derivedArchetype == archetype &&
                derivedSeed == seed &&
                Mathf.Approximately(derivedRadius, radius) &&
                Mathf.Approximately(derivedGravity, gravity))
            {
                return generationProfile;
            }

            ReleaseDerived();
            if (archetype == null)
            {
                return null;
            }

            generationProfile = archetype.Derive(seed, radius, gravity);
            derivedArchetype = archetype;
            derivedSeed = seed;
            derivedRadius = radius;
            derivedGravity = gravity;
            SyncSubscriptions();
            return generationProfile;
        }

        void ReleaseDerived()
        {
            generationProfile?.Release();
            generationProfile = null;
            derivedArchetype = null;
            terrainRangeBaseRadius = -1f;
        }

        void SyncSubscriptions()
        {
            if (subscribedArchetype != archetype)
            {
                if (subscribedArchetype != null)
                {
                    subscribedArchetype.Changed -= HandleTemplateChanged;
                }

                subscribedArchetype = archetype;
                if (subscribedArchetype != null)
                {
                    subscribedArchetype.Changed += HandleTemplateChanged;
                }
            }

            CelestialShapeProfile shapeTemplate = archetype != null ? archetype.ShapeTemplate : null;
            if (subscribedShapeTemplate == shapeTemplate)
            {
                return;
            }

            if (subscribedShapeTemplate != null)
            {
                subscribedShapeTemplate.Changed -= HandleTemplateChanged;
            }

            subscribedShapeTemplate = shapeTemplate;
            if (subscribedShapeTemplate != null)
            {
                subscribedShapeTemplate.Changed += HandleTemplateChanged;
            }
        }

        void UnsubscribeAll()
        {
            if (subscribedArchetype != null)
            {
                subscribedArchetype.Changed -= HandleTemplateChanged;
                subscribedArchetype = null;
            }

            if (subscribedShapeTemplate != null)
            {
                subscribedShapeTemplate.Changed -= HandleTemplateChanged;
                subscribedShapeTemplate = null;
            }
        }

        void HandleTemplateChanged()
        {
            ReleaseDerived();
            SyncSubscriptions();
            Changed?.Invoke();
        }

        public void SetSampleFootprint(float angularFootprint)
        {
            float clamped = Mathf.Clamp(angularFootprint, 0f, 0.05f);
            if (Mathf.Approximately(surfaceSampleFootprint, clamped))
            {
                return;
            }

            surfaceSampleFootprint = clamped;
            terrainRangeBaseRadius = -1f;
            Changed?.Invoke();
        }

        public bool TrySampleSurface(CelestialBody sourceBody, Vector3 position, out CelestialSurfaceSample sample)
        {
            sample = default;
            CelestialBody source = ResolveBody();
            if (source == null || sourceBody == null || sourceBody != source)
            {
                return false;
            }

            ResolveLocalDirection(
                source,
                position,
                out Vector3 localDirection,
                out float centerDistance);
            sample = BuildSurfaceGeometry(
                source,
                localDirection,
                centerDistance,
                out _,
                out _);
            return true;
        }

        public bool TrySampleGeology(
            Vector3 localDirection,
            out CelestialGeologySample geology)
        {
            CelestialShapeProfile shape = ResolveShapeProfile();
            CelestialBody sourceBody = ResolveBody();
            if (shape == null || sourceBody == null)
            {
                geology = CelestialGeologySample.None;
                return false;
            }

            return shape.TrySampleGeology(
                sourceBody.Radius,
                localDirection.normalized,
                out geology);
        }

        public bool TrySamplePlanetSurface(CelestialBody sourceBody, Vector3 position, out PlanetSurfaceSample sample)
        {
            sample = default;
            CelestialBody source = ResolveBody();
            if (source == null || sourceBody == null || sourceBody != source)
            {
                return false;
            }

            ResolveLocalDirection(
                source,
                position,
                out Vector3 localDirection,
                out float centerDistance);
            return TryBuildPlanetSurfaceSample(source, localDirection, centerDistance, out sample);
        }

        static void ResolveLocalDirection(
            CelestialBody source,
            Vector3 position,
            out Vector3 localDirection,
            out float centerDistance)
        {
            Vector3 centerToPoint = position - source.Position;
            centerDistance = centerToPoint.magnitude;
            Vector3 worldDirection = centerDistance > 0.0001f
                ? centerToPoint / centerDistance
                : source.transform.up;
            localDirection = source.transform.InverseTransformDirection(worldDirection);
            localDirection = localDirection.sqrMagnitude > 0.0001f
                ? localDirection.normalized
                : Vector3.up;
        }

        CelestialSurfaceSample BuildSurfaceGeometry(
            CelestialBody source,
            Vector3 localDirection,
            float centerDistance,
            out float surfaceRadius,
            out float slopeAngle)
        {
            CelestialShapeProfile shape = ResolveShapeProfile();
            surfaceRadius = EvaluateSurfaceRadius(source.Radius, localDirection, shape);
            Vector3 worldDirection = source.transform.TransformDirection(localDirection).normalized;
            Vector3 worldNormal = source.transform.TransformDirection(
                EvaluateSurfaceNormal(source.Radius, localDirection, shape)).normalized;
            slopeAngle = Vector3.Angle(worldDirection, worldNormal);
            return new CelestialSurfaceSample(
                source,
                source.transform.TransformPoint(localDirection * surfaceRadius),
                worldNormal,
                centerDistance,
                centerDistance - surfaceRadius,
                slopeAngle);
        }

        public bool TrySamplePlanetSurface(Vector3 localDirection, out PlanetSurfaceSample sample)
        {
            sample = default;
            CelestialBody source = ResolveBody();
            if (source == null)
            {
                return false;
            }

            Vector3 direction = localDirection.sqrMagnitude > 0.0001f
                ? localDirection.normalized
                : Vector3.up;
            float surfaceRadius = EvaluateSurfaceRadius(source.Radius, direction, ResolveShapeProfile());
            return TryBuildPlanetSurfaceSample(source, direction, surfaceRadius, out sample);
        }

        public bool TrySampleLocalRadius(Vector3 localDirection, out float surfaceRadius)
        {
            surfaceRadius = 0f;
            CelestialBody source = ResolveBody();
            if (source == null)
            {
                return false;
            }

            Vector3 direction = localDirection.sqrMagnitude > 0.0001f
                ? localDirection.normalized
                : Vector3.up;
            surfaceRadius = EvaluateSurfaceRadius(source.Radius, direction, ResolveShapeProfile());
            return true;
        }

        public bool TrySampleLocalTerrain(
            Vector3 localDirection,
            out float surfaceRadius,
            out Vector3 localNormal)
        {
            surfaceRadius = 0f;
            localNormal = Vector3.up;
            CelestialBody source = ResolveBody();
            if (source == null)
            {
                return false;
            }

            Vector3 direction = localDirection.sqrMagnitude > 0.0001f
                ? localDirection.normalized
                : Vector3.up;
            CelestialShapeProfile shape = ResolveShapeProfile();
            surfaceRadius = EvaluateSurfaceRadius(source.Radius, direction, shape);
            localNormal = EvaluateSurfaceNormal(source.Radius, direction, shape);
            return true;
        }

        bool TryBuildPlanetSurfaceSample(
            CelestialBody source,
            Vector3 localDirection,
            float centerDistance,
            out PlanetSurfaceSample sample)
        {
            sample = default;
            PlanetaryGenerationProfile generationProfile = ResolveGenerationProfile();
            PlanetGenerationContext context = CreateContext(source);
            CelestialSurfaceSample surface = BuildSurfaceGeometry(
                source,
                localDirection,
                centerDistance,
                out float surfaceRadius,
                out float slopeAngle);
            float terrainAltitude = surfaceRadius - source.Radius;

            PlanetClimateSample climate = SampleClimate(
                source,
                generationProfile,
                context,
                localDirection,
                terrainAltitude,
                slopeAngle);
            BiomeSample biome = generationProfile != null && generationProfile.BiomeDistribution != null
                ? generationProfile.BiomeDistribution.SampleBiome(context, climate, localDirection, terrainAltitude, slopeAngle)
                : new BiomeSample(null, 0f);
            SurfaceMaterialSample surfaceMaterial =
                generationProfile != null && generationProfile.SurfaceMaterialDistribution != null
                    ? generationProfile.SurfaceMaterialDistribution.SampleDominant(
                        context,
                        climate,
                        biome.Biome,
                        localDirection,
                        terrainAltitude,
                        slopeAngle)
                    : default;
            PlanetSurfaceStateSample surfaceState =
                generationProfile != null && generationProfile.SurfaceStateProfile != null
                    ? generationProfile.SurfaceStateProfile.Evaluate(
                        context,
                        climate,
                        localDirection,
                        slopeAngle)
                    : default;
            TerrainFeatureSample terrainFeature = generationProfile != null && generationProfile.TerrainFeatureDistribution != null
                ? generationProfile.TerrainFeatureDistribution.SampleFeature(
                    context,
                    climate,
                    biome.Biome,
                    localDirection,
                    terrainAltitude,
                    slopeAngle)
                : new TerrainFeatureSample(null, 0f, terrainAltitude, slopeAngle);

            sample = new PlanetSurfaceSample(
                context,
                surface,
                localDirection,
                surfaceRadius,
                climate,
                biome,
                surfaceMaterial,
                surfaceState,
                terrainFeature);
            return true;
        }

        public PlanetGenerationContext CreateContext(CelestialBody sourceBody = null)
        {
            CelestialBody source = sourceBody != null ? sourceBody : ResolveBody();
            PlanetaryGenerationProfile generationProfile = ResolveGenerationProfile();
            if (generationProfile == null)
            {
                return PlanetGenerationContext.CreateDefault(0);
            }

            return generationProfile.CreateContext(
                source != null ? source.Radius : 0f,
                source != null ? source.SurfaceGravity : 0f);
        }

#if UNITY_EDITOR
        [ContextMenu("Log Generation Validation Report")]
        public void LogValidationReport()
        {
            CelestialBody source = ResolveBody();
            PlanetaryGenerationProfile generationProfile = ResolveGenerationProfile();
            if (source == null || generationProfile == null)
            {
                Debug.LogWarning($"{name}: planet surface validation skipped because body or archetype is missing.", this);
                return;
            }

            List<PlanetGenerationValidationIssue> issues = new();
            generationProfile.CollectValidationIssues(source.Radius, source.SurfaceGravity, issues);
            if (issues.Count == 0)
            {
                Debug.Log($"{name}: planet surface model is compatible with physical body settings.", this);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                PlanetGenerationValidationIssue issue = issues[i];
                if (issue.Severity == PlanetGenerationValidationSeverity.Error)
                {
                    Debug.LogError($"{name}: {issue}", this);
                }
                else
                {
                    Debug.LogWarning($"{name}: {issue}", this);
                }
            }
        }
#endif

        PlanetClimateSample SampleClimate(
            CelestialBody sourceBody,
            PlanetaryGenerationProfile generationProfile,
            PlanetGenerationContext context,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            PlanetClimateProfile climateProfile = generationProfile != null
                ? generationProfile.ClimateProfile
                : null;
            CelestialInsolationSample insolation = CelestialInsolationSample.None;
            bool hasInsolation = primaryRadiationSource != null &&
                climateProfile != null &&
                climateProfile.ThermalProfile != null &&
                primaryRadiationSource.TrySampleInsolation(
                    sourceBody,
                    sourceBody.Position,
                    sourceBody.transform.TransformDirection(localDirection),
                    climateProfile.ThermalProfile.BondAlbedo,
                    out insolation);

            if (climateProfile != null)
            {
                return climateProfile.Evaluate(
                    context,
                    generationProfile != null ? generationProfile.HydrosphereProfile : null,
                    hasInsolation ? insolation : CelestialInsolationSample.None,
                    localDirection,
                    altitude,
                    slopeDegrees);
            }

            float latitudeDegrees = Mathf.Asin(Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            return new PlanetClimateSample(
                context,
                localDirection,
                latitudeDegrees,
                altitude,
                slopeDegrees,
                12f,
                0.3f,
                0.3f,
                0.7f,
                context.BackgroundRadiation);
        }

        float EvaluateSurfaceRadius(float bodyRadius, Vector3 localDirection, CelestialShapeProfile shape)
        {
            return shape != null
                ? shape.EvaluateSample(bodyRadius, localDirection, surfaceSampleFootprint).Radius
                : Mathf.Max(0.01f, bodyRadius);
        }

        Vector3 EvaluateSurfaceNormal(float bodyRadius, Vector3 localDirection, CelestialShapeProfile shape)
        {
            Vector3 tangentA = Vector3.ProjectOnPlane(Vector3.forward, localDirection);
            if (tangentA.sqrMagnitude <= 0.0001f)
            {
                tangentA = Vector3.ProjectOnPlane(Vector3.right, localDirection);
            }

            if (tangentA.sqrMagnitude <= 0.0001f)
            {
                return localDirection;
            }

            tangentA.Normalize();
            Vector3 tangentB = Vector3.Cross(localDirection, tangentA).normalized;
            float step = Mathf.Clamp(
                Mathf.Clamp(surfaceNormalSampleMeters, 0.25f, 64f) / Mathf.Max(0.01f, bodyRadius),
                0.00002f,
                0.05f);

            Vector3 pointA0 = EvaluateRelativeSurfacePoint(bodyRadius, (localDirection - tangentA * step).normalized, shape);
            Vector3 pointA1 = EvaluateRelativeSurfacePoint(bodyRadius, (localDirection + tangentA * step).normalized, shape);
            Vector3 pointB0 = EvaluateRelativeSurfacePoint(bodyRadius, (localDirection - tangentB * step).normalized, shape);
            Vector3 pointB1 = EvaluateRelativeSurfacePoint(bodyRadius, (localDirection + tangentB * step).normalized, shape);
            Vector3 normal = Vector3.Cross(pointA1 - pointA0, pointB1 - pointB0);

            if (normal.sqrMagnitude <= 0.0001f)
            {
                return localDirection;
            }

            normal.Normalize();
            return Vector3.Dot(normal, localDirection) >= 0f ? normal : -normal;
        }

        Vector3 EvaluateRelativeSurfacePoint(float bodyRadius, Vector3 localDirection, CelestialShapeProfile shape)
        {
            return localDirection * EvaluateSurfaceRadius(bodyRadius, localDirection, shape);
        }

        CelestialShapeProfile ResolveShapeProfile()
        {
            return ResolveGenerationProfile()?.ShapeProfile;
        }

        CelestialBody ResolveBody()
        {
            if (body == null)
            {
                body = GetComponent<CelestialBody>();
            }

            return body;
        }
    }
}
