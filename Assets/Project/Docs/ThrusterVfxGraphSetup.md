# Starter Shuttle Thruster VFX

The starter shuttle owns a layered, project-authored thruster presentation.
Flight physics produces telemetry. The VFX system consumes it and never feeds
forces, input, or audio state back into simulation.

`SpacecraftThrusterVfxController` builds one stable
`SpacecraftThrusterVfxFrame` from:

- `SpacecraftMotor.Telemetry`;
- `PersonalShipRuntimeBinding.State` for hull damage;
- `CelestialActorProbe.CurrentSample` for atmosphere density;
- `SpacecraftOceanInteractor.CurrentInteraction` for underwater suppression.

Each `SpacecraftThrusterNozzleVfx` applies that frame to its mesh layers, VFX
Graphs, steering transform, and local light. FMOD spacecraft audio remains
owned by `Assets/Project/Audio/Runtime`.

## Production Prefab Contract

```text
PF_PlayerStarterShuttle
`-- VisualRoot
    `-- VFX
        |-- THR_MainRear_L  (sideSign -1)
        |   |-- CoreGlow
        |   |-- InnerCone
        |   |-- OuterPlasma
        |   |-- ShockDiamonds
        |   |-- Distortion
        |   |-- VFX_Sparks
        |   |-- VFX_Smoke
        |   `-- LT_Thruster
        `-- THR_MainRear_R  (sideSign 1)
            `-- same authored children
```

Both nozzle roots are positioned at their engine exits. Every direct child has
zero local position. Mesh and VFX content emits along nozzle-local positive Z.
The two VFX Graph instances use different deterministic seeds.

The `VFX` owner contains one `SpacecraftThrusterVfxController` with both nozzle
references explicitly serialized. Runtime hierarchy lookup exists only as a
defensive recovery path.

## Layer Responsibilities

- `CoreGlow`: stable HDR emission at the engine mouth, including low idle glow.
- `InnerCone`: bright, compact plasma volume and primary throttle length.
- `OuterPlasma`: softer Fresnel/noise shell that provides volume.
- `ShockDiamonds`: restrained boost-sensitive compression pattern.
- `Distortion`: URP scene-color refraction driven by load, boost, and heat.
- `VFX_Sparks`: optional boost/damage accents.
- `VFX_Smoke`: atmosphere-only exhaust; suppressed underwater.
- `LT_Thruster`: local hull illumination with no shadows.

The five primary visual layers use project-owned procedural meshes and authored
URP materials. Do not revive the deleted legacy beam, third-party flamethrower,
runtime prefab-instantiation, or independent particle-controller paths.

## Runtime Contract

The frame publishes:

```text
Throttle 0..1
Boost 0..1
Heat 0..1
Damage 0..1
AtmosphereDensity 0..1
RelativeSpeed >= 0
LocalTranslation -1..1
LocalRotation -1..1
LocalLinearAcceleration
LocalAngularAcceleration
ThrusterCommand
```

Rear main-plume load follows forward thrust, boost, positive forward-axis
acceleration, and a restrained angular-stabilization floor. Reverse, strafe,
and vertical translation do not incorrectly light the rear main engines as if
they were forward thrust.

Graphs may expose any of these telemetry properties plus:

```text
Rate
Count
Speed
Lifetime
Size
Alpha
```

Missing optional properties are ignored. Spawn rate reaches zero before a
graph receives `Stop`; the component stays enabled for its configured lifetime
so existing particles can decay without popping.

## Authoring

Run `Farion > VFX > Rebuild Starter Shuttle Thrusters` after replacing the
starter-shuttle model, changing nozzle hierarchy, or recreating materials. The
operation is idempotent and only modifies
`PF_PlayerStarterShuttle.prefab`.

`FarionProjectValidation` requires both nozzle signs, all five mesh-layer
references, valid mesh renderers/materials, all three VFX Graph references, the
local light, and explicit controller nozzle references.

## Acceptance

1. Run EditMode flight/VFX tests and project validation.
2. Open `SC_PhysicsSandbox`; confirm both engine effects originate from separate
   nozzle exits and point aft.
3. Check idle, cruise, boost, throttle release, yaw, roll, atmosphere, water,
   damaged, and repaired states.
4. Confirm core, cones, diamonds, distortion, particles, light, and audio respond
   from the same telemetry without changing flight physics.
5. Confirm release tails decay naturally and repeated boost does not restart or
   pop the whole effect.
6. Inspect exterior chase and cockpit cameras at gameplay FOV with Bloom enabled.
