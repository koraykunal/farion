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
2. Assign `Assets/Project/Design/Physics/DefaultGravitySettings.asset`.
3. Leave `Auto Discover Bodies` enabled for the first sandbox.

### World Origin Rebase Setup

The Solar-System reference keeps the player/camera near local origin by shifting
the physical scene when the camera moves too far away. Farion uses the same core
idea, but keeps it as an explicit authored service so it does not become a hidden
scene builder or future networking authority.

On `Simulation`:

1. Add `WorldOriginRebaser`.
2. Assign `Assets/Project/Design/World/SO_WorldOriginSettings.asset` to
   `Settings`.
3. Assign `Probe Ship` to `Tracking Target` while testing ship travel. If this
   is empty, it can fall back to the main camera.
4. Add these transforms to `Shifted Roots`:
   - `Bodies`
   - `Actors`
   - `CameraRig`
   - `Lighting`
5. Keep `Use Main Camera When Target Missing` enabled for sandbox testing.
6. Keep `Include Tracking Target When Missing` enabled. This prevents the ship
   from being left behind if it is not currently under one of the shifted roots.
7. Start with `Rebase Distance = 1000` on `SO_WorldOriginSettings`.

Do not add `Simulation` itself to `Shifted Roots`. The simulation service has no
world position ownership; only authored scene content should move. `Probe Ship`
should normally live under `Actors`, but the rebaser can also include the
tracking target directly as a safety net. Rebase shifts positions only and
preserves Rigidbody velocities, so gravity/orbit math remains consistent after
all bodies and actors receive the same offset.

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
   - `Assets/Project/Design/Celestial/SO_TestStar.asset`
   - `Assets/Project/Design/Celestial/SO_TestPlanet.asset`
   - `Assets/Project/Design/Celestial/SO_TestMoon.asset`
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
8. Keep `Sync Sphere Collider` enabled. This gives the body a correct spherical
   collision surface while the gravity model is still radius-based.
9. Keep `Generate Mesh Collider` disabled for moving planets and moons. Use it
   only on locked/static bodies.
10. Start with `Render Resolution = 32`.
11. Use the component context menu `Rebuild Visual Mesh` if the child mesh does
   not update immediately.

This creates or updates a child object named `Terrain Mesh`. Keep this child
owned by `CelestialBodyVisual`; do not manually scale it to fake the radius.

### Celestial LOD Setup

The first LOD pass follows the Solar-System reference approach: each body
prebuilds a small set of complete sphere meshes, then switches the active
`MeshFilter.sharedMesh` based on the body's viewport height. This is not the
future landing-distance chunk terrain system.

Assets created for the first pass:

- `Assets/Project/Design/Rendering/SO_CelestialLodProfile.asset`
- `Assets/Project/Rendering/Runtime/Celestial/CelestialLodProfile.cs`
- `Assets/Project/Rendering/Runtime/Celestial/CelestialLodController.cs`

On each `CelestialBodyVisual`:

1. Assign `SO_CelestialLodProfile` to `LOD Profile`.
2. Keep `Edit Mode Preview LOD = 0` while authoring visuals.
3. Use `Rebuild Visual Mesh` after changing LOD resolutions.

On the scene `Lighting` root:

1. Add or confirm `CelestialLodController`.
2. Assign the main camera to `Target Camera`.
3. Keep `Auto Discover Visuals` enabled.
4. Keep `Update Every Frame` enabled for Play Mode.

Default profile values:

- `LOD0 Screen Height`: `0.5`
- `LOD1 Screen Height`: `0.2`
- `LOD0 Resolution`: `64`
- `LOD1 Resolution`: `32`
- `LOD2 Resolution`: `16`

Raise `LOD0 Resolution` only after ocean, atmosphere, lighting, and camera
movement are stable. Collision remains separate: moving planets and moons still
use spherical collision by default.

### Simple Surface Profile Setup

Use this only for moon-style or intentionally simple bodies. Earth-like planets
should use the dedicated earth-like shape, surface, and ocean profiles below.

1. Create a URP/Lit material under `Assets/Project/Art/Materials/Celestial`.
2. Create surface profiles with `Create > Farion > Rendering > Celestial Surface
   Profile`.
3. Save them under `Assets/Project/Design/Rendering`.
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

1. Create a profile with `Create > Farion > Rendering > Moon Crater Shape
   Profile`.
2. Save it under `Assets/Project/Design/Rendering`.
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

Useful texture transfers from the Solar-System reference:

- `Assets/Celestial Body/Textures/Normals/Rock1.jpg`
- `Assets/Celestial Body/Textures/Normals/SnowOld.jpg`

Import these into `Assets/Project/Art/Textures/Celestial/Normals` and mark them
as normal maps in Unity's texture importer before assigning them to
`Normal Map Flat` or `Normal Map Steep`. If you do not assign normal maps yet,
the shader still works, but it will rely mostly on geometry and color blending.

### Earth-Like Planet Setup

The earth-like planet pass uses authored profiles, the generated `Terrain Mesh`,
and the screen-space ocean/atmosphere renderer feature. Ocean and atmosphere are
not authored as visible child meshes by default.

Assets created for this pass:

- `Assets/Project/Design/Rendering/SO_EarthLikePlanetVisualProfile.asset`
- `Assets/Project/Design/Rendering/SO_EarthLikeShapeProfile.asset`
- `Assets/Project/Design/Rendering/SO_EarthLikeSurfaceProfile.asset`
- `Assets/Project/Design/Rendering/SO_EarthOceanProfile.asset`
- `Assets/Project/Design/Rendering/SO_EarthAtmosphereProfile.asset`
- `Assets/Project/Art/Materials/Celestial/Earth.mat`
- `Assets/Project/Art/Shaders/Celestial/FarionEarthTriplanar.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionOceanPostProcess.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionAtmospherePostProcess.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionAtmosphereOpticalDepth.compute`

On `Test Planet`:

1. Add `EarthLikePlanetVisual`.
2. Assign `SO_EarthLikePlanetVisualProfile` to `Profile`.
3. Assign the same body's `CelestialBodyVisual` to `Terrain Visual`.
4. Do not add ocean or atmosphere mesh children. Those are rendered by the URP
   full screen pass from the profile data.
5. `CelestialBodyVisual > Material`: leave empty so the surface profile owns
   the material.
6. `CelestialBodyVisual > Render Resolution`: start at `64`; increase later
   only after shader import
   and profile tuning are stable.
7. Use `EarthLikePlanetVisual > Apply Planet Visual Profile` if the linked
   profiles do not apply immediately.

Texture inputs used by `SO_EarthLikeSurfaceProfile`:

- `Noise Texture`: `Assets/Project/Art/Textures/Celestial/Earth Noise.psd`
- `Rock Normal`: `Assets/Project/Art/Textures/Celestial/Normals/Rock5.tif`
- `Snow Normal`: `Assets/Project/Art/Textures/Celestial/Normals/Snow.tif`

The earth-like shape profile follows the Solar-System reference structure:

- `Continent Noise` creates landmass/ocean distribution.
- `Ocean Floor Depth` and `Ocean Floor Smoothing` flatten deep ocean regions.
- `Ridge Noise` creates mountain chains.
- `Mountain Mask Noise` controls where mountains can appear.
- UV0 stores four extra noise channels for biome and detail shading.

The land shader still colors terrain below sea level for continuity under the
screen-space ocean pass. Sea level should be tuned on
`SO_EarthLikePlanetVisualProfile`.

### Ocean And Atmosphere Post-Process Setup

Ocean and atmosphere are a screen-space renderer feature, not part of the
terrain mesh. This matches the Solar-System reference direction: camera depth
and ray-sphere intersection decide where water and air are visible.

Assets created for the first pass:

- `Assets/Project/Design/Rendering/SO_EarthOceanProfile.asset`
- `Assets/Project/Design/Rendering/SO_EarthAtmosphereProfile.asset`
- `Assets/Project/Art/Shaders/Celestial/FarionOceanPostProcess.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionAtmospherePostProcess.shader`
- `Assets/Project/Art/Shaders/Celestial/FarionAtmosphereOpticalDepth.compute`

Renderer setup:

1. Open `Assets/Settings/PC_Renderer.asset`.
2. Confirm `Farion Ocean Post Process` exists and is active.
3. Confirm `Farion Atmosphere Post Process` exists and is active after ocean.
4. Keep ocean at `Before Rendering Post Processing`.
5. Keep atmosphere immediately after ocean.
6. Confirm `SO_EarthLikePlanetVisualProfile` has `SO_EarthOceanProfile`
   and `SO_EarthAtmosphereProfile` assigned.

Texture inputs used by `SO_EarthOceanProfile` and sampled by the screen-space
ocean shader:

- `Wave Normal A`: `Assets/Project/Art/Textures/Celestial/Normals/Wave A.png`
- `Wave Normal B`: `Assets/Project/Art/Textures/Celestial/Normals/Wave B.png`

These are not optional decoration. The full screen pass samples them with
triplanar mapping at the ray/ocean-sphere hit point, matching the Solar-System
reference approach of shading water from camera rays rather than from a visible
ocean mesh.

Useful first tuning values:

- `Ocean Level`: tune this on `SO_EarthLikePlanetVisualProfile`.
- `Normal Strength`: `0.35` to `0.6`
- `Smoothness`: `0.85` to `0.95`
- `Specular Strength`: `1` to `2`

Atmosphere is present as a lightweight screen-space scattering baseline. The
next quality step is a baked optical-depth texture like the Solar-System
reference, not a transparent mesh shell.

## Actor Probe

Create a simple test actor under `Actors`:

1. Create a capsule or cube.
2. Add `Rigidbody`.
3. Add `GravityActor`.
4. Place it above the planet surface.
5. Press Play and verify it accelerates toward the nearest body.

## Camera And Lighting

- Put a camera under `CameraRig` looking at the planet and moon.
- If Game view is blank, make sure the camera is not inside `Test Star`. A
  useful starting position is around `(180, 120, -520)` looking toward the
  planet.
- Put a directional light under `Lighting`.
- For the first visual baseline, use the star as the single authoritative light
  source. Do not add local fill lights around planets unless debugging.

### Star Material

1. Create a material under `Assets/Project/Art/Materials/Celestial` named
   `Star`.
2. Set its shader to `Farion/Lighting/Star Emission`.
3. Start with:
   - `Emission Color`: warm white/orange.
   - `Intensity`: `6` to `10`.
   - `Rim Power`: `1.5` to `3`.
   - `Rim Strength`: `1` to `2`.
4. Assign this material to the star's `CelestialSurfaceProfile`, or directly to
   `CelestialBodyVisual > Material` if you are still testing.
5. On `Test Star > CelestialLightSource`, assign the same material to
   `Emission Material`.
6. Keep `Apply Emission Material`, `Disable Shadow Casting`, `Disable Shadow
   Receiving`, and `Disable Probe Lighting` enabled.

The star must not use a lit planet/moon material. It should be rendered as an
unlit emissive body; otherwise the scene Directional Light will visibly shade
the star surface, which is physically and visually wrong for this setup.

### Celestial Lighting Rig

Create an authored lighting setup:

1. Select `Test Star`.
2. Add `CelestialLightSource`.
3. Create a profile with `Create > Farion > Rendering > Celestial Lighting
   Profile`.
4. Save it under `Assets/Project/Design/Rendering` as
   `SO_SolarLightingProfile`.
5. Select the `Lighting` root.
6. Add `CelestialLightingRig`.
7. Assign:
   - `Profile`: `SO_SolarLightingProfile`
   - `Primary Source`: `Test Star`
   - `Main Directional Light`: the Directional Light under `Lighting`
   - `Sync Light Position To Source`: enabled
   - `Lighting Focus`: `Probe Ship` while flying, or `CameraRig` while viewing
     the sandbox.
   - `Scene Camera`: the main camera.
8. Use the component context menu `Apply Lighting Now`.

Useful first profile values:

- `Reference Distance`: `350`
- `Reference Intensity`: `6`
- `Invert Light Direction`: disabled
- `Use Inverse Square Falloff`: disabled for the first visual calibration
- `Minimum Falloff Distance`: `80`
- `Minimum Intensity`: `1.25`
- `Maximum Intensity`: `8` to `12`
- `Shadows`: `Soft`
- `Shadow Strength`: `0.85` to `0.95`
- `Directional Shadow Angle`: `0.4` to `0.8`
- `Ambient Light`: dark blue/grey, around `(0.04, 0.045, 0.055)`
- `Far Clip Plane`: `5000` for the current sandbox scale

The rig rotates the Directional Light so lit sides face the star. Directional
Light position does not affect Unity lighting, but `Sync Light Position To
Source` keeps the authored light object on the star so the hierarchy remains
easy to reason about.

Farion celestial shaders use the global star properties published by
`CelestialLightingRig`, so moon/planet lighting direction is calculated from
the authored star position. The Directional Light still stays in the scene for
URP main-light compatibility and shadow-map support.

Shadow map resolution is controlled by the URP Render Pipeline Asset, not by
`CelestialLightingRig`. For the PC profile, use
`Assets/Settings/PC_RPAsset.asset > Main Light > Shadow Resolution`.

### Star Dome

The star dome is a procedural skybox, not a physical sphere in the scene. It
does not affect gravity, lighting, atmosphere scattering, or navigation.

Assets created for the first pass:

- `Assets/Project/Design/Rendering/SO_StarDomeProfile.asset`
- `Assets/Project/Art/Materials/Space/StarDome.mat`
- `Assets/Project/Art/Shaders/Space/FarionProceduralStarDome.shader`

Setup:

1. Select the `Lighting` root.
2. Add `StarDomeController`.
3. Assign `SO_StarDomeProfile` to `Profile`.
4. Keep `Apply On Enable` enabled.
5. Keep `Update Every Frame` disabled unless you later add animated space
   weather or time-based sky changes.
6. Use the component context menu `Apply Star Dome Now`.

Tune star density, brightness, size, and the galactic band on
`SO_StarDomeProfile`, not on scene objects. If the background looks too busy,
lower `Star Density` first before lowering exposure.

### Post Processing

The scene can have a single `Global Volume` under `Lighting`:

1. Create or select `Global Volume`.
2. Enable `Is Global`.
3. Create/assign a Volume Profile under `Assets/Project/Design/Rendering`.
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

## Validation

Before moving to spacecraft controls:

- Enter Play Mode with no compile errors.
- Confirm `GravitySimulation.Active` exists.
- Confirm every body has `Rigidbody.useGravity = false`.
- Confirm the star remains locked.
- Confirm the actor uses project gravity rather than Unity default gravity.

## Spacecraft Probe

After the gravity actor test works, create a spacecraft probe:

1. Create a capsule or cube under `Actors` named `Probe Ship`.
2. Add `Rigidbody`.
3. Add `KeyboardSpacecraftInput`.
4. Add `SpacecraftMotor`.
5. On `SpacecraftMotor`, leave `Simulation` empty unless you want to assign the
   scene `GravitySimulation` explicitly. Empty means it uses
   `GravitySimulation.Active`.
6. Keep Unity's default `GravityActor` off this object. `SpacecraftMotor` already
   applies project gravity.
7. Move the probe near the planet, outside the surface. For the current test
   planet, a starting position around `(350, 70, -120)` is useful.

Controls:

- `W/S`: forward/back
- `A/D`: strafe
- `Space/Left Ctrl`: ascend/descend
- `Mouse`: yaw/pitch
- `Q/E`: roll
- `Left Shift`: boost

For a camera:

1. Add `SpacecraftCameraRig` to the scene camera or a `CameraRig` child.
2. Assign `Probe Ship` as the target.
3. Start with `Local Offset = (0, 4, -14)`.
4. Keep `Snap To Target` enabled. This matches the Solar-System reference more
   closely than a delayed follow camera. Disable it only if you deliberately
   want cinematic camera lag later.

This is only the first ship-control sandbox. Do not add survival, inventory,
automation, networking, HUD, or landing systems until this motion feels stable.
