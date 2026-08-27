using Farion.Gameplay.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Farion.Editor.Authoring
{
    internal sealed class FarionPlayerAnimationPreview : EditorWindow
    {
        private const string ModelPath =
            "Assets/Project/Art/Models/Characters/PlayerExplorer/SM_PlayerExplorer_A.fbx";
        private const string ControllerPath =
            "Assets/Project/Art/Animations/Characters/PlayerExplorer/Controllers/AC_PlayerExplorer.controller";
        private const string MotorProfilePath =
            "Assets/Project/Design/Gameplay/Character/SO_DefaultFirstPersonMotorProfile.asset";
        private const string PreviewObjectName = "Player Animation Preview";
        private const float PreviewLandingDuration = 0.8f;

        private static FirstPersonMotorProfile motorProfile;

        private static FirstPersonMotorProfile MotorProfile
        {
            get
            {
                if (motorProfile == null)
                    motorProfile = AssetDatabase.LoadAssetAtPath<FirstPersonMotorProfile>(MotorProfilePath);
                return motorProfile;
            }
        }

        private static float PreviewGravity =>
            MotorProfile != null ? MotorProfile.JumpReferenceGravity : 9.81f;

        private static float PreviewJumpSpeed =>
            MotorProfile != null
                ? Mathf.Clamp(
                    Mathf.Sqrt(2f * MotorProfile.JumpReferenceGravity * MotorProfile.JumpHeight),
                    MotorProfile.MinimumJumpSpeed,
                    MotorProfile.MaximumJumpSpeed)
                : 5.24f;

        [SerializeField] private Animator animator;
        [SerializeField] private Transform previewRoot;

        private bool initializedInPlayMode;
        private Vector3 previewGroundPosition;
        private double jumpStartedAt = -1d;
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
            window.previewRoot = instance.transform;
            window.previewGroundPosition = instance.transform.position;
            window.jumpStartedAt = -1d;
            window.initializedInPlayMode = false;
            window.Show();

            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void OnGUI()
        {
            ResolveAnimator();

            EditorGUILayout.HelpBox(
                "Play Mode'a gir. Tam jump akisi icin Space'e bas veya Full Jump'i kullan.",
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
                    ("Idle", () => SetManualPose(grounded: true)),
                    ("Walk", () => SetManualPose(moveY: 1f, grounded: true)),
                    ("Jog", () => SetManualPose(moveY: 2f, grounded: true)),
                    ("Run", () => SetManualPose(moveY: 3f, grounded: true)));
                ButtonRow(
                    ("Strafe Left", () => SetManualPose(moveX: -1f, grounded: true)),
                    ("Strafe Right", () => SetManualPose(moveX: 1f, grounded: true)));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Air", EditorStyles.boldLabel);
                if (GUILayout.Button("Full Jump (Space)"))
                    StartJumpSequence();
                ButtonRow(
                    ("Jump Start", () => PlayState("JumpStart", verticalSpeed: 4f)),
                    ("Fall", () => PlayState("Airborne", verticalSpeed: -4f)),
                    ("Land", () => PlayState("Land", true, -4f)));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Swim", EditorStyles.boldLabel);
                ButtonRow(
                    ("Swim Idle", () => SetManualPose(submerged: 1f)),
                    ("Swim Forward", () => SetManualPose(moveY: 1f, swimSpeed: 1f, submerged: 1f)));
            }
        }

        private void Update()
        {
            ResolveAnimator();
            if (!EditorApplication.isPlaying || animator == null)
                return;

            if (Keyboard.current?.spaceKey.wasPressedThisFrame == true)
                StartJumpSequence();

            if (jumpStartedAt < 0d)
                return;

            float elapsed = (float)(EditorApplication.timeSinceStartup - jumpStartedAt);
            float flightDuration = 2f * PreviewJumpSpeed / PreviewGravity;
            if (elapsed < flightDuration)
            {
                float verticalSpeed = PreviewJumpSpeed - PreviewGravity * elapsed;
                float height = PreviewJumpSpeed * elapsed - 0.5f * PreviewGravity * elapsed * elapsed;
                previewRoot.position = previewGroundPosition + Vector3.up * height;
                SetPose(verticalSpeed: verticalSpeed);
            }
            else if (elapsed < flightDuration + PreviewLandingDuration)
            {
                previewRoot.position = previewGroundPosition;
                SetPose(grounded: true, verticalSpeed: -PreviewJumpSpeed);
            }
            else
            {
                jumpStartedAt = -1d;
                previewRoot.position = previewGroundPosition;
                SetPose(grounded: true);
            }

            Repaint();
        }

        private void StartJumpSequence()
        {
            ResolveAnimator();
            if (!EditorApplication.isPlaying || animator == null || previewRoot == null)
                return;

            if (jumpStartedAt < 0d)
                previewGroundPosition = previewRoot.position;
            else
                previewRoot.position = previewGroundPosition;

            jumpStartedAt = EditorApplication.timeSinceStartup;
            SetPose(verticalSpeed: PreviewJumpSpeed);
        }

        private void PlayState(
            string state,
            bool grounded = false,
            float verticalSpeed = 0f)
        {
            StopJumpSequence();
            SetPose(grounded: grounded, verticalSpeed: verticalSpeed);
            animator.CrossFadeInFixedTime(state, 0.05f, 0, 0f);
        }

        private void StopJumpSequence()
        {
            if (jumpStartedAt >= 0d && previewRoot != null)
                previewRoot.position = previewGroundPosition;

            jumpStartedAt = -1d;
        }

        private void SetPose(
            float moveX = 0f,
            float moveY = 0f,
            float swimSpeed = 0f,
            float submerged = 0f,
            bool grounded = false,
            float verticalSpeed = 0f)
        {
            animator.SetFloat("MoveX", moveX);
            animator.SetFloat("MoveY", moveY);
            animator.SetFloat("SwimSpeed01", swimSpeed);
            animator.SetFloat("StrideScale", 1f);
            animator.SetFloat("Submerged", submerged);
            animator.SetBool("Grounded", grounded);
            animator.SetFloat("PlanarSpeed", new Vector2(moveX, moveY).magnitude * 1.7f);
            animator.SetFloat("VerticalSpeed", verticalSpeed);
        }

        private void SetManualPose(
            float moveX = 0f,
            float moveY = 0f,
            float swimSpeed = 0f,
            float submerged = 0f,
            bool grounded = false,
            float verticalSpeed = 0f)
        {
            StopJumpSequence();
            SetPose(moveX, moveY, swimSpeed, submerged, grounded, verticalSpeed);
        }

        private void ResolveAnimator()
        {
            if (animator != null && previewRoot != null)
                return;

            var preview = GameObject.Find(PreviewObjectName);
            if (preview != null)
            {
                animator = preview.GetComponentInChildren<Animator>(true);
                previewRoot = preview.transform;
                previewGroundPosition = preview.transform.position;
            }
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
