# Farion UI Runtime Acceptance

Run this checklist after UI prefab, navigation, input, or scene-composition
changes. A successful C# build is required but does not replace these checks.

## Automated Gate

1. Wait for Unity script compilation to finish.
2. Confirm Console has no compiler or missing-prefab errors.
3. Run `Farion > Validation > Validate Project` before a Player build.

Record the date, Unity version, target platform, and failing validation when a
gate fails.

## MainMenu

Open `Assets/Project/Scenes/SC_MainMenu.unity`, clear Console, and enter Play Mode.

### Keyboard and mouse

- New Game receives visible initial focus.
- Arrow keys move exactly once per input.
- Enter activates the focused action.
- Pointer hover does not permanently clear keyboard or gamepad selection.
- Continue and Load Game remain visibly disabled when no save exists.
- Settings opens the authored presentation and accessibility screen.
- Changing language updates Settings and MainMenu labels without reopening the
  scene.
- UI scale, reduced motion, subtitle visibility, and subtitle size survive a
  scene reload.
- Credits returns clear unavailable feedback.
- Feedback includes `INFO`, `SUCCESS`, `CAUTION`, or `ERROR` text as applicable.
- Exit opens confirmation and does not quit immediately.
- Cancel closes only confirmation and restores focus to Exit.
- Confirm executes Quit only after the explicit confirmation action.
- Enabling Reduced Motion in Settings makes subsequent menu, modal, and
  feedback transitions immediate without changing their behavior.

### Gamepad

- D-pad and left stick navigate every available action.
- Left and right adjust the selected Settings value.
- South button submits and East button cancels.
- Pointer movement followed by gamepad input restores visible gamepad focus.
- No selection becomes trapped on a disabled action.

### Loading

- New Game opens the loading system layer before scene activation.
- Repeated submit input does not start a second load.
- Loading blocks pointer and gameplay input.
- Scene-load failure returns to MainMenu without a hidden blocking layer.

### Save and Load

- Continue loads the newest compatible player slot.
- Load Game opens the Save/Load screen with the newest compatible slot focused.
- Primary Slot and Manual Slots 01-03 are present in both English and Turkish.
- Empty, ready, recovery-backup, unsupported, and damaged states remain
  readable without relying on color alone.
- Empty, unsupported, and damaged slots cannot execute Load.
- Delete always opens confirmation and removes only the selected record and
  recovery files.
- Pause > Save opens the same screen and returns to Pause on Cancel.
- Saving to an occupied slot requires overwrite confirmation.
- A successful save refreshes its timestamp without reopening the screen.
- Loading a selected record transitions through the Loading system layer.

## Gameplay

Open `Assets/Project/Scenes/SC_GameplayShell.unity`, clear Console, and enter
Play Mode.

- HUD is visible and does not lock ship or explorer input.
- Interaction binding text changes after keyboard, mouse, and gamepad input.
- Pause opens with Resume focused.
- Pause locks ship and explorer input.
- Inventory replaces Pause as the top screen.
- Cancel from Inventory restores Pause and its prior focus.
- Cancel from Pause restores gameplay and releases the UI control lock.
- Options opens Settings above Pause; Cancel returns to Pause and restores
  focus.
- Save opens the slot browser and produces an explicit success or failure
  message after slot selection.
- Exit to Main Menu and Quit Game always require confirmation.
- Cancel from confirmation restores the invoking action.
- Reduced motion removes transition movement but preserves focus and control
  locking.
- Disabled Blueprints, Journal, Ship, and Map actions cannot be submitted or
  focused as available actions.

## Layout Matrix

Repeat representative MainMenu, Pause, Inventory, Settings, Save/Load,
confirmation, loading, and feedback checks at:

- 1280 x 720, 16:9 minimum representative window.
- 1920 x 1080, reference resolution.
- 2560 x 1440, high-resolution 16:9.
- 3440 x 1440, ultrawide.
- 125% operating-system display scale.
- 100%, 110%, and 125% in-game interface scale.

No required action may leave the visible frame. Text must not overlap icons,
frames, or adjacent controls. Switch between English and Turkish from Settings.
Confirm Turkish characters render correctly and the 125% UI scale does not clip
the longest production labels.

## Player Build

Create a Windows Development Build and verify:

- Process starts in MainMenu without missing-script or missing-prefab errors.
- MainMenu navigation works with keyboard, mouse, and gamepad.
- Language and interface scale persist after restarting the Player.
- MainMenu to gameplay loading completes once.
- Pause, Inventory, Settings, Save/Load, confirmation, and return-to-menu
  complete.
- Application quit confirmation closes the process.
- `Player.log` contains no UI exception, duplicate screen ID, or missing
  reference error.

Runtime acceptance is complete only when both scene passes, the layout matrix,
the project validator, and the Player build pass on the same UI revision.
