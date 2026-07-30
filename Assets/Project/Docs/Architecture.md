# Farion Architecture

This document describes the implemented architecture. Future features belong in
`BetaRoadmap.md` and must not be presented here as active runtime systems.

## Architectural Rules

1. Every mutable state has one runtime owner.
2. ScriptableObjects are immutable authoring data, never save-game state.
3. UI and audio consume state; they do not own gameplay authority.
4. Scene composition is explicit. Runtime code does not build production scenes
   or prefabs to compensate for missing authoring.
5. New abstractions are added only when a playable workflow needs them.
6. Save schema changes follow runtime ownership, never precede it.
7. Single-player commands are designed so a later host can become the authority
   without moving rules into UI or networking code.

## Assembly Direction

```text
Farion.Core.Runtime
        |
        +--> Farion.Simulation.Runtime
        |
Farion.Gameplay.Domain
        |
        +--> Farion.Gameplay.Runtime
                    |
                    +--> Farion.Application.Runtime
                                |
                                +--> Farion.UI.Runtime

Farion.Rendering.Runtime --> Core + Simulation
Farion.Audio.Runtime     --> Core + Simulation + Gameplay + FMOD
```

- `Farion.Core.Runtime` owns shared physics, time, persistence identity, and
  save-slot infrastructure.
- `Farion.Simulation.Runtime` owns gravity composition, authored and generated
  celestial state, planetary sampling, deterministic world identity, streaming
  coordinates, and local origin rebasing.
- `Farion.Gameplay.Domain` has `noEngineReferences`. It currently owns
  `DefinitionId`, `PersistentEntityId`, revisioned stack inventory state, and
  Fleet Knowledge state.
- `Farion.Gameplay.Runtime` adapts domain and simulation contracts to Unity. It
  owns character, flight, possession, resources, inventory adapters, the
  assigned shuttle binding, Fleet Knowledge runtime state, and save
  participants.
- `Farion.Application.Runtime` owns game flow, local-session composition, save
  requests, and the command facade. Its current gameplay command surface is
  resource harvesting only.
- `Farion.UI.Runtime` presents menus, HUD, settings, save/load, inventory, and
  feedback. It requests application operations and never mutates domain state
  directly.
- `Farion.Rendering.Runtime` owns URP-specific celestial presentation.
- `Farion.Audio.Runtime` owns FMOD presentation driven by gameplay telemetry.

Dependencies do not point back up this list. Domain code does not reference
Unity, gameplay does not reference UI, and simulation does not reference
rendering.

## Runtime Composition

`GameplayRuntimeRoot` is the authored composition root for the active gameplay
scene. It explicitly references:

- `GameplayDefinitionRegistry`;
- `GravitySimulation`;
- `WorldOriginRebaser`;
- resource deposit streamers;
- the local `PlayerInventory`;
- `PlayerPossessionController`;
- the assigned `ShuttleRuntimeBinding`;
- `FleetKnowledgeRuntime`.

`GameplayRuntimeBindings` is the immutable view of those references.
`GameplaySessionRuntime`, `GameplaySessionController`, command handlers, and
save participants consume that same view. They do not perform independent scene
searches or create parallel state.

`GameplaySessionIdentity` keeps local player, explorer actor, assigned shuttle,
and carried inventory ids distinct. Possession describes who is currently
controlled; it does not decide which shuttle belongs to the session.

## Implemented Gameplay State

### Inventory

`InventoryContainerState` is the stack quantity, capacity, and revision
authority. `InventoryContainerComponent` is its reusable Unity adapter.
`PlayerInventory` and `ShuttleCargoInventory` specialize that adapter without
duplicating stack mutation.

The current inventory model is stack-only. Unique equipment instances, loadout
slots, equipment repositories, and installation transactions are intentionally
absent until a real equipment workflow is selected.

### Shuttle

`ShuttleRuntimeBinding` owns only the shuttle persistent id and references to
its `SpacecraftMotor` and `ShuttleCargoInventory`. Flight physics remains in the
flight system.

Hull, fuel, repair, module slots, and upgrade stats are not modeled by a
placeholder shuttle aggregate. Each will receive an explicit owner only when
its first playable use case is implemented.

### Fleet Knowledge

`FleetKnowledgeState` stores fleet-owned capabilities, blueprints, and
discoveries as independent id sets with one monotonic revision.
`FleetKnowledgeRuntime` is the Unity owner and snapshot adapter.

Fleet Knowledge does not imply that a research terminal, research tree, or
crafting system currently exists.

### Resources and Commands

Deterministic resource deposits separate generated base data from extraction
deltas. `ResourceNodeInteractable` submits harvest intent through
`IGameplayCommandGateway`. `GameplayCommandService` and
`InventoryCommandHandler` validate the active session and execute the harvest
transaction.

There are no inactive crafting, research, equipment, repair, or cargo-terminal
commands in the application facade.

## Persistence

Schema `5` persists:

- celestial body snapshots;
- world-origin metadata;
- player inventory;
- assigned shuttle cargo;
- player possession;
- Fleet Knowledge;
- resource extraction deltas.

Schemas `3` and `4` migrate to schema `5` with explicit empty shuttle-cargo and
Fleet-Knowledge defaults. The serialized JSON field for shuttle cargo remains
`personalShipCargo` for schema compatibility; runtime code exposes it as
`ShuttleCargo`.

The save system uses participants composed from `GameplayRuntimeRoot`, validates
before applying, and restores a pre-load snapshot if application fails.

Fleet Storage, capital-ship state, shuttle upgrades, and equipment are not in
schema `5`. A new schema is allowed only after those runtime owners exist.

## Simulation and Presentation

- `GravitySimulation` is the gravity and translating-reference-frame authority.
- `CelestialBody` owns runtime physical body state.
- `PlanetSurfaceModel` owns generated surface, climate, biome, material, and
  terrain-feature sampling.
- `WorldOriginRebaser` is a local Unity-space precision adapter, not a universe
  coordinate authority.
- `SpacecraftMotor` owns spacecraft motion and publishes telemetry.
- Camera, HUD, FMOD, and thruster VFX consume telemetry and do not feed state
  back into flight physics.
- Ocean and atmosphere rendering remain in rendering-owned URP paths while
  simulation exposes narrow environment contracts needed by gameplay.

## Authoring and Validation

- Production scenes and prefabs are authored assets under
  `Assets/Project/Scenes` and `Assets/Project/Prefabs`.
- `Assets/Project/Editor` may validate or rebuild a specific authored asset, but
  editor builders are not runtime composition owners.
- `FarionProjectValidation` checks missing scripts, persistent ids, runtime-root
  references, definition registry consistency, and authored flight/VFX
  contracts.
- Definitions live under `Assets/Project/Design`; runtime quantities and
  unlocks never live in those assets.

## Deliberately Absent

The following systems are product direction, not current implementation:

- Fleet runtime identity and shared Fleet Storage;
- authored capital ship and docking;
- shuttle unload workflow;
- refinery, fabricator, recipes, queues, power, or maintenance;
- shuttle and capital-ship upgrades;
- equipment instances and loadouts;
- research terminals or technology voting;
- multiplayer transport and replication.

The next safe architectural increment is Fleet runtime identity plus shared
Fleet Storage, followed by one explicit shuttle-to-storage unload transaction.
See `FleetArchitecture.md` and `BetaRoadmap.md`.
