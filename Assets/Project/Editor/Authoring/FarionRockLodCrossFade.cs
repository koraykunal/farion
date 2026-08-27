using UnityEditor;
using UnityEngine;

namespace Farion.Editor.Authoring
{
    public static class FarionRockLodCrossFade
    {
        const string PrefabRoot = "Assets/Realistic Cliffs and Rocks/Prefabs";
        const float TransitionWidth = 0.35f;

        public static void ApplyFromMenu()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
            int changed = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                LODGroup group = prefab.GetComponent<LODGroup>();
                if (group == null)
                {
                    continue;
                }

                LOD[] levels = group.GetLODs();
                if (levels.Length <= 1)
                {
                    continue;
                }

                for (int level = 0; level < levels.Length; level++)
                {
                    levels[level].fadeTransitionWidth = TransitionWidth;
                }

                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = false;
                group.SetLODs(levels);
                EditorUtility.SetDirty(prefab);
                changed++;
                Debug.Log($"FARION-LOD {path} lods={levels.Length} crossfade=on");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"FARION-LOD-RESULT changed={changed}");
        }
    }
}
