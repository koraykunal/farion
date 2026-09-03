using Farion.Audio.Character;
using Farion.Gameplay.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Authoring
{
    static class FarionExplorerFeelIntake
    {
        [MenuItem("Farion/Character/Install Explorer Feel")]
        public static void Run()
        {
            InstallPrefabComponents();
            InstallViewEffects();
            AssetDatabase.SaveAssets();
        }

        static void InstallPrefabComponents()
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(FarionAssetPaths.CoreExplorerPrefab);
            try
            {
                bool changed = false;
                if (root.GetComponent<ExplorerLocomotionSignals>() == null)
                {
                    root.AddComponent<ExplorerLocomotionSignals>();
                    changed = true;
                }

                if (root.GetComponent<ExplorerAudioController>() == null)
                {
                    root.AddComponent<ExplorerAudioController>();
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        FarionAssetPaths.CoreExplorerPrefab);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void InstallViewEffects()
        {
            Scene scene = EditorSceneManager.OpenScene(
                FarionAssetPaths.GameplayShellScene,
                OpenSceneMode.Single);
            FirstPersonCameraRig rig = null;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                rig = rootObject.GetComponentInChildren<FirstPersonCameraRig>(true);
                if (rig != null)
                {
                    break;
                }
            }

            if (rig == null)
            {
                Debug.LogError(
                    $"{FarionAssetPaths.GameplayShellScene}: FirstPersonCameraRig not found.");
                return;
            }

            if (rig.GetComponent<FirstPersonViewEffects>() != null)
            {
                return;
            }

            rig.gameObject.AddComponent<FirstPersonViewEffects>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
