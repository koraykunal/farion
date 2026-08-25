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
GroundProximity 0..1
```

`GroundProximity` comes from `CelestialFrameSample.SurfaceAltitude` normalized
against `groundEffectAltitude`, and is zero without a probe sample.

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

Both nozzle roots sit at their engine exits and are mirrored about the hull
centreline, so the two plumes read at the same height and depth from behind.
Direct children use zero local position, and visual content emits along
nozzle-local positive Z. The VFX owner contains one controller with both nozzle
references serialized explicitly. Hierarchy lookup is a defensive recovery path,
not primary composition.

## Layer Responsibilities

- `CoreGlow`: stable HDR engine-mouth emission and low idle visibility. Drawn as
  a camera-facing billboard, so it never collapses to a line in side views.
- `InnerCone`: compact primary plasma volume.
- `OuterPlasma`: softer volume shell.
- `ShockDiamonds`: standing compression nodes, gated by ambient pressure.
- `Distortion`: load, boost, and heat-driven refraction.
- `VFX_Sparks`: boost accent.
- `VFX_Smoke`: atmospheric exhaust plus ground-proximity dust, suppressed
  underwater.
- `LT_Thruster`: shadowless local point light. Its colour follows the plume
  through idle, boost, and overheat, and its range grows in vacuum where the
  plume runs longer.

Mach diamonds are a standing wave: their spacing widens downstream, their
amplitude damps, and they do not scroll. They disappear in vacuum because the
nozzle-load feed multiplies by `AtmosphereDensity`. Vacuum instead widens the
plume through `_BellExpansion` and lengthens it through `vacuumLengthScale`.
Ambient pressure also damps the tail: at sea level the plume dims downstream,
in vacuum it stays bright over its whole length.

Plume colour cools along the axis. `_ThroatTint` and `_TailTint` multiply the
throttle/boost/heat colour through `exp(-axial * _CoolingRate)`, so the nozzle
mouth reads hotter and whiter than the tail.

## Plume Deflection

Each nozzle tracks a lagged world rotation and hands the mesh layers a
nozzle-local bend vector, so the exhaust column trails behind hard rotation
instead of snapping with the hull. `plumeBendResponse` sets how fast it catches
up; per-layer `plumeBendGain` scales how far the tail swings.

`GroundProximity` also flares the tail radially and shortens the column through
each layer's `groundSplashGain`, so hovering close to a surface spreads the
plume instead of driving it through the ground.

## Render Pipeline Requirements

- Plume layers read scene depth for soft intersection. `softFadeDistance` on
  each mesh layer is forced to zero when the active render pipeline asset has
  no `m_RequireDepthTexture`.
- `Distortion` samples the opaque texture and draws at `Transparent-10`, ahead
  of the plasma layers, so the plasma composites over the refracted scene rather
  than being overwritten by it. The layer disables itself entirely when the
  active render pipeline asset has no `m_RequireOpaqueTexture` — which is the
  case for `Mobile_RPAsset`.
- Distortion strength scales with `_DistortionReferenceDistance / viewDistance`,
  so a plume that covers a few pixels does not warp a screen-sized area.

## VFX Graph Contract

`heatGlowRenderers` on each nozzle is optional and unassigned by default. Point
it at the engine-bell renderers of the shuttle model to drive `_EmissionColor`
from thermal soak; leaving it empty simply skips the effect.

Rear plume load follows forward thrust, boost, forward acceleration, and a
restrained angular-stabilization floor. Reverse, strafe, and vertical movement
do not present as full forward thrust.

Optional VFX Graph properties include the telemetry values plus:

```text
Rate
Speed
```

Missing optional properties are ignored. `ThrusterGraphTuning` only exposes what
the authored graphs actually consume; `lifetime` is not published to the graph
and exists solely to hold a stopped effect alive while its tail decays, so it
must match the graph particle lifetime. Spawn rate reaches zero before a graph
receives `Stop`, allowing existing particles to decay without popping.

Graph capacity has to cover peak spawn rate times lifetime. Ground-effect dust
multiplies the smoke rate, so `VFX_Thruster_AtmosphereSmoke` runs a larger
capacity than its cruise rate alone would suggest.

## Authoring

The nozzle hierarchy, materials, graph references, and local lights are authored
directly in `PF_PlayerStarterShuttle.prefab`. If the shuttle model or nozzle
hierarchy changes, update that prefab in Prefab Mode; no runtime or Editor
builder owns the effect.

`FarionProjectValidation` checks both nozzle signs, all five mesh layers, their
renderers and materials, the sparks and smoke graphs, local lights, and explicit
controller nozzle references.

## Manual Acceptance

1. Open `SC_GameplayShell`.
2. Confirm both effects originate from their own nozzle and point aft.
3. Check idle, cruise, boost, throttle release, yaw, roll, atmosphere, and
   underwater behavior.
4. Confirm mesh layers, particles, light, and FMOD follow flight telemetry
   without changing flight physics.
5. Confirm release tails decay naturally and repeated boost does not restart
   the entire effect.
6. Inspect cockpit and chase cameras at gameplay FOV with Bloom enabled.
7. Confirm diamonds stand still in atmosphere and vanish in orbit, the plume
   flares briefly on ignition, and it does not cut a hard line into terrain on
   final approach.
8. Confirm the core glow stays visible when the chase camera passes the nozzle
   side-on, and that the tail lags behind a hard yaw instead of snapping.
9. Switch to `Mobile_RPAsset` and confirm the distortion layer disappears
   instead of rendering a solid silhouette.
