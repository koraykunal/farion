using Farion.Gameplay.Character;
using Farion.Gameplay.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Authoring
{
    static class FarionAnimationArenaBuilder
    {
        const string ScenePath = "Assets/Project/Scenes/SC_AnimationArena.unity";
        const string PlayerPrefabPath =
            "Assets/Project/Prefabs/Gameplay/Character/PF_PlayerExplorerCore.prefab";
        const string WaterMaterialPath =
            "Assets/Project/Art/Materials/Characters/PlayerExplorer/MAT_PlayerExplorer_HelmetGlass.mat";

        [MenuItem("Farion/Character/Build Animation Arena")]
        static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            Object.DestroyImmediate(GameObject.Find("Main Camera"));

            Transform environment = new GameObject("Environment").transform;
            Transform parkour = new GameObject("Parkour").transform;
            Transform pool = new GameObject("SwimmingPool").transform;
            Transform teleportPoints = new GameObject("TeleportPoints").transform;

            Box("MainFloor", new Vector3(-8f, -0.5f, 0f), new Vector3(56f, 1f, 54f), environment);
            BuildParkour(parkour);
            BuildPool(pool);

            GameObject gravity = new("ArtificialGravity");
            gravity.transform.SetParent(environment);
            BoxCollider volume = gravity.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.center = new Vector3(0f, 20f, 0f);
            volume.size = new Vector3(90f, 80f, 70f);
            gravity.AddComponent<ArtificialGravityVolume>();

            GameObject playerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player =
                (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(
                new Vector3(0f, 1.5f, 0f),
                Quaternion.identity);
            if (player.GetComponent<ExplorerInput>() == null)
            {
                player.AddComponent<ExplorerInput>();
            }

            GameObject arenaCamera = new("ArenaCamera");
            arenaCamera.transform.position = new Vector3(-3f, 4f, -6f);
            arenaCamera.AddComponent<Camera>();
            arenaCamera.AddComponent<AudioListener>();
            AnimationArenaCameraRig cameraRig = arenaCamera.AddComponent<AnimationArenaCameraRig>();
            arenaCamera.tag = "MainCamera";
            SerializedObject cameraSerialized = new(cameraRig);
            cameraSerialized.FindProperty("target").objectReferenceValue =
                player.GetComponent<ExplorerMotor>();
            cameraSerialized.FindProperty("inputSource").objectReferenceValue =
                player.GetComponent<ExplorerInput>();
            cameraSerialized.ApplyModifiedPropertiesWithoutUndo();

            AnimationArenaBootstrap bootstrap =
                new GameObject("ArenaBootstrap").AddComponent<AnimationArenaBootstrap>();
            SerializedObject serialized = new(bootstrap);
            serialized.FindProperty("player").objectReferenceValue =
                player.GetComponent<ExplorerMotor>();
            SerializedProperty points = serialized.FindProperty("teleportPoints");
            points.arraySize = 3;
            points.GetArrayElementAtIndex(0).objectReferenceValue =
                Point("Teleport2m", new Vector3(13f, 3.4f, -13f), teleportPoints);
            points.GetArrayElementAtIndex(1).objectReferenceValue =
                Point("Teleport5m", new Vector3(13f, 6.4f, 0f), teleportPoints);
            points.GetArrayElementAtIndex(2).objectReferenceValue =
                Point("Teleport10m", new Vector3(13f, 11.4f, 14f), teleportPoints);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(
                $"Built {ScenePath}. Play: WASD moves, Space jumps/swims upward, " +
                "hold left mouse to turn the explorer, right mouse to orbit, " +
                "wheel zooms, F resets the camera, " +
                "1/2/3 selects drop heights, R respawns.");
        }

        static void BuildParkour(Transform parent)
        {
            Vector3[] steppingStones =
            {
                new(-27f, 0.15f, -13f), new(-24f, 0.35f, -13f),
                new(-21f, 0.55f, -13f), new(-18f, 0.25f, -13f),
                new(-15f, 0.7f, -13f), new(-12f, 0.4f, -13f)
            };
            for (int index = 0; index < steppingStones.Length; index++)
            {
                Box($"StepStone_{index + 1}", steppingStones[index], new Vector3(2f, 0.3f, 2f), parent);
            }

            for (int index = 0; index < 6; index++)
            {
                float height = (index + 1) * 0.45f;
                Box(
                    $"Stair_{index + 1}",
                    new Vector3(-26f + index * 1.5f, height * 0.5f, 12f),
                    new Vector3(1.5f, height, 5f),
                    parent);
            }

            Ramp("LongSlope", new Vector3(-13f, 0f, 12f), new Vector3(-3f, 3f, 12f), 4f, parent);
            Box("SlopeDeck", new Vector3(1f, 3f, 12f), new Vector3(8f, 0.5f, 5f), parent);
            Ramp("SlopeDown", new Vector3(5f, 3f, 12f), new Vector3(10f, 0f, 12f), 4f, parent);

            Box("JumpPlatform_1", new Vector3(-7f, 0.6f, -13f), new Vector3(3f, 1.2f, 4f), parent);
            Box("JumpPlatform_2", new Vector3(-2f, 1.1f, -13f), new Vector3(3f, 2.2f, 4f), parent);
            Box("JumpPlatform_3", new Vector3(3.5f, 1.6f, -13f), new Vector3(3f, 3.2f, 4f), parent);

            Box("DropTower_2m", new Vector3(13f, 1f, -13f), new Vector3(6f, 2f, 6f), parent);
            Box("DropTower_5m", new Vector3(13f, 2.5f, 0f), new Vector3(6f, 5f, 6f), parent);
            Box("DropTower_10m", new Vector3(13f, 5f, 14f), new Vector3(6f, 10f, 6f), parent);
        }

        static void BuildPool(Transform parent)
        {
            Box("PoolFloor", new Vector3(28f, -3.5f, 0f), new Vector3(16f, 1f, 14f), parent);
            Box("PoolFarWall", new Vector3(36.5f, -1.5f, 0f), new Vector3(1f, 4f, 16f), parent);
            Box("PoolLeftWall", new Vector3(28f, -1.5f, -7.5f), new Vector3(17f, 4f, 1f), parent);
            Box("PoolRightWall", new Vector3(28f, -1.5f, 7.5f), new Vector3(17f, 4f, 1f), parent);
            Ramp("PoolEntryRamp", new Vector3(19.5f, 0f, 0f), new Vector3(25f, -3f, 0f), 5f, parent);

            GameObject water = new("WaterVolume");
            water.transform.SetParent(parent);
            water.transform.position = new Vector3(28f, -1.45f, 0f);
            BoxCollider waterVolume = water.AddComponent<BoxCollider>();
            waterVolume.isTrigger = true;
            waterVolume.size = new Vector3(16f, 3.1f, 14f);
            water.AddComponent<ArtificialWaterVolume>();

            GameObject surface = Box(
                "WaterSurface",
                new Vector3(28f, 0.05f, 0f),
                new Vector3(16f, 0.05f, 14f),
                parent);
            Object.DestroyImmediate(surface.GetComponent<BoxCollider>());
            surface.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
        }

        static GameObject Box(
            string name,
            Vector3 position,
            Vector3 scale,
            Transform parent)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent);
            box.transform.position = position;
            box.transform.localScale = scale;
            return box;
        }

        static void Ramp(
            string name,
            Vector3 bottom,
            Vector3 top,
            float width,
            Transform parent)
        {
            Vector3 direction = top - bottom;
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            ramp.transform.SetParent(parent);
            ramp.transform.position = (bottom + top) * 0.5f;
            ramp.transform.rotation =
                Quaternion.LookRotation(direction.normalized, Vector3.up);
            ramp.transform.localScale = new Vector3(width, 0.3f, direction.magnitude);
        }

        static Transform Point(string name, Vector3 position, Transform parent)
        {
            GameObject point = new(name);
            point.transform.SetParent(parent);
            point.transform.position = position;
            return point.transform;
        }
    }
}
