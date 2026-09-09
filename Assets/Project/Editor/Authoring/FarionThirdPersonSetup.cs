using Farion.Gameplay.Character;
using Farion.Gameplay.Presentation.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Farion.Editor.Authoring
{
    static class FarionThirdPersonSetup
    {
        const string ScannerModel = "Assets/Project/Art/Models/Tools/SM_Tool_Scanner_A.fbx";
        const string ScannerMaterial = "Assets/Project/Art/Materials/Tools/MAT_Tool_Scanner.mat";
        const string BeamMaterial = "Assets/Project/Art/Materials/Tools/MAT_ScannerBeam.mat";
        const string ScannerName = "Scanner";

        [MenuItem("Farion/Character/Build Third Person Setup")]
        public static void Run()
        {
            try
            {
                CleanShellCamera();
                BuildScanner();
                AssetDatabase.SaveAssets();
                Debug.Log("Third person setup built: shell camera cleaned, scanner socketed on PF_PlayerExplorerCore.");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Third person setup FAILED: " + exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }

        static void CleanShellCamera()
        {
            Scene scene = EditorSceneManager.OpenScene(FarionAssetPaths.GameplayShellScene, OpenSceneMode.Single);
            ExplorerCameraRig rig = null;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                rig = rootObject.GetComponentInChildren<ExplorerCameraRig>(true);
                if (rig != null) break;
            }

            if (rig == null)
            {
                throw new System.InvalidOperationException("ExplorerCameraRig not found in SC_GameplayShell.");
            }

            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(rig.gameObject);
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        static void BuildScanner()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(FarionAssetPaths.CoreExplorerPrefab);
            try
            {
                Transform visualRoot = root.transform.Find("VisualRoot")
                    ?? throw new System.InvalidOperationException("VisualRoot missing on the explorer core prefab.");
                Animator animator = visualRoot.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    throw new System.InvalidOperationException("No humanoid Animator under VisualRoot.");
                }

                Transform cameraAnchor = root.transform.Find("CameraAnchor");
                if (cameraAnchor != null)
                {
                    Object.DestroyImmediate(cameraAnchor.gameObject);
                }

                ExplorerMotor motor = root.GetComponent<ExplorerMotor>();
                SerializedObject motorSerialized = new(motor);
                motorSerialized.FindProperty("viewReference").objectReferenceValue = null;
                motorSerialized.ApplyModifiedPropertiesWithoutUndo();

                ExplorerTool tool = root.GetComponent<ExplorerTool>();
                if (tool == null) tool = root.AddComponent<ExplorerTool>();
                SerializedObject toolSerialized = new(tool);
                toolSerialized.FindProperty("motor").objectReferenceValue = motor;
                toolSerialized.FindProperty("input").objectReferenceValue = root.GetComponent<ExplorerInput>();
                toolSerialized.ApplyModifiedPropertiesWithoutUndo();

                Transform existing = visualRoot.Find(ScannerName);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ScannerModel)
                    ?? throw new System.InvalidOperationException(ScannerModel + " is missing.");
                GameObject scanner = (GameObject)PrefabUtility.InstantiatePrefab(model);
                scanner.name = ScannerName;
                scanner.transform.SetParent(visualRoot, false);
                Material scannerMaterial = AssetDatabase.LoadAssetAtPath<Material>(ScannerMaterial);
                Bounds bounds = new(Vector3.zero, Vector3.zero);
                bool hasBounds = false;
                foreach (MeshRenderer renderer in scanner.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (scannerMaterial != null) renderer.sharedMaterial = scannerMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    Bounds local = filter.sharedMesh.bounds;
                    Vector3 min = scanner.transform.InverseTransformPoint(renderer.transform.TransformPoint(local.min));
                    Vector3 max = scanner.transform.InverseTransformPoint(renderer.transform.TransformPoint(local.max));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(min, Vector3.zero);
                        hasBounds = true;
                    }

                    bounds.Encapsulate(min);
                    bounds.Encapsulate(max);
                }

                GameObject tip = new("Tip");
                tip.transform.SetParent(scanner.transform, false);
                tip.transform.localPosition = hasBounds ? new Vector3(bounds.center.x, bounds.center.y, bounds.max.z) : Vector3.zero;

                GameObject beamObject = new("ScanBeam");
                beamObject.transform.SetParent(scanner.transform, false);
                LineRenderer beam = beamObject.AddComponent<LineRenderer>();
                beam.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterial);
                beam.positionCount = 2;
                beam.useWorldSpace = true;
                beam.widthMultiplier = 0.02f;
                beam.shadowCastingMode = ShadowCastingMode.Off;
                beam.receiveShadows = false;
                beam.enabled = false;
                scanner.SetActive(false);

                ExplorerToolView view = visualRoot.GetComponent<ExplorerToolView>();
                if (view == null) view = visualRoot.gameObject.AddComponent<ExplorerToolView>();
                SerializedObject viewSerialized = new(view);
                viewSerialized.FindProperty("tool").objectReferenceValue = tool;
                viewSerialized.FindProperty("motor").objectReferenceValue = motor;
                viewSerialized.FindProperty("animator").objectReferenceValue = animator;
                viewSerialized.FindProperty("seatedPose").objectReferenceValue =
                    visualRoot.GetComponentInChildren<PlayerExplorerSeatedPose>(true);
                viewSerialized.FindProperty("toolModel").objectReferenceValue = scanner.transform;
                viewSerialized.FindProperty("tip").objectReferenceValue = tip.transform;
                viewSerialized.FindProperty("beam").objectReferenceValue = beam;
                viewSerialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, FarionAssetPaths.CoreExplorerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
