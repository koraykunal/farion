using System.Collections.Generic;
using FishNet.Managing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.World
{
    [DisallowMultipleComponent]
    public sealed class ZonePhysicsTickDriver : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManager;

        readonly List<SimulationZoneContext> zones = new();
        bool subscribed;

        public int RegisteredZoneCount => zones.Count;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
        }

        void Start()
        {
            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                zones[i]?.UnbindPhysicsDriver(this);
            }

            zones.Clear();
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
            context.BindPhysicsDriver(this);
            return true;
        }

        public bool UnregisterZone(SimulationZoneContext context)
        {
            if (context == null || !zones.Remove(context))
            {
                return false;
            }

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
            SimulateRegisteredZones(deltaTime);
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
