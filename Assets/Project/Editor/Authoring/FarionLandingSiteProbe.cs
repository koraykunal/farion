using Farion.Rendering.Celestial;
using Farion.Simulation.Planetary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Authoring
{
    static class FarionLandingSiteProbe
    {
        const string ScenePath = "Assets/Project/Scenes/SC_WorldZone.unity";

        [MenuItem("Farion/Validation/Probe Landing Site")]
        public static void Probe()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SurfaceDecorationRenderer renderer in
                    root.GetComponentsInChildren<SurfaceDecorationRenderer>(true))
                {
                    ReportRenderer(renderer);
                }
            }
        }

        static void ReportRenderer(SurfaceDecorationRenderer renderer)
        {
            PlanetSurfaceModel model = renderer.GetComponent<PlanetSurfaceModel>();
            if (model == null ||
                !model.TrySamplePlanetSurface(
                    renderer.transform.InverseTransformDirection(Vector3.up),
                    out PlanetSurfaceSample sample))
            {
                Debug.Log($"[LandingProbe] {renderer.gameObject.name}: no sample.");
                return;
            }

            string biome = sample.Biome.Biome != null ? sample.Biome.Biome.name : "<none>";
            Debug.Log(
                $"[LandingProbe] {renderer.gameObject.name} " +
                $"surfaceRadius={sample.SurfaceRadius:0.0} " +
                $"bodyRadius={sample.Surface.Body.Radius:0.0} " +
                $"terrainAltitude={sample.SurfaceRadius - sample.Surface.Body.Radius:0.0} " +
                $"slope={sample.Surface.SlopeAngleDegrees:0.0} " +
                $"biome={biome} suitability={sample.Biome.Suitability:0.000} " +
                $"temperature={sample.Climate.TemperatureCelsius:0.0} " +
                $"moisture={sample.Climate.EffectiveMoisture:0.000}");

            if (renderer.Profile == null)
            {
                return;
            }

            foreach (SurfaceDecorationRule rule in renderer.Profile.Rules)
            {
                bool allowsBiome = rule.Suitability.AllowsBiome(sample.Biome.Biome);
                float score = rule.Suitability.Evaluate(
                    sample,
                    sample.Biome.Suitability,
                    false,
                    0f);
                Debug.Log(
                    $"[LandingProbe]   rule allowsBiome={allowsBiome} score={score:0.000}");
            }
        }
    }
}
