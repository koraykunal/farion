using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    public sealed class PlanetSurfaceModel : MonoBehaviour, ICelestialSurfaceProvider
    {
        [Header("Source")]
        [SerializeField] CelestialBody body;
        [SerializeField] PlanetaryGenerationProfile generationProfile;
        [SerializeField] CelestialShapeProfile shapeProfile;

        [Header("Stellar Heating")]
        [SerializeField] CelestialRadiationSource primaryRadiationSource;
        [SerializeField] PlanetThermalProfile thermalProfile;

        [Header("Climate")]
        [SerializeField] float polarTemperatureDrop = 35f;
        [SerializeField] float altitudeCoolingPerUnit = 0.45f;
        [SerializeField] float temperatureNoiseAmplitude = 8f;
        [SerializeField] float temperatureNoiseScale = 2.25f;
        [SerializeField] float moistureNoiseScale = 3f;
        [SerializeField] float atmosphereMoistureRetention = 0.35f;

        [Header("Sampling")]
        [SerializeField, Range(0.0005f, 0.05f)] float surfaceNormalSampleStep = 0.004f;

        public CelestialBody Body => ResolveBody();
        public PlanetaryGenerationProfile GenerationProfile => generationProfile;
        public CelestialShapeProfile ShapeProfile => ResolveShapeProfile();
        public float SurfaceNormalSampleStep => surfaceNormalSampleStep;
        public event System.Action Changed;

        void Awake()
        {
            ResolveBody();
        }

        void OnValidate()
        {
            ResolveBody();
            polarTemperatureDrop = Mathf.Max(0f, polarTemperatureDrop);
            altitudeCoolingPerUnit = Mathf.Max(0f, altitudeCoolingPerUnit);
            temperatureNoiseAmplitude = Mathf.Max(0f, temperatureNoiseAmplitude);
            temperatureNoiseScale = Mathf.Max(0.01f, temperatureNoiseScale);
            moistureNoiseScale = Mathf.Max(0.01f, moistureNoiseScale);
            atmosphereMoistureRetention = Mathf.Clamp01(atmosphereMoistureRetention);
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

            PlanetGenerationContext context = CreateContext(source);
            Vector3 centerToPoint = position - source.Position;
            float centerDistance = centerToPoint.magnitude;
            Vector3 worldDirection = centerDistance > 0.0001f ? centerToPoint / centerDistance : source.transform.up;
            Vector3 localDirection = source.transform.InverseTransformDirection(worldDirection);
            localDirection = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;

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
                worldSurfacePoint,
                worldNormal,
                terrainAltitude,
                slopeAngle);
            BiomeSample biome = generationProfile != null && generationProfile.BiomeDistribution != null
                ? generationProfile.BiomeDistribution.SampleBiome(context, climate, localDirection, terrainAltitude, slopeAngle)
                : new BiomeSample(null, climate.TemperatureCelsius, climate.Moisture, terrainAltitude, slopeAngle);
            TerrainFeatureSample terrainFeature = generationProfile != null && generationProfile.TerrainFeatureDistribution != null
                ? generationProfile.TerrainFeatureDistribution.SampleFeature(
                    context,
                    climate,
                    biome.Biome,
                    localDirection,
                    terrainAltitude,
                    slopeAngle)
                : new TerrainFeatureSample(null, 0f, terrainAltitude, slopeAngle);

            sample = new PlanetSurfaceSample(context, surface, localDirection, surfaceRadius, climate, biome, terrainFeature);
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
            Vector3 worldSurfacePoint,
            Vector3 worldNormal,
            float altitude,
            float slopeDegrees)
        {
            float latitudeDegrees = Mathf.Asin(Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            float polarFactor = Mathf.Pow(Mathf.Abs(localDirection.y), 1.5f);
            float temperatureNoise = SampleSignedDirectionalNoise(
                localDirection,
                temperatureNoiseScale,
                SeedUtility.Derive(context.PlanetSeed, "climate.temperature"));
            float moistureNoise = SampleDirectionalNoise(
                localDirection,
                moistureNoiseScale,
                SeedUtility.Derive(context.PlanetSeed, "climate.moisture"));

            float climateBias = context.Climate switch
            {
                ClimateType.Cold => -18f,
                ClimateType.Hot => 22f,
                ClimateType.Toxic => 10f,
                ClimateType.Irradiated => 6f,
                _ => 0f
            };
            CelestialInsolationSample insolation = CelestialInsolationSample.None;
            bool hasInsolation = primaryRadiationSource != null &&
                thermalProfile != null &&
                primaryRadiationSource.TrySampleInsolation(
                    sourceBody,
                    worldSurfacePoint,
                    worldNormal,
                    thermalProfile.BondAlbedo,
                    out insolation);

            float temperature = hasInsolation
                ? thermalProfile.EvaluateSurfaceTemperature(
                    context,
                    insolation,
                    climateBias,
                    polarFactor,
                    altitude,
                    temperatureNoise,
                    polarTemperatureDrop,
                    altitudeCoolingPerUnit,
                    temperatureNoiseAmplitude)
                : context.MeanTemperatureCelsius +
                    climateBias -
                    polarTemperatureDrop * polarFactor -
                    Mathf.Max(0f, altitude) * altitudeCoolingPerUnit +
                    temperatureNoise * temperatureNoiseAmplitude;

            float liquidMoisture = context.HasStableLiquidSurface ? context.LiquidCoverage : 0f;
            float atmosphereMoisture = context.HasAtmosphere ? context.AtmosphereDensity * atmosphereMoistureRetention : 0f;
            float moisture = Mathf.Clamp01(liquidMoisture + atmosphereMoisture + moistureNoise * 0.45f - Mathf.Max(0f, altitude) * 0.005f);
            float radiation = hasInsolation
                ? thermalProfile.EvaluateSurfaceRadiation(context, insolation, altitude)
                : Mathf.Clamp01(context.RadiationLevel + Mathf.Max(0f, altitude) * 0.002f + (context.HasAtmosphere ? 0f : 0.12f));

            return new PlanetClimateSample(
                context,
                localDirection,
                latitudeDegrees,
                altitude,
                slopeDegrees,
                temperature,
                moisture,
                radiation);
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

        static float SampleDirectionalNoise(Vector3 direction, float scale, int seed)
        {
            float u = Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f) + 0.5f;
            float v = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) / Mathf.PI + 0.5f;
            float seedOffset = (seed & 2047) * 0.00731f;
            return Mathf.PerlinNoise(u * Mathf.Max(0.01f, scale) + seedOffset, v * Mathf.Max(0.01f, scale) + seedOffset * 1.731f);
        }

        static float SampleSignedDirectionalNoise(Vector3 direction, float scale, int seed)
        {
            return SampleDirectionalNoise(direction, scale, seed) * 2f - 1f;
        }
    }
}

