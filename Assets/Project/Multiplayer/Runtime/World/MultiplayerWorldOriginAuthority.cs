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
        const uint FrameChangeLeadTicks = 12;

        [SerializeField] NetworkManager networkManager;

        readonly WorldOriginSequenceState state = new();
        readonly List<CelestialSurfaceCollisionObserverState> trackingObservers = new();
        WorldOriginRebaser rebaser;
        GravitySimulation simulation;
        Transform serverTrackingTarget;
        MonoBehaviour serverTrackingObserverSource;
        ICelestialSurfaceCollisionObserverGroup serverTrackingObserverGroup;
        WorldOriginBroadcast pendingFrame;
        bool hasPendingFrame;

        public uint CurrentSequence => state.Sequence;

        public bool SharesReferenceFrame(uint sequence)
        {
            if (sequence == state.Sequence)
            {
                return true;
            }

            if (state.TryGetReferenceBody(sequence, out int referenceBodyId))
            {
                return referenceBodyId == state.ReferenceBodyId;
            }

            if (hasPendingFrame && pendingFrame.Sequence == sequence)
            {
                return pendingFrame.ReferenceBodyId == state.ReferenceBodyId;
            }

            return false;
        }

        public event Action<Vector3> OriginShifted;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            networkManager.ClientManager.RegisterBroadcast<WorldOriginBroadcast>(
                OnOriginBroadcast);
            networkManager.TimeManager.OnPreTick += OnPreTick;
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
            networkManager.TimeManager.OnPreTick -= OnPreTick;
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

        public void BindSimulation(GravitySimulation nextSimulation)
        {
            simulation = nextSimulation;
            if (simulation != null && state.Sequence > 0)
            {
                simulation.ApplyNetworkPhysicsReferenceBody(
                    state.ReferenceBodyId);
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
            simulation = null;
            serverTrackingTarget = null;
            SetServerTrackingObserverSource(null);
            pendingFrame = default;
            hasPendingFrame = false;
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

            state.RecordServerShift(
                rebaser.AccumulatedOriginOffset,
                simulation != null ? simulation.PhysicsReferenceStableId : state.ReferenceBodyId);
            networkManager.ServerManager.Broadcast(
                CreateCurrentBroadcast(networkManager.TimeManager.Tick),
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
                hasPendingFrame
                    ? pendingFrame
                    : CreateCurrentBroadcast(networkManager.TimeManager.Tick),
                requireAuthenticated: true,
                channel: Channel.Reliable);
        }

        void OnPreTick()
        {
            if (!hasPendingFrame ||
                !HasReachedTick(networkManager.TimeManager.Tick, pendingFrame.ApplyTick))
            {
                return;
            }

            WorldOriginBroadcast frame = pendingFrame;
            hasPendingFrame = false;
            pendingFrame = default;
            if (!state.TryAccept(
                    frame.Sequence,
                    frame.AccumulatedOrigin,
                    frame.ReferenceBodyId,
                    out Vector3 delta))
            {
                return;
            }

            simulation?.ApplyNetworkPhysicsReferenceBody(frame.ReferenceBodyId);
            if (rebaser != null && delta.sqrMagnitude > Mathf.Epsilon)
            {
                rebaser.Rebase(delta);
            }
        }

        void OnPostTick()
        {
            if (!networkManager.IsServerStarted || hasPendingFrame)
            {
                return;
            }

            int referenceBodyId = ResolveServerReferenceBodyId();
            bool referenceChanged = referenceBodyId != state.ReferenceBodyId;
            bool originChanged = TryResolveServerRebaseOffset(out Vector3 originDelta);
            if (!referenceChanged && !originChanged)
            {
                return;
            }

            uint applyTick = networkManager.TimeManager.Tick + FrameChangeLeadTicks;
            pendingFrame = new WorldOriginBroadcast(
                state.Sequence + 1,
                state.AccumulatedOrigin + originDelta,
                referenceBodyId,
                applyTick);
            hasPendingFrame = true;
            networkManager.ServerManager.Broadcast(
                pendingFrame,
                requireAuthenticated: true,
                channel: Channel.Reliable);
        }

        int ResolveServerReferenceBodyId()
        {
            if (simulation == null || serverTrackingObserverSource == null)
            {
                return state.ReferenceBodyId;
            }

            serverTrackingObserverGroup ??=
                serverTrackingObserverSource as ICelestialSurfaceCollisionObserverGroup;
            if (serverTrackingObserverGroup == null)
            {
                return simulation.PhysicsReferenceStableId;
            }

            trackingObservers.Clear();
            serverTrackingObserverGroup.GetSurfaceCollisionObservers(trackingObservers);
            CelestialBody candidate =
                simulation.ResolvePhysicsReferenceBodyCandidate(trackingObservers);
            return candidate != null
                ? candidate.StableId
                : simulation.PhysicsReferenceStableId;
        }

        bool TryResolveServerRebaseOffset(out Vector3 originDelta)
        {
            originDelta = Vector3.zero;
            if (rebaser == null)
            {
                return false;
            }

            bool hasCollectivePosition =
                TryResolveCollectiveTrackingPosition(out Vector3 trackingPosition);
            if (!hasCollectivePosition && serverTrackingTarget == null)
            {
                return false;
            }

            if (!hasCollectivePosition)
            {
                trackingPosition = serverTrackingTarget.position;
            }

            if (serverTrackingTarget != null)
            {
                rebaser.SetTrackingTarget(serverTrackingTarget);
            }

            return rebaser.TryGetRebaseOffset(trackingPosition, out originDelta);
        }

        void OnOriginBroadcast(WorldOriginBroadcast message, Channel channel)
        {
            if (networkManager.IsServerStarted ||
                !state.CanAccept(message.Sequence) ||
                (hasPendingFrame && message.Sequence <= pendingFrame.Sequence))
            {
                return;
            }

            pendingFrame = message;
            hasPendingFrame = true;
        }

        WorldOriginBroadcast CreateCurrentBroadcast(uint applyTick)
        {
            return new WorldOriginBroadcast(
                state.Sequence,
                state.AccumulatedOrigin,
                state.ReferenceBodyId,
                applyTick);
        }

        internal static bool HasReachedTick(uint currentTick, uint targetTick)
        {
            return unchecked((int)(currentTick - targetTick)) >= 0;
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
