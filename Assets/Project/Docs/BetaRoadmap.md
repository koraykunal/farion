# Farion Beta Roadmap

This roadmap keeps the project moving toward a playable beta without mixing
prototype-only systems into production gameplay.

## Product Flow

1. `MainMenu` is the first build scene.
2. `Farion.Application.Runtime` owns menu-to-gameplay scene routing through
   `SO_GameFlowSettings` and `GameFlowService`.
3. `SC_PhysicsSandbox` remains the current gameplay proving ground until the
   first vertical slice is stable enough to be renamed or split.
4. The target game loop starts from an authored capital ship, sends each player
   out in a personal ship, and returns expedition results to fleet progression.

The long-term progression chain is:

`Planet -> Resources -> Machines -> Components -> Ship Upgrades -> New Systems
-> Rare Technology -> Capital Ship Expansion -> New Gameplay`

Resources are inputs to capability unlocks, not progression goals by themselves.

## Beta-0 Vertical Slice

The first beta target proves the expedition loop locally before transport-level
multiplayer:

1. Start from the main menu.
2. Load an authored fleet/capital-ship session.
3. Board the assigned personal ship and depart.
4. Fly to the authored planet, enter atmosphere, and land.
5. Exit as the explorer and collect one useful resource.
6. Return to the personal ship and then to the capital ship.
7. Refine the resource into a component.
8. Apply one personal-ship or capital-ship capability upgrade.
9. Unlock a destination, planet class, or activity that was unreachable before.

Do not add co-op networking before this loop works locally. Multiplayer should
replicate a proven loop, not define the loop while the foundation is still
moving.

## Production Boundaries

- Import item, resource, and UI assets through `Assets/Project/Docs/AssetIntake.md`
  so runtime art, source art, ScriptableObject definitions, and prefabs stay in
  separate ownership paths.
- Keep debug HUDs behind editor/development build flags.
- Keep planetary generation deterministic. `PlanetaryGenerationProfile` owns
  planet identity, generation-environment assumptions, shape, biome
  distribution, and terrain-feature distribution; later resource, weather,
  fauna, and POI layers must consume generated data instead of writing runtime
  state into ScriptableObjects.
- Validate generation against physical body settings through
  `PlanetSurfaceModel`. `CelestialBodyDefinition` remains the physics
  source, while generation profiles are checked against radius, gravity,
  atmosphere, stable surface liquid, temperature, radiation, type, climate, and
  biome compatibility.
- Keep biome authoring in `Simulation/Planetary`. Gameplay resources may filter
  by biome, but they do not own biome definitions or sampling.
- Keep terrain features separate from biomes and shape. Shape profiles generate
  actual height; terrain-feature profiles classify regions such as crater
  fields or mountain ridges for resources, POI, and future modifiers.
- Keep biome visualization opt-in and separated under rendering/debug tooling.
  Do not make gameplay systems depend on debug gizmos or temporary visual maps.
- `FarionInputActions` owns keyboard/mouse and gamepad bindings. Gameplay
  systems consume input interfaces and do not read devices directly.
- Gameplay UI focus must not pause simulation. ESC, inventory, and later station
  panels should use `PlayerControlLock` to block local player input while the
  world, physics, resource streaming, and co-op simulation continue.
- Put `PlayerInventory` on the active explorer actor before wiring any resource
  collection nodes. Resource nodes should use `ResourceNodeInteractable` and a
  `ResourceNodeDefinition` asset; item definitions are yielded by resource
  definitions, not assigned directly on scene nodes.
- `PlayerInventory` is an adapter over `InventoryContainerState`. New cargo,
  storage, machine, and suit inventories must reuse the same container domain;
  they must not copy the old player-specific stack mutation logic.
- Stackable resources/components and unique equipment are different persistence
  models. A unique equipment item must have a persistent instance id before it
  can carry condition, serial number, modules, or upgrades.
- Item progression is category-driven, not linear tier-driven. Item definitions
  carry `InventoryItemCategory`, `InventoryItemForm`, and `TechnologyDomain` so
  crafting, research, upgrades, and future co-op authority can reason about
  resource purpose without hard-coding names.
- Crafting and research definitions are authored contracts only. They may
  reference inventory items, recipes, and unlock capability ids, but current
  progress, active jobs, researched ids, and station queues must live in runtime
  services/save deltas later.
- Resource deposits should keep generated base data and runtime deltas separate.
  `ResourceDepositData` describes deterministic base state; depletion is tracked
  as extracted amount against a `GeneratedEntityId`, not by mutating the
  generated definition or storing full node snapshots.
- Keep `WorldOriginRebaser` local-only. Future co-op needs a separate
  authoritative world-coordinate model above local rebasing.
- Keep world coordinates and generated entity ids as pure Simulation contracts.
  They are not a streaming manager. Streaming, save deltas, and multiplayer
  authority should consume these contracts instead of storing persistent state
  in Unity transforms.
- Keep survival state out of UI and interaction components. Resource, inventory,
  upgrade, and objective state need dedicated gameplay services before beta.

## Next Implementation Order

Completed foundation: deterministic planetary/resource generation, local
resource streaming and pooling, revisioned inventory/equipment/fleet domain
aggregates, reusable inventory Unity projection, authoritative equipment
location ledger and loadout transactions, equipment/slot authoring contracts,
registry installation policy, inventory and definition registries, gameplay UI
focus, possession/boarding composition, schema-5 save/load with schema-3/4
migration, centralized input
actions, application flow, audio assembly isolation, compiler-enforced pure
domain assembly, explicit local-session identity, shared runtime/save
composition bindings, build validation, session-owned harvesting/crafting
commands, and the authored starter-shuttle `PersonalShipState`/cargo binding.

1. Run the new domain EditMode tests and Play Mode-prove the current
   possession, inventory, resource, and save/load behavior did not regress.
2. Add one authored immediate processing terminal using the existing recipe
   transaction. Do not add queues, power, heat, workers, or maintenance yet.
3. Compose the first concrete equipment-instance repository and add the
   application install/uninstall command handler when its authored container
   owner exists.
4. Apply one produced component through a real personal-ship upgrade and prove
   that its runtime effect has a single authority.
5. Add the unique-equipment repository and design its next-schema snapshot
   only after its runtime owner exists. Validate every ownership
   cross-reference before replacing live state.
6. Extend the active research terminal content through
   `FleetKnowledgeState` capability/blueprint unlocks instead of direct stat
   bonuses.
7. Prove the local planet, collection, processing, upgrade, and save/load loop
   before capital-ship production work.
8. Define the machine aggregate, including queue, power, heat, condition,
   efficiency, and maintenance, only after the immediate processing loop is
   useful and measurable.
9. Define authored capital-ship room sockets and machine-placement contracts;
   keep meshes and prefabs replaceable.
10. Add the authored capital ship and explicit new-game/load-game fleet
   bootstrap. Do not procedurally invent the fleet in a scene component.
11. Prove the complete capital ship, personal ship, planet, return, processing,
   upgrade, and unlock loop locally.
12. Add host-authoritative command/replication adapters around the proven
   aggregate boundaries, then validate two players before scaling to four.
13. Profile terrain streaming and ocean/atmosphere budgets on target hardware.
14. Defer weather, fauna, Addressables, and ECS until the expedition loop is
   readable, save-safe, and measurable.
