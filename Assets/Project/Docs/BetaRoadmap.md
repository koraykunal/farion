# Farion Beta Roadmap

This roadmap keeps the project moving toward a playable beta without mixing
prototype-only systems into production gameplay.

## Product Flow

1. `MainMenu` is the first build scene.
2. `SO_GameFlowSettings` owns menu-to-gameplay scene routing.
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
- Keep direct keyboard readers as temporary adapter components. Gameplay systems
  should consume input interfaces, not `Keyboard.current`.
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

1. Validate deterministic biome and terrain-feature sampling with the selected
   planet coverage reports.
2. Use the same biome and terrain-feature result to filter the first procedural
   resource deposits.
3. Add a minimal resource streaming/spawn pass near the player.
4. Add item taxonomy, recipe definitions, and research definitions before any
   upgrade UI. This keeps progression tied to purpose and knowledge instead of
   flat material tiers.
5. Add local gameplay UI focus control for ESC and inventory without pausing
   simulation.
6. Prove one local economy chain: collect raw resources, refine them into
   materials, unlock one research, and produce one upgrade component.
7. Add the first upgrade contract only after that economy chain is readable.
8. Add save/load deltas for depleted resources, crafted inventory changes,
   researched ids, and discovered points.
9. Defer weather, fauna, and networking until the local exploration/resource
   and first economy loop are readable.
