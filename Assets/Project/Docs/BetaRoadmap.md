# Farion Beta Roadmap

This roadmap keeps the project moving toward a playable beta without mixing
prototype-only systems into production gameplay.

## Product Flow

1. `MainMenu` is the first build scene.
2. `Farion.Application.Runtime` owns menu-to-gameplay scene routing through
   `SO_GameFlowSettings` and `GameFlowService`.
3. `SC_PhysicsSandbox` remains the current gameplay proving ground until the
   first vertical slice is stable enough to be renamed or split.

## Beta-0 Vertical Slice

The first beta target is a complete single-player loop:

1. Start from the main menu.
2. Spawn in or near the active ship.
3. Fly to the authored planet.
4. Enter atmosphere and land with readable guidance.
5. Exit the ship as the explorer.
6. Collect one surface resource.
7. Return to the ship.
8. Apply one ship or suit upgrade.
9. Unlock one new objective marker or story beat.

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
resource streaming and pooling, inventory and definition registries, gameplay
UI focus, possession/boarding composition, schema-4 save/load, centralized
input actions, application flow, audio assembly isolation, and build validation.

1. Play Mode-prove the complete pilot exit, interior, exterior, board, and
   re-enter loop.
2. Add a runtime crafting station service and one readable raw-to-refined
   resource transaction.
3. Add research runtime state and persist researched capability ids.
4. Apply one real suit or ship upgrade through a dedicated upgrade service.
5. Add one objective that observes the economy/upgrade result and persist it.
6. Play Mode-tune and accessibility-test the implemented production flight and
   landing HUD.
7. Add local terrain-collision patches before generated moving planets become
   regular landing targets.
8. Profile resource patch streaming and ocean/atmosphere render budgets on
   target hardware.
9. Defer weather, fauna, Addressables, ECS, and networking until this local loop
   is readable, tested, and save-safe.
