using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Farion.Editor.Authoring
{
    internal sealed class FarionPlayerAnimationPreview : EditorWindow
    {
        private const string ModelPath =
            "Assets/Project/Art/Models/Characters/PlayerExplorer/SM_PlayerExplorer_A.fbx";
        private const string ControllerPath =
            "Assets/Project/Art/Animations/Characters/PlayerExplorer/Controllers/AC_PlayerExplorer.controller";
        private const string PreviewObjectName = "Player Animation Preview";

        [SerializeField] private Animator animator;

        private bool initializedInPlayMode;
        [MenuItem("Farion/Character/Animation Preview")]
        private static void CreatePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                GetWindow<FarionPlayerAnimationPreview>("Animation Preview").Show();
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (model == null || controller == null)
            {
                EditorUtility.DisplayDialog("Animation Preview", "Karakter modeli veya Animator Controller bulunamadi.", "Tamam");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            instance.name = PreviewObjectName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(instance, "Create animation preview character");

            var previewAnimator = instance.GetComponentInChildren<Animator>(true);
            previewAnimator.runtimeAnimatorController = controller;
            previewAnimator.applyRootMotion = false;
            previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Preview Floor";
            floor.transform.localScale = Vector3.one * 0.3f;
            Undo.RegisterCreatedObjectUndo(floor, "Create animation preview floor");

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 1.15f, 4f);
                camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            }

            var light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(45f, 150f, 0f);
                light.intensity = 1.25f;
            }

            var window = GetWindow<FarionPlayerAnimationPreview>("Animation Preview");
            window.animator = previewAnimator;
            window.initializedInPlayMode = false;
            window.Show();

            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void OnGUI()
        {
            ResolveAnimator();

            EditorGUILayout.HelpBox(
                "Play Mode'a gir; sonra asagidaki dugmelerle animasyonlari izle.",
                MessageType.Info);

            animator = (Animator)EditorGUILayout.ObjectField("Animator", animator, typeof(Animator), true);

            if (!EditorApplication.isPlaying)
            {
                initializedInPlayMode = false;
                if (GUILayout.Button("Play Mode'a Gir"))
                    EditorApplication.isPlaying = true;
            }
            else if (!initializedInPlayMode && animator != null)
            {
                SetPose(grounded: true);
                initializedInPlayMode = true;
            }

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || animator == null))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Hareket", EditorStyles.boldLabel);
                ButtonRow(
                    ("Idle", () => SetPose(grounded: true)),
                    ("Walk", () => SetPose(moveY: 1f, speed: 0.55f, grounded: true)),
                    ("Run", () => SetPose(moveY: 2f, speed: 1f, grounded: true)));
                ButtonRow(
                    ("Strafe Left", () => SetPose(moveX: -1f, speed: 0.55f, grounded: true)),
                    ("Strafe Right", () => SetPose(moveX: 1f, speed: 0.55f, grounded: true)));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Turn", EditorStyles.boldLabel);
                ButtonRow(
                    ("Turn Left", () => SetPose(grounded: true, turnRate: -90f)),
                    ("Turn Right", () => SetPose(grounded: true, turnRate: 90f)));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Air", EditorStyles.boldLabel);
                ButtonRow(
                    ("Jump", () => SetPose(verticalSpeed: 4f)),
                    ("Fall", () => SetPose(verticalSpeed: -4f)),
                    ("Hard Land", () => SetPose(grounded: true, verticalSpeed: -8f)));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Swim", EditorStyles.boldLabel);
                ButtonRow(
                    ("Swim Idle", () => SetPose(submerged: 1f)),
                    ("Swim Forward", () => SetPose(moveY: 1f, speed: 1f, submerged: 1f)));
            }
        }

        private void SetPose(
            float moveX = 0f,
            float moveY = 0f,
            float speed = 0f,
            float submerged = 0f,
            bool grounded = false,
            float verticalSpeed = 0f,
            float turnRate = 0f)
        {
            animator.SetFloat("MoveX", moveX);
            animator.SetFloat("MoveY", moveY);
            animator.SetFloat("Speed01", speed);
            animator.SetFloat("StrideScale", 1f);
            animator.SetFloat("Submerged", submerged);
            animator.SetBool("Grounded", grounded);
            animator.SetFloat("VerticalSpeed", verticalSpeed);
            animator.SetFloat("TurnRate", turnRate);
        }

        private void ResolveAnimator()
        {
            if (animator != null)
                return;

            var preview = GameObject.Find(PreviewObjectName);
            if (preview != null)
                animator = preview.GetComponentInChildren<Animator>(true);
        }

        private static void ButtonRow(params (string label, System.Action action)[] buttons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var button in buttons)
                {
                    if (GUILayout.Button(button.label))
                        button.action();
                }
            }
        }

    }
}
