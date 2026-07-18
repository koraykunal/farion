# Farion Architecture

Farion is currently a clean gravity-first foundation for a future single-player
and co-op space survival/automation game. Keep the low-level simulation stable
before adding survival loops, automation systems, multiplayer transport, or
high-end celestial rendering.

## Project Layout

- `Assets/Project/Core/Runtime`
  - Engine-agnostic game rules that should not know about gameplay, rendering,
    UI, scenes, or networking.
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
- `Assets/Project/Rendering/Runtime`
  - URP-specific visual systems such as procedural celestial presentation,
    ocean, atmosphere, stars, and camera render passes.
- `Assets/Project/Design`
  - ScriptableObject data for physics, celestial body profiles, rendering
    profiles, spacecraft tuning, and later survival/automation definitions.
- `Assets/Project/Art`
  - Shaders, textures, models, generated lookup textures, and transferred
    reference assets.
- `Assets/Project/Prefabs`
  - Authored prefabs only. Do not recreate scene-builder workflows here.
- `Assets/Project/Scenes`
  - Source-controlled authored scenes.
- `Assets/Project/Docs`
  - Architecture notes and setup instructions.

## Assembly Boundaries

- `Farion.Core.Runtime`
  - No dependency on Simulation, Gameplay, Rendering, networking, UI, or URP.
- `Farion.Simulation.Runtime`
  - Depends on Core. Owns simulation authoring and later procedural celestial
    runtime contracts.
- `Farion.Gameplay.Runtime`
  - Depends on Core and Simulation. Future co-op authority belongs here or in a
    separate networking assembly above this layer.
- `Farion.Rendering.Runtime`
  - Depends on Core and Simulation. URP-specific atmosphere/ocean work belongs
    here, not in Core.

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
- `GravityActor` applies the active simulation's gravity to ordinary
  Rigidbodies and can align their up axis against the gravity vector.
- `WorldOriginRebaser` follows an authored target such as the ship or camera and
  shifts authored scene roots by the same offset when the local scene drifts too
  far from `(0, 0, 0)`. It preserves Rigidbody velocities and only changes local
  scene coordinates. Future co-op state should still use an authoritative world
  coordinate model above this render/physics origin layer.
- `CelestialBodyVisual` owns the generated visual mesh set. It creates a child
  `Terrain Mesh` object, prebuilds full-sphere LOD meshes from the same
  shape/surface profiles, and keeps the physical body root at identity scale.
- `CelestialLodProfile` owns screen-height thresholds and mesh resolutions for
  the reference-style full-sphere LOD baseline.
- `CelestialLodController` reads the active camera and applies LOD levels to
  authored `CelestialBodyVisual` components. It does not generate bodies,
  create scenes, or own procedural shape data.
- `CelestialSurfaceProfileBase` is the shared contract for applying per-body
  material properties. Moon and earth-like bodies use separate profile classes
  instead of sharing unrelated crater, ocean, and biome fields.
- `CelestialSurfaceProfile` owns moon/simple visual surface data such as base
  material, per-body color, smoothness, triplanar normals, and simple fallback
  displacement. It must not contain gravity, orbit, mass, gameplay, or
  networking data.
- `EarthLikeSurfaceProfile` owns earth-like land, shore, steep terrain, snow,
  texture, and triplanar normal settings.
- `CelestialOceanProfile` owns reusable ocean color, transparency, fresnel, and
  specular settings plus the referenced wave normal textures for the
  screen-space ocean pass.
- `CelestialAtmosphereProfile` owns reusable atmosphere radius, density,
  optical-depth LUT generation, scattering coefficients, and dither inputs for
  the screen-space atmosphere pass.
- `EarthLikePlanetVisualProfile` is the high-level authored rendering profile
  for an earth-like body. It references the terrain shape, land surface, and
  ocean profile, and owns shared values such as sea level.
- `EarthLikePlanetVisual` binds that high-level profile to authored terrain
  rendering and registers screen-space ocean/atmosphere data for the URP
  renderer features. It is not a scene builder and does not instantiate a solar
  system. Ocean and atmosphere are not component slots or child meshes in the
  default architecture.
- `FarionOceanRendererFeature` and `FarionAtmosphereRendererFeature` render
  registered bodies as ordered full-screen passes. Each body uses its own
  runtime material instance so per-profile textures such as wave normals,
  optical-depth LUTs, and blue-noise textures cannot leak between planets.
- `CelestialShapeProfile` owns procedural height generation. Moon craters,
  ridges, asteroid deformation, and earth-like terrain should live in shape
  profiles instead of physical body definitions.
- `EarthLikeShapeProfile` ports the reference earth-like continent/ocean-floor
  and mountain-mask shape model into the authored profile system.
- `CelestialLightSource` marks an authored star/body as a physical light
  source. It does not create scene lights.
- `CelestialLightingProfile` owns reusable lighting/exposure defaults such as
  color temperature, inverse-square intensity, shadows, ambient space light, and
  camera clipping.
- `CelestialLightingRig` applies one profile to the scene's authored
  Directional Light, render settings, and camera. It can follow a camera or ship
  focus, but it must not instantiate solar-system objects. It also publishes
  global star shader properties so celestial materials can shade from the real
  star position instead of relying only on Directional Light rotation.
- `StarDomeProfile` and `StarDomeController` own the procedural skybox star
  field. The star dome is render-only background context; it must not create
  physical stars, gravity bodies, gameplay navigation points, or scene objects.
- Visual terrain does not modify physical gravity. Runtime collision is
  spherical by default because dynamic celestial bodies have Rigidbodies. Mesh
  collision should be used only for locked/static bodies until terrain collision
  has a dedicated design.

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

## Roadmap

1. Physics sandbox.
2. Spacecraft gravity/thrust sandbox.
3. Procedural moon mesh generation.
4. Procedural earth-like planet mesh generation.
5. URP celestial surface shaders.
6. URP screen-space ocean and atmosphere baseline.
7. Procedural star dome and space presentation baseline.
8. Reference-style full-sphere celestial LOD.
9. Local world-origin rebasing for ship-scale travel.
10. Player/ship interaction loop.
11. Survival and automation gameplay.
12. Co-op authority and replication.
