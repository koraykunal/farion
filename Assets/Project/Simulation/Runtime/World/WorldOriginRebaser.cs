using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.World
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class WorldOriginRebaser : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] WorldOriginSettings settings;

        [Header("Tracking")]
        [SerializeField] Transform trackingTarget;

        [Header("Shifted Scene Roots")]
        [SerializeField] List<Transform> shiftedRoots = new();

        [Header("Runtime State")]
        [SerializeField] Vector3 accumulatedOriginOffset;
        [SerializeField] Vector3 lastOriginOffset;
        [SerializeField] int shiftCount;
        [SerializeField] int lastShiftFrame = -1;
        [SerializeField] float trackingDistanceFromOrigin;

        readonly List<Transform> uniqueShiftRoots = new();

        public bool AutomaticRebasing { get; private set; } = true;

        public Vector3 AccumulatedOriginOffset => accumulatedOriginOffset;
        public Vector3 LastOriginOffset => lastOriginOffset;
        public int ShiftCount => shiftCount;
        public int LastShiftFrame => lastShiftFrame;
        public float TrackingDistanceFromOrigin => trackingDistanceFromOrigin;
        public Transform TrackingTarget => trackingTarget;
        public event Action<Vector3> Rebased;

        public WorldOriginSnapshot CaptureSnapshot()
        {
            return new WorldOriginSnapshot(accumulatedOriginOffset, shiftCount);
        }

        public bool RestoreSnapshot(WorldOriginSnapshot snapshot)
        {
            if (!snapshot.IsSupported)
            {
                return false;
            }

            accumulatedOriginOffset = snapshot.AccumulatedOffset;
            shiftCount = snapshot.ShiftCount;
            lastOriginOffset = Vector3.zero;
            lastShiftFrame = -1;
            trackingDistanceFromOrigin = trackingTarget != null ? GetTrackingPosition().magnitude : 0f;
            return true;
        }

        public void ResetRuntimeState()
        {
            accumulatedOriginOffset = Vector3.zero;
            lastOriginOffset = Vector3.zero;
            shiftCount = 0;
            lastShiftFrame = -1;
            trackingDistanceFromOrigin = trackingTarget != null ? GetTrackingPosition().magnitude : 0f;
        }

        void Awake()
        {
            RefreshShiftRoots();
            ValidateSetup(logWarnings: false);
        }

        void FixedUpdate()
        {
            if (AutomaticRebasing)
            {
                RebaseIfNeeded();
            }
        }

        void OnValidate()
        {
            shiftedRoots ??= new List<Transform>();
        }

        [ContextMenu("Refresh Shift Roots")]
        public void RefreshShiftRoots()
        {
            uniqueShiftRoots.Clear();

            foreach (Transform root in shiftedRoots)
            {
                AddShiftRoot(root);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Setup")]
        public bool ValidateSetup()
        {
            return ValidateSetup(logWarnings: true);
        }
#endif

        [ContextMenu("Rebase Now")]
        public void RebaseNow()
        {
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(WorldOriginRebaser)} can only rebase from the context menu in Play Mode.", this);
#endif
                return;
            }

            if (trackingTarget == null)
            {
                return;
            }

            Rebase(GetTrackingPosition());
        }

        public bool RebaseIfNeeded()
        {
            if (trackingTarget == null)
            {
                trackingDistanceFromOrigin = 0f;
                return false;
            }

            Vector3 offset = GetTrackingPosition();
            if (settings != null && !settings.RebaseAllAxes)
            {
                offset.y = 0f;
            }

            trackingDistanceFromOrigin = offset.magnitude;
            float rebaseDistance = settings != null ? settings.RebaseDistance : 1000f;
            if (offset.sqrMagnitude <= rebaseDistance * rebaseDistance)
            {
                return false;
            }

            Rebase(offset);
            return true;
        }

        public void Rebase(Vector3 originOffset)
        {
            if (originOffset.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            RefreshShiftRoots();

            foreach (Transform root in uniqueShiftRoots)
            {
                if (root != null)
                {
                    root.position -= originOffset;
                }
            }

            UnityEngine.Physics.SyncTransforms();

            accumulatedOriginOffset += originOffset;
            lastOriginOffset = originOffset;
            shiftCount++;
            lastShiftFrame = Time.frameCount;
            trackingDistanceFromOrigin = 0f;
            Rebased?.Invoke(originOffset);
        }

        public void SetTrackingTarget(Transform target)
        {
            trackingTarget = target;
            if (Application.isPlaying)
            {
                ValidateSetup(logWarnings: false);
            }
        }

        public void SetAutomaticRebasing(bool enabled)
        {
            AutomaticRebasing = enabled;
        }

        bool ValidateSetup(bool logWarnings)
        {
            RefreshShiftRoots();

            bool valid = true;
            if (settings == null)
            {
                valid = false;
                if (logWarnings)
                {
                    Debug.LogWarning($"{nameof(WorldOriginRebaser)} has no {nameof(WorldOriginSettings)} assigned.", this);
                }
            }

            if (trackingTarget == null)
            {
                valid = false;
                if (logWarnings)
                {
                    Debug.LogWarning($"{nameof(WorldOriginRebaser)} has no tracking target assigned.", this);
                }
            }

            if (uniqueShiftRoots.Count == 0)
            {
                valid = false;
                if (logWarnings)
                {
                    Debug.LogWarning($"{nameof(WorldOriginRebaser)} has no shifted roots. Add authored scene roots such as Bodies, Actors, CameraRig, and Lighting.", this);
                }
            }

            if (trackingTarget != null && !IsTrackingTargetCovered())
            {
                valid = false;
                if (logWarnings)
                {
                    Debug.LogWarning($"{nameof(WorldOriginRebaser)} tracking target is not under any shifted root. Add the target's authored root to shifted roots.", this);
                }
            }

            return valid;
        }

        void AddShiftRoot(Transform candidate)
        {
            if (candidate == null || uniqueShiftRoots.Contains(candidate))
            {
                return;
            }

            for (int i = uniqueShiftRoots.Count - 1; i >= 0; i--)
            {
                Transform existing = uniqueShiftRoots[i];
                if (existing == null)
                {
                    uniqueShiftRoots.RemoveAt(i);
                    continue;
                }

                if (candidate.IsChildOf(existing))
                {
                    return;
                }

                if (existing.IsChildOf(candidate))
                {
                    uniqueShiftRoots.RemoveAt(i);
                }
            }

            uniqueShiftRoots.Add(candidate);
        }

        bool IsTrackingTargetCovered()
        {
            if (trackingTarget == null)
            {
                return false;
            }

            foreach (Transform root in uniqueShiftRoots)
            {
                if (root != null && trackingTarget.IsChildOf(root))
                {
                    return true;
                }
            }

            return false;
        }

        Vector3 GetTrackingPosition()
        {
            if (trackingTarget != null && trackingTarget.TryGetComponent(out Rigidbody rb))
            {
                return rb.position;
            }

            return trackingTarget != null ? trackingTarget.position : Vector3.zero;
        }
    }
}
