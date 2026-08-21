using Farion.Gameplay.Flight;
using Farion.Multiplayer.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Authoring
{
    static class FarionWorldLayoutAuthoring
    {
        const string ScenePath = "Assets/Project/Scenes/SC_WorldZone.unity";
        const string FleetPath = "Actors/PF_CapitalShip_Fleet";
        const string FormationPath = "Actors/Multiplayer/StarterShipFormations";
        const string ExplorerPath = "Actors/Player Explorer";
        const string PossessionPath = "Actors/PlayerPossession";
        const string ShuttlePath = "Actors/PF_PlayerStarterShuttle";

        static readonly Vector3 FleetPosition = new(174f, 12000f, 0f);
        static readonly float[] ShipOffsets = { -39f, -13f, 13f, 39f };

        [MenuItem("Farion/Authoring/Apply World Layout")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"Could not open {ScenePath}.");
                return;
            }

            Transform fleet = FindByPath(scene, FleetPath);
            Transform formation = FindByPath(scene, FormationPath);
            Transform explorer = FindByPath(scene, ExplorerPath);
            Transform possession = FindByPath(scene, PossessionPath);
            Transform shuttle = FindByPath(scene, ShuttlePath);
            if (fleet == null ||
                formation == null ||
                explorer == null ||
                possession == null ||
                shuttle == null)
            {
                Debug.LogError($"{ScenePath}: expected actor hierarchy is missing.");
                return;
            }

            fleet.localPosition = FleetPosition;
            possession.localPosition = Vector3.zero;
            explorer.SetPositionAndRotation(
                ResolveExplorerAnchor(shuttle).position,
                shuttle.rotation);

            for (int slot = 0; slot < ShipOffsets.Length; slot++)
            {
                Transform anchor = formation.Find($"Ship_{slot + 1}");
                if (anchor == null)
                {
                    anchor = new GameObject($"Ship_{slot + 1}").transform;
                    anchor.SetParent(formation, false);
                }

                anchor.SetSiblingIndex(slot);
                anchor.localPosition = new Vector3(ShipOffsets[slot], 0f, 0f);
                anchor.localRotation = Quaternion.identity;
                anchor.localScale = Vector3.one;
            }

            for (int count = 1; count <= 4; count++)
            {
                Transform legacy = formation.Find($"Count_{count}");
                if (legacy != null)
                {
                    Object.DestroyImmediate(legacy.gameObject);
                }
            }

            MultiplayerSceneContext context =
                Object.FindAnyObjectByType<MultiplayerSceneContext>();
            if (context == null)
            {
                Debug.LogError($"{ScenePath}: MultiplayerSceneContext is missing.");
                return;
            }

            SerializedObject serialized = new(context);
            serialized.FindProperty("starterShuttleFormation").objectReferenceValue =
                formation;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("World layout applied.");
        }

        static Transform ResolveExplorerAnchor(Transform shuttle)
        {
            SpacecraftRig rig = shuttle.GetComponent<SpacecraftRig>();
            return rig != null && rig.ExteriorExitPoint != null
                ? rig.ExteriorExitPoint
                : shuttle;
        }

        static Transform FindByPath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            GameObject[] roots = scene.GetRootGameObjects();
            Transform current = null;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == parts[0])
                {
                    current = roots[i].transform;
                    break;
                }
            }

            for (int i = 1; current != null && i < parts.Length; i++)
            {
                current = current.Find(parts[i]);
            }

            return current;
        }
    }
}
