# Farion Audio Architecture

Farion audio is a presentation layer. It reads gameplay telemetry and audio
authoring state, but it must not own flight physics, possession, damage,
survival, UI flow, or scene routing.

## Runtime Ownership

- `SceneAudioPlayer`
  - Plays simple Unity-authored scene loops such as main-menu music and ambient
    beds.
  - Lives on a scene GameObject, usually `Audio` or `MainMenuAudio`.
  - Clips and mixer groups are assigned in the Inspector.
- `ShipAudioTelemetryProvider`
  - Reads `SpacecraftMotor.Telemetry`, `SpacecraftRig`, `CelestialActorProbe`,
    `SpacecraftSurfaceContactProbe`, `SpacecraftOceanInteractor`, and
    `PlayerPossessionController`.
  - Converts gameplay state into one `ShipAudioTelemetry` contract.
  - Does not read keyboard input and does not apply physics.
- `ShipAudioController`
  - Owns the spacecraft FMOD event instance.
  - Pushes telemetry into FMOD parameters every frame.
  - Starts, attaches, stops, and releases the FMOD instance.
  - Does not play Unity `AudioSource` engine loops.

## Folder Layout

```text
Assets/Project/Audio/Runtime
|-- Common
|   |-- AudioClipSet.cs
|   |-- AudioLevelUtility.cs
|   `-- SceneAudioPlayer.cs
`-- Spacecraft
    |-- ShipAudioController.cs
    |-- ShipAudioTelemetry.cs
    |-- ShipAudioTelemetryProvider.cs
    |-- SpacecraftAudioPerspective.cs
    `-- SpacecraftAudioTuningProfile.cs
```

Author audio data under:

```text
Assets/Project/Art/Audio
|-- Mixers
|-- Music
|-- Ambience
`-- Spacecraft
```

FMOD Studio project and built banks live under:

```text
FMODProject/FarionAudio/FarionAudio
```

## Main Menu Setup

1. Import the main-menu `.wav` under `Assets/Project/Art/Audio/Music`.
2. Create/select a `MainMenuAudio` GameObject in the main-menu scene.
3. Add `SceneAudioPlayer`.
4. Add one loop entry:
   - `Label`: `MainMenuMusic`
   - `Clip`: your main-menu WAV
   - `Spatial`: disabled
   - `Volume`: start around `0.55`
   - `Fade In Seconds`: `1.5`
   - `Fade Out Seconds`: `0.8`
5. Leave `Log Playback` disabled after confirming the music plays.

## Gameplay Ambience Setup

1. Import ambient `.wav` files under `Assets/Project/Art/Audio/Ambience`.
2. In the gameplay scene, create/select an `Audio` GameObject.
3. Add `SceneAudioPlayer`.
4. Add one or more loop entries:
   - Space bed: 2D, low volume, long fade.
   - Planet atmosphere bed: 2D or wide 3D, lower volume until atmosphere logic
     drives it.

## Spacecraft FMOD Contract

`PF_PlayerStarterShuttle` owns spacecraft audio through root components only:

```text
PF_PlayerStarterShuttle
|-- ShipAudioTelemetryProvider
`-- ShipAudioController
```

The controller event must be:

```text
event:/Ships/StarterShuttle/Engine
```

Required FMOD parameters:

```text
Rpm          0..100
Load         0..1
Boost        0..1
Roll        -1..1
EngineState  0=Off, 1=Startup, 2=Running, 3=Shutdown
Perspective  0=Exterior, 1=Cockpit/ShipInterior
```

Optional telemetry parameters can be added to the same event later:

```text
HullStress       0..1
Impact           0..1
Atmosphere       0..1
WaterSubmersion  0..1
```

Their controller fields intentionally remain empty until the matching FMOD
parameters exist. After authoring and rebuilding banks, enter the exact names
on `ShipAudioController`; empty names are skipped without warnings.

Unity drives these values from produced motion and telemetry, not raw input:

- `Rpm`: slow-spooled blend of body-relative speed, main thruster activity,
  engine load, and boost. It is not a raw throttle key value.
- `Load`: smoothed `ShipAudioTelemetry.EngineLoad`; use it for pressure,
  strain, filtering, and layer weight, not for muting the whole event.
- `Boost`: smoothed `ShipAudioTelemetry.Boost`; use it for a separate boost
  layer/envelope.
- `Roll`: `SpacecraftMotor.LastLocalRotationInput.z`, smoothed.
- `EngineState`: startup for the configured startup time, then running;
  shutdown when the controller stops.
- `Perspective`: `0` for exterior/on-foot exterior listening and `1` for
  cockpit or ship-interior listening. Use it for filtering and mechanical
  transmission, not as a second engine-state control.

## FMOD Authoring Rules

The engine event should be authored as a continuous engine system:

- Startup transient plays when `EngineState == 1`.
- Running bed/loops continue while `EngineState == 2`.
- Shutdown transient plays when `EngineState == 3`.
- `Rpm` and `Load` must audibly change pitch, filter, volume, or layer blend.
- `Boost` should add a separate layer or transition, not just raise volume.
- `Roll` can add lateral thruster texture, width, pan, or mechanical strain.
- Keep a quiet idle/running bed audible while `EngineState == 2`. Do not let
  `Rpm`, `Load`, `Boost`, or `Roll` automation pull the whole event to silence.
- `Z` is the flight-assist toggle on the current keyboard input. It must not
  trigger shutdown or stop the running loop in FMOD.

For a ship engine, avoid making the event a simple one-shot. If the Unity FMOD
cache reports the event as one-shot, fix the event timeline/loop/sustain logic
in FMOD Studio, build banks, then refresh banks in Unity.

## Scene Requirements

- Exactly one active `FMOD Studio Listener` should exist on the active gameplay
  camera.
- Do not add `FMOD Studio Event Emitter` for the player ship engine. The ship
  root `ShipAudioController` creates and controls the event instance.
- Do not add Unity `AudioSource` engine loops to the starter shuttle while FMOD
  owns spacecraft audio.

## Rule

Spacecraft audio must follow `SpacecraftMotor.Telemetry`, not raw input. If the
pilot presses a key but the ship cannot produce thrust because of future power,
damage, mass, or environment constraints, the engine sound should reflect the
actual produced thrust/load.
