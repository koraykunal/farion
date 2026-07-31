using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Ships;
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
        [SerializeField] WorldOriginRebaser originRebaser;
        [SerializeField] List<ResourceDepositRuntimeSpawner> resourceStreamers = new();

        [Header("Local Session")]
        [SerializeField] PlayerInventory localPlayerInventory;
        [SerializeField] PlayerPossessionController possession;
        [SerializeField] ShuttleRuntimeBinding assignedShuttle;

        [Header("Fleet")]
        [SerializeField] FleetRuntime fleet;

        GameplayRuntimeBindings bindings;

        public GameplayRuntimeBindings Bindings =>
            bindings ??= new GameplayRuntimeBindings(
                definitions,
                gravitySimulation,
                originRebaser,
                localPlayerInventory,
                possession,
                fleet,
                assignedShuttle,
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
