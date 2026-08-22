using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Authoring
{
    static class FarionCelestialLayoutAuthoring
    {
        const string ScenePath = "Assets/Project/Scenes/SC_WorldZone.unity";
        const string MoonDefinitionPath =
            "Assets/Project/Design/Simulation/Physics/SO_StartingMoon.asset";
        const string MoonShapeProfilePath =
            "Assets/Project/Design/Simulation/Celestial/SO_MoonCraterShapeProfile.asset";

        const float MoonOrbitRadius = 13000f;
        const int PlanetLod0Resolution = 96;
        const int MoonLod0Resolution = 80;

        [MenuItem("Farion/Authoring/Apply Celestial Layout")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"Could not open {ScenePath}.");
                return;
            }

            CelestialBody[] bodies = Object.FindObjectsByType<CelestialBody>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            CelestialBody star = FindBody(bodies, "Starting Star");
            CelestialBody planet = FindBody(bodies, "Starting Planet");
            CelestialBody moon = FindBody(bodies, "Starting Moon");
            CelestialBody ember = FindBody(bodies, "Ember");
            CelestialBody rime = FindBody(bodies, "Rime");
            if (star == null || planet == null || moon == null || ember == null || rime == null)
            {
                Debug.LogError($"{ScenePath}: expected celestial bodies are missing.");
                return;
            }

            ApplyMoonOrbit(planet, moon);
            ApplySurfaceFootprint(ember, AngularFootprint(PlanetLod0Resolution));
            ApplySurfaceFootprint(rime, AngularFootprint(PlanetLod0Resolution));
            EnsureMoonSurfaceModel(star, moon);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Celestial layout applied.");
        }

        static void ApplyMoonOrbit(CelestialBody planet, CelestialBody moon)
        {
            CelestialBodyDefinition planetDefinition = ResolveDefinition(planet);
            CelestialBodyDefinition moonDefinition = AssetDatabase
                .LoadAssetAtPath<CelestialBodyDefinition>(MoonDefinitionPath);
            if (planetDefinition == null || moonDefinition == null)
            {
                Debug.LogError("Planet or moon celestial body definition is missing.");
                return;
            }

            Vector3 offsetDirection = Vector3.right;
            moon.transform.position =
                planet.transform.position + offsetDirection * MoonOrbitRadius;

            float planetMu = planetDefinition.SurfaceGravity *
                planetDefinition.Radius * planetDefinition.Radius;
            float circularSpeed = Mathf.Sqrt(planetMu / MoonOrbitRadius);
            Vector3 velocityDirection = Vector3.Cross(Vector3.down, offsetDirection).normalized;
            Vector3 moonVelocity =
                planetDefinition.InitialVelocity + velocityDirection * circularSpeed;

            SerializedObject serializedDefinition = new(moonDefinition);
            serializedDefinition.FindProperty("initialVelocity").vector3Value = moonVelocity;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(moonDefinition);

            CelestialBodyDefinitionAuthoring authoring =
                moon.GetComponent<CelestialBodyDefinitionAuthoring>();
            if (authoring != null)
            {
                authoring.Apply();
            }

            EditorUtility.SetDirty(moon);
        }

        static void ApplySurfaceFootprint(CelestialBody body, float footprint)
        {
            PlanetSurfaceModel surfaceModel = body.GetComponent<PlanetSurfaceModel>();
            if (surfaceModel == null)
            {
                Debug.LogError($"{body.name} has no PlanetSurfaceModel.");
                return;
            }

            SerializedObject serialized = new(surfaceModel);
            serialized.FindProperty("surfaceSampleFootprint").floatValue = footprint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(surfaceModel);
        }

        static void EnsureMoonSurfaceModel(CelestialBody star, CelestialBody moon)
        {
            CelestialShapeProfile shapeProfile = AssetDatabase
                .LoadAssetAtPath<CelestialShapeProfile>(MoonShapeProfilePath);
            if (shapeProfile == null)
            {
                Debug.LogError($"Missing moon shape profile at {MoonShapeProfilePath}.");
                return;
            }

            PlanetSurfaceModel surfaceModel = moon.GetComponent<PlanetSurfaceModel>();
            if (surfaceModel == null)
            {
                surfaceModel = moon.gameObject.AddComponent<PlanetSurfaceModel>();
            }

            SerializedObject serialized = new(surfaceModel);
            serialized.FindProperty("body").objectReferenceValue = moon;
            serialized.FindProperty("shapeProfile").objectReferenceValue = shapeProfile;
            serialized.FindProperty("primaryRadiationSource").objectReferenceValue =
                star.GetComponent<CelestialRadiationSource>();
            serialized.FindProperty("surfaceSampleFootprint").floatValue =
                AngularFootprint(MoonLod0Resolution);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(surfaceModel);

            CelestialFrameProvider provider = Object.FindObjectsByType<CelestialFrameProvider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None) is { Length: > 0 } providers
                ? providers[0]
                : null;
            if (provider == null)
            {
                Debug.LogError("No CelestialFrameProvider found in the scene.");
                return;
            }

            SerializedObject serializedProvider = new(provider);
            SerializedProperty sources = serializedProvider.FindProperty("surfaceSources");
            for (int i = 0; i < sources.arraySize; i++)
            {
                if (sources.GetArrayElementAtIndex(i).objectReferenceValue == surfaceModel)
                {
                    return;
                }
            }

            sources.arraySize++;
            sources.GetArrayElementAtIndex(sources.arraySize - 1).objectReferenceValue =
                surfaceModel;
            serializedProvider.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(provider);
        }

        static CelestialBodyDefinition ResolveDefinition(CelestialBody body)
        {
            CelestialBodyDefinitionAuthoring authoring =
                body.GetComponent<CelestialBodyDefinitionAuthoring>();
            return authoring != null ? authoring.Definition : null;
        }

        static CelestialBody FindBody(CelestialBody[] bodies, string bodyName)
        {
            foreach (CelestialBody body in bodies)
            {
                if (body != null && body.BodyName == bodyName)
                {
                    return body;
                }
            }

            return null;
        }

        static float AngularFootprint(int lod0Resolution)
        {
            return Mathf.PI * 0.5f / (lod0Resolution + 1f);
        }
    }
}
