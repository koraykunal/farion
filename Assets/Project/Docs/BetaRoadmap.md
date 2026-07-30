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
- resource harvesting;
- local session identity and explicit scene composition;
- schema-5 save/load for the active vertical-slice state;
- Fleet Knowledge snapshot state;
- gameplay HUD, inventory, pause, settings, and save/load presentation;
- FMOD spacecraft presentation and authored thruster VFX.

There is no capital ship, shared Fleet Storage, unload workflow, processing,
upgrade application, or multiplayer loop yet.

## Phase 1: Shared Fleet Storage

Goal: prove that an expedition result becomes shared Fleet value.

1. Add an authored Fleet runtime id and shared storage container.
2. Bind both through `GameplayRuntimeRoot`.
3. Add one session-authorized, atomic unload transaction from the assigned
   shuttle cargo to Fleet Storage.
4. Add a minimal authored interaction/presentation surface for unloading.
5. Prove harvest -> shuttle cargo -> Fleet Storage in Play Mode.
6. Keep schema `5` unchanged until the live ownership and unload semantics are
   stable.

No repair, equipment, crafting, research terminal, capital-ship room, or
networking work belongs in this phase.

## Phase 2: One Processing Action

Goal: prove that shared resources can become a useful intermediate material.

1. Choose one input and one output.
2. Add one typed processing definition only after the action shape is known.
3. Execute one atomic Fleet-Storage exchange.
4. Present success, insufficient input, and insufficient capacity.
5. Persist the result through the Fleet-owned save model selected after Phase 1.

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

## Phase 4: Expedition Closure

Goal: complete the local loop before broad content work.

1. Add an authored capital-ship return/docking boundary.
2. Make unload available only through that boundary.
3. Use Fleet Knowledge or the applied upgrade to reveal one new destination or
   activity.
4. Verify start -> expedition -> return -> unload -> progression -> next
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
