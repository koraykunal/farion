# Farion Product

## Register

product

## Users

Farion is built for PC players who enjoy space exploration, survival,
engineering, progression, and cooperative problem solving. The primary release
context is Steam with keyboard and mouse plus Xbox and PlayStation style
gamepads. The intended multiplayer shape is one to four online players without
split-screen.

Players must be able to read critical flight, landing, equipment, inventory,
objective, and session information quickly while the simulation continues
around them. Interface flows must remain understandable during both quiet
planning and high-pressure piloting.

## Product Purpose

Farion is a lightly serious space survival and progression game about leaving a
shared fleet, completing expeditions with a personal spacecraft, returning with
useful resources, and converting those results into new capabilities.

The interface exists to make complex systems legible without overwhelming the
player or separating them from the world. Success means that players understand
their current state, the next meaningful action, and the result of every command
without needing debug information or external explanation.

English is the initial content language. The product must be structured for a
later Turkish localization without rebuilding screens or gameplay contracts.

## Brand Personality

Farion is intriguing, composed, and credible. Its interface is lightly serious
rather than militaristic, theatrical, or sterile. It should feel like equipment
that belongs inside the same spacecraft, fleet, and planetary world as the
gameplay.

The product can use moments of visual character, motion, sound, and diegetic
framing, but clarity remains the dominant priority. Main-menu presentation may
be more cinematic; gameplay HUDs and operational screens remain restrained and
task-focused.

## Anti-references

- Excessive neon cyberpunk styling.
- Constant holographic decoration or visual noise.
- Crowded HUDs that show every available metric at all times.
- Decorative glassmorphism and blur used as a default surface treatment.
- Generic science-fiction panels disconnected from the actual game systems.
- Unreadable microtext, ornamental abbreviations, or color-only warnings.
- Empty buttons, placeholder screens, prototype overlays, and dead-end flows in
  production scenes.

## Design Principles

1. **Show the current decision.** Present information that changes what the
   player should do now; keep deeper telemetry available through progressive
   disclosure.
2. **Belong to the world.** Menus, HUDs, terminals, and alerts share one visual
   vocabulary shaped by Farion's spacecraft and expedition setting.
3. **Earn every interruption.** Modal screens and alerts are reserved for
   destructive choices, blocking errors, or decisions that cannot be handled
   inline.
4. **Preserve simulation authority.** UI reads state and submits intent.
   Gameplay, application, persistence, and networking systems own outcomes.
5. **Support every primary device.** Keyboard, mouse, and gamepad navigation are
   first-class and expose the correct active binding.
6. **Build for replacement and growth.** Authored prefabs, presentation data,
   and explicit contracts allow visuals to change without rewriting gameplay.

## Accessibility & Inclusion

- Support scalable UI, readable type, clear focus states, and safe-area-aware
  layout.
- Never communicate warning severity through color alone. Pair color with text,
  shape, iconography, motion, or sound as appropriate.
- Provide reduced-motion behavior for nonessential movement and transitions.
- Keep subtitles and captions structurally supported even before final voice
  content exists.
- Allow complete control rebinding and show device-aware prompts.
- Treat localization expansion, longer Turkish strings, and font fallback as
  layout requirements from the beginning.
