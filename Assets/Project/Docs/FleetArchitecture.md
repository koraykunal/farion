# Fleet Architecture

This document is the product and ownership source of truth for fleet-centered
progression. It distinguishes implemented foundations from planned systems.

## Terminology

- **Fleet**: the shared progression identity that owns shared storage and
  knowledge, and will own the capital ship and its assigned shuttles.
- **Capital ship**: the crew's persistent physical home ship.
- **Crew**: the players operating one Fleet.
- **Shuttle**: an expedition craft assigned to a crew member.
- **Fleet Storage**: the shared material pool used by processing and upgrades.
- **Fleet Knowledge**: shared capabilities, blueprints, discoveries, and
  navigation knowledge.

Use `Shuttle` in code and documentation. The legacy serialized save field
`personalShipCargo` is retained for compatibility with existing saves.

`Fleet` names the shared progression identity. Physical home ships use
`CapitalShip` in code and asset names. The first capital ship's display name is
`Fleet`, so its assets use names such as `SM_CapitalShip_Fleet_A` and
`PF_CapitalShip_Fleet` without changing the meaning of `FleetRuntime`.

## Product Ownership

The economy is cooperative:

```text
Expedition
    |
    v
Shuttle Cargo
    |
    v
Fleet Storage
    |
    +--> Processing
    +--> Fleet / Capital Ship upgrades
    +--> Shuttle parts and upgrades
```

Resources are not personally owned progression. The explorer inventory is an
operational carry container, and shuttle cargo is expedition transport. Items
become part of the shared economy when the shuttle unload workflow commits them
to Fleet Storage.

Personal identity may later include suit loadout, cosmetics, callsign, shuttle
name, quarters, achievements, and discovery attribution. These do not create a
second material economy.

## Current Implementation

Implemented now:

- persistent local player, explorer, assigned shuttle, and inventory ids;
- revisioned stack inventory domain;
- `PlayerInventory`, `ShuttleCargoInventory`, and `FleetStorageInventory`
  Unity adapters;
- explicit `ShuttleRuntimeBinding` for shuttle identity, motor, and cargo;
- authored `FleetRuntime` identity with Fleet Storage and Fleet Knowledge;
- `FleetKnowledgeState` and `FleetKnowledgeRuntime`;
- resource harvesting through the session command gateway;
- session-authorized, atomic assigned-shuttle cargo unload;
- an authored Capital Ship Fleet prefab with the imported model, box collision,
  navigation, docking, unload, artificial-gravity, and interaction boundaries;
- one typed processing recipe and atomic Fleet-Storage exchange;
- schema-6 save/load for local inventory, shuttle cargo, Fleet Storage, and
  Fleet Knowledge;
- migration defaults for schema 3, 4, and 5 saves.

Not implemented now:

- capital-ship mutable state, room ownership, or production-machine state;
- refinery/fabricator queues, timers, power, heat, or maintenance;
- crafting, research terminals, or technology voting;
- shuttle hull, fuel, repair, module, or upgrade state;
- unique equipment instances or installation slots;
- multiplayer authority.

No placeholder class should claim ownership of an item in the second list.

## Current Aggregate Boundaries

### Inventory Container

`InventoryContainerState` owns one container id, slot capacity, stack
quantities, and revision. Multi-stack changes are atomic. Failed changes leave
state and revision unchanged.

### Shuttle Binding

`ShuttleRuntimeBinding` owns the persistent shuttle id and references its
physical motor and cargo adapter. It is composition, not a general shuttle-stat
aggregate.

### Fleet Knowledge

`FleetKnowledgeState` owns capability, blueprint, and discovery ids. Unlocks are
monotonic and idempotent. It does not spend resources or select a technology
branch by itself.

### Fleet Runtime

`FleetRuntime` owns the authored Fleet identity and composes the shared storage
and knowledge owners. `GameplayRuntimeRoot` passes that exact reference into the
session; command handlers do not discover or construct alternate Fleet state.

## Implemented Fleet Increment

The first Fleet code foundation contains:

1. An authored Fleet runtime identity.
2. One shared `FleetStorageInventory` based on the existing inventory adapter.
3. Explicit references from `GameplayRuntimeRoot`.
4. One atomic shuttle-cargo to Fleet-Storage unload transaction.
5. Schema-6 persistence so a successful unload cannot be lost on save.

The unload command is exposed only through the authored docking boundary and
cargo interaction surface. The Capital Ship Fleet prefab uses the same
`VisualRoot`, `CollisionRoot`, `RuntimeRoot`, and `Anchors` composition as the
starter shuttle so later Blender revisions can replace presentation without
taking ownership of gameplay state.

Reusable console prefabs live under
`Assets/Project/Prefabs/Gameplay/Interactables`. Each console owns its
presentation placeholder, physical collider, interaction trigger, and
interaction component. `PF_CapitalShip_Fleet` owns their placement and assigns
the cargo console to its authored docking boundary.

Do not add production queues, equipment, repair, power, heat, or networking
before the single local processing and upgrade loop is proven.

## Later Ownership Rules

- Processing consumes and produces Fleet-owned stacks through explicit
  transactions.
- Upgrades consume Fleet-owned resources and update a typed shuttle or
  capital-ship subsystem owner.
- Fleet Knowledge unlocks permission or availability; it does not directly
  mutate arbitrary stats.
- Artifacts and scientific samples are Fleet-owned. Discovery attribution is
  presentation metadata, not personal material ownership.
- Multiplayer will adapt the same commands to host authority. Networking must
  not become an alternate inventory or progression owner.

Typed subsystem definitions are preferred over generic stat dictionaries. A
real scanner upgrade may own range and power requirements; a general
`Dictionary<string, float>` upgrade bag is not an acceptable shortcut.

## Persistence Direction

Schema `6` is the current baseline; schemas `3`, `4`, and `5` remain readable.
Future Fleet save work requires:

1. a live authored runtime owner;
2. immutable snapshot DTOs;
3. explicit migration defaults from schema `5`;
4. validation of Fleet, shuttle, storage, and knowledge cross-references;
5. atomic replacement of live state only after full validation.

Persistence follows the playable ownership graph. It does not define that graph
in advance.
