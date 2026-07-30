# Fleet Architecture

This document is the source of truth for Farion's fleet-centered progression
foundation. It defines ownership and implementation order without requiring a
capital-ship model, room prefabs, item icons, or runtime fleet spawning.

## Product Hierarchy

The player progresses through five distinct layers:

1. Player: identity, health, carried interaction state, and session membership.
2. Suit: oxygen, thermal protection, radiation protection, armor, and storage.
3. Personal ship: fuel, hull, cargo, modules, scanner, weapons, and landing.
4. Capital ship: jump capability, hangars, rooms, power, manufacturing,
   research, storage, and fleet services.
5. Fleet knowledge: capabilities, blueprints, discoveries, navigation data,
   species data, and unlocked technology.

These layers can interact, but they must not share one upgrade/stat container.
A research unlock may permit a new reactor definition; it does not directly
increase every ship's reactor output.

## Aggregate Ownership

### Inventory Container

`InventoryContainerState` owns:

- persistent container id;
- slot capacity;
- stackable definition quantities and stack limits;
- unique equipment instance membership;
- monotonic revision.

All multi-item exchanges and container transfers are atomic. A machine, suit,
personal ship, capital ship, or player references a container id instead of
embedding another mutable inventory implementation.

### Equipment Instance

`EquipmentInstanceState` owns:

- persistent instance id;
- immutable equipment definition id;
- immutable serial number;
- condition;
- installed upgrade definition ids;
- monotonic revision.

`EquipmentRepositoryState` owns instance lookup, case-insensitive serial
uniqueness, and the authoritative location ledger. Containers and module slots
store instance ids as local projections of that ledger. Placement, transfer,
install, swap, and uninstall services update the repository and affected
aggregates atomically, so one instance cannot be stored or installed twice.

Equipment authoring uses three linked identities:

- `InventoryItemDefinition.ItemId` is the equipment definition id stored by
  every runtime instance and must use `UniqueInstance` storage.
- `EquipmentDefinition` references that item and adds size class, base mass,
  and one or more compatible `slot_type.*` ids.
- `EquipmentSlotDefinition.SlotId` identifies the physical slot while its
  `SlotTypeId` defines the mount interface accepted by that slot.

`RegistryEquipmentInstallationPolicy` resolves both definitions and requires a
matching slot type plus sufficient slot size. Equipment-specific performance
values belong to typed subsystem definitions later; do not create an
unvalidated generic stat dictionary.

### Personal Ship

`PersonalShipState` owns:

- persistent ship, owner-player, and cargo-container ids;
- player-defined display name;
- hull and fuel values;
- module-slot to equipment-instance assignments;
- disabled state derived from hull;
- monotonic revision.

Flight physics remains in the existing spacecraft systems. The domain ship
state supplies persistent progression and damage data; it must not duplicate
Rigidbody movement, landing telemetry, or input control.

### Capital Ship

`CapitalShipState` currently owns:

- persistent ship, fleet, and storage-container ids;
- display name and room capacity;
- registered room ids;
- machine-to-room membership;
- monotonic revision.

Room geometry, sockets, and machine visuals will be authored assets. Runtime
state validates membership and capacity; it does not generate replacement
rooms or meshes.

### Fleet

`FleetState` owns:

- persistent fleet and capital-ship ids;
- a maximum of four members;
- player-to-personal-ship assignment;
- monotonic membership revision.

`FleetKnowledgeState` is a separate monotonic aggregate for capabilities,
blueprints, and discoveries. Its revision is independent so knowledge updates
do not invalidate unrelated member commands.

## Item Taxonomy

- Resource: stackable gathered input such as ore, ice, crystal, or gas.
- Component: stackable manufactured input such as circuits, plates, or pipes.
- Equipment: unique persistent instance such as engines, scanners, reactors,
  shields, or weapons.
- Knowledge: fleet-owned capability, blueprint, discovery, sample analysis, or
  navigation data; it is not a physical inventory stack.

`InventoryItemDefinition.StorageMode` selects stackable or unique-instance
storage. Schema `5` serializes stackable player and personal-ship cargo
containers. Unique equipment becomes saveable only with its repository and a
later schema revision.

## Machine Boundary

A future machine aggregate should own the following only after the local
resource, immediate processing, ship-upgrade, and save/load loop is proven:

- machine id and definition id;
- room id;
- input and output container ids;
- recipe queue and active job;
- power request and delivered power;
- generated heat and heat limit;
- condition and maintenance requirements;
- efficiency modifiers;
- fuel or consumable policy where applicable;
- monotonic revision.

Power, heat, workers, maintenance, and upgrades should influence operation
through explicit inputs. A machine must not read arbitrary scene singletons or
mutate item definitions.

## Persistence Direction

The future save root is:

`Universe -> Fleet -> Capital Ship -> Fleet Knowledge -> Players -> Personal
Ships -> Equipment Instances -> Containers -> Stations -> Planet Deltas ->
Structures`

Schema `5` is the current stable format. It persists the active player,
personal-ship cargo, and fleet-knowledge vertical slice and explicitly migrates
schemas `3` and `4`. A future equipment/fleet schema requires:

1. immutable snapshot DTOs for each newly active aggregate;
2. explicit migration defaults from schema `5`;
3. cross-reference validation for all persistent ids;
4. atomic validation before live state replacement;
5. equipment ownership validation;
6. explicit new-game and load-game fleet composition.

## Authority Direction

Single-player and multiplayer use the same commands:

1. caller sends intent with aggregate id and expected revision;
2. authoritative session validates ownership and command policy;
3. domain aggregate validates invariants and applies one atomic change;
4. persistence records the new snapshot/delta;
5. presentation and future clients receive the resulting revision/state.

Networking transports and Unity scene objects remain adapters. They must not
become alternate owners of inventory, equipment, ship, or knowledge state.

## Current Integration Status

Implemented:

- compiler-enforced `Farion.Gameplay.Domain` assembly without Unity references;
- domain identity values;
- atomic revisioned containers and transfers;
- unique equipment instance state;
- equipment repository and authoritative location ledger;
- atomic equipment placement, container transfer, install, swap, and uninstall;
- equipment/slot authoring contracts and registry installation policy;
- personal ship, capital ship, fleet, and fleet knowledge aggregates;
- reusable `InventoryContainerComponent` Unity adapter;
- generic inventory consumption in resource interaction and gameplay UI;
- legacy `PlayerInventory` compatibility adapter and schema-5 reusable cargo
  snapshot;
- distinct local-player, explorer, personal-ship, and carried-inventory session
  identity;
- shared immutable runtime bindings for session and save composition;
- dedicated explorer exit/surface-placement responsibility outside the
  possession controller;
- session-owned harvesting and immediate-crafting command gateway;
- atomic resource harvesting with inventory compensation on stale source;
- authored starter-shuttle `PersonalShipState` runtime binding;
- root-owned personal-ship cargo adapter;
- revision-aware rename, refuel, repair, damage, and fuel-consumption commands;
- registry/editor validation for definition and capability ids;
- EditMode contract tests.

Intentionally not implemented:

- runtime fleet or capital-ship spawning;
- fleet session singleton/service locator;
- machine aggregate and queues;
- equipment install/uninstall application command handler and active repository
  composition;
- immediate authored processing terminal and ship-upgrade loop;
- concrete equipment content assets, models, and icons;
- unique-equipment and fleet composition snapshot DTOs in a later schema;
- networking transport or replication;
- authored room sockets, capital-ship prefabs, or new-game fleet bootstrap.
