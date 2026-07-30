# Fleet Architecture

This document is the product and ownership source of truth for fleet-centered
progression. It distinguishes implemented foundations from planned systems.

## Terminology

- **Fleet**: the shared progression identity that will own the capital ship,
  assigned shuttles, shared storage, and shared knowledge.
- **Capital ship**: the crew's persistent physical home ship.
- **Crew**: the players operating one Fleet.
- **Shuttle**: an expedition craft assigned to a crew member.
- **Fleet Storage**: the shared material pool used by processing and upgrades.
- **Fleet Knowledge**: shared capabilities, blueprints, discoveries, and
  navigation knowledge.

Use `Shuttle` in code and documentation. The legacy serialized save field
`personalShipCargo` is retained only for schema-5 compatibility.

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
- `PlayerInventory` and `ShuttleCargoInventory` Unity adapters;
- explicit `ShuttleRuntimeBinding` for shuttle identity, motor, and cargo;
- `FleetKnowledgeState`, `FleetKnowledgeRuntime`, and schema-5 snapshot;
- resource harvesting through the session command gateway;
- schema-5 save/load for local inventory, shuttle cargo, and Fleet Knowledge.

Not implemented now:

- Fleet runtime id or Fleet aggregate;
- Fleet Storage;
- capital-ship state, rooms, machines, or hangars;
- shuttle-to-Fleet unload;
- crafting, refinery, fabricator, research terminal, or queues;
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

## First Fleet Increment

The first Fleet feature should contain only:

1. An authored Fleet runtime identity.
2. One shared `FleetStorageInventory` based on the existing inventory adapter.
3. Explicit references from `GameplayRuntimeRoot`.
4. One atomic shuttle-cargo to Fleet-Storage unload transaction.
5. One minimal presentation surface showing both containers and the unload
   result.

Do not add capital-ship rooms, production machines, equipment, repair, power,
heat, or networking in this increment.

After the workflow is Play Mode-proven, decide whether Fleet Storage belongs in
a schema-6 Fleet snapshot. Until then, schema `5` remains unchanged.

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

Schema `5` is the current baseline and must remain readable. Future Fleet save
work requires:

1. a live authored runtime owner;
2. immutable snapshot DTOs;
3. explicit migration defaults from schema `5`;
4. validation of Fleet, shuttle, storage, and knowledge cross-references;
5. atomic replacement of live state only after full validation.

Persistence follows the playable ownership graph. It does not define that graph
in advance.
