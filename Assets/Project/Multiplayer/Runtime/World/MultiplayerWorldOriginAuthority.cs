using System;
using System.Collections.Generic;
using Farion.Core.Identity;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class MultiplayerWorldOriginAuthority : MonoBehaviour
    {
        const uint FrameChangeLeadTicks = 12;

        [SerializeField] NetworkManager networkManager;

        readonly Dictionary<GeneratedEntityId, ZoneOriginState> zones = new();
        readonly Dictionary<GeneratedEntityId, WorldOriginBroadcast> unboundFrames = new();
        readonly List<ZoneOriginState> zoneBuffer = new();

        public event Action<GeneratedEntityId, Vector3> OriginShifted;

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

        public ZoneOriginState BindZone(
            GeneratedEntityId zoneId,
            Scene scene,
            WorldOriginRebaser rebaser,
            GravitySimulation simulation,
            MonoBehaviour observerSource)
        {
            if (!zoneId.IsValid)
            {
                return null;
            }

            if (!zones.TryGetValue(zoneId, out ZoneOriginState zone))
            {
                zone = new ZoneOriginState(zoneId, scene);
                zone.OriginShifted += offset =>
                    OriginShifted?.Invoke(zoneId, offset);
                zones[zoneId] = zone;
            }

            zone.Scene = scene;
            zone.BindRuntime(rebaser, simulation, observerSource);
            if (unboundFrames.Remove(zoneId, out WorldOriginBroadcast stashed))
            {
                zone.StashPendingFrame(stashed);
            }

            return zone;
        }

        public bool TryGetZone(GeneratedEntityId zoneId, out ZoneOriginState zone)
        {
            return zones.TryGetValue(zoneId, out zone) && zone != null;
        }

        public void SetServerTrackingTarget(
            GeneratedEntityId zoneId,
            Transform target)
        {
            if (zones.TryGetValue(zoneId, out ZoneOriginState zone))
            {
                zone.SetTrackingTarget(target);
            }
        }

        public void PinReferenceBody(GeneratedEntityId zoneId, int bodyStableId)
        {
            if (zones.TryGetValue(zoneId, out ZoneOriginState zone))
            {
                zone.PinReferenceBody(bodyStableId);
            }
        }

        public void ResetSession()
        {
            foreach (ZoneOriginState zone in zones.Values)
            {
                zone.Reset();
            }

            zones.Clear();
            unboundFrames.Clear();
        }

        public bool AdoptRestoredOrigin(GeneratedEntityId zoneId)
        {
            if (!networkManager.IsServerStarted ||
                !zones.TryGetValue(zoneId, out ZoneOriginState zone) ||
                !zone.AdoptRestoredOrigin())
            {
                return false;
            }

            BroadcastToZone(
                zone,
                zone.CreateCurrentBroadcast(networkManager.TimeManager.Tick));
            return true;
        }

        public void SendCurrentOrigin(
            NetworkConnection connection,
            GeneratedEntityId zoneId)
        {
            if (connection == null ||
                !networkManager.IsServerStarted ||
                !zones.TryGetValue(zoneId, out ZoneOriginState zone))
            {
                return;
            }

            networkManager.ServerManager.Broadcast(
                connection,
                zone.HasPendingFrame
                    ? zone.PendingFrame
                    : zone.CreateCurrentBroadcast(networkManager.TimeManager.Tick),
                requireAuthenticated: true,
                channel: Channel.Reliable);
        }

        void OnPreTick()
        {
            if (zones.Count == 0)
            {
                return;
            }

            zoneBuffer.Clear();
            zoneBuffer.AddRange(zones.Values);
            uint tick = networkManager.TimeManager.Tick;
            for (int i = 0; i < zoneBuffer.Count; i++)
            {
                zoneBuffer[i].ApplyPendingFrameIfDue(tick);
            }
        }

        void OnPostTick()
        {
            if (!networkManager.IsServerStarted || zones.Count == 0)
            {
                return;
            }

            zoneBuffer.Clear();
            zoneBuffer.AddRange(zones.Values);
            uint tick = networkManager.TimeManager.Tick;
            for (int i = 0; i < zoneBuffer.Count; i++)
            {
                ZoneOriginState zone = zoneBuffer[i];
                if (!zone.TryBuildNextFrame(
                        tick,
                        FrameChangeLeadTicks,
                        out WorldOriginBroadcast frame))
                {
                    continue;
                }

                zone.SetServerPendingFrame(frame);
                BroadcastToZone(zone, frame);
            }
        }

        void BroadcastToZone(ZoneOriginState zone, WorldOriginBroadcast frame)
        {
            foreach (NetworkConnection connection in
                     networkManager.ServerManager.Clients.Values)
            {
                if (connection.Scenes.Contains(zone.Scene))
                {
                    networkManager.ServerManager.Broadcast(
                        connection,
                        frame,
                        requireAuthenticated: true,
                        channel: Channel.Reliable);
                }
            }
        }

        void OnOriginBroadcast(WorldOriginBroadcast message, Channel channel)
        {
            if (networkManager.IsServerStarted || message.ZoneId == 0UL)
            {
                return;
            }

            GeneratedEntityId zoneId = new(message.ZoneId);
            if (zones.TryGetValue(zoneId, out ZoneOriginState zone))
            {
                zone.StashPendingFrame(message);
                return;
            }

            if (!unboundFrames.TryGetValue(zoneId, out WorldOriginBroadcast existing) ||
                message.Sequence > existing.Sequence)
            {
                unboundFrames[zoneId] = message;
            }
        }

        internal static bool HasReachedTick(uint currentTick, uint targetTick)
        {
            return unchecked((int)(currentTick - targetTick)) >= 0;
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
