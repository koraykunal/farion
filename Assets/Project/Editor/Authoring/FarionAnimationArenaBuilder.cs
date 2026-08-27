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

            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));
            Box("Tower2m", new Vector3(12f, 1f, -10f), new Vector3(6f, 2f, 6f));
            Box("Tower5m", new Vector3(-12f, 2.5f, -10f), new Vector3(6f, 5f, 6f));
            Box("Tower10m", new Vector3(0f, 5f, 18f), new Vector3(6f, 10f, 6f));
            Ramp(
                "SlopeRamp",
                new Vector3(2f, -0.1f, -10f),
                new Vector3(9.2f, 1.9f, -10f),
                3f);

            GameObject gravity = new("ArtificialGravity");
            BoxCollider volume = gravity.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.center = new Vector3(0f, 20f, 0f);
            volume.size = new Vector3(70f, 60f, 70f);
            gravity.AddComponent<ArtificialGravityVolume>();

            GameObject playerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player =
                (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(
                new Vector3(0f, 1.5f, 0f),
                Quaternion.identity);
            if (player.GetComponent<KeyboardFirstPersonInput>() == null)
            {
                player.AddComponent<KeyboardFirstPersonInput>();
            }

            GameObject arenaCamera = new("ArenaCamera");
            arenaCamera.transform.SetParent(player.transform, false);
            arenaCamera.transform.localPosition = new Vector3(0f, 2.1f, -4.2f);
            arenaCamera.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            arenaCamera.AddComponent<Camera>();
            arenaCamera.AddComponent<AudioListener>();
            arenaCamera.tag = "MainCamera";

            AnimationArenaBootstrap bootstrap =
                new GameObject("ArenaBootstrap").AddComponent<AnimationArenaBootstrap>();
            SerializedObject serialized = new(bootstrap);
            serialized.FindProperty("player").objectReferenceValue =
                player.GetComponent<FirstPersonMotor>();
            SerializedProperty points = serialized.FindProperty("teleportPoints");
            points.arraySize = 3;
            points.GetArrayElementAtIndex(0).objectReferenceValue =
                Point("Teleport2m", new Vector3(12f, 3.4f, -10f));
            points.GetArrayElementAtIndex(1).objectReferenceValue =
                Point("Teleport5m", new Vector3(-12f, 6.4f, -10f));
            points.GetArrayElementAtIndex(2).objectReferenceValue =
                Point("Teleport10m", new Vector3(0f, 11.4f, 18f));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(
                $"Built {ScenePath}. Play the scene: WASD/mouse to move, Space jumps, " +
                "1/2/3 teleport onto the towers, R respawns, Escape frees the cursor.");
        }

        static GameObject Box(string name, Vector3 position, Vector3 scale)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            return box;
        }

        static void Ramp(string name, Vector3 bottom, Vector3 top, float width)
        {
            Vector3 direction = top - bottom;
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            ramp.transform.position = (bottom + top) * 0.5f;
            ramp.transform.rotation =
                Quaternion.LookRotation(direction.normalized, Vector3.up);
            ramp.transform.localScale = new Vector3(width, 0.3f, direction.magnitude);
        }

        static Transform Point(string name, Vector3 position)
        {
            GameObject point = new(name);
            point.transform.position = position;
            return point.transform;
        }
    }
}
