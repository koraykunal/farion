using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.World
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class WorldOriginRebaser : MonoBehaviour
    {
        [SerializeField] WorldOriginSettings settings;
        [SerializeField] Transform trackingTarget;
        [SerializeField] bool useMainCameraWhenTargetMissing = true;
        [SerializeField] bool includeTrackingTargetWhenMissing = true;
        [SerializeField] List<Transform> shiftedRoots = new();

        readonly List<Transform> uniqueShiftRoots = new();
        readonly List<Rigidbody> rigidbodies = new();
        readonly HashSet<Rigidbody> shiftedRigidbodies = new();
        [SerializeField, HideInInspector] int serializedVersion;

        public Vector3 AccumulatedOriginOffset { get; private set; }
        public int ShiftCount { get; private set; }

        void Awake()
        {
            UpgradeSerializedData();
            ResolveFallbackTarget();
            RefreshShiftRoots();
        }

        void FixedUpdate()
        {
            ResolveFallbackTarget();
            RebaseIfNeeded();
        }

        void OnValidate()
        {
            UpgradeSerializedData();
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

            if (includeTrackingTargetWhenMissing)
            {
                AddShiftRoot(trackingTarget);
            }
        }

        [ContextMenu("Rebase Now")]
        public void RebaseNow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"{nameof(WorldOriginRebaser)} can only rebase from the context menu in Play Mode.", this);
                return;
            }

            ResolveFallbackTarget();
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
                return false;
            }

            Vector3 offset = GetTrackingPosition();
            if (settings != null && !settings.RebaseAllAxes)
            {
                offset.y = 0f;
            }

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
                if (root == null)
                {
                    continue;
                }

                ShiftRoot(root, originOffset);
            }

            UnityEngine.Physics.SyncTransforms();

            AccumulatedOriginOffset += originOffset;
            ShiftCount++;
        }

        void ResolveFallbackTarget()
        {
            if (trackingTarget != null || !useMainCameraWhenTargetMissing)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                trackingTarget = mainCamera.transform;
            }
        }

        void UpgradeSerializedData()
        {
            if (serializedVersion >= 1)
            {
                return;
            }

            includeTrackingTargetWhenMissing = true;
            serializedVersion = 1;
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

        void ShiftRoot(Transform root, Vector3 originOffset)
        {
            rigidbodies.Clear();
            shiftedRigidbodies.Clear();
            root.GetComponentsInChildren(true, rigidbodies);

            if (rigidbodies.Count == 0)
            {
                root.position -= originOffset;
                return;
            }

            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb == null || shiftedRigidbodies.Contains(rb))
                {
                    continue;
                }

                rb.transform.position -= originOffset;
                shiftedRigidbodies.Add(rb);
            }

            ShiftNonRigidbodyBranches(root, originOffset);
        }

        void ShiftNonRigidbodyBranches(Transform root, Vector3 originOffset)
        {
            if (TryGetShiftedRigidbody(root, out _))
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (ContainsShiftedRigidbody(child))
                {
                    ShiftNonRigidbodyBranches(child, originOffset);
                }
                else
                {
                    child.position -= originOffset;
                }
            }
        }

        bool ContainsShiftedRigidbody(Transform root)
        {
            if (TryGetShiftedRigidbody(root, out _))
            {
                return true;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                if (ContainsShiftedRigidbody(root.GetChild(i)))
                {
                    return true;
                }
            }

            return false;
        }

        bool TryGetShiftedRigidbody(Transform target, out Rigidbody shiftedRigidbody)
        {
            if (target.TryGetComponent(out Rigidbody rb) && shiftedRigidbodies.Contains(rb))
            {
                shiftedRigidbody = rb;
                return true;
            }

            shiftedRigidbody = null;
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
