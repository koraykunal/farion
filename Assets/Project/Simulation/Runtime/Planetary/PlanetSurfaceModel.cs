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
        [SerializeField] PlanetaryGenerationProfile generationProfile;
        [SerializeField] CelestialShapeProfile shapeProfile;

        [Header("Stellar Heating")]
        [SerializeField] CelestialRadiationSource primaryRadiationSource;

        [Header("Sampling")]
        [SerializeField, Range(0.0005f, 0.05f)] float surfaceNormalSampleStep = 0.004f;

        public CelestialBody Body => ResolveBody();
        public PlanetaryGenerationProfile GenerationProfile => generationProfile;
        public CelestialShapeProfile ShapeProfile => ResolveShapeProfile();
        public float SurfaceNormalSampleStep => surfaceNormalSampleStep;
        public event System.Action Changed;

        const int TerrainRangeSampleCount = 512;
        const float GoldenAngleRadians = 2.39996323f;

        Vector2 terrainRadiusMinMax;
        float terrainRangeBaseRadius = -1f;

        public Vector2 TerrainRadiusMinMax => ResolveTerrainRadiusRange();

        public bool TryGetEnvironment(CelestialBody body, out CelestialEnvironmentSample sample)
        {
            sample = default;
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
                atmosphereBaseRadius);
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
                        FibonacciDirection(i, TerrainRangeSampleCount));
                    min = Mathf.Min(min, radius);
                    max = Mathf.Max(max, radius);
                }
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

        void OnValidate()
        {
            ResolveBody();
            surfaceNormalSampleStep = Mathf.Clamp(surfaceNormalSampleStep, 0.0005f, 0.05f);
            Changed?.Invoke();
        }

        public void Configure(PlanetaryGenerationProfile profile, CelestialShapeProfile shape)
        {
            if (profile != null)
            {
                generationProfile = profile;
            }

            shapeProfile = shape;
            Changed?.Invoke();
        }

        public void SetShapeProfile(CelestialShapeProfile shape)
        {
            shapeProfile = shape;
            Changed?.Invoke();
        }

        public bool TrySampleSurface(CelestialBody sourceBody, Vector3 position, out CelestialSurfaceSample sample)
        {
            if (TrySamplePlanetSurface(sourceBody, position, out PlanetSurfaceSample planetSample))
            {
                sample = planetSample.Surface;
                return true;
            }

            sample = default;
            return false;
        }

        public bool TrySamplePlanetSurface(CelestialBody sourceBody, Vector3 position, out PlanetSurfaceSample sample)
        {
            sample = default;
            CelestialBody source = ResolveBody();
            if (source == null || sourceBody == null || sourceBody != source)
            {
                return false;
            }

            Vector3 centerToPoint = position - source.Position;
            float centerDistance = centerToPoint.magnitude;
            Vector3 worldDirection = centerDistance > 0.0001f ? centerToPoint / centerDistance : source.transform.up;
            Vector3 localDirection = source.transform.InverseTransformDirection(worldDirection);
            localDirection = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            return TryBuildPlanetSurfaceSample(source, localDirection, centerDistance, out sample);
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

        bool TryBuildPlanetSurfaceSample(
            CelestialBody source,
            Vector3 localDirection,
            float centerDistance,
            out PlanetSurfaceSample sample)
        {
            sample = default;
            PlanetGenerationContext context = CreateContext(source);
            Vector3 worldDirection = source.transform.TransformDirection(localDirection).normalized;
            CelestialShapeProfile shape = ResolveShapeProfile();
            float surfaceRadius = EvaluateSurfaceRadius(source.Radius, localDirection, shape);
            Vector3 localSurfacePoint = localDirection * surfaceRadius;
            Vector3 worldSurfacePoint = source.transform.TransformPoint(localSurfacePoint);
            Vector3 worldNormal = source.transform.TransformDirection(EvaluateSurfaceNormal(source.Radius, localDirection, shape)).normalized;
            float slopeAngle = Vector3.Angle(worldDirection, worldNormal);
            float surfaceAltitude = centerDistance - surfaceRadius;
            float terrainAltitude = surfaceRadius - source.Radius;

            CelestialSurfaceSample surface = new(
                source,
                worldSurfacePoint,
                worldNormal,
                centerDistance,
                surfaceAltitude,
                slopeAngle);

            PlanetClimateSample climate = SampleClimate(
                source,
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
            if (source == null || generationProfile == null)
            {
                Debug.LogWarning($"{name}: planet surface validation skipped because body or generation profile is missing.", this);
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
                ? shape.EvaluateSample(bodyRadius, localDirection).Radius
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
            float step = Mathf.Clamp(surfaceNormalSampleStep, 0.0005f, 0.05f);

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
            return generationProfile != null && generationProfile.ShapeProfile != null
                ? generationProfile.ShapeProfile
                : shapeProfile;
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

