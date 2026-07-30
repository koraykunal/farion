# Starter Shuttle Thruster VFX

The starter shuttle owns a layered authored thruster presentation. Flight
physics publishes telemetry; VFX consumes it and never feeds forces, input, or
audio state back into simulation.

## Runtime Contract

`SpacecraftThrusterVfxController` builds one
`SpacecraftThrusterVfxFrame` from:

- `SpacecraftMotor.Telemetry`;
- `CelestialActorProbe.CurrentSample` for atmosphere density;
- `SpacecraftOceanInteractor.CurrentInteraction` for underwater suppression.

The frame contains:

```text
Throttle 0..1
Boost 0..1
Heat 0..1
AtmosphereDensity 0..1
RelativeSpeed >= 0
LocalTranslation -1..1
LocalRotation -1..1
LocalLinearAcceleration
LocalAngularAcceleration
ThrusterCommand
```

`SpacecraftThrusterNozzleVfx` consumes the same frame for mesh layers, VFX
Graphs, steering, and local light. FMOD spacecraft audio remains owned by
`Assets/Project/Audio/Runtime`.

## Prefab Contract

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

Both nozzle roots sit at their engine exits. Direct children use zero local
position, and visual content emits along nozzle-local positive Z. The VFX owner
contains one controller with both nozzle references serialized explicitly.
Hierarchy lookup is a defensive recovery path, not primary composition.

## Layer Responsibilities

- `CoreGlow`: stable HDR engine-mouth emission and low idle visibility.
- `InnerCone`: compact primary plasma volume.
- `OuterPlasma`: softer volume shell.
- `ShockDiamonds`: restrained boost-sensitive compression pattern.
- `Distortion`: load, boost, and heat-driven refraction.
- `VFX_Sparks`: boost accent.
- `VFX_Smoke`: atmosphere-only exhaust, suppressed underwater.
- `LT_Thruster`: shadowless local hull illumination.

Rear plume load follows forward thrust, boost, forward acceleration, and a
restrained angular-stabilization floor. Reverse, strafe, and vertical movement
do not present as full forward thrust.

Optional VFX Graph properties include telemetry values plus:

```text
Rate
Count
Speed
Lifetime
Size
Alpha
```

Missing optional properties are ignored. Spawn rate reaches zero before a graph
receives `Stop`, allowing existing particles to decay without popping.

## Authoring

Run `Farion > VFX > Rebuild Starter Shuttle Thrusters` only after replacing the
starter-shuttle model, changing nozzle hierarchy, or recreating authored VFX
materials. The command is idempotent and edits
`PF_PlayerStarterShuttle.prefab`; it is not a runtime builder.

`FarionProjectValidation` checks both nozzle signs, all five mesh layers, their
renderers and materials, the sparks and smoke graphs, local lights, and explicit
controller nozzle references.

## Manual Acceptance

1. Open `SC_PhysicsSandbox`.
2. Confirm both effects originate from their own nozzle and point aft.
3. Check idle, cruise, boost, throttle release, yaw, roll, atmosphere, and
   underwater behavior.
4. Confirm mesh layers, particles, light, and FMOD follow flight telemetry
   without changing flight physics.
5. Confirm release tails decay naturally and repeated boost does not restart
   the entire effect.
6. Inspect cockpit and chase cameras at gameplay FOV with Bloom enabled.
