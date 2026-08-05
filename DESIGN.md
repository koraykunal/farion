---
name: Farion
description: A quiet expedition interface for readable spaceflight, survival, and fleet operations.
colors:
  farion-void: "#05070A"
  farion-void-raised: "#0B1117"
  farion-void-button: "#06090DB8"
  farion-void-hover: "#0E161EEB"
  farion-void-panel: "#05080BE6"
  cabin-ivory: "#E0E8ED"
  muted-metal: "#8A99A6"
  secondary-blue: "#ADC2CE"
  focus-steel: "#8FAEC2"
  nominal-green: "#94BAAF"
  caution-amber: "#FFB83D"
  critical-red: "#FF4233"
typography:
  headline:
    fontFamily: "IBM Plex Sans, Arial, sans-serif"
    fontSize: "22px"
    fontWeight: 400
    lineHeight: 1.2
    letterSpacing: "0.04em"
  title:
    fontFamily: "IBM Plex Sans, Arial, sans-serif"
    fontSize: "19px"
    fontWeight: 500
    lineHeight: 1.2
    letterSpacing: "0.03em"
  body:
    fontFamily: "IBM Plex Sans, Arial, sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.45
    letterSpacing: "normal"
  label:
    fontFamily: "IBM Plex Sans, Arial, sans-serif"
    fontSize: "13px"
    fontWeight: 500
    lineHeight: 1.2
    letterSpacing: "0.05em"
  instrument:
    fontFamily: "IBM Plex Mono, monospace"
    fontSize: "13px"
    fontWeight: 500
    lineHeight: 1.25
    letterSpacing: "0.02em"
rounded:
  subtle: "4px"
  panel: "6px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
components:
  menu-button:
    backgroundColor: "{colors.farion-void-button}"
    textColor: "{colors.muted-metal}"
    typography: "{typography.title}"
    rounded: "{rounded.subtle}"
    padding: "0 32px"
    height: "56px"
  menu-button-hover:
    backgroundColor: "{colors.farion-void-hover}"
    textColor: "{colors.cabin-ivory}"
    typography: "{typography.title}"
    rounded: "{rounded.subtle}"
    padding: "0 32px"
    height: "56px"
  operational-panel:
    backgroundColor: "{colors.farion-void-panel}"
    textColor: "{colors.cabin-ivory}"
    rounded: "{rounded.panel}"
    padding: "24px"
---

# Design System: Farion

## Overview

**Creative North Star: "Quiet Expedition Console"**

Farion's interface feels like credible expedition equipment: composed,
purposeful, and connected to the spacecraft and fleet around it. It is
interesting through proportion, typography, selective signal color, responsive
feedback, and restrained mechanical detail. It never depends on constant visual
noise.

Main-menu surfaces may use broader cinematic composition. Flight, inventory,
station, and ship-operation surfaces stay compact and task-focused. The player
should notice state and consequence before decoration.

**Key Characteristics:**

- Dark tinted operational surfaces with warm readable text.
- Muted steel blue reserved for focus, selection, and interactive values.
- Green is limited to explicit nominal or successful outcomes.
- Compact motion that confirms state without delaying interaction.
- Authored frames and dividers used to clarify hierarchy, never to decorate
  every container.
- One coherent component vocabulary across keyboard, mouse, and gamepad.

## Colors

The palette is a dark, low-chroma instrument field with scarce, meaningful
signal colors.

### Primary

- **Farion Void:** The canvas and deepest operational surface.
- **Focus Steel:** Primary focus, selection, and available interaction.

### Secondary

- **Secondary Blue:** Live flight guidance and supporting telemetry.
- **Nominal Green:** Explicit successful or nominal outcomes only.
- **Secondary Blue:** Supporting information and low-priority interactive
  detail.

### Tertiary

- **Caution Amber:** Recoverable danger, degraded systems, and time-sensitive
  warnings.
- **Critical Red:** Immediate failure, destructive actions, and critical
  thresholds only.

### Neutral

- **Cabin Ivory:** Primary readable text and high-confidence labels.
- **Muted Metal:** Secondary text, inactive labels, and low-priority metadata.
- **Farion Void Raised:** Tonal separation between adjacent dark surfaces.

**The Scarce Signal Rule.** Focus Steel, Secondary Blue, Nominal Green, Caution
Amber, and Critical Red communicate state. They never fill large inactive
areas.

**The Severity Rule.** Warning color is always paired with explicit text, an
icon, shape, sound, or motion. Color alone is forbidden.

## Typography

**Interface Font:** IBM Plex Sans Regular and Medium
**Instrument Font:** IBM Plex Mono Medium
**Migration Status:** The authored UI migration is complete. Liberation Sans is
not an allowed fallback in project UI prefabs or build-scene compositions.

**Character:** Plex Sans keeps navigation and prose quiet and readable. Plex
Mono is reserved for changing values, measurements, target markers, and console
readouts. It is not a decorative science-fiction display face. Hierarchy comes
from role, scale, weight, spacing, and placement rather than novelty lettering.

### Hierarchy

- **Headline** (400, 22px, 1.2): Screen titles and major operational states.
- **Title** (500, 19px, 1.2): Menu actions and selected item names.
- **Body** (400, 14px, 1.45): Descriptions, explanations, and system outcomes.
  Prose is capped near 70 characters per line.
- **Label** (500, 13px, 0.05em): Short metadata, control labels, and telemetry
  categories.

**The Legibility Before Lore Rule.** Abbreviations are allowed only when they
are standard, repeatedly taught, or constrained by a true instrument surface.
Invented microtext is forbidden.

**The Case Rule.** Uppercase is reserved for short labels, commands, and alerts.
Descriptions and longer messages use sentence case.

## Elevation

Farion is flat by default. Depth comes from tonal surface changes, opacity,
thin full frames, dividers, and controlled overlap. Decorative drop shadows and
default blur are not part of the system.

Panels that interrupt gameplay use a clear opaque or near-opaque surface.
Transient feedback may lift above the HUD through scale, placement, and a
slightly brighter border, not a large ambient shadow.

**The Tonal Depth Rule.** If two surfaces cannot be separated without a heavy
shadow, their hierarchy or spacing is wrong.

## Components

### Buttons

- **Shape:** Slightly softened mechanical rectangle (4px).
- **Primary:** Dark tinted surface, Cabin Ivory label, Focus Steel focus
  frame, 56px standard height.
- **Hover / Focus:** Transition between 140ms and 200ms with ease-out movement
  and color response. Focus remains visible without pointer hover.
- **Destructive:** Keep the label in readable Cabin Ivory; use Critical Red on
  the focus frame or semantic signal, never as low-contrast red-on-red text.
- **Disabled:** Lower neutral contrast with no accent frame. Disabled controls
  remain readable and never masquerade as available actions.

### Cards / Containers

- **Corner Style:** Restrained panel corners (6px).
- **Background:** Farion Void Panel or Farion Void Raised.
- **Shadow Strategy:** No default shadow.
- **Border:** Full-frame treatment only when it communicates grouping, focus, or
  modal priority.
- **Internal Padding:** 24px for screens, 16px for compact operational groups.

### Inputs / Fields

- **Style:** Dark raised surface with a complete low-contrast frame.
- **Focus:** Focus Steel frame plus persistent text-caret or selection
  feedback.
- **Error / Disabled:** Critical Red is reserved for invalid committed input;
  disabled fields use Muted Metal and remain readable.

### Navigation

Navigation uses one selected-state vocabulary across menus and gameplay screens.
Every opened screen defines a first selectable control. Cancel closes the top
layer, and closing restores the previously selected control. Pointer activity
must never permanently erase gamepad focus. A selection created by a pointer
returns to its persistent current state when the pointer leaves.

### Operational HUD

The HUD groups information by the player's current decision: control state,
navigation state, and active advisory. Nominal telemetry is quiet. Caution and
critical states increase salience only when thresholds are crossed.

### Feedback Surfaces

Toasts confirm non-blocking results. Inline messages explain recoverable
problems. Confirmation dialogs are limited to destructive or irreversible
actions. Loading overlays block repeated commands and expose progress or a
clear indeterminate state.

## Do's and Don'ts

### Do:

- **Do** keep gameplay HUDs restrained and reveal deeper telemetry on demand.
- **Do** use Focus Steel only for focus, selection, and actionable state; keep
  Nominal Green for explicit successful outcomes.
- **Do** make every screen usable with keyboard, mouse, and gamepad.
- **Do** pair warning colors with explicit language and another sensory or
  structural cue.
- **Do** use authored prefabs and presentation contracts so visuals can change
  without gameplay rewrites.
- **Do** keep English source strings ready for later Turkish expansion.

### Don't:

- **Don't** use excessive neon cyberpunk styling.
- **Don't** use constant holographic decoration or visual noise.
- **Don't** build crowded HUDs that show every available metric at all times.
- **Don't** use decorative glassmorphism and blur as a default surface
  treatment.
- **Don't** use generic science-fiction panels disconnected from the actual game
  systems.
- **Don't** use unreadable microtext, ornamental abbreviations, or color-only
  warnings.
- **Don't** ship empty buttons, placeholder screens, prototype overlays, or
  dead-end flows in production scenes.
- **Don't** use colored side-stripe borders, gradient text, nested cards, bounce,
  or elastic motion.
