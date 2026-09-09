using System;
using Farion.Core.Physics;
using Farion.Gameplay.Interaction;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public sealed class ExplorerTool : MonoBehaviour
    {
        public const byte Stowed = 0;
        public const byte Ready = 1;
        public const byte Using = 2;

        [SerializeField] ExplorerMotor motor;
        [SerializeField] ExplorerInput input;
        [Tooltip("Whether the scanner starts raised. A raised scanner also puts the camera into aim mode.")]
        [SerializeField] bool equippedOnSpawn;

        [Header("Scan")]
        [Min(0.1f)]
        [SerializeField] float scanDistance = 8f;
        [Tooltip("Seconds the beam must stay on one deposit before its readout is revealed.")]
        [Min(0.1f)]
        [SerializeField] float scanSeconds = 1.5f;

        bool locallyControlled = true;
        bool onFoot = true;
        bool requestedEquipped;
        byte remoteState;

        public byte State { get; private set; }
        public bool Equipped => State != Stowed;
        public bool IsUsing => State == Using;
        public ResourceNodeInteractable ScanTarget { get; private set; }
        public float ScanProgress { get; private set; }
        public bool ScanComplete => ScanTarget != null && ScanProgress >= 1f;
        public event Action<byte> LocalStateChanged;

        void Awake()
        {
            motor ??= GetComponent<ExplorerMotor>();
            input ??= GetComponent<ExplorerInput>();
            requestedEquipped = equippedOnSpawn;
        }

        public void SetControl(bool local, bool walking)
        {
            locallyControlled = local;
            onFoot = walking;
            if (!walking) State = Stowed;
        }

        public void ApplyRemoteState(byte state)
        {
            remoteState = state <= Using ? state : Stowed;
            if (!locallyControlled) State = onFoot ? remoteState : Stowed;
        }

        public static byte ResolveState(bool equipped, bool use, bool onFoot, bool submerged, bool sprinting) =>
            !equipped || !onFoot || submerged ? Stowed : use && !sprinting ? Using : Ready;

        void Update()
        {
            if (!locallyControlled)
            {
                State = onFoot ? remoteState : Stowed;
                return;
            }

            ExplorerInputState current = input != null && input.isActiveAndEnabled
                ? input.CurrentInput
                : ExplorerInputState.None;
            if (onFoot && current.ToggleTool) requestedEquipped = !requestedEquipped;
            float submerged = motor != null ? motor.CaptureState().WaterSubmergedFraction : 0f;
            byte next = ResolveState(requestedEquipped, current.UseTool, onFoot, submerged >= 0.5f, current.Sprint);
            UpdateScan(next);
            if (next == State) return;
            State = next;
            LocalStateChanged?.Invoke(next);
        }

        void UpdateScan(byte state)
        {
            if (state == Stowed || (state == Ready && !ScanComplete))
            {
                ScanTarget = null;
                ScanProgress = 0f;
                return;
            }

            if (state != Using)
            {
                return;
            }

            ResourceNodeInteractable node = FindScanTarget();
            if (node != ScanTarget)
            {
                ScanTarget = node;
                ScanProgress = 0f;
            }

            if (node != null)
            {
                ScanProgress = Mathf.Min(1f, ScanProgress + Time.deltaTime / scanSeconds);
            }
        }

        ResourceNodeInteractable FindScanTarget()
        {
            if (motor == null)
            {
                return null;
            }

            Transform view = motor.ViewReference;
            Vector3 direction = view != null ? view.forward : motor.LookDirection;
            Vector3 origin = view != null
                ? view.position + direction * Mathf.Max(0f, Vector3.Dot(transform.position - view.position, direction))
                : transform.position;
            int mask = FarionLayers.CameraObstacleMask | FarionLayers.InteractionMask;
            return gameObject.scene.GetPhysicsScene().Raycast(
                origin, direction, out RaycastHit hit, scanDistance, mask, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<ResourceNodeInteractable>()
                : null;
        }

        void OnDisable()
        {
            State = Stowed;
            ScanTarget = null;
            ScanProgress = 0f;
        }
    }
}
