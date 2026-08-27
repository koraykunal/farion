using System;
using System.Collections.Generic;
using Farion.Core.Identity;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.World
{
    public sealed class ZoneOriginState
    {
        readonly WorldOriginSequenceState state = new();
        readonly List<CelestialSurfaceCollisionObserverState> trackingObservers = new();

        WorldOriginRebaser rebaser;
        GravitySimulation simulation;
        Transform trackingTarget;
        MonoBehaviour observerSource;
        ICelestialSurfaceCollisionObserverGroup observerGroup;
        WorldOriginBroadcast pendingFrame;
        bool hasPendingFrame;
        int pinnedBodyId;

        internal ZoneOriginState(GeneratedEntityId zoneId, Scene scene)
        {
            ZoneId = zoneId;
            Scene = scene;
        }

        public GeneratedEntityId ZoneId { get; }
        public Scene Scene { get; internal set; }
        public uint CurrentSequence => state.Sequence;
        public int ReferenceBodyId => state.ReferenceBodyId;
        public Vector3 AccumulatedOrigin => state.AccumulatedOrigin;
        internal bool HasPendingFrame => hasPendingFrame;
        internal WorldOriginBroadcast PendingFrame => pendingFrame;
        internal GravitySimulation Simulation => simulation;
        internal WorldOriginRebaser Rebaser => rebaser;
        internal int PinnedBodyId => pinnedBodyId;

        public event Action<Vector3> OriginShifted;

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

        internal void BindRuntime(
            WorldOriginRebaser nextRebaser,
            GravitySimulation nextSimulation,
            MonoBehaviour nextObserverSource)
        {
            if (rebaser != null && rebaser != nextRebaser)
            {
                rebaser.Rebased -= HandleRebased;
            }

            simulation = nextSimulation;
            observerSource = nextObserverSource;
            observerGroup =
                nextObserverSource as ICelestialSurfaceCollisionObserverGroup;
            trackingObservers.Clear();
            if (simulation != null && state.Sequence > 0)
            {
                simulation.ApplyNetworkPhysicsReferenceBody(state.ReferenceBodyId);
            }

            if (rebaser == nextRebaser)
            {
                return;
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

        internal void Unbind()
        {
            if (rebaser != null)
            {
                rebaser.Rebased -= HandleRebased;
            }

            rebaser = null;
            simulation = null;
            trackingTarget = null;
            observerSource = null;
            observerGroup = null;
            trackingObservers.Clear();
            pendingFrame = default;
            hasPendingFrame = false;
        }

        internal void SetTrackingTarget(Transform target)
        {
            trackingTarget = target;
        }

        internal void PinReferenceBody(int bodyStableId)
        {
            pinnedBodyId = bodyStableId;
        }

        internal void StashPendingFrame(WorldOriginBroadcast frame)
        {
            if (!state.CanAccept(frame.Sequence) ||
                (hasPendingFrame && frame.Sequence <= pendingFrame.Sequence))
            {
                return;
            }

            pendingFrame = frame;
            hasPendingFrame = true;
        }

        internal void SetServerPendingFrame(WorldOriginBroadcast frame)
        {
            pendingFrame = frame;
            hasPendingFrame = true;
        }

        internal void ApplyPendingFrameIfDue(uint currentTick)
        {
            if (!hasPendingFrame ||
                !MultiplayerWorldOriginAuthority.HasReachedTick(
                    currentTick,
                    pendingFrame.ApplyTick))
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

        internal bool TryBuildNextFrame(
            uint currentTick,
            uint leadTicks,
            out WorldOriginBroadcast frame)
        {
            frame = default;
            if (hasPendingFrame)
            {
                return false;
            }

            int referenceBodyId = ResolveServerReferenceBodyId();
            bool referenceChanged = referenceBodyId != state.ReferenceBodyId;
            bool originChanged = TryResolveServerRebaseOffset(out Vector3 originDelta);
            if (!referenceChanged && !originChanged)
            {
                return false;
            }

            frame = new WorldOriginBroadcast(
                ZoneId.Value,
                state.Sequence + 1,
                state.AccumulatedOrigin + originDelta,
                referenceBodyId,
                currentTick + leadTicks);
            return true;
        }

        internal bool AdoptRestoredOrigin()
        {
            if (rebaser == null)
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
                simulation != null
                    ? simulation.PhysicsReferenceStableId
                    : state.ReferenceBodyId);
            return true;
        }

        internal WorldOriginBroadcast CreateCurrentBroadcast(uint applyTick)
        {
            return new WorldOriginBroadcast(
                ZoneId.Value,
                state.Sequence,
                state.AccumulatedOrigin,
                state.ReferenceBodyId,
                applyTick);
        }

        internal void Reset()
        {
            state.Reset();
            Unbind();
            pinnedBodyId = 0;
        }

        void HandleRebased(Vector3 originOffset)
        {
            OriginShifted?.Invoke(originOffset);
        }

        int ResolveServerReferenceBodyId()
        {
            if (pinnedBodyId != 0)
            {
                return pinnedBodyId;
            }

            if (simulation == null || observerSource == null)
            {
                return state.ReferenceBodyId;
            }

            observerGroup ??=
                observerSource as ICelestialSurfaceCollisionObserverGroup;
            if (observerGroup == null)
            {
                return simulation.PhysicsReferenceStableId;
            }

            trackingObservers.Clear();
            observerGroup.GetSurfaceCollisionObservers(trackingObservers);
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
            if (!hasCollectivePosition && trackingTarget == null)
            {
                return false;
            }

            if (!hasCollectivePosition)
            {
                trackingPosition = trackingTarget.position;
            }

            if (trackingTarget != null)
            {
                rebaser.SetTrackingTarget(trackingTarget);
            }

            return rebaser.TryGetRebaseOffset(trackingPosition, out originDelta);
        }

        bool TryResolveCollectiveTrackingPosition(out Vector3 position)
        {
            position = default;
            trackingObservers.Clear();
            if (observerSource == null)
            {
                observerGroup = null;
                return false;
            }

            observerGroup ??=
                observerSource as ICelestialSurfaceCollisionObserverGroup;
            if (observerGroup == null)
            {
                return false;
            }

            observerGroup.GetSurfaceCollisionObservers(trackingObservers);
            return MultiplayerWorldOriginAuthority
                .TryResolveCollectiveTrackingPosition(trackingObservers, out position);
        }
    }
}
