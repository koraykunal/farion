# Farion Architecture

Farion is currently a clean gravity-first foundation for a future single-player
and co-op space survival/automation game. Keep the low-level simulation stable
before adding survival loops, automation systems, multiplayer transport, or
high-end celestial rendering.

## Project Layout

- `Assets/Project/Core/Runtime`
  - Feature-agnostic Unity runtime contracts that do not know about gameplay,
    rendering, UI, scenes, or networking. Pure domain code should remain free of
    `MonoBehaviour`; Unity adapters may live here only when they are genuinely
    shared foundation.
- `Assets/Project/Core/Runtime/Physics`
  - Newtonian gravity, celestial body state, gravity samples, and generic
    Rigidbody gravity actors.
- `Assets/Project/Core/Runtime/Time`
  - Shared simulation clock helpers.
- `Assets/Project/Simulation/Runtime`
  - Authored solar-system simulation glue built on top of Core. This layer can
    know about celestial body authoring and later procedural body generation.
- `Assets/Project/Simulation/Runtime/World`
  - Local world-origin rebasing for large authored space scenes. It keeps Unity
    physics near the origin without becoming the future authoritative coordinate
    or networking model.
- `Assets/Project/Gameplay/Runtime`
  - Player, spacecraft, survival, interaction, automation, and co-op gameplay
    systems. This layer consumes Core and Simulation; Core must not depend on it.
- `Assets/Project/Gameplay/Runtime/Domain`
  - Asset-independent identity, economy, equipment, personal-ship,
    capital-ship, fleet, and fleet-knowledge state. Domain aggregates do not
    reference `UnityEngine`, scenes, prefabs, UI, or networking transports.
    `Farion.Gameplay.Domain` enforces this boundary with
    `noEngineReferences`.
- `Assets/Project/Gameplay/Runtime/Actors`
  - Shared actor telemetry that is not specific to spacecraft or on-foot
    characters.
- `Assets/Project/Gameplay/Runtime/Character`
  - On-foot first-person movement, view, and later suit/survival interaction.
- `Assets/Project/Gameplay/Runtime/Input`
  - Local player control locks shared by gameplay input adapters. UI focus,
    dialogue, cinematics, or station interaction can block player controls
    without pausing simulation.
- `Assets/Project/Gameplay/Runtime/Inventory`
  - Item definitions, item taxonomy, serialized inventory views, and Unity
    adapters. `InventoryContainerComponent` owns reusable Unity projection and
    transaction wiring; `PlayerInventory` adds schema-4 compatibility only.
- `Assets/Project/Gameplay/Runtime/Equipment`
  - Unique-equipment and slot authoring definitions plus the registry-backed
    installation-policy adapter. It translates ScriptableObject metadata into
    the asset-independent domain loadout contract.
- `Assets/Project/Gameplay/Runtime/Resources`
  - Deterministic resource deposit generation, node definitions, streaming, and
    runtime depletion deltas.
- `Assets/Project/Gameplay/Runtime/Crafting`
  - Recipe definitions and crafting station categories. Runtime station
    execution should consume these definitions later; definitions must not store
    active crafting jobs.
- `Assets/Project/Gameplay/Runtime/Research`
  - Research definitions and technology domains. Research unlock state belongs
    in runtime/save data, not in ScriptableObject assets.
- `Assets/Project/Application/Runtime`
  - Session commands, startup requests, save orchestration entry points, and
    scene flow. UI requests these operations but does not own them.
- `Assets/Project/Audio/Runtime`
  - Unity and FMOD presentation driven by gameplay telemetry. Audio never owns
    flight physics, possession, inventory, or scene flow.
- `Assets/Project/Rendering/Runtime`
  - URP-specific visual systems such as procedural celestial presentation,
    ocean, atmosphere, stars, and camera render passes.
- `Assets/Project/Design`
  - ScriptableObject authoring data, grouped by the runtime domain that owns the
    data contract. Physics body definitions live under `Design/Physics`,
    procedural shape/radiation/planetary rules under `Design/Simulation`,
    visual profiles under `Design/Rendering`, and gameplay tuning under
    `Design/Gameplay`.
- `Assets/Project/Art`
  - Shaders, textures, models, generated lookup textures, and transferred
    reference assets.
- `Assets/Project/Prefabs`
  - Authored prefabs only. Do not recreate scene-builder workflows here.
- `Assets/Project/Prefabs/UI/Common`
  - Shared UI prefabs such as `UI_MenuButton.prefab` and panel frame pieces.
- `Assets/Project/Prefabs/UI/Gameplay`
  - Gameplay HUD, pause menu, inventory panel, and gameplay slot prefabs.
- `Assets/Project/Scenes`
  - Source-controlled authored scenes.
- `Assets/Project/Docs`
  - Architecture notes, setup instructions, beta planning, and asset intake
    rules.
- `Assets/Project/Editor`
  - Project validation and build-time authoring checks.
- `Assets/Project/Tests/EditMode`
  - Deterministic identity, persistence, input-schema, and gameplay-rule tests.
- `Assets/Project/UI/Runtime/Common`
  - Reusable UI presentation components such as shared menu button visuals and
    panel transitions. These components must not know about main menu,
    gameplay, inventory, save, or upgrade actions.
- `Assets/Project/UI/Runtime/Gameplay`
  - Gameplay screen state, panel switching, inventory presentation, and local
    cursor/control focus. This layer may reference Gameplay contracts but must
    not own inventory, crafting, research, or survival state.

## Assembly Boundaries

- `Farion.Core.Runtime`
  - No dependency on Simulation, Gameplay, Rendering, networking, UI, or URP.
- `Farion.Simulation.Runtime`
  - Depends on Core. Owns simulation authoring and later procedural celestial
    runtime contracts.
- `Farion.Gameplay.Domain`
  - Has no Unity Engine or project-assembly dependency. It owns persistent
    identity and deterministic aggregate rules only.
- `Farion.Gameplay.Runtime`
  - Depends on Gameplay Domain, Core, Simulation, and Input System. It owns
    Unity gameplay adapters and has no FMOD dependency. Future co-op authority
    belongs here or in a separate networking assembly above this layer.
- `Farion.Application.Runtime`
  - Depends on Core, Gameplay Domain, and Gameplay Runtime. It owns
    scene/session flow and the UI-facing gameplay-session command facade.
- `Farion.Audio.Runtime`
  - Depends on Core, Simulation, Gameplay, and FMOD. It consumes telemetry and
    remains presentation-only.
- `Farion.Rendering.Runtime`
  - Depends on Core and Simulation. URP-specific atmosphere/ocean work belongs
    here, not in Core.
- `Farion.UI.Runtime`
  - Depends on Application and Gameplay for presentation of local player state.
    It may request local input locks and session commands, but gameplay
    authority and persistent state remain outside UI.

## Runtime Composition

- Scene-level dependencies are assigned through authored references or scoped
  composition. `PlayerPossessionController` binds pilot-seat and boarding
  interactables only under its serialized spacecraft root. It also supplies
  itself to `IPlayerPossessionContextReceiver` implementations under that root,
  so presentation systems such as ship audio never discover an arbitrary
  global player.
- `GameplayRuntimeBindings` is the immutable composition view for the current
  authored vertical slice. Save participants and `GameplaySessionRuntime`
  consume the same definition, simulation, local inventory, possession,
  personal-ship binding, and resource-streaming references instead of
  constructing parallel contexts.
- `GameplaySessionIdentity` keeps the local player, explorer actor, personal
  ship, and carried inventory ids distinct. `GameplaySessionController`
  validates and composes this identity before exposing save or UI operations.
- `GameplaySessionController` is the UI-facing facade for save, exit-to-menu,
  quit, harvesting, crafting, and personal-ship commands.
  `GameplayCommandService` verifies local session ownership before invoking
  atomic gameplay transactions or revisioned ship operations.
  `GameplayUiController` does not call `SceneManager`, `Application.Quit`,
  `GameplaySaveCoordinator`, or a scene-authored `PlayerInventory` reference
  directly.
- `FarionProjectValidator` blocks builds with missing scripts, empty or
  duplicate persistent ids, incomplete possession/session/save references, or
  invalid definition registries. Definition and research capability ids use the
  same whitespace-free `DefinitionId` policy as runtime domain state.
- The authored starter shuttle owns one `PersonalShipRuntimeBinding` and one
  `PersonalShipCargoInventory`. The binding is the permanent Unity adapter for
  the pure `PersonalShipState`; it does not own flight physics or mutate
  `SpacecraftFlightProfile`. Its current name, hull, and fuel fields are
  bootstrap authoring defaults and will later be hydrated from a ship
  definition or schema-5 snapshot without removing the binding boundary.
- Fleet and capital-ship aggregates still have no scene bootstrap. Do not
  create hidden runtime fleet objects, auto-generated capital-ship rooms, or
  temporary singleton ownership to make these contracts appear active.

## Gameplay Domain Foundation

- `DefinitionId` identifies immutable authored definitions. It is case-sensitive,
  normalized, and rejects empty, whitespace-containing, or control-character
  values.
- `PersistentEntityId` identifies mutable runtime entities such as containers,
  equipment instances, ships, rooms, machines, players, and fleets.
- `InventoryContainerState` is the quantity and capacity authority. Stack
  exchanges and cross-container transfers are atomic; failed operations leave
  quantities and revisions unchanged.
- Stackable items store definition plus quantity. Unique equipment stores a
  persistent instance id and resolves to `EquipmentInstanceState`, which owns
  serial number, condition, installed upgrades, and revision.
- `EquipmentRepositoryState` owns registered instance and case-insensitive
  serial uniqueness plus the authoritative location of every instance.
  Equipment is either unassigned, in exactly one container, or installed in
  exactly one personal-ship slot.
- Equipment placement, container transfer, install, swap, and uninstall use
  copy-on-write transaction services. Direct unique-instance and module-slot
  mutation is not public API.
- `EquipmentDefinition` extends a unique `InventoryItemDefinition` with
  equipment size, base mass, and compatible `slot_type.*` ids.
  `EquipmentSlotDefinition` owns the persistent slot id, slot type, and maximum
  accepted size. Compatibility is data-driven and resolved by
  `RegistryEquipmentInstallationPolicy`.
- `InventoryContainerComponent` is the reusable Unity adapter for a stackable
  container. Its serialized `InventoryStack` list is a presentation/save view,
  not a second writable inventory authority. `PlayerInventory` derives from it
  without duplicating transaction logic.
- `ResourceNodeInteractable` submits harvest intent through the session command
  gateway. `ResourceHarvestTransaction` owns the node/inventory transaction and
  compensates an inventory write if the source revision changes.
- `PersonalShipState`, `CapitalShipState`, `FleetState`, and
  `FleetKnowledgeState` are separate aggregates. They exchange ids, not mutable
  object graphs, so save and future host-authoritative replication can version
  them independently.
- Every successful state-changing command advances its aggregate revision.
  Stale commands are rejected. Authoritative writes must still be serialized by
  the owning simulation/session service; aggregates are not background-thread
  synchronization primitives.
- Research unlocks capabilities, blueprints, and discoveries. It must not become
  a generic percentage-stat store.
- ScriptableObjects remain immutable authoring definitions. Current quantities,
  condition, fuel, hull, unlocked knowledge, room membership, and machine
  placement belong to runtime state and save snapshots.

## Persistence Identity

- New saves use schema `4`; schema `3` remains readable.
- Schema `4` continues to persist the current player inventory/possession
  vertical slice. It does not persist fleet aggregates or unique equipment
  instances. Schema `5` must be introduced only with an explicit migration and
  a real fleet-session runtime owner; do not write partial fleet data into
  schema `4`.
- Celestial snapshots use `PersistentObjectId` as their primary identity and
  retain body-name fallback only for schema `3`.
- Resource deposit ids derive from the persistent planet identity, generation
  version, resource definition, and deterministic slot.
- Save writes are temporary-file replacements with backups. Loading validates
  the complete payload, falls back to a supported backup, and restores a
  pre-load snapshot if participant application fails.
- `WorldOriginSnapshot` persists accumulated local rebase metadata without
  treating Unity float transforms as the future authoritative universe model.

## Input Ownership

- `FarionInputActions` owns the shared OnFoot, Flight, Vehicle, and UI action
  maps with keyboard/mouse and gamepad bindings.
- Device adapters expose `IFirstPersonInputSource`, `ISpacecraftInputSource`,
  and `IBoardingInputSource`; motors and possession do not read devices.
- Spacecraft throttle is persistent pilot state in `KeyboardSpacecraftInput`.
  `W/S` change the commanded throttle and releasing the key holds the current
  value; `X` returns throttle to zero and requests assisted braking. Local UI
  focus blocks new flight input but preserves the current throttle setpoint;
  leaving the pilot seat disables the adapter and returns the motor to zero
  command.
- Mouse look is normalized against a 60 Hz reference and gamepad look remains
  a frame-rate-independent normalized rate command.
- Binding overrides can be saved or reset without changing gameplay systems.

## Gravity Model

The simulation uses:

`acceleration = G * bodyMass / distanceSquared`

- `GravitySettings` owns the gravitational constant, fixed timestep, solver
  substeps, optional minimum interaction distance, and optional acceleration
  clamp.
- `GravitySimulation` owns body registration, fixed-step integration, gravity
  queries, dominant-body sampling, nearest-surface sampling, and the explicit
  translating physics reference frame. The selected reference body's orbital
  velocity remains inertial simulation state, while Unity-space body and actor
  velocities are expressed relative to that frame.
- `CelestialBody` owns runtime body state: radius, surface gravity, initial
  velocity, mass derivation, lock/static behavior, inertial velocity, and
  frame-relative Rigidbody integration.
- `CelestialBodyDefinition` is pure physics authoring data. It deliberately has
  no visual, material, ocean, atmosphere, or procedural generation references.
- `CelestialBodyDefinitionAuthoring` can apply a definition to one scene body.
  It is not a scene builder and must not instantiate or arrange systems.
- `CelestialFrameProvider` samples the active gravity simulation and explicit
  environment providers to describe one actor's local celestial frame: dominant
  body, altitude, surface normal, relative velocity, radial velocity,
  tangential speed, ocean level, and atmosphere state. Landing, HUD, flight
  assist, and later co-op prediction should consume this sampled context rather
  than recalculate body-relative state independently.
- A dominant gravity body is not automatically an active landing target.
  `SpacecraftLandingProfile` limits surface guidance by an absolute altitude
  floor and a body-radius multiplier. Outside that envelope, the dominant frame
  remains available to physics while landing guidance goes offline and the
  flight HUD reports cruise navigation.
- `ICelestialEnvironmentProvider` is the narrow bridge from render-authored
  planet data into simulation context. Rendering can provide ocean and
  atmosphere radii, but Simulation does not depend on Rendering.
- `ICelestialSurfaceProvider` is the equivalent bridge for authored procedural
  terrain height, geometric surface normal, and slope angle. It lets gameplay
  sample the same shaped surface used by the visual mesh without making
  Simulation depend on Rendering.
- `PlanetSurfaceModel` is the authoritative runtime surface, climate, biome,
  surface-material, surface-state, and terrain-feature sampler for generated
  planets. It lives in Simulation,
  owns the link between a physical `CelestialBody`, a
  `PlanetaryGenerationProfile`, and a `CelestialShapeProfile`, and implements
  `ICelestialSurfaceProvider`. Gameplay systems should prefer this model when
  they need terrain height, slope, local temperature, precipitation, effective
  moisture, aridity, radiation, biome, material, surface state, or
  terrain-feature data. Rendering may consume the same model, but it must not
  become the authority for surface gameplay.
- `PlanetThermalProfile` converts stellar irradiance into the long-term thermal
  baseline. `PlanetClimateProfile` adds latitude, altitude, seamless spherical
  variation, precipitation, evaporation, moisture, aridity, and radiation.
  Rotating planets use long-term insolation; instantaneous day/night exposure
  may affect a tidally locked climate only when that mode is explicitly
  selected.
- `BiomeDistributionProfile` classifies long-term ecological regions from the
  climate sample. `BiomeDefinition` contains semantic identity and global
  physical compatibility only; local temperature, aridity, radiation, slope,
  and altitude criteria belong to distribution rules and must not be duplicated
  inside the definition.
- `SurfaceMaterialDistributionProfile` independently chooses physical cover
  such as exposed rock, basalt regolith, sand, soil, or ice inside a biome.
  `PlanetSurfaceStateProfile` separately owns transient or process-driven
  overlays such as volcanism, snow cover, and wetness. A basalt desert therefore
  does not imply a planet-wide lava material.
- `PlanetHydrosphereProfile` is the simulation owner of surface-ocean presence,
  sea level, and climate water availability. Rendering owns ocean appearance,
  not those semantic environment values.
- `GravityActor` applies the active simulation's gravity to ordinary
  Rigidbodies and can align their up axis against the gravity vector. Actor
  gravity is expressed in the active translating frame, so the reference
  body's acceleration is removed as the matching inertial-frame correction.
- `WorldOriginRebaser` follows an authored target such as the ship or camera and
  shifts explicit authored scene roots by the same offset when the local scene
  drifts too far from `(0, 0, 0)`. It does not walk every Rigidbody and does not
  auto-resolve missing targets; origin rebasing is a local coordinate operation,
  not a gameplay event or discovery service. Future co-op state should still use
  an authoritative world coordinate model above this render/physics origin
  layer.
- `WorldCoordinate`, `WorldSectorCoordinate`, `GeneratedEntityId`, and
  `UniverseGenerationContext` are the pure-data contracts for that authoritative
  world layer. They do not generate scene objects or stream systems. Their job
  is to keep persistent identity, sector-normalized position, stable hash-based
  ids, and generation versioning separate from local Unity transforms.
- `CelestialShapeProfile` owns authoritative procedural radius/shading samples
  in Simulation. Moon crater and continent-ridge shape profiles are simulation
  data now, not rendering-only data; renderers consume their samples to build
  meshes and shaders.
- `TerrainFeatureDistributionProfile` classifies authored/generated surface
  samples into semantic geology/features such as crater fields or mountain
  ridges. It does not deform the mesh. Real height, crater, ridge, or continent
  displacement remains in `CelestialShapeProfile`; resources, POI, weather, and
  future visual modifiers consume terrain-feature samples from
  `PlanetSurfaceModel`.
- `CelestialBodyVisual` owns the generated visual mesh set. It creates a child
  `Terrain Mesh` object, prebuilds full-sphere LOD meshes from the same
  shape profiles, and keeps the physical body root at identity scale. Surface
  weights and state are baked into fixed-resolution cubemaps rather than mesh
  UV channels, so biome/material boundaries do not change with mesh LOD.
  Kinematic bodies may assign the exact LOD0 render mesh to the full-sphere
  `MeshCollider`; a second independently sampled collision sphere is forbidden
  because it can visibly diverge from the ground. `CelestialBodyVisual` does
  not provide gameplay surface samples: authored planets that need landing,
  biome, resource, or survival data must expose a `PlanetSurfaceModel`.
- `CelestialLodProfile` owns screen-height thresholds and mesh resolutions for
  the reference-style full-sphere LOD baseline. Bodies with materially different
  silhouette needs may use dedicated profiles; the test moon uses
  `SO_MoonLodProfile` so crater fidelity can increase without raising every
  terrestrial body's mesh cost.
- `CelestialLodController` reads an explicitly assigned camera and authored
  `CelestialBodyVisual` targets, then applies LOD levels. Runtime LOD must not
  discover scene objects on a timer; collecting targets is an authoring action.
  It does not generate bodies, create scenes, or own procedural shape data.
- `CelestialSurfacePatchSystem` owns the landing/exploration rendering
  transition for an explicitly authored kinematic body and camera. A body that
  owns local terrain collision must also be the active
  `GravitySimulation.PhysicsReferenceBody`; a moving planetary MeshCollider is
  not a supported exploration frame. Below its
  altitude threshold it replaces the complete sphere renderer with pooled
  cube-sphere quadtree patches, evaluates those patches through the same shape
  and surface profiles, and enables collision only on nearby deepest-level
  leaves. Every local `MeshCollider` shares its renderer's exact mesh. Ghost
  border samples stabilize normals across patch boundaries; inward skirts hide
  mixed-level cracks. Split/merge hysteresis and a level-independent sampling
  footprint keep shared vertices stable during observer-driven topology
  changes. Mesh generation and collider cooking are staged behind the current
  surface under an explicit per-frame budget, then committed transactionally;
  incomplete topology must never replace the authoritative render/collision
  set. Render and collision transitions are independent. Collision follows an
  explicit `GlobalFallback -> LocalPreparing -> LocalAuthoritative` authority
  state machine; the global collider remains active until deepest-level patches
  cover the controlled actor's current position, safety margin, and predicted
  relative-motion corridor. `ICelestialSurfaceCollisionObserver` is the Core
  dependency-inversion boundary through which possession exposes that actor
  without creating a Rendering-to-Gameplay dependency. Reused scratch buffers
  prevent periodic GC spikes during staging. The patch system does not own
  biome, climate, resources, gravity, possession, or body motion.
- `CelestialOrbitLineRenderer` is a rendering/debug aid for visualizing current
  osculating body orbits from the authored `GravitySimulation`. It owns only
  transient `LineRenderer` objects and can be disabled through `Show Orbit
  Lines`. It is not an authoritative trajectory predictor and should not drive
  gameplay decisions.
- `CelestialSurfaceProfileBase` is the shared contract for applying per-body
  material properties. Moon and terrestrial bodies use separate profile classes
  instead of sharing unrelated crater, ocean, and biome fields.
- `CelestialSurfaceProfile` owns moon/simple visual surface data such as base
  material, per-body color, smoothness, triplanar normals, and simple fallback
  displacement. It must not contain gravity, orbit, mass, gameplay, or
  networking data.
- `FarionMoonTriplanar` treats its multi-channel surface-noise texture as linear
  data and samples surface noise and normals with world-unit tile sizes. It uses
  the same URP PBR lighting path and `CelestialLightingRig` main light contract
  as terrestrial surfaces; crater/ejecta shading data remains authored by
  `MoonCraterShapeProfile`.
- `TerrestrialSurfaceProfile` owns solid-surface land, shore, steep terrain,
  texture, triplanar normal settings, and the visual lava/snow overlay sets.
- `SurfaceVisualProfile` maps simulation surface materials to authored colour
  and surface response. `SurfaceTextureSet` uses world-space tile sizes and treats
  base-color/normal/roughness as the core surface set; AO, height micro-relief,
  and HDR emission are independent optional channels. Height textures affect
  material detail only and must not replace authoritative shape displacement.
- `FarionTerrestrialTriplanar` builds URP `InputData` and `SurfaceData`, then
  delegates direct light, shadows, indirect light, reflection probes, metallic,
  smoothness, occlusion, and emission to URP's PBR path. `CelestialLightingRig`
  owns the synchronized main directional light; surface shaders must not
  duplicate that star light with a second custom diffuse/specular calculation.
- `CelestialOceanProfile` owns reusable ocean color, transparency, fresnel, and
  specular settings plus the referenced wave normal textures for the
  screen-space ocean pass.
- The ocean shader must treat above-water, underwater, horizon, and seabed
  views as outcomes of one ray/volume/interface model. It should derive water
  colour from the ray segment inside the ocean sphere, Beer-Lambert style
  volume extinction, Schlick Fresnel, and water-air total internal reflection
  rather than adding separate visual branches for each camera state.
- `CelestialAtmosphereProfile` owns reusable atmosphere radius, density,
  optical-depth LUT generation, scattering coefficients, and dither inputs for
  the screen-space atmosphere pass.
- The atmosphere shader must treat sky, horizon, and terrain-backed pixels as
  the same ray-marched medium. Depth can choose where the view ray stops, but it
  must not switch to separate sky-only or surface-only colour clamps. The final
  colour should come from view transmittance and in-scattered star light.
- `TerrestrialPlanetVisualProfile` is the high-level authored rendering profile
  for a solid-surface planet. It references terrain shape, land-surface style,
  ocean style, and atmosphere style. Sea level and environment presence are
  read from `PlanetaryGenerationProfile`, not duplicated here.
- `TerrestrialPlanetVisual` binds that high-level profile to authored terrain
  rendering and registers screen-space ocean/atmosphere data for the URP
  renderer features. It is not a scene builder and does not instantiate a solar
  system. It also exposes ocean and atmosphere bounds through
  `ICelestialEnvironmentProvider` so gameplay can reason about entry and water
  level without referencing Rendering directly. Ocean and atmosphere are not
  component slots or child meshes in the default architecture.
- `FarionOceanRendererFeature` and `FarionAtmosphereRendererFeature` render
  registered bodies as ordered full-screen passes with an explicit maximum body
  count. Each body uses its own runtime material instance so per-profile textures
  such as wave normals, optical-depth LUTs, and blue-noise textures cannot leak
  between planets.
- `CelestialShapeProfile` owns procedural height generation. Moon craters,
  ridges, asteroid deformation, and continent-ridge terrain should live in shape
  profiles instead of physical body definitions.
- `MoonCraterShapeProfile` is the current CPU moon terrain baseline. It owns
  crater height, low-frequency deformation, ridge relief, and mesh shading data
  for biome/ejecta masks behind one `CelestialShapeSample` contract. Keep moon
  colours, triplanar textures, smoothness, and lighting response in
  `CelestialSurfaceProfile`; do not add per-view moon visual hacks to the shape
  path. A future GPU compute implementation can replace the internals only if
  it preserves the same shape sample boundary.
- `ContinentRidgeShapeProfile` ports the reference continent/ocean-floor and
  mountain-mask shape model into the authored Simulation profile system.
- `CelestialLightSource` marks an authored star/body as a physical light
  source. It exposes the physical body, radiation profile, position, radius,
  and temperature contract; it does not own renderers, materials, or scene
  lights.
- `CelestialStarVisualProfile` owns reusable photosphere color, granulation,
  sunspot, pulse, and scaled-space presentation parameters.
- `CelestialStarVisual` is the star renderer owner. Its child renderer preserves
  the physical star's direction and angular size while moving only the visual
  proxy inside the active camera's clip range. The physical `CelestialBody`,
  collider, radiation source, and N-body position remain untouched.
- `CelestialLightingProfile` owns reusable lighting/exposure defaults such as
  color temperature, stable Directional Light intensity, shadows, ambient space
  light, and camera clipping. Physical radiation falloff remains a simulation
  concern.
- `CelestialLightingRig` applies one profile to explicitly authored light,
  focus, and camera references. Runtime lighting must not auto-find scene
  objects; missing references can be resolved through manual authoring tools.
  It can follow a camera or ship focus, but it must not instantiate solar-system
  objects. It computes one direction-to-star state and publishes it to the
  Directional Light, atmosphere, and ocean so render paths cannot disagree
  about the lit hemisphere.
- `StarDomeProfile` and `StarDomeController` own the procedural skybox star
  field. The star dome is render-only background context; it must not create
  physical stars, gravity bodies, gameplay navigation points, or scene objects.
- Visual terrain does not modify physical gravity. Dynamic N-body bodies retain
  spherical collision. Kinematic bodies may use the LOD0 render mesh as their
  full-sphere collider, while authored explorable bodies switch to
  `CelestialSurfacePatchSystem` near the observer. Its nearby deepest-level
  render/collision meshes are identical. The explorable body's inertial orbit
  continues in simulation, but its Unity-space collider remains stationary in
  the explicit translating physics frame so contact resolution, camera
  interpolation, and shadow rendering do not fight orbital translation.
- `CelestialActorProbe` is the shared gameplay consumer of the celestial frame.
  It exposes dominant body, surface altitude/slope, local up, radial and
  surface-relative velocities, ocean state, and atmosphere state for any
  Rigidbody actor. Spacecraft and on-foot characters must consume this contract
  instead of duplicating body-relative calculations.
- `SpacecraftFlightProfile` owns speed envelopes, asymmetric thrust authority,
  angular response, input spool rates, boost energy, gravity compensation,
  Rigidbody mass, and optional center-of-mass tuning. Authored ship profiles
  live under `Design/Gameplay/Flight`.
- `SpacecraftMotor` owns Rigidbody sampling and force application only. The
  deterministic `SpacecraftFlightControlLaw` converts a pilot command and one
  sampled flight frame into requested linear/angular acceleration.
- Assisted flight controls body-relative velocity and compensates local gravity
  within the ship's available thrust envelope. Manual flight applies direct
  thrust without hidden gravity cancellation and tapers same-direction thrust
  near its configured safety envelope while retaining full counter-thrust.
- `SpacecraftBoostController` owns boost spool, charge drain, recharge delay,
  depletion lockout, and release-to-rearm behavior. Boost changes forward
  authority only; it does not multiply lateral or vertical thrusters.
- `SpacecraftLandingProfile` owns the current landing policy thresholds:
  altitude bands, safe touchdown speeds, high-descent limits, and deorbit
  descent speed, and safe touchdown slope. Touchdown altitude includes hull/gear
  clearance, so ships with different geometry own different landing profiles.
  Tune landing difficulty through the asset before changing code.
- `SpacecraftLandingComputer` evaluates the active `CelestialActorProbe`
  against a landing profile and emits a phase plus risk flags. It must not apply
  thrust, lock controls, snap the ship, or create landing triggers. Future HUD,
  flight assist, warning audio, and co-op prediction should read this assessment
  first.
- `SpacecraftOrbitComputer` evaluates the active celestial frame as a local
  two-body orbit around the dominant body. It reports regime, circular speed,
  escape speed, specific orbital energy, eccentricity, periapsis, apoapsis,
  orbital period, and flight-path angle. It is telemetry only; it must not apply
  burns, steer the ship, or replace the N-body gravity simulation.
- `SpacecraftEntryCorridorProfile` owns tunable atmosphere-entry limits such as
  minimum/maximum entry angle, speed ratios, and periapsis bands.
- `SpacecraftEntryCorridorComputer` reads `CelestialActorProbe` and
  `SpacecraftOrbitComputer` to classify the current atmospheric entry as safe,
  shallow, steep, overspeed, impacting, escaping, or outside the corridor. It is
  a navigation/warning layer only and must not apply burns.
- `SpacecraftSurfaceContactProbe` aggregates every contact in the active
  collision, samples Rigidbody point velocity (including angular motion), and
  records the contacted body, average point/normal, deepest separation, normal
  speed, and tangential speed. `SpacecraftLandingComputer` uses this data to
  distinguish a valid touchdown from merely being close to the surface.
- `SpacecraftSurfaceContactStabilizer` removes low-speed into-surface velocity,
  damps contact sliding/spin, and recovers small penetrations while a spacecraft
  is touching a celestial surface. It is not an autopilot or fake landing lock;
  high-speed impacts still remain unsafe.
- `SpacecraftOceanInteractionProfile` owns tunable water interaction values:
  effective hull radius, buoyancy acceleration, water drag, angular damping,
  safe water-entry speed, pressure warning depth, and crush depth.
- `SpacecraftOceanInteractor` reads the active `CelestialActorProbe` and
  applies water buoyancy/drag from the mathematical ocean volume. It does not
  use an ocean collider and does not belong to Rendering. Damage, alarms, and
  underwater controls should consume its sample later instead of duplicating
  water-depth logic.
- `SpacecraftLandingGuidanceComputer` converts landing assessment and contact
  state into pilot-facing guidance: severity level, command, advisory text, and
  normalized speed/stress ratios. It can warn the pilot to seek a flatter
  surface, but it still does not steer the ship or own UI.
- `SpacecraftFlightHudPresenter` reads flight, boost, gear, celestial-frame,
  and landing-guidance telemetry for the production HUD. It does not compute
  landing rules, apply flight forces, or own progression state.
- `FirstPersonMotorProfile` owns on-foot movement tuning: walk/sprint speed,
  acceleration, jump speed, ground probe, slope limit, and upright response.
- `FirstPersonMotor` is a Rigidbody/CapsuleCollider first-person movement
  controller aligned to `CelestialActorProbe.LocalUp`. It applies project
  gravity, projects movement onto the current surface frame, and does not use
  Unity `CharacterController` so custom gravity, terrain mesh collision, and
  future network authority remain explicit.
- `FirstPersonCameraRig` follows a `FirstPersonMotor` eye position and applies
  pitch while the motor owns yaw/upright alignment. Position smoothing happens
  in target-relative eye-offset space so moving celestial bodies and
  world-origin rebases are inherited without visual lag. It is a camera
  presenter, not a gameplay authority.
- `KeyboardBoardingInput`, `VehicleBoardingPoint`, and
  `PlayerPossessionController` own the first ship/on-foot handoff. The
  controller switches active input and camera presenters, but keeps
  `SpacecraftMotor` enabled so gravity and vehicle physics continue while the
  player is outside the ship. `PlayerExplorerPlacement` separately owns exit
  pose construction, celestial-surface sampling, velocity inheritance, and
  terrain-aware surface placement.
- Exterior `VehicleBoardingPoint` interaction enters ship-interior mode; only
  an interior `PilotSeatInteractable` may enter spacecraft piloting mode.
  Possession mode changes invalidate stale interaction targets and immediately
  reconcile flight-HUD visibility, including restored on-foot saves.

The current flight/landing/orbit stack is the authoritative baseline: it covers
persistent six-degree-of-freedom pilot input, assisted/manual control laws,
gravity feed-forward, bounded boost energy, spherical-body altitude, body-relative radial
velocity, tangential velocity, atmosphere/ocean membership, procedural
terrain-aware surface altitude, approximate local surface slope, water
submersion, first-pass buoyancy/drag, multi-contact touchdown validation, osculating
two-body orbit estimates around the dominant body, and first-pass atmosphere
entry classification, and a first adaptive local terrain-patch baseline for
kinematic explorable bodies. It is not yet a full damage, subsystem, cargo-mass,
autopilot, or dynamic-N-body terrain-collision model. Those systems must extend
the existing telemetry/control contracts rather than bypassing them.

## Current Rule

Do not add atmospheres, survival systems, automation, or co-op networking until
`SC_PhysicsSandbox` proves:

1. `GravitySimulation` applies a stable fixed timestep.
2. Star/planet/moon bodies move or stay locked as intended.
3. A simple test Rigidbody can fall toward and align to a body.
4. A simple spacecraft can thrust, rotate, and stay camera-stable near a body.
5. Star-driven directional lighting, shadows, exposure, and camera clipping are
   stable in the authored scene.
6. Terrain visuals are authored components, while ocean and atmosphere effects
   are renderer features fed by authored profile data.
7. The scene is authored by hand, not rebuilt by a tool.
8. Large-distance travel keeps the active ship/camera near local origin through
   explicit world-origin rebasing.
9. The active ship can report a valid celestial frame sample: dominant body,
   surface altitude, radial velocity, tangential speed, atmosphere state, and
   ocean state.
10. The active ship can report a landing assessment phase and risk flags without
    applying control assistance.
11. Surface contact is reported separately from altitude and confirms touchdown
    only when contact speed is inside the landing profile limits.
12. Atmosphere entry state can be classified from orbit and frame telemetry
    without applying burns or control assistance.
13. Landing telemetry reads procedural surface slope and rejects steep
    touchdown candidates through the landing profile.
14. Ocean physics reads the same screen-space ocean bounds as gameplay
    telemetry, applies water forces without a water collider, and exposes
    pressure/entry risk separately from landing contact.
15. A first-person test actor can stand, walk, sprint, jump, and align to the
    local procedural planet surface using `CelestialActorProbe`.
16. A single possession controller can switch between piloting the active ship
    and controlling the on-foot explorer without leaving both input stacks
    active.
17. Near an explorable kinematic body, adaptive terrain patches replace the
    global renderer. Collision transfers separately from the global fallback to
    exact rendered patch meshes only after predicted actor coverage is ready;
    the outgoing authority is never disabled first.

`PlanetSurfaceModel` is the source-of-truth boundary for survival and resource
work. Resource generation, visual coverage reports, landing slope checks, and
future suit survival should consume one planet surface sample instead of
recomputing climate, biome, surface material/state, terrain feature, and terrain
height in separate systems.

## Roadmap

1. Physics sandbox.
2. Spacecraft gravity/thrust sandbox.
3. Procedural moon mesh generation.
4. Procedural terrestrial planet mesh generation.
5. URP celestial surface shaders.
6. URP screen-space ocean and atmosphere baseline.
7. Procedural star dome and space presentation baseline.
8. Reference-style full-sphere celestial LOD.
9. Local world-origin rebasing for ship-scale travel.
10. Celestial frame sampling for ship/body-relative gameplay.
11. Landing assessment, warning states, and flight constraints.
12. Surface contact and touchdown validation.
13. Landing guidance and debug cockpit feedback.
14. Atmosphere entry corridor telemetry.
15. First-person on-foot movement baseline.
16. Player/ship possession and boarding loop.
17. Burn-window and deorbit maneuver guidance.
18. Adaptive local terrain patch rendering and collision baseline.
19. Underwater damage, controls, and resource interaction.
20. Survival and automation gameplay.
21. Co-op authority and replication.
