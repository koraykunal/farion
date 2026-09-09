using Farion.Audio.Character;
using Farion.Gameplay.Character;
using FMODUnity;
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

                ExplorerAudioController audio = root.GetComponent<ExplorerAudioController>();
                if (audio == null)
                {
                    audio = root.AddComponent<ExplorerAudioController>();
                    changed = true;
                }

                changed |= BindCharacterEvents(audio);
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

        static bool BindCharacterEvents(ExplorerAudioController audio)
        {
            var serialized = new SerializedObject(audio);
            bool changed = false;
            changed |= BindEvent(serialized, "footstepEvent", "event:/Character/Footstep");
            changed |= BindEvent(serialized, "jumpEvent", "event:/Character/Jump");
            changed |= BindEvent(serialized, "landEvent", "event:/Character/Land");
            return changed && serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static bool BindEvent(SerializedObject serialized, string field, string path)
        {
            EventReference reference = EventReference.Find(path);
            if (reference.Guid.IsNull)
            {
                Debug.LogError($"FMOD event {path} is not in the built banks; rebuild FMODProject first.");
                return false;
            }

            SerializedProperty property = serialized.FindProperty(field);
            SerializedProperty guid = property.FindPropertyRelative("Guid");
            SerializedProperty data1 = guid.FindPropertyRelative("Data1");
            SerializedProperty data2 = guid.FindPropertyRelative("Data2");
            SerializedProperty data3 = guid.FindPropertyRelative("Data3");
            SerializedProperty data4 = guid.FindPropertyRelative("Data4");
            SerializedProperty pathProperty = property.FindPropertyRelative("Path");
            if (data1.intValue == reference.Guid.Data1 &&
                data2.intValue == reference.Guid.Data2 &&
                data3.intValue == reference.Guid.Data3 &&
                data4.intValue == reference.Guid.Data4 &&
                pathProperty.stringValue == path)
            {
                return false;
            }

            data1.intValue = reference.Guid.Data1;
            data2.intValue = reference.Guid.Data2;
            data3.intValue = reference.Guid.Data3;
            data4.intValue = reference.Guid.Data4;
            pathProperty.stringValue = path;
            return true;
        }

    }
}
