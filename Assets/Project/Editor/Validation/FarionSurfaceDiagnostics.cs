using System.Collections.Generic;
using System.Text;
using Farion.Rendering.Celestial;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Validation
{
    public static class FarionSurfaceDiagnostics
    {
        const string WorldZoneScenePath = "Assets/Project/Scenes/SC_WorldZone.unity";
        const int SampleCount = 4096;

        [MenuItem("Farion/Validation/Report Surface Formation Coverage")]
        public static void Report()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != WorldZoneScenePath)
            {
                EditorSceneManager.OpenScene(WorldZoneScenePath, OpenSceneMode.Single);
            }

            SurfaceFormationSpawner[] spawners =
                Object.FindObjectsByType<SurfaceFormationSpawner>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (spawners.Length == 0)
            {
                Debug.LogError("[Farion] No SurfaceFormationSpawner found in the scene.");
                return;
            }

            for (int i = 0; i < spawners.Length; i++)
            {
                ReportSpawner(spawners[i]);
            }
        }

        [MenuItem("Farion/Validation/Report Surface Relief")]
        public static void ReportRelief()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != WorldZoneScenePath)
            {
                EditorSceneManager.OpenScene(WorldZoneScenePath, OpenSceneMode.Single);
            }

            PlanetSurfaceModel[] models = Object.FindObjectsByType<PlanetSurfaceModel>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < models.Length; i++)
            {
                ReportRelief(models[i]);
            }
        }

        static void ReportRelief(PlanetSurfaceModel model)
        {
            CelestialBody body = model.Body;
            CelestialShapeProfile shape = model.ShapeProfile;
            if (body == null || shape == null)
            {
                return;
            }

            float radius = Mathf.Max(0.01f, body.Radius);
            StringBuilder report = new();
            report.AppendLine($"[Farion] {model.name} surface relief (radius {radius:F0} m)");
            float[] arcs = { 2.5f, 5f, 10f, 25f, 60f };
            float firstMedian = 0f;
            float lastMedian = 0f;
            for (int a = 0; a < arcs.Length; a++)
            {
                float arc = arcs[a];
                float step = arc / radius;
                float[] deviations = new float[512];
                for (int i = 0; i < deviations.Length; i++)
                {
                    Vector3 direction = FibonacciDirection(i, deviations.Length);
                    Vector3 reference = Mathf.Abs(direction.y) < 0.95f ? Vector3.up : Vector3.right;
                    Vector3 tangent = Vector3.Cross(reference, direction).normalized;
                    float before = shape.EvaluateRadius(
                        radius,
                        (direction - tangent * step).normalized);
                    float here = shape.EvaluateRadius(radius, direction);
                    float after = shape.EvaluateRadius(
                        radius,
                        (direction + tangent * step).normalized);
                    deviations[i] = Mathf.Abs(here - (before + after) * 0.5f) * 100f;
                }

                System.Array.Sort(deviations);
                float median = deviations[deviations.Length / 2];
                float p90 = deviations[Mathf.Min(deviations.Length - 1, deviations.Length * 9 / 10)];
                if (a == 0)
                {
                    firstMedian = median;
                }

                lastMedian = median;
                report.AppendLine($"  over {arc,5:F1} m : median {median,8:F1} cm   p90 {p90,9:F1} cm");
            }

            if (firstMedian > 0.0001f)
            {
                float exponent = Mathf.Log(lastMedian / firstMedian) / Mathf.Log(60f / 2.5f);
                report.AppendLine(
                    $"  scale exponent  : {exponent:F2} (natural terrain sits near 0.8-1.0)");
            }

            Debug.Log(report.ToString(), model);
        }

        static void ReportSpawner(SurfaceFormationSpawner spawner)
        {
            CelestialBody body = spawner.GetComponent<CelestialBody>();
            PlanetSurfaceModel model = spawner.GetComponent<PlanetSurfaceModel>();
            if (body == null || model == null || spawner.Profile == null)
            {
                Debug.LogError($"[Farion] '{spawner.name}' is missing body, surface model or profile.");
                return;
            }

            for (int r = 0; r < spawner.Profile.Rules.Count; r++)
            {
                SurfaceFormationRule rule = spawner.Profile.Rules[r];
                if (rule == null)
                {
                    continue;
                }

                int[] slopeBuckets = new int[5];
                float slopeSum = 0f;
                float slopeMax = 0f;
                float suitabilitySum = 0f;
                int suitableCount = 0;
                int sampled = 0;
                int geologyCount = 0;
                Dictionary<string, int> materialCounts = new();
                float strengthSum = 0f;
                float featureHeightSum = 0f;
                float convexitySum = 0f;
                float altitudeMin = float.PositiveInfinity;
                float altitudeMax = float.NegativeInfinity;

                for (int i = 0; i < SampleCount; i++)
                {
                    Vector3 direction = FibonacciDirection(i, SampleCount);
                    if (!model.TrySamplePlanetSurface(direction, out PlanetSurfaceSample sample))
                    {
                        continue;
                    }

                    sampled++;
                    altitudeMin = Mathf.Min(altitudeMin, sample.TerrainAltitude);
                    altitudeMax = Mathf.Max(altitudeMax, sample.TerrainAltitude);
                    float slope = sample.Surface.SlopeAngleDegrees;
                    slopeSum += slope;
                    slopeMax = Mathf.Max(slopeMax, slope);
                    slopeBuckets[slope < 15f ? 0 : slope < 30f ? 1 : slope < 45f ? 2 : slope < 60f ? 3 : 4]++;

                    if (model.TrySampleGeology(direction, out CelestialGeologySample geology) &&
                        geology.HasFeature)
                    {
                        geologyCount++;
                        strengthSum += geology.FeatureStrength;
                        featureHeightSum += geology.FeatureHeight;
                        convexitySum += geology.Convexity;
                    }

                    string materialName = sample.SurfaceMaterial.Material != null
                        ? sample.SurfaceMaterial.Material.name
                        : "<none>";
                    materialCounts.TryGetValue(materialName, out int materialCount);
                    materialCounts[materialName] = materialCount + 1;

                    float suitability = rule.Suitability.Evaluate(sample, 1f, false, 0f);
                    suitabilitySum += suitability;
                    if (suitability > 0.01f)
                    {
                        suitableCount++;
                    }
                }

                if (sampled == 0)
                {
                    Debug.LogError($"[Farion] '{spawner.name}' surface model returned no samples.");
                    continue;
                }

                float geologyScale = Mathf.Max(1, geologyCount);
                StringBuilder materials = new();
                foreach (KeyValuePair<string, int> pair in materialCounts)
                {
                    materials.Append(
                        $"\n    {pair.Key,-34} {Percent(pair.Value, sampled)}");
                }

                Debug.Log(
                    $"[Farion] {spawner.name} / {rule.DisplayName}\n" +
                    $"  samples          : {sampled}\n" +
                    $"  altitude min/max : {altitudeMin:F1} / {altitudeMax:F1} m\n" +
                    $"  slope mean/max   : {slopeSum / sampled:F1} / {slopeMax:F1} deg\n" +
                    $"  slope <15/15-30/30-45/45-60/60+ : " +
                    $"{Percent(slopeBuckets[0], sampled)} / {Percent(slopeBuckets[1], sampled)} / " +
                    $"{Percent(slopeBuckets[2], sampled)} / {Percent(slopeBuckets[3], sampled)} / " +
                    $"{Percent(slopeBuckets[4], sampled)}\n" +
                    $"  rule slope range : {rule.Suitability.SlopeRange.x:F0}-{rule.Suitability.SlopeRange.y:F0} deg\n" +
                    $"  suitability >0   : {Percent(suitableCount, sampled)} (mean {suitabilitySum / sampled:F3})\n" +
                    $"  geology feature  : {Percent(geologyCount, sampled)}\n" +
                    $"  feature strength : mean {strengthSum / geologyScale:F3}\n" +
                    $"  feature height   : mean {featureHeightSum / geologyScale:F1} m\n" +
                    $"  convexity        : mean {convexitySum / geologyScale:F2}\n" +
                    $"  spawn chance     : {rule.Distribution.SpawnChance:F2}\n" +
                    $"  visibility       : {rule.Distribution.NearVisibilityDistance:F0}-" +
                    $"{rule.Distribution.FarVisibilityDistance:F0} m at " +
                    $"{rule.Distribution.SpacingMeters:F0} m spacing\n" +
                    $"  surface materials:{materials}",
                    spawner);
            }
        }

        [MenuItem("Farion/Validation/Probe Surface Determinism")]
        public static void ProbeDeterminism()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != WorldZoneScenePath)
            {
                EditorSceneManager.OpenScene(WorldZoneScenePath, OpenSceneMode.Single);
            }

            PlanetSurfaceModel[] models = Object.FindObjectsByType<PlanetSurfaceModel>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < models.Length; i++)
            {
                ProbeDeterminism(models[i]);
            }
        }

        static void ProbeDeterminism(PlanetSurfaceModel model)
        {
            CelestialBody body = model.Body;
            if (body == null)
            {
                return;
            }

            const int SampleCount = 512;
            Quaternion original = body.transform.rotation;
            float[] temperature = new float[SampleCount];
            float[] moisture = new float[SampleCount];
            float[] snow = new float[SampleCount];
            for (int i = 0; i < SampleCount; i++)
            {
                if (!model.TrySamplePlanetSurface(FibonacciDirection(i, SampleCount), out PlanetSurfaceSample sample))
                {
                    continue;
                }

                temperature[i] = sample.Climate.TemperatureCelsius;
                moisture[i] = sample.Climate.EffectiveMoisture;
                snow[i] = sample.SurfaceState.SnowCover;
            }

            body.transform.rotation = original * Quaternion.Euler(0f, 137f, 11f);
            float worstTemperature = 0f;
            float worstMoisture = 0f;
            float worstSnow = 0f;
            for (int i = 0; i < SampleCount; i++)
            {
                if (!model.TrySamplePlanetSurface(FibonacciDirection(i, SampleCount), out PlanetSurfaceSample sample))
                {
                    continue;
                }

                worstTemperature = Mathf.Max(
                    worstTemperature,
                    Mathf.Abs(sample.Climate.TemperatureCelsius - temperature[i]));
                worstMoisture = Mathf.Max(
                    worstMoisture,
                    Mathf.Abs(sample.Climate.EffectiveMoisture - moisture[i]));
                worstSnow = Mathf.Max(
                    worstSnow,
                    Mathf.Abs(sample.SurfaceState.SnowCover - snow[i]));
            }

            body.transform.rotation = original;
            bool stable = worstTemperature < 0.01f && worstMoisture < 0.001f && worstSnow < 0.001f;
            string message =
                $"[Farion] {model.name} placement determinism under body rotation\n" +
                $"  temperature drift : {worstTemperature:F3} C\n" +
                $"  moisture drift    : {worstMoisture:F4}\n" +
                $"  snow drift        : {worstSnow:F4}\n" +
                $"  verdict           : {(stable ? "stable" : "ROTATION DEPENDENT")}";
            if (stable)
            {
                Debug.Log(message, model);
            }
            else
            {
                Debug.LogError(message, model);
            }
        }

        [MenuItem("Farion/Validation/Measure Formation Kit Prefabs")]
        public static void MeasureKitPrefabs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SurfaceFormationProfile"))
            {
                SurfaceFormationProfile profile =
                    AssetDatabase.LoadAssetAtPath<SurfaceFormationProfile>(
                        AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null)
                {
                    continue;
                }

                StringBuilder report = new();
                report.AppendLine($"[Farion] {profile.name} kit measurements");
                for (int r = 0; r < profile.Rules.Count; r++)
                {
                    SurfaceFormationRule rule = profile.Rules[r];
                    if (rule?.Kit == null)
                    {
                        continue;
                    }

                    report.AppendLine($"  {rule.DisplayName}");
                    for (int role = 0; role <= (int)SurfaceFormationRole.Debris; role++)
                    {
                        foreach (SurfaceFormationPiece piece in
                            rule.Kit.Resolve((SurfaceFormationRole)role))
                        {
                            if (piece?.Prefab == null)
                            {
                                continue;
                            }

                            MeasurePrefab(piece.Prefab, out float footprint, out float height);
                            report.AppendLine(
                                $"    {(SurfaceFormationRole)role,-9} {piece.Prefab.name,-18} " +
                                $"footprint {footprint:F3} (authored {piece.FootprintRadius:F3}) " +
                                $"height {height:F3} (authored {piece.PieceHeight:F3})");
                        }
                    }
                }

                Debug.Log(report.ToString(), profile);
            }
        }

        static void MeasurePrefab(GameObject prefab, out float footprintRadius, out float height)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                footprintRadius = 2f;
                height = 2f;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            footprintRadius = Mathf.Max(0.05f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            height = Mathf.Max(0.05f, bounds.size.y);
        }

        static string Percent(int count, int total)
        {
            return total <= 0 ? "0%" : $"{100f * count / total:F1}%";
        }

        static Vector3 FibonacciDirection(int index, int count)
        {
            float y = 1f - 2f * (index + 0.5f) / count;
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = index * 2.3999632f;
            return new Vector3(
                Mathf.Cos(angle) * radius,
                y,
                Mathf.Sin(angle) * radius);
        }
    }
}
