using System;
using System.Collections.Generic;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Farion.Multiplayer.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class MultiplayerWorldOriginAuthority : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManager;

        readonly WorldOriginSequenceState state = new();
        readonly List<CelestialSurfaceCollisionObserverState> trackingObservers = new();
        WorldOriginRebaser rebaser;
        Transform serverTrackingTarget;
        MonoBehaviour serverTrackingObserverSource;
        ICelestialSurfaceCollisionObserverGroup serverTrackingObserverGroup;

        public uint CurrentSequence => state.Sequence;

        public event Action<Vector3> OriginShifted;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            networkManager.ClientManager.RegisterBroadcast<WorldOriginBroadcast>(
                OnOriginBroadcast);
            networkManager.TimeManager.OnPostTick += OnPostTick;
        }

        void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ClientManager.UnregisterBroadcast<WorldOriginBroadcast>(
                OnOriginBroadcast);
            networkManager.TimeManager.OnPostTick -= OnPostTick;
        }

        public void BindRebaser(WorldOriginRebaser nextRebaser)
        {
            if (rebaser != null)
            {
                rebaser.Rebased -= HandleRebased;
            }

            rebaser = nextRebaser;
            if (rebaser == null)
            {
                return;
            }

            rebaser.Rebased += HandleRebased;
            rebaser.SetAutomaticRebasing(false);
            Vector3 pendingDelta =
                state.AccumulatedOrigin - rebaser.AccumulatedOriginOffset;
            if (pendingDelta.sqrMagnitude > Mathf.Epsilon)
            {
                rebaser.Rebase(pendingDelta);
            }
        }

        public void SetServerTrackingTarget(Transform target)
        {
            serverTrackingTarget = target;
            if (networkManager.IsServerStarted && rebaser != null)
            {
                rebaser.SetTrackingTarget(target);
            }
        }

        public void SetServerTrackingObserverSource(MonoBehaviour source)
        {
            serverTrackingObserverSource = source;
            serverTrackingObserverGroup =
                source as ICelestialSurfaceCollisionObserverGroup;
            trackingObservers.Clear();
        }

        public void ResetSession()
        {
            state.Reset();
            serverTrackingTarget = null;
            SetServerTrackingObserverSource(null);
            if (rebaser != null)
            {
                rebaser.Rebased -= HandleRebased;
            }

            rebaser = null;
        }

        void HandleRebased(Vector3 originOffset)
        {
            OriginShifted?.Invoke(originOffset);
        }

        public bool AdoptRestoredOrigin()
        {
            if (!networkManager.IsServerStarted || rebaser == null)
            {
                return false;
            }

            if ((rebaser.AccumulatedOriginOffset - state.AccumulatedOrigin)
                .sqrMagnitude <= Mathf.Epsilon)
            {
                return true;
            }

            state.RecordServerShift(rebaser.AccumulatedOriginOffset);
            networkManager.ServerManager.Broadcast(
                CreateBroadcast(),
                requireAuthenticated: true,
                channel: Channel.Reliable);
            return true;
        }

        public void SendCurrentOrigin(NetworkConnection connection)
        {
            if (connection == null || !networkManager.IsServerStarted)
            {
                return;
            }

            networkManager.ServerManager.Broadcast(
                connection,
                CreateBroadcast(),
                requireAuthenticated: true,
                channel: Channel.Reliable);
        }

        void OnPostTick()
        {
            if (!networkManager.IsServerStarted ||
                rebaser == null)
            {
                return;
            }

            bool hasCollectivePosition =
                TryResolveCollectiveTrackingPosition(out Vector3 trackingPosition);
            if (!hasCollectivePosition && serverTrackingTarget == null)
            {
                return;
            }

            if (!hasCollectivePosition)
            {
                trackingPosition = serverTrackingTarget.position;
            }

            if (serverTrackingTarget != null)
            {
                rebaser.SetTrackingTarget(serverTrackingTarget);
            }

            if (!rebaser.RebaseIfNeeded(trackingPosition))
            {
                return;
            }

            state.RecordServerShift(rebaser.AccumulatedOriginOffset);
            networkManager.ServerManager.Broadcast(
                CreateBroadcast(),
                requireAuthenticated: true,
                channel: Channel.Reliable);
        }

        void OnOriginBroadcast(WorldOriginBroadcast message, Channel channel)
        {
            if (!state.TryAccept(
                    message.Sequence,
                    message.AccumulatedOrigin,
                    out Vector3 delta) ||
                rebaser == null ||
                delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            rebaser.Rebase(delta);
        }

        WorldOriginBroadcast CreateBroadcast()
        {
            return new WorldOriginBroadcast(
                state.Sequence,
                rebaser != null ? rebaser.LastOriginOffset : Vector3.zero,
                state.AccumulatedOrigin);
        }

        bool TryResolveCollectiveTrackingPosition(out Vector3 position)
        {
            position = default;
            trackingObservers.Clear();
            if (serverTrackingObserverSource == null)
            {
                serverTrackingObserverGroup = null;
                return false;
            }

            serverTrackingObserverGroup ??=
                serverTrackingObserverSource as ICelestialSurfaceCollisionObserverGroup;
            if (serverTrackingObserverGroup == null)
            {
                return false;
            }

            serverTrackingObserverGroup.GetSurfaceCollisionObservers(trackingObservers);
            return TryResolveCollectiveTrackingPosition(trackingObservers, out position);
        }

        internal static bool TryResolveCollectiveTrackingPosition(
            IReadOnlyList<CelestialSurfaceCollisionObserverState> observers,
            out Vector3 position)
        {
            position = default;
            if (observers == null)
            {
                return false;
            }

            Vector3 minimum = default;
            Vector3 maximum = default;
            bool hasPosition = false;
            for (int i = 0; i < observers.Count; i++)
            {
                CelestialSurfaceCollisionObserverState observer = observers[i];
                if (!observer.IsValid)
                {
                    continue;
                }

                if (!hasPosition)
                {
                    minimum = observer.Position;
                    maximum = observer.Position;
                    hasPosition = true;
                    continue;
                }

                minimum = Vector3.Min(minimum, observer.Position);
                maximum = Vector3.Max(maximum, observer.Position);
            }

            position = (minimum + maximum) * 0.5f;
            return hasPosition;
        }
    }
}
