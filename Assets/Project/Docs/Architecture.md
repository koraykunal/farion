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

- `App`, `Audio`, `Core`, `Gameplay`, `Multiplayer`, `Rendering`, `Simulation`,
  and `UI` contain C# runtime code. Assembly folders follow
  runtime ownership; files are not regrouped merely for visual symmetry.
- `Resources` holds only assets that must load before any scene exists. Its
  single entry is the multiplayer session root prefab, which the development
  command-line runner instantiates at `BeforeSceneLoad`. Nothing else may be
  added; every other asset is referenced explicitly from a scene or prefab.
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
Farion.Core.Identity      --> (nothing; pure C#)
Farion.Core.Runtime       --> Core.Identity
Farion.Gameplay.Domain    --> Core.Identity
Farion.Simulation.Runtime --> Core
Farion.Gameplay.Runtime   --> Core.Identity + Domain + Core + Simulation
Farion.Gameplay.Presentation --> Gameplay + Simulation + VFX Graph
Farion.App.Runtime        --> Core.Identity + Domain + Core + Gameplay
Farion.Rendering.Runtime  --> Core + Simulation + URP
Farion.Audio.Runtime      --> Core + Simulation + Gameplay + FMOD
Farion.UI.Runtime         --> Core + Gameplay + Simulation + App + Audio
Farion.Multiplayer.Runtime --> Core + Simulation + Gameplay + Rendering + UI + FishNet
Farion.Editor            --> every runtime assembly (Editor platform only)
```

Assembly references are written by name. GUID references are not used, so the
graph stays readable in the `.asmdef` files themselves.

A namespace is the owning assembly's root namespace plus at most one feature
segment, and the folder path mirrors it: `Assets/Project/<Area>/<Assembly>/<Feature>`.
A feature folder subdivides further only when its file count makes one flat
folder unreadable; `Gameplay/Runtime/Flight` is the only case today, and its
48 files still share the single namespace `Farion.Gameplay.Flight`.

`Design` mirrors the same shape: an authoring asset lives under
`Design/<Area>/<Feature>` matching the namespace of the type it instantiates.

Every namespace segment is checked against two collision rules before it is
used, because C# resolves a bare identifier by walking the enclosing namespaces
outward, and Unity treats one folder name as reserved anywhere under `Assets`:

| Rejected | Used instead | Why |
| --- | --- | --- |
| `Farion.Application` | `Farion.App` | shadows `UnityEngine.Application` |
| `Farion.Core.Math` | `Farion.Core.Numerics` | shadows `System.Math` |
| `Farion.Audio.System` | `Farion.Audio.Direction` | shadows `System` |
| `Farion.Gameplay.Resources` | `Farion.Gameplay.ResourceNodes` | shadows `UnityEngine.Resources`, and a `Resources` folder is force-included in every build |

The last row is the one with a build cost. Unity treats *any* folder named
`Resources`, at any depth, as a resource folder: its contents ship unstripped
whether or not anything references them. The ore-and-mineral domain is therefore
`ResourceNodes` in the folder, the namespace, and the asset tree, so
`Assets/Project/Resources` stays the single deliberate resource folder.

## Naming Rules

Type prefixes are load-bearing and mechanical, so a reviewer can check them
without reading the body:

- **`Ui`** — every type in `Farion.UI`, with no exception. It matches the `UI_`
  token used on the asset side and keeps the UI surface grouped in the
  Inspector's script picker.
- **`Network`** — reserved for types that derive from FishNet's
  `NetworkBehaviour`. Everything else in `Farion.Multiplayer` is `Multiplayer*`.
  The prefix therefore tells the reader whether a type has an owner, RPCs, and
  sync state. Wire payload structs (`ExplorerReplicateData`,
  `WorldOriginBroadcast`) keep their domain names; they are already unambiguous.
- **`Farion`** — reserved for integration points into the engine or a third
  party: `ScriptableRendererFeature`, FishNet `Authenticator`, the generated
  input actions, and the editor tooling. It is not a general namespace stamp.
- **`Spacecraft`, `Celestial`, `Player`, `Fleet`, `Inventory`, `SurfaceDecoration`**
  and friends are domain prefixes; they are the default when no rule above
  applies.

One type per file, named after that type. A file may hold several small related
types only when its name is the collective noun for them
(`GameplayCommandRequests`, `ExplorerPredictionData`,
`CelestialSurfacePatchTypes`). Partial-class parts are named
`<Type>.<Part>.cs`, as in `FarionProjectValidator.Multiplayer.cs`.

`CreateAssetMenu` and `MenuItem` paths mirror the owning folder exactly:
`Farion/<Area>/<Feature>/<Display Name>`. A menu entry that cannot be derived
from a folder path is a sign the type is filed in the wrong place.

The engine-facing token is `UI` and the C# token is `Ui`: `PF_UI_SettingsScreen`
on disk, `UiSettingsScreenPresenter` in code. Three-letter acronyms are
PascalCase in both (`Hud`, `Lod`), except inside human-readable menu and display
strings.

`Controller` owns a lifecycle or state machine that outlives one screen
(`UiScreenRouter`, `UiFocusController`, `UiMainMenuController`). `Presenter`
binds exactly one screen or panel to state and owns nothing
(`UiSettingsScreenPresenter`, `UiInventoryPanelPresenter`). `View` is a leaf
widget. `Service` is a stateless or process-wide collaborator.

- `Farion.Core.Identity` has `noEngineReferences` and no references at all. It
  owns `IdentifierText`, the single validation rule every Farion identifier
  obeys, and `PersistentEntityId`. It exists so that `Farion.Core.Runtime` and
  `Farion.Gameplay.Domain` share one rule without either depending on the
  other.
- `Farion.Core.Runtime` owns persistence identity, save-slot infrastructure,
  and the physics layer contract (`FarionLayers`).
- `Farion.Simulation.Runtime` owns gravity composition, authored and generated
  celestial state, planetary sampling, deterministic world identity, and local
  origin rebasing.
- `Farion.Gameplay.Domain` has `noEngineReferences`. It currently owns
  `DefinitionId`, revisioned stack inventory state, and Fleet Knowledge state.
- `Farion.Gameplay.Runtime` adapts domain and simulation contracts to Unity. It
  owns character, flight, possession, resources, inventory adapters, the
  assigned shuttle binding, Fleet identity, Fleet Storage, Fleet Knowledge, and
  save participants.
- `Farion.Gameplay.Presentation` owns gameplay-facing VFX components in
  `Farion.Gameplay.Presentation.Flight` and is the only gameplay assembly that
  references VFX Graph. It never shares a namespace with gameplay runtime code.
- `Farion.App.Runtime` owns game flow, local-session composition, save
  requests, and the command facade. Its current gameplay command surface
  authorizes resource harvesting, assigned-shuttle cargo loading/unloading,
  and the first Fleet processing exchange. Its request boundary carries only
  identifiers and expected revisions: `ResourceHarvestRequest`,
  `CargoTransferRequest`, and `FleetProcessingRequest` name a deposit,
  container, or recipe by id. `SessionCommandScope` is the single authoritative
  resolver; a caller cannot smuggle a runtime object past authorization, and a
  stale caller view is rejected by revision instead of silently applied.
- `Farion.UI.Runtime` presents menus, HUD, settings, save/load, inventory, and
  feedback. It requests application operations and never mutates domain state
  directly. Every player-facing string is a key in `UiTextKeys`; no English
  text is embedded in presenters, and the UI validator fails the build if a
  declared key is missing from any locale.
- `Farion.Rendering.Runtime` owns URP-specific celestial presentation.
- `Farion.Audio.Runtime` owns the persistent FMOD mix, scene/environment
  adaptation, UI cue dispatch, and spacecraft presentation driven by gameplay
  telemetry.
- `Farion.Multiplayer.Runtime` owns the optional FishNet listen-server session,
  Tugboat transport, predicted network explorer, connection spawning, and
  shared-origin replication. Lower assemblies do not reference FishNet.
- `Farion.Editor` is an Editor-platform assembly that owns validation and asset
  import policy. It is not auto-referenced and owns no runtime state.
- `Farion.Tests.Support` owns the reflection helper both test assemblies share.

Dependencies do not point back up this list. Domain code does not reference
Unity, gameplay does not reference UI, and simulation does not reference
rendering.

## Physics Layers

`FarionLayers` in `Farion.Core.Runtime` names the layers authored in
`ProjectSettings/TagManager.asset`:

```text
6  CelestialSurface     generated planet, moon, and adaptive patch geometry
7  SpacecraftExterior   solid hull colliders
8  SpacecraftInterior   colliders under a SpacecraftInteriorCollider marker
9  Explorer             the player capsule outside a ship
10 ExplorerInterior     the player capsule inside a ship
11 Interactable         interaction trigger volumes and interactable roots
```

Runtime owners derive layers from authored structure, so prefabs and scenes
never carry layer indices by hand. `SpacecraftRig` classifies its own collider
tree, `CelestialBodyVisual` and `CelestialSurfacePatchSystem` classify generated
surface geometry, each `IInteractable` classifies itself, and possession moves
the explorer capsule between `Explorer` and `ExplorerInterior`.

The collision matrix carries the rules that used to be runtime
`Physics.IgnoreCollision` bookkeeping. `ExplorerInterior` does not collide with
`SpacecraftExterior`, which is the whole hull pass-through rule; interior
geometry and interaction volumes still collide. Celestial surfaces do not
collide with each other or with interior geometry, and interaction volumes do
not collide with terrain or with each other.

`FarionProjectValidator` asserts both the layer names and the matrix, so a
changed project setting fails the build instead of silently changing physics.

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

Schema `7` persists:

- the celestial simulation time, which alone determines every body pose;
- celestial body snapshots, kept only to prove the save belongs to this system;
- world-origin metadata;
- player inventory;
- assigned shuttle cargo;
- Fleet Storage;
- player possession;
- Fleet Knowledge;
- resource extraction deltas.

Schemas `3` and `4` migrate to schema `7` with explicit empty shuttle-cargo,
Fleet-Storage, and Fleet-Knowledge defaults. Schema `5` preserves its shuttle
cargo and Fleet Knowledge while adding the authored Fleet Storage as empty.
Schema `6` preserves everything and starts the celestial clock at its epoch.
The serialized JSON field for shuttle cargo remains `personalShipCargo` for
schema compatibility; runtime code exposes it as `ShuttleCargo`.

The save system uses participants composed from `GameplayRuntimeRoot`, validates
before applying, and restores a pre-load snapshot if application fails.

Capital-ship state, shuttle upgrades, and equipment are not in schema `7`. A
new schema is allowed only after those runtime owners exist.

## Simulation and Presentation

- `GravitySimulation` is the gravity and translating-reference-frame authority.
  Celestial motion is analytic, not integrated: at startup each body derives
  Kepler `OrbitalElements` from its authored position and velocity relative to
  its attractor, and its pose afterwards is a pure function of
  `SimulationTime`. Spin is likewise `initialRotation` advanced by elapsed
  time. Nothing drifts, and two peers that agree on the time agree on every
  pose without replicating anything.
- Offline the clock advances with `Time.fixedDeltaTime`. In multiplayer
  `ZonePhysicsTickDriver` sets it to `Tick * TickDelta`, so a late joiner that
  jumps straight to the shared tick lands on exactly the same pose. Saves
  persist the clock; body snapshots only prove the save belongs to this system.
- Bodies are positioned relative to the physics reference body, which is the
  anchor local space is built around. That keeps analytic motion compatible
  with world-origin rebasing without either system knowing about the other.
- Offline possession binds its surface observer to `GravitySimulation`. A
  reference transition preserves world poses and converts every dynamic
  Rigidbody velocity into the new translating frame, so the explored body keeps
  its terrain collider stationary without launching the actor.
- Multiplayer keeps the authored translating frame on every peer. Prediction
  and reconciliation therefore never depend on a locally chosen body. Each
  actor still samples its own dominant body's point velocity, and adaptive
  terrain collision gathers every active explorer or piloted ship.
- One multiplayer zone has one translating physics frame and one replicated
  origin. The server rebases around the bounds center of all active observers,
  minimizing the worst local coordinate instead of privileging the host. A
  later multi-zone design is required only when player separation exceeds the
  precision budget of the current starting-system scene.
- `DynamicNBody` remains the only integrated mode and is unused by authored
  content.
- `CelestialBody` owns runtime physical body state.
- `PlanetSurfaceModel` owns generated surface, climate, biome, material, and
  terrain-feature sampling.
- `WorldOriginRebaser` is a local Unity-space precision adapter, not a universe
  coordinate authority. No universe coordinate type exists; one will be added
  only when a workflow needs to address positions outside a single zone. Camera
  rigs shift in the same transaction, then the presentation shell snaps follow
  state and invalidates URP temporal history before the next rendered frame.
- `SpacecraftMotor` owns spacecraft motion and publishes telemetry.
- Camera, HUD, FMOD, and thruster VFX consume telemetry and do not feed state
  back into flight physics.
- Multiplayer uses a separate one-shot session mode. Offline save, inventory,
  possession, shuttle control, and commands remain inactive during that mode;
  the server owns player spawning, movement reconciliation, and origin shifts.
- Gameplay composition is `SC_GameplayShell` for local presentation plus
  one shared `SC_WorldZone` connection scene with isolated 3D physics. The
  current origin authority is intentionally single-zone; loading a different
  zone is rejected until origin state and broadcasts are keyed by zone id.
- Starter ships are server-spawned into authored formations and stay unowned
  until claimed. Claiming transfers ownership, boarding moves the explorer
  through `OnFoot -> ShipInterior -> Spacecraft`, and flight is predicted and
  reconciled from the owner's input. Landing gear is a server-owned SyncVar
  rather than a predicted input, because gear colliders must agree on every
  peer; the network ship therefore disables its local manual toggle.
- `PlanetSurfaceModel` is the environment authority. It owns terrain radius
  range, ocean radius, and atmosphere extent, computed from simulation profiles
  alone, and publishes them through `ICelestialEnvironmentProvider`. Rendering
  consumes that sample for its ocean, atmosphere, and cloud shells instead of
  computing geometry of its own, so a headless server keeps a complete
  environment. Rendering profiles hold appearance only.
- Surface scatter has one placement core and two consumers. The shared types
  carry the `SurfaceScatter` prefix: `SurfaceScatterPlacement` owns cell hashing
  and the candidate direction, `SurfaceScatterSuitability` owns every biome,
  material, terrain-feature, and climate gate, `SurfaceScatterDistribution` owns
  spacing, cluster noise, and the visibility ladder, and
  `SurfaceScatterShaderBinding` maps surface state onto the rock shader.
  `SurfaceDecorationRenderer` draws instanced meshes with no colliders,
  `SurfaceFormationSpawner` pools collider-bearing prefabs, and both read the
  same authoring types instead of duplicating rule fields.
- Scatter content is observer-independent. There is no active-instance ceiling;
  each cell derives its own visibility distance from `(planetSeed, ruleId, cell)`
  and is realised whenever an observer is inside it. Far rings therefore carry
  only the cells that hashed high, and the same approach direction is not
  required to see the same world. Frame cost is bounded by cells evaluated per
  frame, never by the result.
- `SurfaceFormationRole` names a geological chain, not a visual tier: `Outcrop`
  is in-place rock on a convex break, `Buttress` flanks it, `Talus` runs
  downhill from each outcrop with the grade resampled per piece, and `Debris`
  scatters past the talus apron. `CelestialShapeProfile.TrySampleGeology`
  supplies the break strength, local relief, downhill direction, and convexity
  those roles key off.
- The shape profile carries a detail band that only patch geometry resolves.
  `FadeFootprint` on a noise layer silences it once the angular sample footprint
  grows past the threshold, so scaled-space spheres stay smooth while adaptive
  patches carry metre-scale relief, and no level-dependent branch reintroduces
  split/merge popping.
- `CelestialSurfacePatchSystem` publishes its angular sample footprint to
  `PlanetSurfaceModel` on enable. Collision, placement, and rendered geometry
  therefore resolve the same octaves, so scattered props sit on the mesh the
  player actually walks on.
- Formation pieces are seated on their mesh base, not their pivot.
  `SurfaceFormationSpawner` measures each prefab's lowest mesh bound once and
  offsets by it, so `embedFraction` means the same thing whatever the source
  art centred its pivot on.
- Formation colliders are distance-gated. A cliff kit carries hundreds of convex
  mesh colliders per formation, so `SurfaceFormationRule.CollisionDistance`
  keeps only the nearby ones enabled; everything further out renders without
  taking physics cost. Spawned pieces live under one `Surface Formations`
  container on the body, classified to `FarionLayers.CelestialSurface`.
- Rock appearance is driven, not authored per biome. `SurfaceScatterShaderBinding`
  reads the terrain's own `SurfaceVisualProfile` steep colours plus the sampled
  snow cover and moisture, and pushes them into the rock shader through a
  `MaterialPropertyBlock`. A desert cliff and a glacial cliff are the same
  prefab with different surface state, not two asset sets.
- Cloud decks clear the terrain. `CelestialCloudProfile.GetLayerRadii` takes the
  body's terrain ceiling and lifts its floor above it, so ridges never intersect
  the volumetric layer.
- Local presentation binding is shared: `LocalPlayerCameraBinding` owns which
  camera rig follows which actor and the cursor capture state, and
  `WorldFocusTracking` owns the origin-rebase and resource-streaming target.
  Offline possession and the multiplayer scene context both call the same
  owners instead of each duplicating the rules.
- `ILocalPilotContext` is the single answer to what the local player controls
  and from which viewpoint. `PlayerPossessionController` implements it offline
  and `MultiplayerSceneContext` implements it online; the flight HUD, ship audio
  perspective, and pilot camera view read only that contract.
  `LocalPilotContextBinding` injects it into `ILocalPilotContextReceiver`
  components under the controlled actor, so no owner serializes a cross-scene
  reference and no second presentation path exists.
- `SC_GameplayShell` owns every presentation service: the camera, both camera
  rigs, `PlayerControlLock`, UI composition, flight HUD, post-process rig, star
  dome, LOD controller, orbit lines, and the scene's single directional light and
  `CelestialLightingRig`. `SC_WorldZone` owns world content only and receives
  those owners through `UiGameplaySceneShellController`. A second light or
  lighting rig in the zone scene fails validation.
- Multiplayer freezes celestial integration and automatic origin rebasing on
  purpose: every peer must agree on body state, and frozen bodies agree
  trivially. Enabling integration requires a replicated or provably
  deterministic solver first.

## Authoring and Validation

- Production scenes and prefabs are authored assets under
  `Assets/Project/Scenes` and `Assets/Project/Prefabs`. They are the source of
  truth. No editor script generates them, because a generator and the asset it
  once produced drift apart and neither one stays authoritative.
- `Assets/Project/Editor` validates authored assets and owns import policy. It
  never owns runtime state and never builds production scenes or prefabs.
- `FarionProjectValidation` checks physics layers and the collision matrix,
  missing scripts, persistent ids, runtime-root references, definition registry
  consistency, audio composition, and authored flight/VFX contracts.
- `FarionUiProjectValidation` additionally checks that every localization key
  is translated in every locale and that no authored `UiLocalizedText`
  references an unknown key.
- Definitions live under `Assets/Project/Design`; runtime quantities and
  unlocks never live in those assets.

## Deliberately Absent

The following systems are product direction, not current implementation:

- mutable capital-ship state, owned rooms, or production-machine state;
- refinery/fabricator queues, timers, power, heat, or maintenance;
- shuttle and capital-ship upgrades;
- equipment instances and loadouts;
- research terminals or technology voting;
- inventory, interaction, shuttle, Fleet, save, and progression replication.

The next safe architectural increment is to finish the imported Capital Ship
Fleet visual alignment, present processing results, and prove the local
unload/processing state through save/load. See `FleetArchitecture.md` and
`BetaRoadmap.md`.
