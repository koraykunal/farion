# SC_PhysicsSandbox Setup

Use `Assets/Project/Scenes/SC_PhysicsSandbox.unity` as the first authored scene.
Do not use a scene builder.

## Scene Hierarchy

Create these root objects:

- `Simulation`
- `Bodies`
- `Actors`
- `Lighting`
- `CameraRig`

## Simulation

On `Simulation`:

1. Add `GravitySimulation`.
2. Assign `Assets/Project/Design/Physics/Gravity/DefaultGravitySettings.asset`.
3. Add `Test Star`, `Test Planet`, and `Test Moon` to `Registered Bodies`.
4. Assign the currently explorable `Test Planet` to `Physics Reference Body`.
5. Keep `Auto Discover Bodies` disabled once those references are assigned.

`Physics Reference Body` is not a position lock and does not remove the
planet's orbit. `CelestialBody.InertialVelocity` continues to evolve through
the N-body solver. The local Unity physics scene subtracts the reference
body's translation from every registered celestial body and applies the
matching reference-frame acceleration correction to actors. This keeps the
explorable non-convex terrain collider stationary under the ship and explorer
without falsifying relative orbits.

Only the active reference body may own adaptive terrain collision. Switching
exploration to another planet must be an explicit reference-frame transition,
not a second moving terrain collider.

### Celestial Frame Provider

`CelestialFrameProvider` is the bridge from gravity simulation to gameplay. It
does not move objects and does not create scene content. It samples the dominant
body, local surface frame, relative velocity, ocean level, and atmosphere state
for actors such as the ship.

On `Simulation`:

1. Add `CelestialFrameProvider`.
2. Assign the scene `GravitySimulation` to `Simulation`.
3. Add `Test Planet > TerrestrialPlanetVisual` to `Environment Sources`.
4. Add terrain visuals that should affect gameplay altitude to `Surface
   Sources`, such as:
   - `Test Planet > CelestialBodyVisual`
   - `Test Moon > CelestialBodyVisual`

Keep `Environment Sources` explicit. If a later planet has ocean or atmosphere,
add that planet's environment provider here. Moon-only bodies can be omitted
until they expose terrain/environment gameplay data.

Keep `Surface Sources` explicit as well. Without this list, `Surface Altitude`
falls back to the body's perfect sphere radius and will not reflect mountains,
craters, or procedural terrain displacement.

`Surface Sources` also provide the sampled terrain normal and `Surface Slope`.
If slope stays near `0 deg` everywhere on a visibly mountainous planet, confirm
the planet's `CelestialBodyVisual` is in this list and rebuild the visual mesh.

### World Origin Rebase Setup

The Solar-System reference keeps the player/camera near local origin by shifting
the physical scene when the camera moves too far away. Farion uses the same core
idea, but keeps it as an explicit authored service so it does not become a hidden
scene builder or future networking authority.

On `Simulation`:

1. Add `WorldOriginRebaser`.
2. Assign `Assets/Project/Design/World/SO_WorldOriginSettings.asset` to
   `Settings`.
3. Assign `Player Starter Shuttle` to `Tracking Target` while testing ship travel. If this
   is empty, rebasing is disabled and the component will warn.
4. Add these transforms to `Shifted Roots`:
   - `Bodies`
   - `Actors`
   - `CameraRig`
   - `Lighting`
5. Start with `Rebase Distance = 1000` on `SO_WorldOriginSettings`.

Do not add `Simulation` itself to `Shifted Roots`. The simulation service has no
world position ownership; only authored scene content should move. `Player Starter Shuttle`
should live under `Actors`; do not rely on automatic target insertion. Rebase
shifts positions only, preserves Rigidbody velocities, and runs in `FixedUpdate`
so physics and camera follow do not fight each other.

Use `WorldOriginRebaser > Validate Setup` after changing the hierarchy. In Play
Mode, `Shift Count`, `Last Origin Offset`, and `Tracking Distance From Origin`
show whether rebasing is actually happening. `Player Starter Shuttle` local values may stay
large if it is nested under a shifted root; what matters is that its world
position and camera remain near local origin after a shift.

When the camera uses `SpacecraftCameraRig`, keep `CameraRig` in `Shifted Roots`.
The camera rig should receive the same origin offset as the ship and world, then
the follow script refreshes its final target-relative position after the shift.

## Bodies

For each body:

1. Create a sphere under `Bodies`.
2. Add `Rigidbody`.
3. Add `CelestialBody`.
4. Add `CelestialBodyDefinitionAuthoring`.
5. Assign one of:
   - `Assets/Project/Design/Physics/CelestialBodies/SO_TestStar.asset`
   - `Assets/Project/Design/Physics/CelestialBodies/SO_TestPlanet.asset`
   - `Assets/Project/Design/Physics/CelestialBodies/SO_TestMoon.asset`
6. Use the component context menu `Apply Definition` if the inspector values do
   not update immediately.
7. Keep `Sync Transform Scale To Radius` enabled while using primitive
   placeholder spheres. Disable it later when a separate procedural visual child
   owns the rendered planet mesh.

Recommended first positions:

- `Test Star`: `(0, 0, 0)`
- `Test Planet`: `(350, 0, 0)`
- `Test Moon`: `(410, 0, 0)`

Keep `Test Star` locked. Tune the planet and moon `Initial Velocity` values on
their definition assets until the motion is stable enough for a sandbox.

## Celestial Visual Mesh

Primitive sphere scaling is only a temporary visibility aid. Once the body is
visible and physics works, move to generated visual meshes:

1. Select a body root such as `Test Planet`.
2. On `CelestialBodyDefinitionAuthoring`, disable `Sync Transform Scale To
   Radius`.
3. Add `CelestialBodyVisual`.
4. Leave `Normalize Body Transform Scale` enabled. The physical body root should
   stay at `(1, 1, 1)` scale.
5. Leave `Disable Root Renderer` and `Disable Root Colliders` enabled if the
   body was created from a Unity primitive sphere.
6. Assign a `CelestialSurfaceProfile`.
7. Assign a `CelestialShapeProfile` if the body should have procedural terrain.
8. Keep `Sync Sphere Collider` enabled for dynamic N-body planets and moons.
   Their physical collision remains spherical.
9. Use `Generate Mesh Collider` only on kinematic bodies. The generated
   `Terrain Mesh` child then assigns its exact LOD0 render mesh to the
   `MeshCollider`; there is no separate collision-resolution mesh. The root
   sphere collider is disabled so one surface owns contact.
10. Keep `Bake Mesh Collider` enabled for kinematic body collision.
11. Start with `Render Resolution = 32`.
12. Use the component context menu `Rebuild Visual Mesh` if the child mesh does
   not update immediately.

This creates or updates a child object named `Terrain Mesh`. Keep this child
owned by `CelestialBodyVisual`; do not manually scale it to fake the radius.

### Celestial LOD Setup

The orbital-distance LOD path follows the Solar-System reference approach: each body
prebuilds a small set of complete sphere meshes, then switches the active
`MeshFilter.sharedMesh` based on the body's viewport height. Authored explorable
bodies hand over to the adaptive patch system near the surface.

Assets created for the first pass:

- `Assets/Project/Design/Rendering/Celestial/SO_CelestialLodProfile.asset`
- `Assets/Project/Rendering/Runtime/Celestial/CelestialLodProfile.cs`
- `Assets/Project/Rendering/Runtime/Celestial/CelestialLodController.cs`

On each `CelestialBodyVisual`:

1. Assign `SO_CelestialLodProfile` to `LOD Profile`.
2. Keep `Edit Mode Preview LOD = 0` while authoring visuals.
3. Use `Rebuild Visual Mesh` after changing LOD resolutions.

On the scene `Lighting` root:

1. Add or confirm `CelestialLodController`.
2. Assign the main camera to `Target Camera`.
3. Add the authored planet/moon `CelestialBodyVisual` components to `Visuals`.
4. Keep `Auto Discover Visuals` disabled once the list is assigned.
5. Keep `Update Every Frame` enabled for Play Mode.

Default profile values:

- `LOD0 Screen Height`: `0.5`
- `LOD1 Screen Height`: `0.2`
- `LOD0 Resolution`: `96`
- `LOD1 Resolution`: `48`
- `LOD2 Resolution`: `24`

Raise `LOD0 Resolution` only after ocean, atmosphere, lighting, and camera
movement are stable. Dynamic N-body planets and moons still use spherical
collision by default.

### Adaptive Surface Patch Setup

Use adaptive patches only on a kinematic body that the player can approach,
land on, or explore:

1. Add `CelestialSurfacePatchSystem` beside `CelestialBodyVisual`.
2. Assign
   `Assets/Project/Design/Rendering/Celestial/SO_CelestialSurfacePatchProfile.asset`.
3. Assign the same body's `CelestialBodyVisual`.
4. Assign the authored gameplay camera; do not use periodic camera discovery.
5. Assign a `collisionObserverSource` that implements
   `ICelestialSurfaceCollisionObserver`. The sandbox uses
   `PlayerPossessionController`, which reports the spacecraft while piloting or
   inside it and the explorer while on foot.
6. Keep the body on a motion mode whose `CelestialBody` supports a non-convex
   surface collider.

Below the entry altitude, the system disables the complete `Terrain Mesh`
renderer and renders a pooled cube-sphere quadtree instead. Collision has a
separate `GlobalFallback -> LocalPreparing -> LocalAuthoritative` handoff. The
global `Terrain Mesh` collider stays authoritative until deepest-level patches
cover the controlled actor's current position, safety margin, and predicted
motion corridor. Only then do local colliders enable before the global collider
disables. Unsafe speed, missing coverage, a visual rebuild, ascent, or an
observer change restores the global collider first. At no point may collision
authority be empty.

Each local collider shares its renderer's exact mesh. Entry/exit hysteresis
prevents rapid whole-system toggling; ghost border normals and patch skirts
prevent lighting seams and visible cracks. Split/merge hysteresis prevents a
branch from oscillating at its LOD boundary, and every adaptive level uses the
same sampling footprint so a topology change does not resample shared vertices
at a different height.

Patch topology changes are transactional. New meshes and cooked colliders are
prepared behind the currently active surface within the profile's per-frame
build-count and millisecond budgets. The global terrain remains visible and
collidable during first activation; an existing patch set remains authoritative
during later refreshes. Only a fully prepared set may commit. Do not restore a
single-frame rebuild path or disable the fallback terrain before staging
finishes.

The default profile uses `16` cells per patch, subdivision level `6`, entry/exit
altitude ratios `0.7/0.9`, collision within `0.16` body radii, a `0.75 s`
prediction horizon, `0.5` coverage safety ratio, and a `0.015` body-radius
safety margin. Tune these as one performance and safety budget; do not raise
patch resolution, subdivision depth, collision radius, prediction horizon,
build count, and millisecond budget together. The sandbox baseline permits at
most `6` patch operations and approximately `2 ms` of patch work per frame.

### Orbit Line Display

Orbit lines are debug/piloting presentation, not gameplay authority. They draw
the current osculating orbit from body position and velocity so you can reason
about orbital insertion and deorbit burn direction.

On the `Lighting` root:

1. Add or confirm `CelestialOrbitLineRenderer`.
2. Assign the scene `GravitySimulation` to `Simulation`.
3. Use `Show Orbit Lines` to enable or disable the lines.
4. Keep `Draw Locked Bodies` disabled so the star does not draw an orbit.
5. Keep `Draw Unbound Trajectories` disabled for the current sandbox.

The renderer creates transient `Orbit Lines` objects for `LineRenderer`
instances. Do not edit those generated children; tune the component fields
instead.

### Simple Surface Profile Setup

Use this only for moon-style or intentionally simple bodies. Terrestrial planets
should use the dedicated terrestrial shape, surface, and ocean profiles below.

1. Create a URP/Lit material under `Assets/Project/Art/Materials/Celestial`.
2. Create surface profiles with `Create > Farion > Rendering > Celestial Surface
   Profile`.
3. Save them under `Assets/Project/Design/Rendering/Celestial`.
4. Assign the shared material to each profile.
5. Tune profile values first, not the generated `Terrain Mesh` child.

Useful starting values:

- Planet profile:
  - `Base Color`: muted blue/green
  - `Smoothness`: `0.35`
  - `Height Amplitude`: `1.5`
  - `Noise Scale`: `2.5`
  - `Noise Octaves`: `4`
- Moon profile:
  - `Base Color`: neutral grey
  - `Smoothness`: `0.2`
- Star profile:
  - Use an emission material later. For now keep displacement at `0`.

### Moon Crater Shape Setup

For a cratered moon:

1. Create a profile with `Create > Farion > Simulation > Celestial > Moon
   Crater Shape Profile`.
2. Save it under `Assets/Project/Design/Simulation/Celestial/Shapes`.
3. Assign it to `CelestialBodyVisual > Shape Profile`.
4. Start with:
   - `Crater Count`: `400`
   - `Crater Radius Min Max`: `(0.01, 0.1)`
   - `Size Distribution`: `0.6`
   - `Rim Steepness`: `0.13`
   - `Rim Width`: `1.6`
   - `Crater Depth Scale`: `1`
   - `Low Frequency Amplitude`: `0.01`
   - `Ridge Amplitude`: `0.006`
   - `Desired Ejecta Crater Count`: `2`
   - `Ejecta Ray Scale`: `10`
   - `Biome Point Count`: `24`
5. Increase `Render Resolution` to `64` when tuning crater detail. Keep it lower
   while editing many values quickly.

### Moon Triplanar Material Setup

For the first URP moon surface:

1. Create a material under `Assets/Project/Art/Materials/Celestial`.
2. Set its shader to `Farion/Celestial/Moon Triplanar`.
3. Assign this material to the moon `CelestialSurfaceProfile`.
4. In the same surface profile, tune:
   - `Base Color`: light grey
   - `Secondary Color`: darker grey
   - `Steep Color`: dark grey
   - `Smoothness`: `0.15` to `0.25`
   - `Albedo Scale`: `6` to `10`
   - `Normal Flat Scale`: `15` to `20`
   - `Normal Steep Scale`: `12` to `20`
   - `Normal Strength`: `0.3` to `0.6`
   - `Ejecta Strength`: `0.4` to `0.8`
   - `Ejecta Ray Frequency`: `28` to `48`

The moon shape profile writes shader data into mesh UV0. If ejecta rays or
biome variation do not appear after changing shape settings, run
`CelestialBodyVisual > Rebuild Visual Mesh` on the moon object.

The active moon maps live under
`Assets/Project/Art/Textures/Celestial/Moon`. Older Solar-System comparison
maps are retained under `ArtSource/Celestial/Legacy`; do not
assign those legacy copies to runtime profiles without an explicit art pass.

### Terrestrial Planet Setup

The terrestrial planet pass uses authored profiles, the generated `Terrain Mesh`,
and the screen-space ocean/atmosphere renderer feature. Ocean and atmosphere are
not authored as visible child meshes by default.

Assets created for this pass:

- `Assets/Project/Design/Rendering/Celestial/SO_TerrestrialPlanetVisualProfile.asset`
- `Assets/Project/Design/Simulation/Celestial/Shapes/SO_ContinentRidgeShapeProfile.asset`
- `Assets/Project/Design/Rendering/Celestial/SO_TerrestrialSurfaceProfile.asset`
- `Assets/Project/Design/Rendering/Celestial/SO_DefaultOceanProfile.asset`
- `Assets/Project/Design/Rendering/Celestial/SO_DefaultAtmosphereProfile.asset`
- `Assets/Project/Art/Materials/Celestial/MAT_Celestial_Terrestrial.mat`
- `Assets/Project/Art/Shaders/Celestial/FarionTerrestrialTriplanar.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionOceanPostProcess.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionAtmospherePostProcess.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionAtmosphereOpticalDepth.compute`

On `Test Planet`:

1. Add `TerrestrialPlanetVisual`.
2. Assign `SO_TerrestrialPlanetVisualProfile` to `Profile`.
3. Assign the same body's `CelestialBodyVisual` to `Terrain Visual`.
4. Do not add ocean or atmosphere mesh children. Those are rendered by the URP
   full screen pass from the profile data.
5. `CelestialBodyVisual > Material`: leave empty so the surface profile owns
   the material.
6. Assign `SO_CelestialLodProfile`; its `96/48/24` full-sphere meshes cover
   orbital and medium-distance presentation. Do not raise the fallback
   `Render Resolution` when a LOD profile is assigned.
7. Use `TerrestrialPlanetVisual > Apply Planet Visual Profile` if the linked
   profiles do not apply immediately.

Texture inputs used by `SO_TerrestrialSurfaceProfile`:

- `Noise Texture`: `Assets/Project/Art/Textures/Celestial/Terrestrial/TX_Celestial_Terrestrial_Noise.psd`
- `Rock Normal`: `Assets/Project/Art/Textures/Celestial/Terrestrial/TX_Celestial_Terrestrial_Rock_Normal.tif`
- Snow and frozen-surface detail comes from the texture sets under
  `Assets/Project/Art/Textures/Celestial/SurfaceMaterials`, not from a material-owned
  legacy snow normal.

The continent-ridge shape profile follows the Solar-System reference structure:

- `Continent Noise` creates landmass/ocean distribution.
- `Ocean Floor Depth` and `Ocean Floor Smoothing` flatten deep ocean regions.
- `Ridge Noise` creates mountain chains.
- `Mountain Mask Noise` controls where mountains can appear.
- UV0 stores four shape-detail channels for large, detail, small, and warped
  shading noise. Surface-material weights and state use fixed-resolution
  cubemaps and are independent from mesh LOD.

The land shader still colors terrain below sea level for continuity under the
screen-space ocean pass. Sea level is authored once on
`SO_TestPlanetHydrosphere`; `SO_TerrestrialPlanetVisualProfile` owns only the
ocean and atmosphere rendering styles.

### Ocean And Atmosphere Post-Process Setup

Ocean and atmosphere are a screen-space renderer feature, not part of the
terrain mesh. This matches the Solar-System reference direction: camera depth
and ray-sphere intersection decide where water and air are visible.

Assets created for the first pass:

- `Assets/Project/Design/Rendering/Celestial/SO_DefaultOceanProfile.asset`
- `Assets/Project/Design/Rendering/Celestial/SO_DefaultAtmosphereProfile.asset`
- `Assets/Project/Art/Shaders/Celestial/FarionOceanPostProcess.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionAtmospherePostProcess.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionAtmosphereOpticalDepth.compute`

Renderer setup:

1. Open `Assets/Settings/PC_Renderer.asset`.
2. Confirm `Farion Ocean Post Process` exists and is active.
3. Confirm `Farion Atmosphere Post Process` exists and is active after ocean.
4. Keep ocean at `Before Rendering Post Processing`.
5. Keep atmosphere immediately after ocean.
6. Keep `Max Rendered Bodies` at `8` for now. Lower it only for profiling, not
   for visual tuning.
7. Confirm `SO_TerrestrialPlanetVisualProfile` has `SO_DefaultOceanProfile`
   and `SO_DefaultAtmosphereProfile` assigned.

Texture inputs used by `SO_DefaultOceanProfile` and sampled by the screen-space
ocean shader:

- `Wave Normal A`: `Assets/Project/Art/Textures/Celestial/Ocean/TX_Celestial_Ocean_WaveA_Normal.png`
- `Wave Normal B`: `Assets/Project/Art/Textures/Celestial/Ocean/TX_Celestial_Ocean_WaveB_Normal.png`

These are not optional decoration. The full screen pass samples them with
triplanar mapping at the ray/ocean-sphere hit point, matching the Solar-System
reference approach of shading water from camera rays rather than from a visible
ocean mesh. The celestial import policy keeps both maps linear, repeatable,
mipmapped, trilinear, and imported as normal maps.

Useful first tuning values:

- `Ocean Level`: tune this on `SO_TerrestrialPlanetVisualProfile`.
- `Wave Strength`: `0.25` to `0.45`
- `Smoothness`: `0.85` to `0.95`
- `Specular Strength`: `1` to `2`

Atmosphere uses the same screen-space ownership model and a profile-generated
baked optical-depth lookup:

- `FarionAtmosphereOpticalDepth.compute` integrates normalized atmospheric
  density into a reusable floating-point lookup.
- `FarionAtmospherePostProcess.shader` ray-marches visible atmosphere segments
  and uses the lookup for both view and star-ray extinction.
- `SO_DefaultAtmosphereProfile` owns thickness, density falloff, wavelengths,
  scattering strength, sample counts, and dither controls.
- Ray-march jitter uses URP's package-owned
  `Textures/BlueNoise256/LDR_LLL1_0`. Do not replace it with a color texture,
  sparse dot mask, or sRGB noise.

Atmosphere and ocean must be judged in Game view from at least three positions:
orbit with a visible limb, surface daylight looking toward the horizon, and the
night-side terminator. Parameter tuning from Scene view alone is not a release
validation.

## Actor Probe

Create a simple test actor under `Actors`:

1. Create a capsule or cube.
2. Add `Rigidbody`.
3. Add `GravityActor`.
4. Place it above the planet surface.
5. Keep `Rigidbody > Use Gravity` disabled. `GravityActor` applies Farion
   gravity from `GravitySimulation`; Unity's built-in gravity must not be mixed
   into the sandbox.
6. Press Play and verify it accelerates toward the nearest body.

## Camera And Lighting

- Put a camera under `CameraRig` looking at the planet and moon.
- If Game view is blank, make sure the camera is not inside `Test Star`. A
  useful starting position is around `(180, 120, -520)` looking toward the
  planet.
- Put a directional light under `Lighting`.
- For the first visual baseline, use the star as the single authoritative light
  source. Do not add local fill lights around planets unless debugging.

### Star Material

The current authored assets are:

- `Assets/Project/Art/Materials/Celestial/MAT_Celestial_Star.mat`
- `Assets/Project/Art/Shaders/Lighting/FarionStarEmission.shader`
- `Assets/Project/Design/Rendering/Celestial/SO_TestStarVisualProfile.asset`

Use this ownership hierarchy:

1. Keep the physical `CelestialBody`, collider, radiation source, and
   `CelestialLightSource` on `Test Star`.
2. Add a child named `Star Visual` with the sphere `MeshFilter` and
   `MeshRenderer`.
3. Add `CelestialStarVisual` to `Test Star`.
4. Assign:
   - `Profile`: `SO_TestStarVisualProfile`
   - `Scaled Space Profile`: `SO_CelestialScaledSpaceProfile`
   - `Light Source`: the root `CelestialLightSource`
   - `Star Renderer`: `Star Visual > MeshRenderer`
   - `Star Visual Transform`: the `Star Visual` child
   - `Observer Camera`: the authored gameplay camera
5. Keep the child renderer's shadow casting, shadow receiving, light probes,
   reflection probes, and motion vectors disabled. `CelestialStarVisual`
   enforces these settings when it applies the profile.

Do not place `CelestialStarVisual` on the child and do not assign the physical
root as `Star Visual Transform`. Scaled-space placement is allowed to move only
the render child, never the physical body.

The star must not use a lit planet/moon material. It should be rendered as an
unlit emissive body; otherwise the scene Directional Light will visibly shade
the star surface, which is physically and visually wrong for this setup. The
photosphere shader uses seamless object-space granulation, larger convection
cells, sunspots, limb darkening, subtle rotation, and HDR emission. Scene bloom
provides the corona response.

`SO_CelestialScaledSpaceProfile` is shared by stars, planets, and moons. Beyond
its transition distance it logarithmically compresses render distance while
reducing visual scale by the same ratio. Direction, angular size, and depth
ordering remain stable; physical bodies, colliders, gravity, and orbits stay at
their simulation positions. Add `CelestialScaledSpaceVisual` to planet and moon
roots and assign their existing `CelestialBodyVisual`, the shared profile, and
the gameplay camera. The proxy reuses the lowest existing LOD mesh and material;
it does not create a second surface or physics system. Ocean and atmosphere
passes follow the same projected center and scale.

Use `CelestialVisualValidation.md` as the shared automated and Game View quality
gate after changing any celestial material, profile, texture, or renderer
feature.

### Celestial Lighting Rig

Create an authored lighting setup:

1. Select `Test Star`.
2. Add `CelestialLightSource`.
3. Create a profile with `Create > Farion > Rendering > Celestial Lighting
   Profile`.
4. Save it under `Assets/Project/Design/Rendering/Lighting` as
   `SO_SolarLightingProfile`.
5. Select the `Lighting` root.
6. Add `CelestialLightingRig`.
7. Assign:
   - `Profile`: `SO_SolarLightingProfile`
   - `Primary Source`: `Test Star`
   - `Main Directional Light`: the Directional Light under `Lighting`
   - `Sync Light Position To Source`: disabled; Directional Light position has
     no lighting meaning.
   - `Lighting Focus`: the main gameplay camera transform.
   - `Scene Camera`: the main camera.
8. Keep `Use Main Camera As Fallback Focus` enabled as a recovery path; the
   explicit `Lighting Focus` remains authoritative.
9. Use the component context menu `Apply Lighting Now`.

Useful first profile values:

- `Reference Distance`: `350`
- `Reference Intensity`: `1.1`
- `Invert Light Direction`: disabled
- `Use Inverse Square Falloff`: disabled for a Directional Light
- `Minimum Falloff Distance`: `50`
- `Minimum Intensity`: `0`
- `Maximum Intensity`: `4`
- `Shadows`: `Soft`
- `Shadow Strength`: around `0.86`
- `Directional Shadow Angle`: `0.4` to `0.8`
- `Ambient Light`: dark blue/grey, around `(0.018, 0.021, 0.028)`
- `Far Clip Plane`: `5000` for the current sandbox scale

The rig rotates the Directional Light so lit sides face the star. Keep its
transform at the `Lighting` root rather than moving it to astronomical
coordinates. Physical radiation falloff belongs to simulation profiles;
the local URP Directional Light is a stable, effectively infinite source.

Farion celestial shaders use the global star properties published by
`CelestialLightingRig`. Terrain, atmosphere, and ocean consume the same
camera-focused direction-to-star state. The Directional Light remains the URP
main light and shadow-map owner; shaders must not derive a second competing
direction independently.

Shadow map resolution is controlled by the URP Render Pipeline Asset, not by
`CelestialLightingRig`. For the PC profile, use
`Assets/Settings/PC_RPAsset.asset > Main Light > Shadow Resolution`.

### Star Dome

The star dome is a procedural skybox, not a physical sphere in the scene. It
does not affect gravity, lighting, atmosphere scattering, or navigation.

Assets created for the first pass:

- `Assets/Project/Design/Rendering/Space/SO_StarDomeProfile.asset`
- `Assets/Project/Art/Materials/Space/MAT_Space_StarDome.mat`
- `Assets/Project/Art/Shaders/Space/FarionProceduralStarDome.shader`

Setup:

1. Select the `Lighting` root.
2. Add `StarDomeController`.
3. Assign `SO_StarDomeProfile` to `Profile`.
4. Keep `Apply On Enable` enabled.
5. Keep `Update Every Frame` disabled unless you later add animated space
   weather or time-based sky changes.
6. Use the component context menu `Apply Star Dome Now`.

Tune star density, brightness, and size on `SO_StarDomeProfile`, not on scene
objects. If the background looks too busy, lower `Star Density` first before
lowering exposure. The star dome does not own nebula rendering.

### Volumetric Nebula

`Bodies/NebulaVolume` owns the single world-space nebula in
`SC_PhysicsSandbox`. `FarionNebulaRendererFeature` raymarches only inside that
sphere, stops at scene depth, and is wired through `PC_Renderer.asset`.

The density model uses the supplied Shadertoy spiral-wave volume. `Structure
Scale` changes turbulent feature size. `Max Step Count` is only the GPU budget;
do not use it as a detail control. `Extinction` controls background visibility.

The authored camera far clip is `50000`, enough for physical nearby planets
without expanding shadow distance. Celestial bodies needed beyond that range
should use a scaled-space visual rather than a still larger physical far clip.

### Post Processing

The scene must have exactly one `GlobalVolume` under `Lighting`:

1. Create or select `GlobalVolume`.
2. Enable `Is Global`.
3. Create/assign a scene Volume Profile under
   `Assets/Project/Scenes/SC_PhysicsSandbox`.
4. Add these overrides:
   - `Bloom`: enabled, low threshold, moderate intensity for the star material.
   - `Tonemapping`: `ACES`.
   - `Color Adjustments`: small negative exposure if the moon clips to white,
     slight positive contrast.
5. On the main camera's URP camera data, enable `Render Post Processing`.
6. On `SO_CelestialLightingProfile`, keep `Camera Clear Flags` set to `Skybox`
   if the procedural star dome should appear in Game view.

Keep the post-process profile subtle. The moon surface should still read from
real geometry, normals, and shadows, not from over-bloomed exposure.
For the sandbox baseline use Bloom threshold `1.1`, intensity `1`, scatter
`0.55`, post exposure `-0.1`, and contrast `4`.

## Validation

Before moving to spacecraft controls:

- Enter Play Mode with no compile errors.
- Confirm `GravitySimulation.Active` exists.
- Confirm every body has `Rigidbody.useGravity = false`.
- Confirm the star remains locked.
- Confirm the actor uses project gravity rather than Unity default gravity.

## Player Starter Shuttle

After the gravity actor test works, create the player starter spacecraft:

1. Create an empty object under `Actors` named `Player Starter Shuttle`.
2. Add `Rigidbody`.
3. Add `KeyboardSpacecraftInput`.
4. Add `SpacecraftMotor`.
5. Assign
   `Assets/Project/Design/Gameplay/Flight/SO_PlayerStarterShuttleFlightProfile.asset`
   to `Flight Profile`.
6. Keep `Rigidbody > Use Gravity` disabled. `SpacecraftMotor` applies project
   gravity when `Apply Gravity` is enabled.
7. On `SpacecraftMotor`, leave `Simulation` empty unless you want to assign the
   scene `GravitySimulation` explicitly. Empty means it uses
   `GravitySimulation.Active`.
8. Keep Unity's default `GravityActor` off this object. `SpacecraftMotor` already
   applies project gravity.
9. Move the ship near the planet, outside the surface. For the current test
   planet, a starting position around `(350, 70, -120)` is useful.
10. Add `CelestialActorProbe`.
11. Assign `Simulation > CelestialFrameProvider` to `Frame Provider`.
12. Add `SpacecraftSurfaceContactProbe`.
13. Add `SpacecraftLandingComputer`.
14. Assign
    `Assets/Project/Design/Gameplay/Flight/SO_PlayerStarterShuttleLandingProfile.asset`
    to `Profile`.
15. Assign the same `CelestialActorProbe` component to `Celestial Probe`.
16. Assign the same `SpacecraftSurfaceContactProbe` component to
    `Surface Contact Probe`.
17. Add `SpacecraftLandingGuidanceComputer`.
18. Assign the same `SpacecraftLandingComputer` to `Landing Computer`.
19. Assign the same `CelestialActorProbe` to `Celestial Probe`.
20. On `SpacecraftMotor`, assign the same `SpacecraftSurfaceContactProbe` to
    `Surface Contact Probe`.
21. Keep `Suspend Rotation While In Surface Contact` enabled. This follows the
    Solar-System reference approach of not forcing ship rotation through a
    surface contact.
22. Add `SpacecraftSurfaceContactStabilizer`.
23. Assign the same `SpacecraftSurfaceContactProbe`.
24. Add `SpacecraftOceanInteractor`.
25. Assign
    `Assets/Project/Design/Gameplay/Flight/SO_PlayerStarterShuttleOceanInteractionProfile.asset`
    to `Profile`.
26. Assign the same `CelestialActorProbe` to `Celestial Probe`.
27. Keep `Apply Buoyancy`, `Apply Drag`, and `Damp Angular Velocity` enabled.
28. Add `SpacecraftOrbitComputer`.
29. Assign the same `CelestialActorProbe` to `Celestial Probe`.
30. Assign `Simulation > GravitySimulation` to `Simulation`.
31. Add `SpacecraftEntryCorridorComputer`.
32. Assign `Assets/Project/Design/Gameplay/Flight/SO_DefaultEntryCorridorProfile.asset`
    to `Profile`.
33. Assign the same `CelestialActorProbe` and `SpacecraftOrbitComputer`.
34. Read `SpacecraftLandingGuidanceComputer`, `SpacecraftLandingComputer`,
    `SpacecraftOrbitComputer`, `SpacecraftEntryCorridorComputer`,
    `CelestialActorProbe`, `SpacecraftSurfaceContactProbe`, and
    `SpacecraftOceanInteractor` directly in the Inspector while tuning. The
    product-facing HUD should consume those components later; do not add a
    separate IMGUI debug presenter to production scenes.

Starter shuttle scene contract:

- Keep the physics/input owner on `Player Starter Shuttle`. Do not rotate this
  root to fix model orientation.
- Put the imported FBX instance under `VisualRoot`; use `VisualRoot` local
  rotation for model-facing corrections.
- Do not keep a root `MeshRenderer`, `MeshFilter`, or broad root `BoxCollider`
  on `Player Starter Shuttle`. The root owns runtime systems and Rigidbody
  only.
- Use simple compound child colliders under the ship root: `COL_Hull_Main`,
  `COL_Hull_Nose`, `COL_Hull_EngineBlock`, `COL_Wing_Left`, `COL_Wing_Right`,
  `COL_Landing_Front`, `COL_Landing_Left`, and `COL_Landing_Right`.
- Keep `BoardingPoint` at the side hatch/door trigger. Assign its
  `VehicleBoardingPoint > Exit Point` to `ExteriorExitPoint`.
- Add `PilotSeatPoint`, `InteriorSpawnPoint`, and `ExteriorExitPoint` under the
  ship root. `InteriorSpawnPoint` is where `F` places the explorer when exiting
  the pilot seat into the cockpit.
- Assign those three points on `SpacecraftRig`; camera targets should use named
  child transforms such as `ChaseCameraTarget`, `LandingCameraTarget`, and
  `CockpitCameraTarget`.

Controls:

- `W/S`: increase/decrease persistent forward throttle
- `A/D`: strafe
- `Space/Left Ctrl`: ascend/descend
- `Mouse`: yaw/pitch
- `Q/E`: roll
- `X`: zero throttle and assisted brake
- `Left Shift`: boost while forward throttle is positive and charge remains
- `Z`: toggle flight assist
- `G`: deploy/retract landing gear
- `C`: exterior/cockpit camera
- `F`: leave the pilot seat

For a camera:

1. Add `SpacecraftCameraRig` to the scene camera or a `CameraRig` child.
2. Assign `Player Starter Shuttle > ChaseCameraTarget` as the target.
3. Start with `Local Offset = (0, 5.5, -18)`.
4. Keep `Snap To Target` disabled for the starter shuttle and use the authored
   responsiveness/lag limits. Enable snapping only for initial acquisition or
   explicit camera cuts.
5. Keep obstacle avoidance enabled. The camera ignores colliders under its
   spacecraft target and sphere-casts against the configured obstacle layers.

`SC_PhysicsSandbox` includes the production flight HUD presenter under
`GameplayCanvas/HudRoot/SpacecraftFlightHud`. It is visible only while the
player is piloting and reads existing flight/landing telemetry.

### Starter Shuttle Production Rig Gate

The current FBX exposes one shared `Engine Nozzle` transform. Do not duplicate
that pivot in code or place guessed RCS emitters on the physics root. Before the
final VFX/gimbal pass, update the source model with these named empties:

```text
EngineGimbal_Left
EngineGimbal_Right
Thruster_Main_Left
Thruster_Main_Right
Thruster_Reverse_Left
Thruster_Reverse_Right
RCS_Front_Left
RCS_Front_Right
RCS_Rear_Left
RCS_Rear_Right
GearContact_Front
GearContact_Left
GearContact_Right
LandingCameraTarget
```

1. Put each engine-gimbal pivot at the actual mechanical hinge and parent its
   main-thruster marker below it.
2. Orient every thruster marker so its local axis matches the plume component's
   configured extension direction.
3. Keep the imported model under `VisualRoot`; do not rotate or offset the
   Rigidbody root.
4. Reimport the FBX, then replace the shared `Engine Nozzle` entry in
   the two authored `SpacecraftThrusterNozzleVfx` steering roots.
5. Create the production rear-thruster VFX under `THR_MainRear_L/R` using
   `SpacecraftThrusterVfxController` and `SpacecraftThrusterNozzleVfx`.
   Follow `Assets/Project/Docs/ThrusterVfxGraphSetup.md` for the VFX Graph
   properties, explicit nozzle lights, smoke, and validation checklist.
6. Align the three small authored landing-contact colliders with the final
   `GearContact_*` model markers. Keep all three assigned to
   `SpacecraftLandingGearAnimator > Landing Gear Colliders`.
7. Assign `LandingCameraTarget` only when a third landing-camera mode is
   intentionally designed; the current production toggle is exterior/cockpit.

While in Play Mode, select `Player Starter Shuttle` and watch
`CelestialActorProbe > Runtime Sample`:

- `Dominant Body Name` should become the nearest/highest-gravity body.
- `Surface Altitude` should decrease as you approach the surface.
- `Surface Slope` should rise on steep terrain and stay low on flatter terrain.
- `Radial Velocity` is negative while descending and positive while climbing.
- `Tangential Speed` is the body-relative orbital/horizontal speed.
- `Inside Atmosphere` should enable inside the atmosphere radius.
- `Below Ocean Level` should enable only under the ocean sphere.

These values are the basis for the future landing loop. The next step is not to
fake landing with a trigger, but to derive entry corridor, burn timing, safe
vertical speed, and touchdown rules from this frame sample.

Then check `SpacecraftLandingComputer > Runtime Assessment`:

- `Phase` should move through `Orbit`, `Deorbiting`, `AtmosphericDescent`,
  `LowApproach`, and finally `TouchdownWindow` or `UnsafeTouchdown` depending
  on speed and altitude.
- `Risks` should flag excessive vertical or tangential speed near the surface.
- `Risks` should flag `ExcessiveSurfaceSlope` when the current sampled terrain
  is steeper than
  `SO_PlayerStarterShuttleLandingProfile > Safe Touchdown Slope Angle`.
- `Normalized Stress` approaches `1` as the current descent becomes unsafe for
  the active altitude.
- `Safe Touchdown Window` should be true only very close to the surface with low
  vertical speed, low tangential speed, and acceptable surface slope.
- `Impact Risk` should be true when low-altitude speed is outside the profile
  limits.
- `Has Surface Contact` should become true only while the ship collider is
  touching a celestial body collider.
- `Touchdown Confirmed` should become true only when the ship has surface
  contact and normal speed, tangential speed, and sampled slope are inside the
  profile's strict touchdown limits. Center altitude does not veto a valid
  physical gear contact.
- `Unsafe Surface Contact` means contact happened, but not inside safe
  touchdown limits.

Then check `SpacecraftLandingGuidanceComputer > Runtime Guidance`:

- `Level` should move from `Advisory` or `Caution` into `Warning` or `Critical`
  only when speed/contact risk crosses the landing profile limits.
- `Command` should explain the next piloting priority without changing controls.
- `Command` should show `SeekLevelSurface` when speed is acceptable but the
  terrain slope is not safe for touchdown.
- `Stress` should rise as descent or lateral speed approaches unsafe limits.

Then check `SpacecraftOceanInteractor > Runtime Ocean`:

- `Has Ocean` should be true while the dominant body exposes an ocean profile.
- `Touching Water` becomes true before the ship center passes below sea level,
  based on `Effective Hull Radius`.
- `Center Below Water` becomes true only after the ship center is below the
  ocean sphere.
- `Submerged Fraction` should rise from `0` to `1` as the hull goes underwater.
- `Buoyancy Acceleration` and `Drag Acceleration` should be non-zero only while
  touching water.
- `Water Entry Speed` is the ocean-radial impact speed into the water.
- `Unsafe Water Entry` becomes true when entering water faster than
  `SO_PlayerStarterShuttleOceanInteractionProfile > Safe Water Entry Speed`.
- `Pressure Stress` should rise with depth after `Pressure Warning Depth`.
- `Crushing Depth` becomes true at or below `Crush Depth`; this is telemetry for
  future damage, not damage by itself yet.

The ocean has no collider. Water physics is derived from the same mathematical
ocean sphere used by the screen-space renderer feature. The terrain mesh
collider still owns sea-floor or land contact.

Then check `SpacecraftOrbitComputer > Runtime Orbit`:

- `Regime` should show `NearCircular` or `Elliptic` when the ship is in a bound
  orbit, `Suborbital` when the current path intersects the body, and `Escape`
  when velocity is above escape energy.
- `Circular Velocity` and `Escape Velocity` are computed from the dominant
  body's gravitational parameter at the ship's current radius.
- `Periapsis` below `0 m` means the current trajectory intersects the body's
  spherical collision surface.
- `Flight Path Angle` is negative while descending, positive while climbing, and
  near zero while moving mostly along the horizon.

Then check `SpacecraftEntryCorridorComputer > Runtime Entry Corridor`:

- `SafeEntry` means the current speed, entry angle, and periapsis are inside the
  active profile limits.
- `ShallowEntry` means the trajectory is too close to horizontal and may skip
  the atmosphere.
- `SteepEntry` or `Impacting` means periapsis/angle should be corrected before
  descent.
- `Overspeed` means the ship should slow down before committing to atmosphere
  entry.

Landing, orbit, entry, and ocean telemetry are currently inspected from the
ship components. Surface these values through the gameplay HUD only after the
resource/inventory/crafting loop is readable.

## First-Person Explorer

The first on-foot actor is a physics actor, not a `CharacterController`. It uses
the same `CelestialActorProbe` contract as the spacecraft, so local up, surface
slope, ocean state, and atmosphere state come from the authored celestial
simulation.

Create a test explorer under `Actors`:

1. Create a capsule named `Player Explorer`.
2. Add `Rigidbody`.
3. Add `CapsuleCollider`.
4. Add `KeyboardFirstPersonInput`.
5. Add `CelestialActorProbe`.
6. Assign `Simulation > CelestialFrameProvider` to `Frame Provider`.
7. Add `FirstPersonMotor`.
8. Assign `Assets/Project/Design/Gameplay/Character/SO_DefaultFirstPersonMotorProfile.asset`
   to `Profile`.
9. Assign the same `KeyboardFirstPersonInput` to `Input Source`.
10. Assign the main camera transform to `View Reference` after adding the camera
    rig below.
11. Keep `Apply Celestial Gravity` enabled.
12. On the `Rigidbody`, keep `Use Gravity` disabled.
13. Start with `Rigidbody > Mass = 80`, `Drag = 0`, and `Angular Drag = 0.05`.
14. Start with `CapsuleCollider > Radius = 0.35`, `Height = 1.8`, and
    `Center = (0, 0, 0)`.
15. Place the explorer slightly above the active physics-reference planet mesh
    collider. The body may retain `KinematicOrbit`; do not move its collider
    through Unity space while it is the exploration reference.

For the first-person camera:

1. Select the scene camera or a camera under `CameraRig`.
2. Disable or remove `SpacecraftCameraRig` while testing on-foot movement.
3. Add `FirstPersonCameraRig`.
4. Assign `Player Explorer > FirstPersonMotor` to `Target`.
5. Assign `Player Explorer > KeyboardFirstPersonInput` to `Input Source`.
6. Start with `Eye Height = 1.65`.
7. Keep `Lock Cursor On Enable` enabled in Play Mode.

`FirstPersonCameraRig` smooths only the eye offset relative to the interpolated
player pose. Do not reintroduce absolute world-position smoothing: it turns
planetary translation and origin rebases into visible camera lag against the
ground.

On `Simulation > WorldOriginRebaser`, keep `Actors` and `CameraRig` in
`Shifted Roots`. While testing the explorer alone, set `Tracking Target` to
`Player Explorer`. When testing the ship again, set it back to `Player Starter Shuttle`.

Controls:

- `W/S`: forward/back along the local surface frame.
- `A/D`: strafe along the local surface frame.
- `Mouse`: yaw/pitch.
- `Space`: jump.
- `Left Shift`: sprint.
- `E`: reserved for interaction/boarding; it is captured by input but does not
  trigger gameplay until the boarding layer is added.

In Play Mode, check `Player Explorer > CelestialActorProbe > Runtime Sample`:

- `Dominant Body Name` should be the planet.
- `Surface Altitude` should stay near the capsule foot clearance while grounded.
- `Surface Slope` should change over terrain.
- `Local Up` should follow the planet surface normal.

Then check `FirstPersonMotor > Runtime Movement`:

- `Grounded` should be true while standing on the terrain mesh collider.
- `Walkable Ground` should become false on slopes above
  `SO_DefaultFirstPersonMotorProfile > Max Walkable Slope Angle`.
- `Surface Speed` should rise while walking or sprinting and fall when input is
  released.
- The capsule should rotate upright against the current celestial local up.

Do not add inventory, tools, resource collection, health, or co-op replication
until this basic explorer can walk, jump, and camera-look reliably on the
planet surface. The next gameplay layer is possession/boarding: a single active
control owner that switches between `Player Explorer` and `Player Starter Shuttle`.

## Player Possession And Boarding

Use this layer to switch between the spacecraft and the on-foot explorer. The
ship motor stays enabled in both modes because it owns gravity and vehicle
physics. Only the active input source and camera presenter change.

Create the boarding point:

1. Under `Player Starter Shuttle`, create an empty child named `BoardingPoint`.
2. Place it near the hatch/door where the player should stand to re-enter.
3. Match its forward direction to `ExteriorExitPoint` if it also drives a door
   trigger.
4. Add `VehicleBoardingPoint`.
5. Use a trigger `BoxCollider` around the side hatch.
6. Assign `ExteriorExitPoint` to `VehicleBoardingPoint > Exit Point`.

Create the possession owner:

1. Create an empty object under `Actors` named `Player Possession`.
2. Add `KeyboardBoardingInput`.
3. Add `PlayerPossessionController`.
4. Set `Initial Mode = Spacecraft`.
5. Assign `KeyboardBoardingInput` to `Boarding Input Source`.
6. Assign `Player Starter Shuttle` to `Spacecraft Root`.
7. Assign `Player Starter Shuttle > Rigidbody` to `Spacecraft Rigidbody`.
8. Assign `Player Starter Shuttle > SpacecraftMotor` to `Spacecraft Motor`.
9. Assign `Player Starter Shuttle > KeyboardSpacecraftInput` to `Spacecraft Input`.
10. Assign the scene camera's `SpacecraftCameraRig` to `Spacecraft Camera Rig`.
11. Assign `Player Starter Shuttle > ChaseCameraTarget` to `Spacecraft Camera Target`.
12. Assign `Player Starter Shuttle > BoardingPoint` to `Boarding Point`.
13. Assign `Player Explorer` to `Explorer Root`.
14. Assign `Player Explorer > Rigidbody` to `Explorer Rigidbody`.
15. Assign `Player Explorer > FirstPersonMotor` to `Explorer Motor`.
16. Assign `Player Explorer > KeyboardFirstPersonInput` to `Explorer Input`.
17. Assign the scene camera's `FirstPersonCameraRig` to `First Person Camera
    Rig`.
18. Assign `Simulation > WorldOriginRebaser` to `Origin Rebaser`.
19. Keep `Update Origin Tracking Target` enabled.

Controls:

- `F`: exit the spacecraft.
- `E`: enter the spacecraft while the explorer is inside the boarding radius.

In Play Mode:

1. Start in the ship.
2. Press `F`. `Player Explorer` should activate at `InteriorSpawnPoint` in the
   cockpit/interior.
3. The ship should keep simulating physics, but `KeyboardSpacecraftInput` should
   be disabled.
4. `FirstPersonCameraRig` and `KeyboardFirstPersonInput` should be enabled.
5. Open the boarding door/ramp, then walk through `ExteriorExitPoint.forward` to
   transition from ship interior to on-foot outside.
6. Walk back into `BoardingPoint` radius and press `E`.
7. `Player Explorer` should deactivate, `SpacecraftCameraRig` should re-enable,
   and `WorldOriginRebaser > Tracking Target` should return to `Player Starter Shuttle`.

## First Resource Loop

The first vertical-slice resource path is intentionally small. It proves that
on-foot interaction can mutate player state without putting inventory logic in
the raycaster or UI.

### Planetary Biome And Resource Preview

Biome, terrain-feature, and resource generation are separated from runtime
state. Use the context-menu reports while authoring or validating a planet;
runtime spawning is handled by `ResourceDepositRuntimeSpawner`.

On the target planet:

1. Add `PlanetSurfaceModel`.
2. Assign the planet `CelestialBody` to `Body`.
3. Assign `Assets/Project/Design/Simulation/Planetary/SO_TestPlanetaryGeneration.asset`
   to `Generation Profile`.
4. Assign the same terrain shape profile used by the planet visual, or assign
   it through `PlanetaryGenerationProfile > Shape Profile`.
5. Use `Log Generation Validation Report` after changing radius, gravity,
   atmosphere, climate, biome compatibility, or terrain-feature constraints.
6. Use `CelestialBodyVisual > Log Biome Visual Coverage Report` to confirm
   terrain altitude, slope, local temperature, moisture, radiation, biome, and
   terrain-feature sampling all come from the same `PlanetSurfaceModel`.

`SO_TestStellarRadiation > Reference Orbit Distance` is calibrated to the
current authored `Test Star` to `Test Planet` distance. If you move the star or
rescale the sandbox, update the radiation profile or the planet's thermal report
will legitimately shift biome coverage. The test biome set includes cold,
temperate, rocky, and hot-basalt coverage so reports should not show every
sample as missing biome under ordinary sandbox tuning.

For resource distribution:

1. Add `ResourceDepositRuntimeSpawner` to the target planet.
2. Assign the same `Body` and `PlanetSurfaceModel`.
3. Assign `Assets/Project/Design/Gameplay/Resources/SO_TestPlanetResourceDistribution.asset`
   to `Resource Distribution`.
4. Use `Log Resource Distribution Report` to verify sampled surface count,
   missing biome count, allowed resource rules, and generated deposit counts.
5. Use `Regenerate Resource Nodes` while tuning distribution density.

The generated deposit data stores local surface directions and resource
definitions plus biome and terrain-feature classification. Runtime state such
as depleted deposits must remain outside the ScriptableObject assets.
Resource node definitions must have explicit `Visual Prefab` assignments;
missing prefabs are skipped instead of being replaced with primitive fallback
objects.

Possession changes only schedule resource streaming; they must not synchronously
spawn a complete surface population. The sandbox limits one refresh to `8`
successful spawns and `32` candidate evaluations. Remaining deposits stream on
later refreshes, preventing ship exit and camera handoff from becoming a
single-frame generation spike.

The current test distribution uses real starter resources instead of generic
surface pickups. Iron is limited to rocky, basalt, and temperate biomes; nickel
is limited to rocky and basalt biomes; ice crystal is limited to frozen crust.
Terrain features are not resource filters yet; they should become resource
modifiers after their coverage is stable.

Current vertical-slice placeholder prefabs:

- `Assets/Project/Prefabs/Gameplay/Resources/PF_IronOreNode.prefab`
- `Assets/Project/Prefabs/Gameplay/Resources/PF_NickelFragmentNode.prefab`
- `Assets/Project/Prefabs/Gameplay/Resources/PF_IceCrystalNode.prefab`

These are authored temporary prefabs, not runtime fallbacks. Replace their
visuals later through the `ResourceNodeDefinition > Visual Prefab` field when
the final resource art direction is defined.

On `Player Explorer`:

1. Add or confirm `PlayerInventory`.
2. Keep `Slot Capacity = 12` for the first slice.

For a collectible surface node:

1. Create or select a visible object near the landing area.
2. Add a collider that is reachable by the player's interaction ray. Trigger
   colliders are valid for collectible nodes.
3. Add `CelestialSurfaceAnchor`.
4. Assign the target planet's `CelestialBody` to `Body`.
5. Assign the same planet's `CelestialBodyVisual` to `Surface Provider` when
   the node should sit on procedural terrain instead of the perfect sphere.
6. Use `Capture Current Pose` from the component context menu after placing the
   object visually on the surface.
7. Keep `Update Mode = Locked Surface Pose` for ordinary resource nodes. Use
   `Resample Surface Every Frame` only for explicitly deforming terrain tests;
   resource spawning should not make every node query procedural surface data
   every frame.
8. Add `ResourceNodeInteractable`.
9. Assign `Assets/Project/Design/Gameplay/Resources/SO_IronOreNode.asset` to
   `Definition`.
10. Keep `Initialize Reserve On Awake` and `Consume On Depleted` enabled for the
   first test.

In Play Mode, exit the ship, look at the node, press `E`, and verify the node
disables itself and `Player Explorer > PlayerInventory > Stacks` gains one
`Iron Ore`.

## First Economy Contracts

The current economy foundation contains typed item definitions and one proven
Fleet Storage processing exchange. It does not yet contain production queues,
research, or upgrade application.

Item definitions live under `Assets/Project/Design/Gameplay/Inventory` and now
carry:

- `Category`: Structural, Electronic, Energy, Chemical, Biological, or Exotic.
- `Form`: Raw, Refined, Component, Consumable, Research Sample, or Artifact.
- `Primary Tech Domain`: the research branch most directly associated with the
  item.
- `Requires Identification`: reserved for later research-gated unknown
  materials.

Current starter item chain:

- Raw: `SO_IronOreItem.asset`, `SO_NickelFragmentItem.asset`,
  `SO_IceCrystalItem.asset`.
- Refined: `SO_IronIngotItem.asset`, `SO_NickelPlateItem.asset`,
  `SO_CoolantItem.asset`.
- Components: `SO_ReinforcedHullPanelItem.asset`,
  `SO_ThermalRegulatorItem.asset`.

Processing definitions live under
`Assets/Project/Design/Gameplay/Processing`:

- `SO_Process_IronOre.asset`: the first typed Iron Ore processing exchange.

Do not create Crafting or Research folders until those systems have a real
runtime consumer. Do not wire multiplayer, station queues, or upgrade
application directly into definition assets; runtime services own mutations
and authority.

## Gameplay UI Focus

Gameplay UI focus is local-only. It must not pause simulation because the game
is planned for co-op. ESC, inventory, and later station screens block local
player controls through `PlayerControlLock` while gravity, ships, resources,
and other players keep running.

The production source of truth is now:

```text
GameplayCanvas
|-- HudRoot                     UiScreenView: GameplayHud / Hud
|-- UI_PauseMenuScreen          UiScreenView: PauseMenu / Screen
|-- UI_InventoryScreen          UiScreenView: Inventory / Screen
|-- UI_SettingsScreen           UiScreenView: Settings / Screen
|-- UI_SaveLoadScreen           UiScreenView: SaveLoad / Screen
|-- UI_SystemRoot               scene-scoped services
|-- UI_ConfirmationDialog       UiScreenView: Confirmation / Modal
|-- UI_LoadingOverlay           UiScreenView: Loading / System
`-- UI_FeedbackOverlay          queued non-blocking messages
```

`GameplayUiController` receives one explicit `UiSystemRoot` reference.
`UiScreenRouter` is the sole owner of screen visibility, history, cancel, focus
restoration, and UI-related `PlayerControlLock`. The router merges its explicit
screen list with all `UiScreenView` instances owned by the same Canvas,
including Settings, Save/Load, Confirmation, Loading, and Feedback.

The scene has exactly one EventSystem. `UiInputModuleBinder` assigns the
`FarionInputActions` UI map only in Play Mode. Never serialize a generated
runtime action asset or action references into the scene.

Runtime behavior:

- `Escape` opens Pause or closes only the current top cancelable screen.
- `I` toggles Inventory through the same router.
- Opening either panel unlocks and shows the cursor.
- Closing all panels locks and hides the cursor.
- `HudRoot` stays active during gameplay. The interaction prompt text is shown
  only when `PlayerInteractionRaycaster` has a valid target and no blocking
  gameplay panel is open.
- While a panel is open, first-person look/move, spacecraft input, and
  interaction raycasts read `PlayerControlLock` and return empty input.
- `Time.timeScale` must stay unchanged.

### Pause Menu Visual Target

Use the authored
`Assets/Project/Prefabs/UI/Screens/UI_PauseMenuScreen.prefab`. It owns the
restrained full-screen panel, `CanvasGroup`, `FarionPanelFader`,
`GameplayMenuListPresenter`, and menu entries. The presenter creates
`UI_MenuButton` views from its serialized entries and submits
`GameplayMenuAction` values to `GameplayUiController`.

Do not add per-button action components or a second panel switcher. New menu
actions belong in `GameplayMenuAction`, their presentation data belongs in the
pause prefab entry list, and the outcome remains in `GameplayUiController` or a
lower application/gameplay service.

### Terrain Mesh Collision

Farion deliberately does not retain the Solar-System reference's independent
collision-resolution sphere. A visible mesh and a separately sampled collider
can disagree near steep terrain. Kinematic bodies therefore share render
geometry with collision; dynamic N-body bodies retain spherical collision.

For a stable terrain-collision test:

1. Select the target body definition, such as `SO_TestPlanet`.
2. Assign the scene body to `GravitySimulation > Physics Reference Body`.
3. Apply the definition to the scene body. It may remain `KinematicOrbit`;
   the reference frame, not a static-data workaround, owns local stability.
4. Select the body's `CelestialBodyVisual`.
5. Enable `Generate Mesh Collider`.
6. Keep `Bake Mesh Collider` enabled.
7. Use `Rebuild Visual Mesh`.

After rebuilding, the generated `Terrain Mesh` child should have an enabled
`MeshCollider`. The root `SphereCollider` should be disabled while mesh
collision is active, and `MeshFilter.sharedMesh` must be the same object as
`MeshCollider.sharedMesh`.

The player spacecraft uses `ContinuousDynamic` collision detection because its
Rigidbody owns a compound of primitive colliders and can approach the static
terrain MeshCollider at flight speed. Keep the on-foot capsule on
`ContinuousSpeculative`; changing CCD modes does not replace the global/local
collision-authority handoff.

For the authored explorable test planet, also use the adaptive surface patch
setup above. Near the ground, the global renderer is disabled. Its collider
remains the fallback until local patch colliders have complete predicted
coverage, then collision ownership transfers without an empty authority frame.
Dynamic N-body bodies must leave `Generate Mesh Collider` disabled. Project
validation rejects an adaptive collision body that is not the scene
simulation's physics reference body.

Tune `SO_PlayerStarterShuttleLandingProfile` only after confirming the sample
values make sense in Play Mode. Its touchdown altitude includes the current
starter-shuttle gear clearance; a different hull must own a separate landing
profile rather than silently reusing this geometry-dependent value.
