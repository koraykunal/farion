using System.Collections.Generic;
using Farion.Multiplayer.Session;
using Farion.Simulation.Physics;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace Farion.Multiplayer.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class ZoneHandoffMonitor : MonoBehaviour
    {
        const float PollIntervalSeconds = 0.5f;
        const float DominanceHysteresisBias = 1.1f;

        [SerializeField] NetworkManager networkManager;
        [SerializeField] MultiplayerZoneCoordinator zoneCoordinator;
        [Tooltip("SOI değişiminin devir tetiklemeden önce kesintisiz sürmesi gereken saniye.")]
        [SerializeField, Min(0f)] float stableSeconds = 2.5f;

        readonly Dictionary<int, PendingDominance> pendingDominance = new();
        float nextPollTime;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            zoneCoordinator ??= GetComponent<MultiplayerZoneCoordinator>();
        }

        void Update()
        {
            if (networkManager == null ||
                zoneCoordinator == null ||
                !networkManager.IsServerStarted ||
                Time.unscaledTime < nextPollTime)
            {
                return;
            }

            nextPollTime = Time.unscaledTime + PollIntervalSeconds;
            Poll();
        }

        void Poll()
        {
            foreach (NetworkConnection connection in
                     networkManager.ServerManager.Clients.Values)
            {
                if (!connection.IsActive ||
                    zoneCoordinator.IsHandoffPending(connection))
                {
                    pendingDominance.Remove(connection.ClientId);
                    continue;
                }

                NetworkObject firstObject = connection.FirstObject;
                MultiplayerSceneContext context = firstObject != null
                    ? MultiplayerSceneContext.FindIn(firstObject.gameObject.scene)
                    : null;
                GravitySimulation simulation =
                    context != null ? context.GravitySimulation : null;
                if (simulation == null)
                {
                    pendingDominance.Remove(connection.ClientId);
                    continue;
                }

                CelestialBody pinned = ResolveBodyByStableId(
                    simulation,
                    simulation.PhysicsReferenceStableId);
                GravitySample dominant = simulation.FindDominantBody(
                    firstObject.transform.position,
                    null,
                    pinned,
                    DominanceHysteresisBias);
                if (!dominant.HasBody ||
                    pinned == null ||
                    dominant.Body == pinned)
                {
                    pendingDominance.Remove(connection.ClientId);
                    continue;
                }

                if (pendingDominance.TryGetValue(
                        connection.ClientId,
                        out PendingDominance pending) &&
                    pending.BodyStableId == dominant.Body.StableId)
                {
                    if (Time.unscaledTime - pending.StartedAt >= stableSeconds)
                    {
                        pendingDominance.Remove(connection.ClientId);
                        zoneCoordinator.BeginHandoff(connection, dominant.Body);
                    }

                    continue;
                }

                pendingDominance[connection.ClientId] = new PendingDominance(
                    dominant.Body.StableId,
                    Time.unscaledTime);
            }
        }

        static CelestialBody ResolveBodyByStableId(
            GravitySimulation simulation,
            int stableId)
        {
            if (stableId == 0)
            {
                return null;
            }

            IReadOnlyList<CelestialBody> bodies = simulation.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] != null && bodies[i].StableId == stableId)
                {
                    return bodies[i];
                }
            }

            return null;
        }

        readonly struct PendingDominance
        {
            public PendingDominance(int bodyStableId, float startedAt)
            {
                BodyStableId = bodyStableId;
                StartedAt = startedAt;
            }

            public int BodyStableId { get; }
            public float StartedAt { get; }
        }
    }
}
