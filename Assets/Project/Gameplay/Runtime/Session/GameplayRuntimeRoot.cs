using System.Collections.Generic;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.ResourceNodes;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Session
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameplayRuntimeRoot : MonoBehaviour
    {
        [Header("Definitions")]
        [SerializeField] GameplayDefinitionRegistry definitions;

        [Header("Simulation")]
        [SerializeField] GravitySimulation gravitySimulation;
        [SerializeField] CelestialFrameProvider celestialFrameProvider;
        [SerializeField] WorldOriginRebaser originRebaser;
        [SerializeField] List<ResourceDepositRuntimeSpawner> resourceStreamers = new();

        [Header("Fleet")]
        [SerializeField] FleetRuntime fleet;

        GameplayRuntimeBindings bindings;

        public GameplayRuntimeBindings Bindings =>
            bindings ??= new GameplayRuntimeBindings(
                definitions,
                gravitySimulation,
                celestialFrameProvider,
                originRebaser,
                fleet,
                resourceStreamers);

        public bool HasValidAuthoring => Bindings.IsValid;

        void OnValidate()
        {
            fleet ??= GetComponent<FleetRuntime>();
            resourceStreamers ??= new List<ResourceDepositRuntimeSpawner>();
            resourceStreamers.RemoveAll(streamer => streamer == null);
            bindings = null;
        }
    }
}
