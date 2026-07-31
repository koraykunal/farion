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

The spacecraft engine event is:

```text
event:/Ships/StarterShuttle/Engine
```

Keep the event name `Engine`; speed is a parameter of the engine event, not
part of its event name.

Required parameters on the continuous engine event:

```text
Speed  0..100, initial 0
Roll   -1..1,  initial 0
Boost  0..1,   initial 0
```

`Speed` is body-relative ship speed normalized by the Unity audio profile.
`Roll` is signed pilot roll demand. `Boost` is the produced boost blend and
should drive a continuous layer, filter, or intensity change.

Boost start and stop transients are separate 3D one-shot events:

```text
event:/Ships/StarterShuttle/BoostIgnition
event:/Ships/StarterShuttle/BoostShutdown
```

This keeps one-shot triggering deterministic. No `BoostState` parameter is
required: Unity detects the actual boost-active rising and falling edges and
plays each event once.

Optional parameters can be added to the continuous engine event later:

```text
Load             0..1
Perspective      0=Exterior, 1=Cockpit/ShipInterior
HullStress       0..1
Impact           0..1
Atmosphere       0..1
WaterSubmersion  0..1
```

Their controller fields intentionally remain empty until the matching FMOD
parameters exist. After authoring and rebuilding banks, enter the exact names
on `ShipAudioController`; empty names are skipped without warnings.

Unity drives these values from produced motion and telemetry, not raw input:

- `Speed`: body-relative speed normalized to `0..100`.
- `Load`: smoothed `ShipAudioTelemetry.EngineLoad`; use it for pressure,
  strain, filtering, and layer weight, not for muting the whole event.
- `Boost`: smoothed `ShipAudioTelemetry.Boost`; use it for a separate boost
  layer/envelope.
- `Roll`: `SpacecraftMotor.LastLocalRotationInput.z`, smoothed.
- `Perspective`: `0` for exterior/on-foot exterior listening and `1` for
  cockpit or ship-interior listening. Use it for filtering and mechanical
  transmission.

## Boost Authoring Setup

1. Open `Engine`.
2. Keep the existing `Speed` and `Roll` authoring.
3. Set the `Roll` parameter initial value to `0`, not `-1`.
4. Add a continuous `Boost` parameter with minimum `0`, maximum `1`, and
   initial value `0`.
5. Use `Boost` to automate a restrained engine change:
   - `0`: normal engine.
   - `0.2`: boost layer begins to become audible.
   - `1`: full boost layer/intensity.
6. Do not place the ignition or shutdown WAV files on the continuous `Boost`
   parameter sheet.
7. Create a new event named `BoostIgnition`.
8. Drop `BoostIgnition.wav` on its timeline at `0:00`; leave the instrument as
   a one-shot and do not add a loop region.
9. Create a new event named `BoostShutdown`.
10. Drop `Shutdown.wav` on its timeline at `0:00`; leave it as a one-shot and
    do not add a loop region.
11. Make `Engine`, `BoostIgnition`, and `BoostShutdown` 3D events. Start
    with a minimum distance around `5 m` and maximum distance around `200 m`,
    then tune in Play Mode.
12. Assign all three events to `Master` and build the Desktop banks.
13. Return to Unity and allow the FMOD bank refresh.
14. On `PF_PlayerStarterShuttle > ShipAudioController`, assign:
    - `Engine Event`: `event:/Ships/StarterShuttle/Engine`
    - `Boost Ignition Event`: `event:/Ships/StarterShuttle/BoostIgnition`
    - `Boost Shutdown Event`: `event:/Ships/StarterShuttle/BoostShutdown`
15. Keep the parameter fields exactly:
    - `Speed Parameter`: `Speed`
    - `Boost Parameter`: `Boost`
    - `Roll Parameter`: `Roll`
    - optional fields empty until those parameters exist.

## FMOD Authoring Rules

The engine event should be authored as a continuous engine system:

- Running beds/loops remain continuous for the event lifetime.
- `Speed` must audibly change pitch, filter, volume, or layer blend.
- `Boost` should add a separate layer or transition, not just raise volume.
- `Roll` can add lateral thruster texture, width, pan, or mechanical strain.
- Keep a quiet idle/running bed audible at `Speed == 0`. Do not let `Load`,
  `Boost`, or `Roll` automation pull the whole event to silence.
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
