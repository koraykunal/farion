using Farion.Core.Physics;
using Farion.Gameplay.Character;
using UnityEngine;

namespace Farion.Gameplay.Presentation.Character
{
    [DefaultExecutionOrder(240)]
    [DisallowMultipleComponent]
    public sealed class ExplorerToolView : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] ExplorerTool tool;
        [SerializeField] ExplorerMotor motor;
        [SerializeField] Animator animator;
        [SerializeField] PlayerExplorerSeatedPose seatedPose;
        [SerializeField] Transform toolModel;
        [SerializeField] Transform tip;
        [SerializeField] LineRenderer beam;

        [Header("Grip (right hand bone space)")]
        [Tooltip("Scanner offset from the right hand bone. Tune in Play Mode with the scanner raised; changes apply live.")]
        [SerializeField] Vector3 gripPosition;
        [SerializeField] Vector3 gripEuler;

        [Header("Scan")]
        [SerializeField, Min(0.1f)] float scanDistance = 8f;

        public bool IsScanning => beam != null && beam.enabled;

        void Awake()
        {
            motor ??= GetComponentInParent<ExplorerMotor>();
            tool ??= GetComponentInParent<ExplorerTool>();
            animator ??= GetComponentInChildren<Animator>(true);
            seatedPose ??= GetComponentInChildren<PlayerExplorerSeatedPose>(true);
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null && toolModel != null)
            {
                toolModel.SetParent(hand, false);
            }

            ApplyGrip();
            if (beam != null) beam.enabled = false;
        }

        void OnDisable()
        {
            if (toolModel != null) toolModel.gameObject.SetActive(false);
            if (beam != null) beam.enabled = false;
        }

        void Update()
        {
            if (toolModel == null || tool == null) return;
            ApplyGrip();
            bool shown = tool.Equipped && !(seatedPose != null && seatedPose.IsSeated);
            if (toolModel.gameObject.activeSelf != shown) toolModel.gameObject.SetActive(shown);
        }

        void LateUpdate()
        {
            if (beam == null || tip == null || tool == null || motor == null) return;
            beam.enabled = tool.IsUsing && toolModel != null && toolModel.gameObject.activeSelf;
            if (!beam.enabled) return;

            PhysicsScene physics = gameObject.scene.GetPhysicsScene();
            int mask = FarionLayers.CameraObstacleMask | FarionLayers.InteractionMask;
            Vector3 origin;
            Vector3 direction;
            if (motor.ViewReference != null)
            {
                Transform view = motor.ViewReference;
                direction = view.forward;
                origin = view.position + direction * Mathf.Max(0f, Vector3.Dot(motor.transform.position - view.position, direction));
            }
            else
            {
                origin = tip.position;
                direction = motor.LookDirection;
            }

            Vector3 end = physics.Raycast(origin, direction, out RaycastHit hit, scanDistance, mask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : origin + direction * scanDistance;
            Vector3 path = end - tip.position;
            if (path.sqrMagnitude > 0.0001f && physics.Raycast(tip.position, path.normalized, out RaycastHit blocked,
                path.magnitude, mask, QueryTriggerInteraction.Ignore))
            {
                end = blocked.point;
            }

            beam.SetPosition(0, tip.position);
            beam.SetPosition(1, end);
        }

        void ApplyGrip()
        {
            if (toolModel == null) return;
            toolModel.localPosition = gripPosition;
            toolModel.localRotation = Quaternion.Euler(gripEuler);
        }
    }
}
