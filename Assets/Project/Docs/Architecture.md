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

## Project Folder Ownership

- `Application`, `Audio`, `Core`, `Gameplay`, `Rendering`, `Simulation`, and
  `UI` contain C# runtime code. Assembly folders follow runtime ownership;
  files are not regrouped merely for visual symmetry.
- `Art` contains Unity-ready models, materials, textures, shaders, and VFX.
  Editable source art lives in repository-level `ArtSource`; production audio
  media is owned by the repository-level FMOD Studio project.
- `Design` contains immutable ScriptableObject definitions.
- `Prefabs` contains authored reusable GameObject composition; `Scenes`
  contains authored scene composition.
- `Editor` contains validation and authoring support that never owns runtime
  state.
- `Localization`, `Tests`, and `Docs` contain their named support assets.

`Assets/Project/Audio` is runtime audio code. `FMODProject/FarionAudio` owns
audio authoring and built banks; Unity contains only the runtime bank copies.
`Fleet` is a gameplay ownership term; the physical home ship is organized as a
`CapitalShip` asset.

## Assembly Direction

```text
Farion.Simulation.Runtime --> Core
Farion.Gameplay.Runtime   --> Domain + Core + Simulation
Farion.Gameplay.Presentation --> Gameplay + Simulation + VFX Graph
Farion.App.Runtime        --> Domain + Core + Gameplay
Farion.Rendering.Runtime  --> Core + Simulation + URP
Farion.Audio.Runtime      --> Core + Simulation + Gameplay + FMOD
Farion.UI.Runtime         --> Core + Gameplay + Simulation + App + Audio
```

- `Farion.Core.Runtime` owns cross-cutting time, persistence identity, and
  save-slot infrastructure.
- `Farion.Simulation.Runtime` owns gravity composition, authored and generated
  celestial state, planetary sampling, deterministic world identity, streaming
  coordinates, and local origin rebasing.
- `Farion.Gameplay.Domain` has `noEngineReferences`. It currently owns
  `DefinitionId`, `PersistentEntityId`, revisioned stack inventory state, and
  Fleet Knowledge state.
- `Farion.Gameplay.Runtime` adapts domain and simulation contracts to Unity. It
  owns character, flight, possession, resources, inventory adapters, the
  assigned shuttle binding, Fleet identity, Fleet Storage, Fleet Knowledge, and
  save participants.
- `Farion.Gameplay.Presentation` owns gameplay-facing VFX components and is the
  only gameplay assembly that references VFX Graph.
- `Farion.App.Runtime` owns game flow, local-session composition, save
  requests, and the command facade. Its current gameplay command surface
  authorizes resource harvesting, assigned-shuttle cargo loading/unloading,
  and the first Fleet processing exchange. The present local gateway still
  accepts resolved Unity runtime objects; before networking, its outer request
  boundary must carry persistent ids and expected revisions while application
  handlers remain the authoritative object resolver.
- `Farion.UI.Runtime` presents menus, HUD, settings, save/load, inventory, and
  feedback. It requests application operations and never mutates domain state
  directly.
- `Farion.Rendering.Runtime` owns URP-specific celestial presentation.
- `Farion.Audio.Runtime` owns the persistent FMOD mix, scene/environment
  adaptation, UI cue dispatch, and spacecraft presentation driven by gameplay
  telemetry.

Dependencies do not point back up this list. Domain code does not reference
Unity, gameplay does not reference UI, and simulation does not reference
rendering.

## Runtime Composition

`GameplayRuntimeRoot` is the authored composition root for the active gameplay
scene. It explicitly references:

- `GameplayDefinitionRegistry`;
- `GravitySimulation`;
- `CelestialFrameProvider` bound to the same simulation;
- `WorldOriginRebaser`;
- resource deposit streamers;
- the local `PlayerInventory`;
- `PlayerPossessionController`;
- the assigned `ShuttleRuntimeBinding`;
- `FleetRuntime`, which owns the Fleet identity and references Fleet Storage
  and Fleet Knowledge.

`GameplayRuntimeBindings` is the immutable view of those references.
`GameplaySessionRuntime`, `GameplaySessionController`, command handlers, and
save participants consume that same view. The root assigns gravity and celestial
frame authority to the session shuttle explicitly; production actors do not use
static active-instance fallbacks or independent scene searches.

`GameplaySessionIdentity` keeps Fleet, local player, explorer actor, assigned
shuttle, and carried inventory ids distinct. Possession describes who is
currently controlled; it does not decide which shuttle belongs to the session.

## Implemented Gameplay State

### Inventory

`InventoryContainerState` is the stack quantity, capacity, and revision
authority. `InventoryContainerComponent` is its reusable Unity adapter.
`PlayerInventory`, `ShuttleCargoInventory`, and `FleetStorageInventory`
specialize that adapter without duplicating stack mutation.

`InventoryTransferService` performs copy-on-write, revision-checked transfers
between domain containers. A failed transfer leaves both containers unchanged.

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

### Fleet

`FleetRuntime` is the authored shared-progression owner. Its persistent Fleet
id determines the Fleet Storage identity, and it references the co-located
`FleetStorageInventory` and `FleetKnowledgeRuntime`.

`FleetKnowledgeState` stores Fleet-owned capabilities, blueprints, and
discoveries as independent id sets with one monotonic revision. Fleet Storage
owns shared material stacks.

Fleet Knowledge does not imply that a research terminal, research tree, or
crafting system currently exists.

Production spacecraft prefabs use the same authored composition roots:
`VisualRoot`, `CollisionRoot`, `RuntimeRoot`, and `Anchors`. Physical colliders
use `COL_*` names under `CollisionRoot`; `Exterior`, `Interior`, and `Landing`
subgroups are added only when that category exists. Runtime components remain
on the owning spacecraft root, while authored interaction objects and volumes
live under `RuntimeRoot`.

`PF_CapitalShip_Fleet` is the physical home-ship presentation and interaction
boundary. The production prefab does not duplicate `FleetRuntime` or introduce
mutable capital-ship state.

### Resources and Commands

Deterministic resource deposits separate generated base data from extraction
deltas. `ResourceNodeInteractable` submits harvest intent through
`IGameplayCommandGateway`. `GameplayCommandService` and
`InventoryCommandHandler` validate the active session and execute the harvest
transaction.

`FleetCargoCommandHandler` resolves only the session-assigned shuttle cargo and
the local carried inventory or active Fleet Storage.
`CargoTransferTransaction` validates definition compatibility and commits the
complete transfer atomically.

`ShuttleCargoHatchInteractable` loads the assigned shuttle.
`FleetCargoUnloadInteractable` exposes unload only through the authored docking
boundary. `FleetProcessingInteractable` submits a typed processing recipe
through the same session command facade; the handler commits one atomic
Fleet-Storage exchange. There are no inactive crafting, research, equipment,
repair, queue, power, or maintenance commands in the application facade.

## Persistence

Schema `6` persists:

- celestial body snapshots;
- world-origin metadata;
- player inventory;
- assigned shuttle cargo;
- Fleet Storage;
- player possession;
- Fleet Knowledge;
- resource extraction deltas.

Schemas `3` and `4` migrate to schema `6` with explicit empty shuttle-cargo,
Fleet-Storage, and Fleet-Knowledge defaults. Schema `5` preserves its shuttle
cargo and Fleet Knowledge while adding the authored Fleet Storage as empty.
The serialized JSON field for shuttle cargo remains `personalShipCargo` for
schema compatibility; runtime code exposes it as `ShuttleCargo`.

The save system uses participants composed from `GameplayRuntimeRoot`, validates
before applying, and restores a pre-load snapshot if application fails.

Capital-ship state, shuttle upgrades, and equipment are not in schema `6`. A
new schema is allowed only after those runtime owners exist.

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
  references, definition registry consistency, audio composition, and authored
  flight/VFX contracts.
- Definitions live under `Assets/Project/Design`; runtime quantities and
  unlocks never live in those assets.

## Deliberately Absent

The following systems are product direction, not current implementation:

- mutable capital-ship state, owned rooms, or production-machine state;
- refinery/fabricator queues, timers, power, heat, or maintenance;
- shuttle and capital-ship upgrades;
- equipment instances and loadouts;
- research terminals or technology voting;
- multiplayer transport and replication.

The next safe architectural increment is to finish the imported Capital Ship
Fleet visual alignment, present processing results, and prove the local
unload/processing state through save/load. See `FleetArchitecture.md` and
`BetaRoadmap.md`.
