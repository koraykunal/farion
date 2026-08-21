using System.Collections.Generic;
using Farion.Multiplayer.Session;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Predicting;
using FishNet.Managing.Timing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.World
{
    [DisallowMultipleComponent]
    public sealed class ZonePhysicsTickDriver : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManager;

        readonly List<SimulationZoneContext> zones = new();
        readonly List<MultiplayerSceneContext> zoneSceneContexts = new();
        PredictionManager predictionManager;
        bool subscribed;
        double simulationEpochSeconds;

        public int RegisteredZoneCount => zones.Count;
        public double SimulationEpochSeconds => simulationEpochSeconds;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            if (networkManager != null)
            {
                predictionManager = networkManager.GetComponent<PredictionManager>();
                networkManager.ClientManager
                    .RegisterBroadcast<SimulationEpochBroadcast>(OnEpochBroadcast);
            }
        }

        void Start()
        {
            Subscribe();
        }

        void OnDestroy()
        {
            networkManager?.ClientManager
                .UnregisterBroadcast<SimulationEpochBroadcast>(OnEpochBroadcast);
            Unsubscribe();
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                zones[i]?.UnbindPhysicsDriver(this);
            }

            zones.Clear();
            zoneSceneContexts.Clear();
        }

        public bool RegisterZone(SimulationZoneContext context)
        {
            if (context == null ||
                !context.IsConfigured ||
                !TryGetIsolatedPhysicsScene(context, out PhysicsScene physicsScene))
            {
                return false;
            }

            for (int i = 0; i < zones.Count; i++)
            {
                SimulationZoneContext registered = zones[i];
                if (registered == context ||
                    (registered != null &&
                     TryGetIsolatedPhysicsScene(registered, out PhysicsScene existing) &&
                     existing == physicsScene))
                {
                    return false;
                }
            }

            zones.Add(context);
            zoneSceneContexts.Add(null);
            context.BindPhysicsDriver(this);
            return true;
        }

        public bool UnregisterZone(SimulationZoneContext context)
        {
            if (context == null)
            {
                return false;
            }

            int index = zones.IndexOf(context);
            if (index < 0)
            {
                return false;
            }

            zones.RemoveAt(index);
            zoneSceneContexts.RemoveAt(index);
            context.UnbindPhysicsDriver(this);
            return true;
        }

        public int SimulateRegisteredZones(float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    "Zone physics requires a finite positive tick delta.");
            }

            int simulated = 0;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                SimulationZoneContext context = zones[i];
                if (context == null ||
                    !context.IsConfigured ||
                    !TryGetIsolatedPhysicsScene(context, out PhysicsScene physicsScene))
                {
                    zones.RemoveAt(i);
                    zoneSceneContexts.RemoveAt(i);
                    context?.UnbindPhysicsDriver(this);
                    continue;
                }

                physicsScene.Simulate(deltaTime);
                simulated++;
            }

            return simulated;
        }

        void Subscribe()
        {
            if (subscribed || networkManager?.TimeManager == null)
            {
                return;
            }

            subscribed = true;
            networkManager.TimeManager.OnPrePhysicsSimulation +=
                TimeManager_OnPrePhysicsSimulation;
        }

        void Unsubscribe()
        {
            if (!subscribed || networkManager?.TimeManager == null)
            {
                return;
            }

            subscribed = false;
            networkManager.TimeManager.OnPrePhysicsSimulation -=
                TimeManager_OnPrePhysicsSimulation;
        }

        void TimeManager_OnPrePhysicsSimulation(float deltaTime)
        {
            ApplyNetworkSimulationTime();
            SimulateRegisteredZones(deltaTime);
        }

        public void ResetSession()
        {
            simulationEpochSeconds = 0d;
        }

        public void AdoptRestoredSimulationTime()
        {
            if (networkManager == null || !networkManager.IsServerStarted)
            {
                return;
            }

            double restored = ResolveRestoredSimulationTime();
            simulationEpochSeconds = System.Math.Max(
                0d,
                restored - ResolveSimulationTick() * networkManager.TimeManager.TickDelta);
            BroadcastEpoch();
        }

        public void SendSimulationEpoch(NetworkConnection connection)
        {
            if (connection == null || networkManager?.ServerManager == null)
            {
                return;
            }

            networkManager.ServerManager.Broadcast(
                connection,
                new SimulationEpochBroadcast(simulationEpochSeconds),
                requireAuthenticated: true,
                channel: Channel.Reliable);
        }

        double ResolveRestoredSimulationTime()
        {
            for (int i = 0; i < zones.Count; i++)
            {
                MultiplayerSceneContext context = ResolveSceneContext(i);
                if (context?.GravitySimulation != null)
                {
                    return context.GravitySimulation.SimulationTime;
                }
            }

            return 0d;
        }

        void BroadcastEpoch()
        {
            networkManager.ServerManager.Broadcast(
                new SimulationEpochBroadcast(simulationEpochSeconds),
                requireAuthenticated: true,
                channel: Channel.Reliable);
        }

        void OnEpochBroadcast(SimulationEpochBroadcast message, Channel channel)
        {
            if (networkManager != null && networkManager.IsServerStarted)
            {
                return;
            }

            simulationEpochSeconds = message.EpochSeconds >= 0d
                ? message.EpochSeconds
                : 0d;
        }

        MultiplayerSceneContext ResolveSceneContext(int index)
        {
            MultiplayerSceneContext context = zoneSceneContexts[index];
            if (context != null)
            {
                return context;
            }

            context = MultiplayerSceneContext.FindIn(zones[index].Scene);
            zoneSceneContexts[index] = context;
            return context;
        }

        void ApplyNetworkSimulationTime()
        {
            if (networkManager?.TimeManager == null)
            {
                return;
            }

            double seconds = simulationEpochSeconds +
                ResolveSimulationTick() * networkManager.TimeManager.TickDelta;
            for (int i = 0; i < zoneSceneContexts.Count; i++)
            {
                ResolveSceneContext(i)?.ApplyNetworkSimulationTime(seconds);
            }
        }

        uint ResolveSimulationTick()
        {
            return predictionManager != null &&
                predictionManager.IsReconciling &&
                predictionManager.ClientReplayTick != TimeManager.UNSET_TICK
                    ? predictionManager.ClientReplayTick
                    : networkManager.TimeManager.Tick;
        }

        static bool TryGetIsolatedPhysicsScene(
            SimulationZoneContext context,
            out PhysicsScene physicsScene)
        {
            physicsScene = context.PhysicsScene;
            return context.Scene.IsValid() &&
                context.Scene.isLoaded &&
                physicsScene.IsValid() &&
                physicsScene != Physics.defaultPhysicsScene;
        }
    }
}
