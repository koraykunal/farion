# Farion UI Architecture

## Status

This document defines the production UI foundation. `PRODUCT.md` owns product
intent and `DESIGN.md` owns visual rules. This document owns runtime boundaries,
folder responsibilities, naming, scene composition, and the migration path.

The foundation supports one local UI focus owner, keyboard and mouse, Xbox and
PlayStation style gamepads, and English-first content with Turkish
localization. Online co-op players do not receive independent local UI roots
unless split-screen becomes an explicit product requirement.

## Ownership

UI is a presentation and intent-submission layer.

```text
InputSystemUIInputModule
  -> UiInputModuleBinder
  -> FarionInputActions UI map
  -> Unity Selectable / screen presenter
  -> application or gameplay command
  -> authoritative result
  -> presenter / UiFeedbackService
```

- `FarionInputActions` owns runtime input actions and saved binding overrides.
- `UiInputModuleBinder` binds the EventSystem to those same actions.
- `UiScreenRouter` owns screen visibility, modal history, cancel behavior, and
  focus restoration.
- `UiFocusController` is the only component that changes EventSystem selection
  as part of navigation.
- `UiInputDeviceService` owns the active prompt device.
- `UiCompositionScope` resolves only components owned by the same nearest
  Canvas. It is the single discovery rule for sibling system prefabs.
- `UiTheme` owns shared presentation tokens. It does not own layout.
- `UiPreferencesService` owns persisted locale, UI scale, reduced motion,
  subtitle visibility, and subtitle size. It applies scene-level presentation
  changes and exposes one stable preferences contract to feature presenters.
- `UiSystemRoot` remains the scene composition boundary. Visual components
  consume reduced motion through the root contract rather than reading
  `PlayerPrefs`.
- `UiFeedbackService` owns queued non-blocking result messages.
- `UiConfirmationDialog` owns destructive-action confirmation presentation.
- Feature presenters translate domain state into view state and submit intent.
- Application, gameplay, persistence, and networking assemblies own outcomes.

No UI presenter writes save data, inventory state, ship state, or session state
directly.

## Runtime Structure

```text
Assets/Project/UI/Runtime/
  Common/       Reusable visual behaviours and component views
  Feedback/     Toasts, alerts, confirmations, blocking feedback
  Foundation/   Stable UI identity and scene composition root
  Gameplay/     Gameplay-specific presenters and migration adapters
  Input/        EventSystem binding, active-device detection, prompt display
  Localization/ Localized-string access and reusable localized text binding
  MainMenu/     Main-menu presenters and migration adapters
  Navigation/   Screen registry, stack, visibility, focus restoration
  SaveLoad/     Save-slot presentation, selection, and player-facing actions
  Settings/     Persistent UI preferences and the Settings screen presenter
  Styling/      Theme ScriptableObjects and shared presentation tokens
```

Authored reusable UI prefabs live under:

```text
Assets/Project/Prefabs/UI/
  Foundation/   System root, confirmation, loading, and feedback prefabs
  Screens/      One prefab per routed screen
  Widgets/      Reusable leaf widgets: buttons, dropdowns, slots, tooltips
  Hud/          Gameplay heads-up displays
```

Theme assets live under `Assets/Project/Design/UI/Styling/`. Runtime-ready UI art and fonts live under
`Assets/Project/Art/Icons` and `Assets/Project/Art/Textures/UI` according to
`AssetIntake.md`. English and Turkish
string tables live under `Assets/Project/Localization/`, outside runtime scripts.

## Naming

- Every runtime type in `Farion.UI` carries the `Ui` prefix, with no
  exception: `UiScreenRouter`, `UiTheme`, `UiMainMenuController`,
  `UiSpacecraftFlightHudPresenter`. The rule is mechanical so a reviewer can
  check it without reading the type.
- `Controller` coordinates user intent and application flow.
- `Presenter` maps state into an authored view.
- `View` owns component-level visual state and no domain authority.
- `Service` is a shared scene-level capability with explicit ownership.
- `Router` owns navigation history and screen transitions.
- Prefabs use `PF_UI_<Role>`; ScriptableObjects use `SO_<Subject><Type>`.
  The asset-side token is `UI`, the C# token is `Ui`.
- Scene names use `SC_<Purpose>` for new gameplay/test scenes. Existing
  production scene names change only in a dedicated scene-catalog migration.

Do not introduce new `Manager`, `Handler`, `Helper`, or `System` type names when
one of the roles above describes the responsibility.

## Screen Contract

Every routed screen has exactly one `UiScreenView`:

- A unique `UiScreenId`.
- A layer: `Hud`, `Screen`, `Modal`, or `System`.
- An explicit first selectable control when the screen is interactive.
- Explicit cancel and gameplay-input-lock behavior.
- A `CanvasGroup`; `UiPanelFader` is optional.

Layer behavior:

- `Hud` remains outside modal history and never owns navigation focus.
- `Screen` is a full feature surface such as Pause or Inventory. Opening a new
  screen hides the previous screen but preserves it in history.
- `Modal` sits above a screen and returns focus to its caller when closed.
- `System` is reserved for blocking loading and fatal-error surfaces.

Escape / gamepad East is consumed by `UiScreenRouter` before feature
controllers inspect the Pause binding. It closes only the top screen when
`Close On Cancel` is enabled, and the gameplay controller then synchronizes its
compatibility state to the router. Destructive actions never execute when their
confirmation surface is missing.

Runtime-authored interactive lists must assign their first available control to
`UiScreenView` after building. `UiGameplayMenuListPresenter` owns that contract
for the current Pause menu. Authored controls continue to serialize their
initial selection directly.

### Motion contract

Routed operational screens use `UiPanelFader` for one restrained transition
language: a 160 ms ease-out entrance, a 100 ms exit, and a 24-pixel horizontal
offset. The component animates only opacity and anchored position, runs on
unscaled time, and resolves timing plus offset from `UiTheme`.

Wide full-screen surfaces keep their backdrop on the screen root and assign an
inner authored `MotionRoot` to the fader. This lets the backdrop crossfade while
the readable console content settles into place without exposing a screen edge.
Safe-area fitting remains outside that motion root so layout authority and
transition authority never write the same transform. Reduced Motion converts
the same navigation result to an immediate state change.

System loading is the deliberate exception to positional motion. Its full-screen
surface uses the same timing and reduced-motion contract but fades only, keeping
scene transition feedback visually stable while assets are deserialized.

## Scene Composition

Each interactive UI Canvas owns exactly one authored composition:

```text
MainCanvas / GameplayCanvas
  UI_SystemRoot             service-only prefab
  UI_ConfirmationDialog     sibling prefab
  UI_LoadingOverlay         sibling prefab
  UI_FeedbackOverlay        sibling prefab
  UI_SettingsScreen         shared feature-screen prefab
  UI_SaveLoadScreen         shared feature-screen prefab
  Feature screens           authored scene objects or prefab instances
```

`UI_SystemRoot` contains only:

1. `UiSystemRoot`
2. `UiFocusController`
3. `UiScreenRouter`
4. `UiInputDeviceService`
5. `UiPreferencesService`

Confirmation, loading, and feedback are direct Canvas children and siblings of
the root. They must never be nested into `UI_SystemRoot`. This separation keeps
the service prefab importer-safe and prevents scene instances from depending on
nested prefab asset serialization.

Gameplay scenes also place `PlayerControlLock` on this root or explicitly
assign it to the router. Each scene has one EventSystem with:

1. `EventSystem`
2. `InputSystemUIInputModule`
3. `UiInputModuleBinder`

The root references `SO_DefaultUiTheme`. Scene controllers receive the root
explicitly and resolve its services through that one contract.
`UiCompositionScope` finds feedback, confirmation, loading, and feature screens
under the same nearest Canvas while excluding nested Canvas scopes. The router
merges its scene-assigned feature screens with the screens discovered in that
Canvas composition.

The root is scene-scoped; it is not spawned implicitly and is not made
`DontDestroyOnLoad`.

Cross-prefab service references are not serialized into `UI_SystemRoot`.
Discovery occurs within the Canvas scope at runtime, and the project validator
ensures exactly one required sibling of each system role exists.

### Authored Loading boundary

`UI_LoadingOverlay.prefab` is the shared system-layer transition surface in
MainMenu and gameplay compositions. It owns the opaque backdrop, structural
frame, localized operation copy, actual scene-load progress bar, and input
blocking. It remains a direct Canvas sibling of `UI_SystemRoot`.

`UiLoadingOverlayPresenter` accepts a scene-load operation factory from a
feature controller, opens the system screen for one rendered frame, then maps
Unity's `0..0.9` asynchronous load range to a player-facing `0..100%` value.
It does not choose destinations, save startup modes, or scene names.
`UiMainMenuController` raises `SoloHostRequested` or `CoopHostRequested` and
shows the overlay; `UiGameplayController` chooses the return-to-menu
presentation; `MultiplayerSessionController` remains the scene-loading authority.

Load failure closes the system layer and returns control to the existing screen
stack with visible error feedback. Loading never uses fake tips, decorative
spinners, or an artificial minimum delay.

### Authored Settings boundary

`UI_SettingsScreen.prefab` is the only visual source of truth for the Settings
screen. It is edited in Prefab Mode and is never regenerated from an Editor
menu or runtime builder.

- `UiSettingsScreenPresenter` binds localization and `UiPreferencesService`
  state to the authored views.
- `UiSettingsCategoryView` owns category focus and selection presentation.
- `UiSettingsOptionView` owns one adjustable row and reports player intent.
  Audio rows use its native uGUI `Slider` range contract; keyboard and gamepad
  left/right input keep a ten-percent step while pointer drag remains continuous.
- `UiPreferencesService` remains the authority for persisted values and locale
  changes.

The Controls category exposes keyboard rebinding through `UiKeyBindingsPanel`.
It supports capture, cancel, conflict rejection, persistence, and full reset.
Gamepad, mouse-button, secondary-slot, and swap-on-conflict authoring are not
part of the current contract and must not be presented as available controls.

The prefab keeps category navigation, option groups, context copy, and the back
action as authored references. Scenes only hold standard prefab-instance
overrides such as transform and object name. Its panels and focus frames reuse
`UI_MenuButtonPanel` and `UI_MenuButtonFrame`, the same sliced visual assets as
the Main Menu, so screen density can change without inventing a second frame
language. Its root `CanvasGroup` crossfades while the authored content below
`SafeArea/MotionRoot` uses the shared horizontal screen transition.

### Authored Save/Load boundary

`UI_SaveLoadScreen.prefab` is the shared save browser used by MainMenu and
gameplay. It remains a direct Canvas sibling and contains four authored
`UI_SaveSlot` instances: the backwards-compatible primary slot and three
manual slots.

- `SaveGameSlotCatalog` owns the player-facing slot identifiers.
- `SaveGameSlotService` reads factual schema, timestamp, backup, and integrity
  state without depending on gameplay types.
- `UiSaveLoadScreenPresenter` owns selection, localized copy, overwrite/delete
  confirmation, and action availability.
- `UiMainMenuController` records the startup request and asks the session
  bridge to host.
- `UiGameplayController` receives the save callback from
  `MultiplayerSessionController` and opens the same slot screen for it.

Continue resolves the newest compatible record. Unsupported or damaged records
remain visible for diagnosis and deletion but cannot be loaded. The screen does
not invent screenshots, playtime, ship names, or other metadata absent from the
save contract. Delete is deliberately scoped to the selected primary, backup,
and temporary files.

### Authored Pause boundary

`UI_PauseMenuScreen.prefab` owns the Pause screen composition: its near-opaque
left panel, structural frame, localized title, and the separate gameplay-action
and session-action mounts. The shell follows the Main Menu's 480-pixel authored
column proportion instead of stretching a generic full-screen menu.

`UiGameplayMenuListPresenter` remains a narrow binding adapter. It reads the
prefab-configured action entries, creates the shared `UI_MenuButton` views in
the authored mounts, assigns the first available selection, and submits intent
to `UiGameplayController`. It does not decide gameplay or persistence outcomes.
Unavailable or unfinished features are omitted instead of appearing as dead
production controls. Exit-to-menu and quit actions are placed in the lower
session group so destructive navigation is visually separated from normal
play actions.

### Authored Inventory boundary

`UI_InventoryScreen.prefab` owns the Inventory shell: structural frame, compact
capacity header, two-column slot region, and selected-item detail panel.
`UI_InventorySlot.prefab` owns one focusable slot row and its pointer, keyboard,
and gamepad focus presentation.

`UiInventoryPanelPresenter` is the binding boundary. It may instantiate enough
slot prefab instances to match the authoritative container capacity because
inventory slots are a dynamic collection, but it never creates the surrounding
screen composition. It binds `InventoryContainerComponent.Stacks`, assigns the
first available slot to `UiScreenView`, and updates the detail panel when focus
moves. Slot and screen chrome use the shared English and Turkish string table.

Item names, categories, forms, and research domains currently come from the
gameplay definitions. The UI does not invent icons or descriptive copy that the
domain does not provide. Definition-backed names and interaction prompts are
not yet localized; that requires a structured localization identity at the
gameplay-to-presentation boundary rather than translating fallback strings in
individual presenters. The presenter remains the domain-to-prefab adapter.

## Validation Contract

`Farion/Validation/Validate Project` runs the UI checks as part of the complete
project validation. `Farion/Validation/Validate UI Foundation` runs only the UI
contract.

The validator blocks a build when:

- A required foundation prefab is missing or contains a missing script.
- `UI_SystemRoot` contains child views instead of remaining service-only.
- A scene composition is missing its confirmation, loading, or feedback
  sibling prefab.
- A required system prefab is unpacked, replaced, or nested below the root.
- A Canvas has duplicate screen IDs or an invalid scaling contract.
- An authored interactive screen has controls but no initial focus provider.
- A scene has an invalid EventSystem or input-module binding.
- Feedback severity labels are disabled and state would depend on color alone.
- A selection frame uses anything other than the shared menu-frame sprite.
- A settings category or option identity is duplicated, or an enum-defined
  settings contract entry is missing from the authored Settings prefab.
- An authored UI prefab or build-scene composition references the retired
  Liberation Sans asset.
- Theme text and interactive-surface combinations fall below WCAG AA contrast.
- Full-screen HUD/loading content lacks Safe Area fitting, or a fixed screen
  leaves the 32-pixel reference safe margin.

EditMode tests cover Canvas-sibling discovery, confirmation resolution, modal
history, real EventSystem focus restoration, device prompt labels, accessible
feedback text, and Pause runtime initial focus. PlayMode tests cover modal
control locking and asynchronous focus restoration.

## Keep, Migrate, Remove

### Keep

- Authored prefabs in `Prefabs/UI`.
- `UiMenuButtonView` as the common button visual behaviour.
- `UiPanelFader` as the restrained transition behaviour.
- `UiSpacecraftFlightHudPresenter` as a feature presenter.
- `UiMainMenuController`, `UiGameplayController`, and
  `UiInventoryPanelPresenter` as feature entry points.

### Migrate

- Add the completed Credits screen when its information architecture is
  approved; its unavailable action currently returns feedback.
- Continue moving remaining confirmation, feedback, HUD, and inventory strings
  into the shared `Farion UI` English and Turkish string tables.
- Remove `interactionPromptPrefix` after every supported binding is guaranteed
  to have an English fallback label.

### Removed

- `MainMenuPanelSwitcher` and `GameplayPanelSwitcher`.
- `GameplayScreenState` and `GameplayMenuButton`.
- Empty Settings, Credits, Confirm, and Loading scene placeholders.
- Scene-serialized copies of the runtime `FarionInputActions` asset and its
  action references.

Their production scene and prefab references were checked before removal.

## Inspector Setup

### MainMenu

The production scene is already wired:

1. Keep the `UI_SystemRoot` prefab instance under `MainCanvas`.
2. Keep `UI_ConfirmationDialog`, `UI_LoadingOverlay`, and
   `UI_FeedbackOverlay` as direct `MainCanvas` children beside the root.
3. Keep `UiMainMenuController > Ui System Root` assigned to the root instance.
4. Keep MainPanel registered as `MainMenu` and Initial Screen as `MainMenu`.
5. Keep one EventSystem and one `UiInputModuleBinder`. Do not generate or
   serialize runtime Input Actions in Edit Mode.
6. Keep `UI_SettingsScreen` as a direct `MainCanvas` child. It is discovered by
   the Canvas-scoped router and must remain a prefab instance.
7. Keep `UI_SaveLoadScreen` as a direct `MainCanvas` child. Continue uses the
   newest compatible slot; Load Game opens the browser.
8. When Credits is implemented, create an authored screen prefab, give it a
   unique `UiScreenView`, then add it to the Canvas composition. Do not create
   an empty placeholder.

### Gameplay scene

The production sandbox is already wired:

1. Keep the `UI_SystemRoot` prefab instance below `GameplayCanvas`.
2. Keep `UI_ConfirmationDialog`, `UI_LoadingOverlay`, and
   `UI_FeedbackOverlay` as direct `GameplayCanvas` children beside the root.
3. Keep `UiGameplayController > Ui System Root` assigned to the root instance.
4. Keep HUD, Pause, Inventory, Settings, and Save/Load in the same Canvas
   composition.
5. Keep the Canvas `PlayerControlLock` assigned to the router.
6. Keep one EventSystem and one `UiInputModuleBinder`; its action references
   remain empty in Edit Mode and are assigned from `FarionInputActions` only
   when Play Mode starts.
7. Enter Play Mode and complete the validation checklist below before treating
   the migration as production-approved.

## Acceptance Checklist

- Every enabled action opens a complete screen or returns clear feedback.
- No pointer-exit event clears gamepad selection.
- Each interactive screen has a visible initial focus target.
- Cancel closes only the top layer and restores prior focus.
- HUD input prompts change with the active input device.
- Pause and modal layers lock gameplay input; HUD does not.
- Save reports success or failure.
- Exit and quit require confirmation.
- Feedback displays a semantic severity label as well as signal color.
- Reduced motion disables panel, menu-button, and feedback transition motion
  without changing navigation or result timing.
- Layout survives 125% UI scale and representative longer Turkish strings.
- Play Mode validation covers 16:9, ultrawide, keyboard/mouse, and gamepad.
- The built Windows Player completes MainMenu to gameplay loading and returns
  to MainMenu without a missing prefab, missing script, or focus error.
