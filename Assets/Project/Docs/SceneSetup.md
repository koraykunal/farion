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
4. Keep `Auto Discover Bodies` disabled once those references are assigned.

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
8. Keep `Sync Sphere Collider` enabled for moving planets and moons. This gives
   the body a correct spherical collision surface while the gravity model is
   still radius-based.
9. Keep `Generate Mesh Collider` disabled for moving planets and moons. Use it
   only on locked/static bodies. When mesh collision is active, the generated
   `Terrain Mesh` child receives a separate baked `MeshCollider`, and the root
   sphere collider is disabled so terrain shape owns surface contact.
10. Keep `Bake Mesh Collider` enabled for static/locked body collision tests.
11. Start with `Render Resolution = 32`.
12. Use the component context menu `Rebuild Visual Mesh` if the child mesh does
   not update immediately.

This creates or updates a child object named `Terrain Mesh`. Keep this child
owned by `CelestialBodyVisual`; do not manually scale it to fake the radius.

### Celestial LOD Setup

The first LOD pass follows the Solar-System reference approach: each body
prebuilds a small set of complete sphere meshes, then switches the active
`MeshFilter.sharedMesh` based on the body's viewport height. This is not the
future landing-distance chunk terrain system.

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
- `LOD0 Resolution`: `64`
- `LOD1 Resolution`: `32`
- `LOD2 Resolution`: `16`

Raise `LOD0 Resolution` only after ocean, atmosphere, lighting, and camera
movement are stable. Collision remains separate: moving planets and moons still
use spherical collision by default.

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

Useful texture transfers from the Solar-System reference:

- `Assets/Celestial Body/Textures/Normals/Rock1.jpg`
- `Assets/Celestial Body/Textures/Normals/SnowOld.jpg`

Import these into `Assets/Project/Art/Textures/Celestial/Normals` and mark them
as normal maps in Unity's texture importer before assigning them to
`Normal Map Flat` or `Normal Map Steep`. If you do not assign normal maps yet,
the shader still works, but it will rely mostly on geometry and color blending.

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
- `Assets/Project/Art/Materials/Celestial/Terrestrial.mat`
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
6. `CelestialBodyVisual > Render Resolution`: start at `64`; increase later
   only after shader import
   and profile tuning are stable.
7. Use `TerrestrialPlanetVisual > Apply Planet Visual Profile` if the linked
   profiles do not apply immediately.

Texture inputs used by `SO_TerrestrialSurfaceProfile`:

- `Noise Texture`: `Assets/Project/Art/Textures/Celestial/Terrestrial Noise.psd`
- `Rock Normal`: `Assets/Project/Art/Textures/Celestial/Normals/Rock5.tif`
- `Snow Normal`: `Assets/Project/Art/Textures/Celestial/Normals/Snow.tif`

The continent-ridge shape profile follows the Solar-System reference structure:

- `Continent Noise` creates landmass/ocean distribution.
- `Ocean Floor Depth` and `Ocean Floor Smoothing` flatten deep ocean regions.
- `Ridge Noise` creates mountain chains.
- `Mountain Mask Noise` controls where mountains can appear.
- UV0 stores four extra noise channels for biome and detail shading.

The land shader still colors terrain below sea level for continuity under the
screen-space ocean pass. Sea level should be tuned on
`SO_TerrestrialPlanetVisualProfile`.

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

- `Wave Normal A`: `Assets/Project/Art/Textures/Celestial/Normals/Wave A.png`
- `Wave Normal B`: `Assets/Project/Art/Textures/Celestial/Normals/Wave B.png`

These are not optional decoration. The full screen pass samples them with
triplanar mapping at the ray/ocean-sphere hit point, matching the Solar-System
reference approach of shading water from camera rays rather than from a visible
ocean mesh.

Useful first tuning values:

- `Ocean Level`: tune this on `SO_TerrestrialPlanetVisualProfile`.
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
4. Save it under `Assets/Project/Design/Rendering/Lighting` as
   `SO_SolarLightingProfile`.
5. Select the `Lighting` root.
6. Add `CelestialLightingRig`.
7. Assign:
   - `Profile`: `SO_SolarLightingProfile`
   - `Primary Source`: `Test Star`
   - `Main Directional Light`: the Directional Light under `Lighting`
   - `Sync Light Position To Source`: enabled
   - `Lighting Focus`: `Player Starter Shuttle` while flying, or `CameraRig` while viewing
     the sandbox.
   - `Scene Camera`: the main camera.
8. Disable `Auto Find Primary Source`, `Auto Find Main Directional Light`, and
   `Auto Find Main Camera` after the explicit references above are assigned.
   Keep `Use Main Camera As Fallback Focus` disabled for the authored sandbox.
9. Use the component context menu `Apply Lighting Now`.

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

- `Assets/Project/Design/Rendering/Space/SO_StarDomeProfile.asset`
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
5. Keep `Rigidbody > Use Gravity` disabled. `SpacecraftMotor` applies project
   gravity when `Apply Gravity` is enabled.
6. On `SpacecraftMotor`, leave `Simulation` empty unless you want to assign the
   scene `GravitySimulation` explicitly. Empty means it uses
   `GravitySimulation.Active`.
7. Keep Unity's default `GravityActor` off this object. `SpacecraftMotor` already
   applies project gravity.
8. Move the ship near the planet, outside the surface. For the current test
   planet, a starting position around `(350, 70, -120)` is useful.
9. Add `CelestialActorProbe`.
10. Assign `Simulation > CelestialFrameProvider` to `Frame Provider`.
11. Add `SpacecraftSurfaceContactProbe`.
12. Add `SpacecraftLandingComputer`.
13. Assign `Assets/Project/Design/Gameplay/Flight/SO_DefaultLandingProfile.asset` to
    `Profile`.
14. Assign the same `CelestialActorProbe` component to `Celestial Probe`.
15. Assign the same `SpacecraftSurfaceContactProbe` component to
    `Surface Contact Probe`.
16. Add `SpacecraftLandingGuidanceComputer`.
17. Assign the same `SpacecraftLandingComputer` to `Landing Computer`.
18. Assign the same `CelestialActorProbe` to `Celestial Probe`.
19. On `SpacecraftMotor`, assign the same `SpacecraftSurfaceContactProbe` to
    `Surface Contact Probe`.
20. Keep `Suspend Rotation While In Surface Contact` enabled. This follows the
    Solar-System reference approach of not forcing ship rotation through a
    surface contact.
21. Add `SpacecraftSurfaceContactStabilizer`.
22. Assign the same `SpacecraftSurfaceContactProbe`.
23. Add `SpacecraftOceanInteractor`.
24. Assign `Assets/Project/Design/Gameplay/Flight/SO_DefaultOceanInteractionProfile.asset`
    to `Profile`.
25. Assign the same `CelestialActorProbe` to `Celestial Probe`.
26. Keep `Apply Buoyancy`, `Apply Drag`, and `Damp Angular Velocity` enabled.
27. Add `SpacecraftOrbitComputer`.
28. Assign the same `CelestialActorProbe` to `Celestial Probe`.
29. Assign `Simulation > GravitySimulation` to `Simulation`.
30. Add `SpacecraftEntryCorridorComputer`.
31. Assign `Assets/Project/Design/Gameplay/Flight/SO_DefaultEntryCorridorProfile.asset`
    to `Profile`.
32. Assign the same `CelestialActorProbe` and `SpacecraftOrbitComputer`.
33. Read `SpacecraftLandingGuidanceComputer`, `SpacecraftLandingComputer`,
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
  and `COL_Landing_Footprint`.
- Keep `BoardingPoint` at the side hatch/door trigger. Assign its
  `VehicleBoardingPoint > Exit Point` to `ExteriorExitPoint`.
- Add `PilotSeatPoint`, `InteriorSpawnPoint`, and `ExteriorExitPoint` under the
  ship root. `InteriorSpawnPoint` is where `F` places the explorer when exiting
  the pilot seat into the cockpit.
- Assign those three points on `SpacecraftRig`; camera targets should use named
  child transforms such as `ChaseCameraTarget`, `LandingCameraTarget`, and
  `CockpitCameraTarget`.

Controls:

- `W/S`: forward/back
- `A/D`: strafe
- `Space/Left Ctrl`: ascend/descend
- `Mouse`: yaw/pitch
- `Q/E`: roll
- `Left Shift`: boost

For a camera:

1. Add `SpacecraftCameraRig` to the scene camera or a `CameraRig` child.
2. Assign `Player Starter Shuttle > ChaseCameraTarget` as the target.
3. Start with `Local Offset = (0, 5.5, -18)`.
4. Keep `Snap To Target` enabled. This matches the Solar-System reference more
   closely than a delayed follow camera. Disable it only if you deliberately
   want cinematic camera lag later.

This is only the first ship-control sandbox. Do not add survival, inventory,
automation, networking, HUD, or landing systems until this motion feels stable.

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
  is steeper than `SO_DefaultLandingProfile > Safe Touchdown Slope Angle`.
- `Normalized Stress` approaches `1` as the current descent becomes unsafe for
  the active altitude.
- `Safe Touchdown Window` should be true only very close to the surface with low
  vertical speed, low tangential speed, and acceptable surface slope.
- `Impact Risk` should be true when low-altitude speed is outside the profile
  limits.
- `Has Surface Contact` should become true only while the ship collider is
  touching a celestial body collider.
- `Touchdown Confirmed` should become true only when the ship has surface
  contact and both normal/tangential speeds are inside the active profile
  limits.
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
  `SO_DefaultOceanInteractionProfile > Safe Water Entry Speed`.
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
15. Place the explorer slightly above a locked/static planet mesh collider.

For the first-person camera:

1. Select the scene camera or a camera under `CameraRig`.
2. Disable or remove `SpacecraftCameraRig` while testing on-foot movement.
3. Add `FirstPersonCameraRig`.
4. Assign `Player Explorer > FirstPersonMotor` to `Target`.
5. Assign `Player Explorer > KeyboardFirstPersonInput` to `Input Source`.
6. Start with `Eye Height = 1.65`.
7. Keep `Lock Cursor On Enable` enabled in Play Mode.

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

The first economy pass is data-only. It does not add UI, station interaction,
save data, or upgrade application yet. Its purpose is to keep item purpose,
crafting recipes, and research unlocks authored cleanly before runtime systems
consume them.

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

Recipe definitions live under `Assets/Project/Design/Gameplay/Crafting`:

- `SO_Recipe_IronIngot.asset`: Iron Ore to Iron Ingot.
- `SO_Recipe_NickelPlate.asset`: Nickel Fragment to Nickel Plate.
- `SO_Recipe_Coolant.asset`: Ice Crystal to Coolant.
- `SO_Recipe_ReinforcedHullPanel.asset`: refined structural materials to a hull
  component.
- `SO_Recipe_ThermalRegulator.asset`: coolant and nickel plate to a thermal
  component, locked behind research.

Research definitions live under `Assets/Project/Design/Gameplay/Research`:

- `SO_Research_ThermalRegulation.asset`: consumes early frozen/structural
  materials, unlocks `SO_Recipe_ThermalRegulator.asset`, and declares the future
  capability id `capability.environment.thermal_regulation`.

Do not wire multiplayer, station queues, or upgrade application directly into
these ScriptableObjects. The next runtime layer should consume recipe/research
definitions through a gameplay service that can later become server-authority.

## Gameplay UI Focus

Gameplay UI focus is local-only. It must not pause simulation because the game
is planned for co-op. ESC, inventory, and later station screens block local
player controls through `PlayerControlLock` while gravity, ships, resources,
and other players keep running.

Shared visual components:

- `MenuButtonView` owns menu button visuals, hover/selection animation, icon,
  title, subtitle, disabled state, and DOTween motion.
- `MainMenuButton` only binds the shared button view to `MainMenuAction`.
- `GameplayMenuButton` only binds the shared button view to
  `GameplayMenuAction`.
- `FarionPanelFader` is optional on panels and gives the same fade/slide
  behavior to main menu and gameplay panels.

Create a gameplay UI root in `SC_PhysicsSandbox`:

1. Add a screen-space `Canvas`.
2. Add an `EventSystem` with `InputSystemUIInputModule` if the scene does not
   already have one.
3. Add `GameplayUiController` to the canvas root. Unity also adds the required
   `PlayerControlLock` component.
4. Add `GameplayPanelSwitcher` to the same root.
5. Create these child roots and assign them to `GameplayPanelSwitcher`:
   - `HudRoot`
   - `PauseMenuPanel`
   - `InventoryPanel`
6. Keep `HudRoot` active. Keep `PauseMenuPanel` and `InventoryPanel` inactive
   in the authored scene.
7. Assign `Player Explorer > PlayerInventory` to `GameplayUiController >
   Player Inventory`.
8. Assign `Player Explorer > PlayerInteractionRaycaster` to
   `GameplayUiController > Interaction Raycaster`.
9. Add a TMP text child under `HudRoot` for the interaction prompt and assign it
   to `GameplayUiController > Interaction Prompt Text`.
10. Add `InventoryPanelPresenter` to `InventoryPanel`.
11. Create an inventory slot child prefab or scene object with
   `InventorySlotView` and TMP text fields for name, detail, and quantity.
12. Assign the slot container and slot prefab/view list to
    `InventoryPanelPresenter`.

Runtime behavior:

- `Escape` toggles `PauseMenuPanel`.
- `I` toggles `InventoryPanel`.
- Opening either panel unlocks and shows the cursor.
- Closing all panels locks and hides the cursor.
- `HudRoot` stays active during gameplay. The interaction prompt text is shown
  only when `PlayerInteractionRaycaster` has a valid target and no blocking
  gameplay panel is open.
- While a panel is open, first-person look/move, spacecraft input, and
  interaction raycasts read `PlayerControlLock` and return empty input.
- `Time.timeScale` must stay unchanged.

### Pause Menu Visual Target

Use the shared `MenuButtonView` look for the reference-style vertical menu.
The reusable button prefab is:

`Assets/Project/Prefabs/UI/Common/UI_MenuButton.prefab`

Recommended pause hierarchy:

```text
GameplayCanvas
`-- PauseMenuPanel
    |-- BackgroundScrim
    `-- MenuColumn
        |-- BrandText
        |-- StateText
        |-- ResumeButton
        |-- InventoryButton
        |-- BlueprintsButton
        |-- JournalButton
        |-- ShipButton
        |-- MapButton
        |-- OptionsButton
        |-- SaveButton
        |-- ExitToMainMenuButton
        `-- QuitGameButton
```

`PauseMenuPanel`:

- Full stretch anchors.
- Add `CanvasGroup`.
- Add `FarionPanelFader`.
- Add a dark transparent image as `BackgroundScrim`.
- Keep the panel inactive in the authored scene.

`MenuColumn`:

- Left anchored.
- Suggested width: `520`.
- Suggested left padding: `32`.
- Use a `VerticalLayoutGroup` for the button list only, not for the full panel
  background.

Each button:

- Use `Assets/Project/Prefabs/UI/Common/UI_MenuButton.prefab`.
- Add `GameplayMenuButton` on gameplay menu instances.
- Set `Action` to the matching `GameplayMenuAction`.
- Add `MainMenuButton` on main-menu instances. Do not create a second visual
  button prefab unless a screen genuinely needs a different layout.

### Terrain Mesh Collision

The Solar-System reference generates a separate collision-resolution mesh and
assigns it to a `MeshCollider`. Farion follows that idea only for locked/static
celestial bodies because dynamic N-body planets should not use full concave
terrain mesh colliders as the primary gameplay surface.

For a static terrain-collision test:

1. Select the target body definition, such as `SO_TestPlanet`.
2. Enable `Lock Position`.
3. Apply the definition to the scene body.
4. Select the body's `CelestialBodyVisual`.
5. Enable `Generate Mesh Collider`.
6. Set `Mesh Collider Resolution` to `24` or `32` first.
7. Keep `Bake Mesh Collider` enabled.
8. Use `Rebuild Visual Mesh`.

After rebuilding, the generated `Terrain Mesh` child should have an enabled
`MeshCollider`. The root `SphereCollider` should be disabled while mesh
collision is active. For moving planets, leave `Generate Mesh Collider`
disabled until the future local terrain patch/surface query system exists.

Tune `SO_DefaultLandingProfile` only after confirming the sample values make
sense in Play Mode. For the current sandbox scale, low approach intentionally
starts near the surface so atmospheric entry can be observed before final
approach.
