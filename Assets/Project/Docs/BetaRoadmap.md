# Farion Beta Roadmap

This roadmap starts from the current working foundation and adds one ownership
boundary at a time.

## Target Loop

```text
Capital Ship
    -> choose expedition
    -> board assigned shuttle
    -> travel / enter atmosphere / land
    -> exit and explore
    -> collect resources or discoveries
    -> return to shuttle
    -> return to capital ship
    -> unload to Fleet Storage
    -> process / unlock / upgrade
    -> reach a new destination or activity
```

The player is not gathering for personal material wealth. The crew prepares the
Fleet for the next expedition.

## Current Playable Foundation

The project currently provides:

- main-menu to gameplay scene flow;
- gravity, generated planetary surface data, world-origin rebasing, and
  resource deposit streaming;
- shuttle flight, boost, guidance, landing support, boarding, and explorer exit;
- player inventory and shuttle cargo;
- authored Fleet identity and shared Fleet Storage;
- authored Capital Ship Fleet blockout, navigation target, docking boundary,
  unload surface, and artificial-gravity volume;
- resource harvesting;
- session-authorized atomic shuttle-cargo unload command;
- local session identity and explicit scene composition;
- schema-6 save/load for the active vertical-slice state;
- Fleet Knowledge snapshot state;
- one typed iron-processing recipe and atomic Fleet-Storage exchange;
- gameplay HUD, inventory, pause, settings, and save/load presentation;
- FMOD spacecraft presentation and authored thruster VFX.

The production capital-ship model, processing result presentation, upgrade
application, and multiplayer loop are not complete yet.

## Phase 1: Complete the Unload Workflow

Goal: prove that an expedition result becomes shared Fleet value.

Implemented code foundation:

1. Authored Fleet runtime id and shared storage container.
2. Explicit `GameplayRuntimeRoot` and session binding.
3. Session-authorized, atomic unload from assigned shuttle cargo to Fleet
   Storage.
4. Schema-6 persistence and schema 3-5 migration defaults.

Implemented playable work:

1. Authored the first capital-ship return/docking boundary.
2. Exposed unload only while the assigned shuttle is inside that boundary.
3. Added a physical unload interaction and Fleet navigation target.

Remaining playable work:

1. Present shuttle cargo, Fleet Storage, and the command result through an
   authored interaction surface.
2. Prove harvest -> shuttle cargo -> dock -> unload -> save/load in Play Mode.

No repair, equipment, crafting, research terminal, capital-ship room, or
networking work belongs in this phase.

## Phase 2: One Processing Action

Goal: prove that shared resources can become a useful intermediate material.

Implemented:

1. Selected Iron Ore as input and Iron Ingot as output.
2. Added one typed `ProcessingRecipeDefinition`.
3. Executed one atomic Fleet-Storage exchange through the session command
   boundary.

Remaining:

1. Present success, insufficient input, and insufficient capacity.
2. Prove the result persists through the existing Fleet-owned schema-6 model.

Do not add queues, timers, workers, power, heat, efficiency, maintenance, or a
general machine aggregate yet.

## Phase 3: One Real Upgrade

Goal: make processed Fleet value change the next expedition.

1. Select one visible shuttle capability such as scanner reach, cargo capacity,
   or destination access.
2. Give that capability one typed runtime owner.
3. Spend Fleet-owned materials through an application command.
4. Apply one measurable gameplay effect.
5. Save and restore the result.

Avoid generic stat bags and placeholder module repositories.

## Phase 4: Progression Closure

Goal: complete the local loop before broad content work.

1. Use Fleet Knowledge or the applied upgrade to reveal one new destination or
   activity.
2. Verify start -> expedition -> return -> unload -> progression -> next
   expedition locally.

## Phase 5: Scale Deliberately

Only after the local loop is stable:

1. Expand processing and upgrade content.
2. Define capital-ship services and rooms from real workflows.
3. Add unique equipment only if installed-instance identity is actually needed.
4. Add research choice presentation around Fleet Knowledge.
5. Wrap proven commands with host-authoritative networking.
6. Validate two players before expanding to four.

## Engineering Gates

Every phase must satisfy:

- one explicit runtime owner per mutable state;
- no scene-wide lookup as normal composition;
- no runtime prefab/scene builder;
- no new save payload before a live owner exists;
- no UI-owned gameplay mutation;
- no future-only abstraction left disconnected;
- no unrelated system added to make the feature appear complete;
- Play Mode proof of the actual user workflow before starting the next phase.

Performance work, weather, fauna, ECS, Addressables, and large content expansion
remain deferred until the expedition loop is complete and measurable.
