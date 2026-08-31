using Farion.Rendering.Celestial;
using Farion.Simulation.Planetary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Authoring
{
    static class FarionSurfaceMapBakeAuthoring
    {
        const string ScenePath = "Assets/Project/Scenes/SC_WorldZone.unity";
        const string MapFolderParent = "Assets/Project/Design/Rendering/Celestial";
        const string MapFolderName = "SurfaceMaps";
        const string MapFolder = MapFolderParent + "/" + MapFolderName;
        const int BakeResolution = 256;

        [MenuItem("Farion/Authoring/Bake Planet Surface Maps")]
        public static void Bake()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"Could not open {ScenePath}.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MapFolder))
            {
                AssetDatabase.CreateFolder(MapFolderParent, MapFolderName);
            }

            int baked = 0;
            foreach (TerrestrialPlanetVisual planetVisual in Object.FindObjectsByType<TerrestrialPlanetVisual>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (BakePlanet(planetVisual))
                {
                    baked++;
                }
            }

            if (baked == 0)
            {
                Debug.LogError($"{ScenePath}: no planet produced a surface map bake.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Baked surface maps for {baked} planet(s) at {BakeResolution}px.");
        }

        static bool BakePlanet(TerrestrialPlanetVisual planetVisual)
        {
            if (planetVisual.Profile == null
                || planetVisual.Profile.SurfaceProfile == null
                || planetVisual.Profile.SurfaceProfile.SurfaceVisualProfile == null
                || !planetVisual.TryGetComponent(out PlanetSurfaceModel surfaceModel)
                || !planetVisual.TryGetComponent(out CelestialBodyVisual bodyVisual))
            {
                return false;
            }

            SurfaceVisualProfile visualProfile = planetVisual.Profile.SurfaceProfile.SurfaceVisualProfile;
            if (!PlanetSurfaceWeightMap.CanBake(surfaceModel, visualProfile))
            {
                return false;
            }

            string assetPath = $"{MapFolder}/SM_{planetVisual.name.Replace(" ", string.Empty)}.asset";
            PlanetSurfaceMapSet mapSet = EnsureMapSet(assetPath);
            PlanetSurfaceWeightMap.Bake(
                surfaceModel,
                visualProfile,
                mapSet.WeightsA,
                mapSet.WeightsB,
                mapSet.SurfaceState);
            PlanetSurfaceWeightMap.BakeNormals(surfaceModel, mapSet.SurfaceNormal);
            EditorUtility.SetDirty(mapSet);

            SerializedObject serialized = new(bodyVisual);
            serialized.FindProperty("bakedSurfaceMaps").objectReferenceValue = mapSet;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bodyVisual);
            Debug.Log($"Baked {assetPath}.");
            return true;
        }

        static PlanetSurfaceMapSet EnsureMapSet(string assetPath)
        {
            PlanetSurfaceMapSet mapSet = AssetDatabase.LoadAssetAtPath<PlanetSurfaceMapSet>(assetPath);
            if (mapSet != null
                && mapSet.IsComplete
                && mapSet.WeightsA.width == BakeResolution
                && mapSet.WeightsA.format == TextureFormat.RGBA32)
            {
                return mapSet;
            }

            if (mapSet != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            mapSet = ScriptableObject.CreateInstance<PlanetSurfaceMapSet>();
            AssetDatabase.CreateAsset(mapSet, assetPath);
            Cubemap mapA = CreateMap("Weights A");
            Cubemap mapB = CreateMap("Weights B");
            Cubemap stateMap = CreateMap("Surface State");
            Cubemap normalMap = CreateMap("Surface Normal");
            AssetDatabase.AddObjectToAsset(mapA, mapSet);
            AssetDatabase.AddObjectToAsset(mapB, mapSet);
            AssetDatabase.AddObjectToAsset(stateMap, mapSet);
            AssetDatabase.AddObjectToAsset(normalMap, mapSet);
            mapSet.SetMaps(mapA, mapB, stateMap, normalMap);
            EditorUtility.SetDirty(mapSet);
            return mapSet;
        }

        static Cubemap CreateMap(string mapName)
        {
            return new Cubemap(BakeResolution, TextureFormat.RGBA32, true)
            {
                name = mapName,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 1
            };
        }
    }
}
