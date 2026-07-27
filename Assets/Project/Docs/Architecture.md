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
  - Item definitions, item taxonomy, stack data, and player inventory state.
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
- `Farion.Gameplay.Runtime`
  - Depends on Core, Simulation, and Input System. It has no FMOD dependency.
    Future co-op authority belongs here or in a separate networking assembly
    above this layer.
- `Farion.Application.Runtime`
  - Depends on Core and Gameplay. Owns scene/session flow and the UI-facing
    gameplay-session command facade.
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
- `GameplaySessionController` is the UI-facing facade for save, exit-to-menu,
  and quit commands. `GameplayUiController` does not call `SceneManager`,
  `Application.Quit`, or `GameplaySaveCoordinator` directly.
- `FarionProjectValidator` blocks builds with missing scripts, empty or
  duplicate persistent ids, incomplete possession/session/save references, or
  invalid definition registries.

## Persistence Identity

- New saves use schema `4`; schema `3` remains readable.
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
  queries, dominant-body sampling, and nearest-surface sampling.
- `CelestialBody` owns runtime body state: radius, surface gravity, initial
  velocity, mass derivation, lock/static behavior, and Rigidbody integration.
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
- `ICelestialEnvironmentProvider` is the narrow bridge from render-authored
  planet data into simulation context. Rendering can provide ocean and
  atmosphere radii, but Simulation does not depend on Rendering.
- `ICelestialSurfaceProvider` is the equivalent bridge for authored procedural
  terrain height, geometric surface normal, and slope angle. It lets gameplay
  sample the same shaped surface used by the visual mesh without making
  Simulation depend on Rendering.
- `PlanetSurfaceModel` is the authoritative runtime surface, climate, biome,
  and terrain-feature sampler for generated planets. It lives in Simulation,
  owns the link between a physical `CelestialBody`, a
  `PlanetaryGenerationProfile`, and a `CelestialShapeProfile`, and implements
  `ICelestialSurfaceProvider`. Gameplay systems should prefer this model when
  they need terrain height, slope, local temperature, moisture, radiation,
  biome, or terrain-feature data. Rendering may consume the same model, but it
  must not become the authority for surface gameplay.
- `PlanetThermalProfile` derives surface temperature from the active stellar
  radiation source. Sandbox star distance and `StellarRadiationProfile`
  reference orbit must be calibrated together; if they are out of scale, biome
  compatibility should fail visibly in reports instead of being hidden by a
  gameplay-side temperature override.
- `GravityActor` applies the active simulation's gravity to ordinary
  Rigidbodies and can align their up axis against the gravity vector.
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
  shape/surface profiles, and keeps the physical body root at identity scale.
  Static/locked bodies can also generate a separate baked collision mesh at its
  own resolution. It does not provide gameplay surface samples; authored
  planets that need landing, biome, resource, or survival data must expose a
  `PlanetSurfaceModel`. Moving N-body planets keep spherical collision until a
  local terrain-collision patch system exists.
- `CelestialLodProfile` owns screen-height thresholds and mesh resolutions for
  the reference-style full-sphere LOD baseline.
- `CelestialLodController` reads an explicitly assigned camera and authored
  `CelestialBodyVisual` targets, then applies LOD levels. Runtime LOD must not
  discover scene objects on a timer; collecting targets is an authoring action.
  It does not generate bodies, create scenes, or own procedural shape data.
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
- `TerrestrialSurfaceProfile` owns solid-surface land, shore, steep terrain, snow,
  texture, and triplanar normal settings.
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
  for a solid-surface planet. It references the terrain shape, land surface,
  ocean profile, and shared values such as sea level.
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
  source. It does not create scene lights.
- `CelestialLightingProfile` owns reusable lighting/exposure defaults such as
  color temperature, inverse-square intensity, shadows, ambient space light, and
  camera clipping.
- `CelestialLightingRig` applies one profile to explicitly authored light,
  focus, and camera references. Runtime lighting must not auto-find scene
  objects; missing references can be resolved through manual authoring tools.
  It can follow a camera or ship focus, but it must not instantiate solar-system
  objects. It also publishes global star shader properties so celestial
  materials can shade from the real star position instead of relying only on
  Directional Light rotation.
- `StarDomeProfile` and `StarDomeController` own the procedural skybox star
  field. The star dome is render-only background context; it must not create
  physical stars, gravity bodies, gameplay navigation points, or scene objects.
- Visual terrain does not modify physical gravity. Runtime collision is
  spherical by default because dynamic celestial bodies have Rigidbodies. Mesh
  collision is generated only for locked/static bodies and uses the same
  shape/surface profile pipeline at a lower resolution. Landing-quality terrain
  collision still needs a dedicated design before gameplay depends on it.
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
  pitch while the motor owns yaw/upright alignment. It is a camera presenter,
  not a gameplay authority.
- `KeyboardBoardingInput`, `VehicleBoardingPoint`, and
  `PlayerPossessionController` own the first ship/on-foot handoff. The
  controller switches active input and camera presenters, but keeps
  `SpacecraftMotor` enabled so gravity and vehicle physics continue while the
  player is outside the ship.

The current flight/landing/orbit stack is the authoritative baseline: it covers
persistent six-degree-of-freedom pilot input, assisted/manual control laws,
gravity feed-forward, bounded boost energy, spherical-body altitude, body-relative radial
velocity, tangential velocity, atmosphere/ocean membership, procedural
terrain-aware surface altitude, approximate local surface slope, water
submersion, first-pass buoyancy/drag, multi-contact touchdown validation, osculating
two-body orbit estimates around the dominant body, and first-pass atmosphere
entry classification. It is not yet a full damage, subsystem, cargo-mass,
autopilot, or moving-planet local terrain-patch model. Those systems must extend
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

`PlanetSurfaceModel` is the source-of-truth boundary for survival and resource
work. Resource generation, visual coverage reports, landing slope checks, and
future suit survival should consume one planet surface sample instead of
recomputing local temperature, moisture, biome, terrain feature, and terrain
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
18. Local terrain patch collision for landing/exploration scale.
19. Underwater damage, controls, and resource interaction.
20. Survival and automation gameplay.
21. Co-op authority and replication.
