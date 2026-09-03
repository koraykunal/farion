# Farion Audio Architecture

FMOD is the only production audio pipeline. Audio consumes gameplay and UI
state; it never owns flight physics, possession, UI navigation, or scene flow.

## Ownership

- `AudioDirector` is the persistent game-mix owner. It starts menu music,
  gameplay music, and world ambience, writes global FMOD parameters, owns bus
  volumes, follows the active camera as the sole FMOD listener, and dispatches
  UI cues.
- `AudioSceneContext` is the scene adapter. Main menu publishes
  `GameContext=0`; gameplay publishes `GameContext=1`, `Atmosphere`, and
  `Interior` from spacecraft telemetry.
- `ShipAudioTelemetryProvider` converts produced spacecraft motion and
  environment state into `ShipAudioTelemetry`.
- `ShipAudioController` owns the spacecraft FMOD instances and pushes engine
  parameters. Flight code remains untouched.
- `ExplorerAudioController` subscribes to `ExplorerLocomotionSignals` on the
  explorer prefab and plays footstep, jump, and landing one-shots with surface
  and intensity parameters. Locomotion code remains untouched.
- `UiAudioFeedback` translates Selectable focus and activation into FMOD UI
  cues. `UiAudioBridge` handles screen-back, pause, success, and error state.

```text
Gameplay/UI state
      |
      +--> AudioSceneContext ----> AudioDirector ----> FMOD globals/buses
      |
      +--> ShipAudioTelemetryProvider --> ShipAudioController --> ship events
      |
      `--> UiAudioFeedback/Bridge ------> AudioDirector --------> UI events
```

## Repository Layout

```text
Assets/Project/Audio/Runtime
|-- Character
|   `-- ExplorerAudioController.cs
|-- System
|   |-- AudioDirector.cs
|   `-- AudioSceneContext.cs
`-- Spacecraft
    |-- ShipAudioController.cs
    |-- ShipAudioTelemetry.cs
    |-- ShipAudioTelemetryProvider.cs
    |-- SpacecraftAudioPerspective.cs
    `-- SpacecraftAudioTuningProfile.cs

Assets/Project/Prefabs/Audio/PF_AudioDirector.prefab
FMODProject/FarionAudio/FarionAudio.fspro
FMODProject/FarionAudio/Assets
FMODProject/FarionAudio/Build/Desktop
FMODProject/Scripts/FarionCharacterEvents.js
Assets/StreamingAssets
```

`FarionCharacterEvents.js` authors `event:/Character/*` headlessly from
`Assets/Character`; it lives outside `FarionAudio/Scripts` on purpose because
Studio auto-runs anything in that folder on project load. Run from
`FMODProject/`, then build and copy the banks:

```text
fmodstudiocl -script Scripts/FarionCharacterEvents.js FarionAudio/FarionAudio.fspro
fmodstudiocl -build -platforms Desktop FarionAudio/FarionAudio.fspro
copy FarionAudio\Build\Desktop\*.bank ..\Assets\StreamingAssets\
```

Footstep media per `Surface` label: Rock and Ice share concrete, Regolith is
gravel, Soil is dirt, Metal is wood until a real deck recording exists.

Unity `AudioSource`, `AudioMixer`, and duplicate audio media under
`Assets/Project/Art/Audio` are intentionally absent. Author and build all
production sound in FMOD Studio.

## FMOD Contract

Global parameters:

| Parameter | Range | Producer | Meaning |
| --- | --- | --- | --- |
| `GameContext` | `0..1` discrete | `AudioSceneContext` | `0` menu, `1` gameplay |
| `Atmosphere` | `0..1` | spacecraft telemetry | local atmosphere density |
| `Interior` | `0..1` | possession perspective | exterior-to-interior blend |
| `Paused` | `0..1` discrete | UI screen router | pause-menu mix state |

Buses:

```text
bus:/
|-- Music
|-- Ambience
|-- SFX
`-- UI
```

Persistent events:

```text
event:/Music/AdaptiveScore
event:/Music/SpaceAmbient
event:/Ambience/World
```

UI one-shots:

```text
event:/UI/Focus
event:/UI/Confirm
event:/UI/Back
event:/UI/Unavailable
event:/UI/Success
event:/UI/Error
```

Spacecraft events remain:

```text
event:/Ships/StarterShuttle/Engine
event:/Ships/StarterShuttle/BoostIgnition
event:/Ships/StarterShuttle/BoostShutdown
```

`Engine`, ignition, and shutdown route to `bus:/SFX`. The engine event keeps
its existing `Speed`, `Roll`, `Boost`, `Load`, `Perspective`, and state
contract. New audio logic must read produced telemetry, never raw input.

Explorer one-shots (3D, routed to `bus:/SFX`):

| Event | Parameters | Meaning |
| --- | --- | --- |
| `event:/Character/Footstep` | `Surface` labeled `0..4`, `Intensity` `0..1`, `Wetness` `0..1` | one step; `Intensity` scales with surface speed |
| `event:/Character/Land` | `Surface` labeled `0..4`, `Intensity` `0..1` | grounding after a real fall; `1` is a hard impact |
| `event:/Character/Jump` | none | short suit/effort cue on jump launch |

`Surface` labels: `0` Rock, `1` Regolith, `2` Soil, `3` Ice, `4` Metal.
`ExplorerAudioController` stays silent and logs one warning per missing event,
so code can ship before the events are authored.

## Authoring Rules

1. Put source WAV files under the relevant FMOD `Assets` subfolder.
2. Keep one event per semantic action; variation belongs inside that event.
3. `AudioDirector` keeps menu and gameplay music mutually exclusive and fades
   in the selected context. Future gameplay-phase layering belongs inside
   `SpaceAmbient` and is driven by semantic FMOD parameters, not clip logic in
   Unity.
4. Keep UI events 2D one-shots routed to `UI`. Use restrained variation inside
   FMOD instead of adding more Unity cue code.
5. Keep spacecraft events 3D and driven by telemetry.
6. Assign every production event to `Master`, build Desktop banks, and copy the
   generated `Master.bank` and `Master.strings.bank` to
   `Assets/StreamingAssets`.

`AdaptiveScore` contains the main-menu music. `SpaceAmbient` layers the `Bass`
and `Synth` stems in one 59-second streaming loop and is audible only during
gameplay. `World` and UI events remain routing slots until their final content
is added in FMOD Studio.

## Scene Contract

Every enabled build scene contains exactly:

- one `PF_AudioDirector` prefab instance;
- one `AudioSceneContext` configured for that scene;
- one active `FMOD Studio Listener`, owned by the persistent audio director and
  following the scene's `MainCamera`;
- zero Unity `AudioListener` and `AudioSource` components.

`AudioDirector` survives scene loads and destroys duplicate scene instances.
The settings screen writes normalized Master, Music, Ambience, SFX, and UI bus
volumes to `PlayerPrefs` and applies a squared perceptual volume curve.
