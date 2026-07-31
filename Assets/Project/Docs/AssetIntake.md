# Farion Asset Intake

Use this document before importing item, resource, UI, or progression design
assets. Keep source art, runtime art, authored data, and prefabs separate.

Raw authoring files live under the repository-level `ArtSource` folder, outside
Unity's `Assets` tree. Keep `.blend` files, bake intermediates, paint sources,
and original vendor textures there. Only runtime-ready models, materials, and
textures belong under `Assets/Project/Art`.

## Item And Progression Data

Runtime gameplay data is authored as ScriptableObject definitions:

- Item definitions: `Assets/Project/Design/Gameplay/Inventory`
  - `SO_<ItemName>Item.asset`
  - owns display name, category, form, tech domain, stack size, and future icon
    reference.
- Resource node definitions: `Assets/Project/Design/Gameplay/Resources`
  - `SO_<ResourceName>Node.asset`
  - owns prompt text, yielded item, reserve range, harvest amount, and visual
    prefab reference.
- Processing recipes: `Assets/Project/Design/Gameplay/Processing`
  - `SO_Process_<InputName>.asset`
  - owns a stable recipe id, display name, inputs, and outputs.

Research definitions do not exist yet. Create
`Assets/Project/Design/Gameplay/Research` only when a real research runtime
consumer is implemented.

Do not store current inventory, active crafting jobs, researched ids, depleted
resource nodes, or objective state in these assets. Those belong in runtime
services and save deltas.

## Capital Ship Models

Capital-ship source models remain presentation assets. They do not contain
Fleet Storage, knowledge, docking authority, artificial gravity, or processing
state.

Folders:

- Models: `Assets/Project/Art/Models/Spacecraft/CapitalShipFleet`
- Materials: `Assets/Project/Art/Materials/Spacecraft/CapitalShipFleet`
- Textures: `Assets/Project/Art/Textures/Spacecraft/CapitalShipFleet`
- Ship prefabs: `Assets/Project/Prefabs/Gameplay/Spacecraft`
- Reusable console prefabs: `Assets/Project/Prefabs/Gameplay/Interactables`

Naming:

- `SM_CapitalShip_<DisplayName>_<Variant>.fbx`
- `MAT_CapitalShip_<DisplayName>_<Surface>.mat`
- `TX_CapitalShip_<DisplayName>_<Surface>_<Map>.<ext>`
- `PF_CapitalShip_<DisplayName>.prefab`

The first ship's display name is `Fleet`, while `FleetRuntime` continues to
mean the shared progression identity. Its working assets therefore use
`SM_CapitalShip_Fleet_A` and `PF_CapitalShip_Fleet`. The
`PF_CapitalShip_Fleet_Greybox` prefab is only a fallback comparison asset.

Keep Blender mesh nodes presentation-only and use explicit names such as
`SM_CapitalShip_Fleet_Exterior`, `SM_CapitalShip_Fleet_HangarFloor`, and
`SM_CapitalShip_Fleet_HangarWall_L`. Unity owns the prefab's `VisualRoot`,
`CollisionRoot`, `RuntimeRoot`, and `Anchors`.

Console models replace only the console prefab's `VisualRoot`. Keep its
`COL_Body`, `InteractionVolume`, and interaction component together so
the same console can be positioned as one nested prefab.

All production spacecraft use the same four composition roots. Put physical
colliders under `CollisionRoot` and name them `COL_*`; use `Exterior`,
`Interior`, or `Landing` subgroups only when the ship has that category.
Presentation stays under `VisualRoot`, authored gameplay objects under
`RuntimeRoot`, and position-only references under `Anchors`.

## Item Icons

Preferred source format:

- `PSD` or layered source files only when they need to stay editable in Unity.
- Exported runtime icon: transparent `PNG`.
- Square canvas: `256x256` minimum, `512x512` preferred for key items.
- Keep silhouettes readable at `32x32`.

Folders:

- Gameplay item icons: `Assets/Project/Art/UI/Icons/Gameplay/Items`
- Gameplay menu/action icons: `Assets/Project/Art/UI/Icons/Gameplay`
- Ship and suit system icons reserved for later HUD/system panels:
  `Assets/Project/Art/UI/Icons/Systems`
- Temporary or review-only source exports: `ArtSource/UI`

Naming:

- `IC_Item_<ItemName>.png`
- `IC_Menu_<ActionName>.png`
- `IC_System_<SystemName>.png`
- `SRC_<FeatureOrScreenName>_<Variant>.psd`

Unity import:

- Texture Type: `Sprite (2D and UI)`
- Alpha Source: input texture alpha
- Mesh Type: `Full Rect` for UI icons unless tight mesh is deliberately needed.

## UI Textures And Screen Art

Preferred runtime format:

- Transparent `PNG` for UI panels, frames, masks, dividers, and decorative
  overlays.
- Use 9-slice-friendly borders for scalable frames and panels.
- Avoid baking text into UI textures unless it is a logo or brand mark.

Folders:

- Shared UI textures: `Assets/Project/Art/UI/Textures`
- HUD-specific textures: `Assets/Project/Art/UI/Textures/HUD`
- Screen/panel textures: `Assets/Project/Art/UI/Textures/Panels`
- Brand marks and logos: `Assets/Project/Art/UI/Textures`

Naming:

- `UI_HUD_<ElementName>.png`
- `UI_Panel_<ElementName>.png`
- `Logo_<BrandOrMode>.png`

## UI Prefabs

Reusable authored prefabs live under:

- Shared widgets: `Assets/Project/Prefabs/UI/Common`
- Gameplay HUD and widgets: `Assets/Project/Prefabs/UI/Gameplay`
- Full screens and modal panels: `Assets/Project/Prefabs/UI/Screens`

Do not create a new screen prefab when a shared widget prefab plus a scene-level
binding is enough. Put reusable visuals in Common, gameplay-only presenters in
Gameplay, and full-screen compositions in Screens.

## Resource Node Visuals

Preferred runtime format:

- Models: `FBX`
- Textures: `PNG` or `TIF`
- Materials: Unity `.mat`
- Prefab: one authored prefab per resource node type.

Folders:

- Models: `Assets/Project/Art/Models/Resources`
- Textures: `Assets/Project/Art/Textures/Resources`
- Materials: `Assets/Project/Art/Materials/Resources`
- Prefabs: `Assets/Project/Prefabs/Gameplay/Resources`

Create model and texture folders only when imported runtime assets actually
exist; Unity primitive-based prefabs do not need empty placeholder folders.

Naming:

- `SM_Resource_<ResourceName>.fbx`
- `TX_Resource_<ResourceName>_<Map>.png`
- `MAT_Resource_<ResourceName>.mat`
- `PF_<ResourceName>Node.prefab`

Resource node prefabs should include their visual hierarchy and interaction
collider. Gameplay values still come from `ResourceNodeDefinition`, not from
hard-coded prefab scripts.

## Design References

Use external design boards for exploration, but import only the assets that the
game needs:

- Final UI exports go under `Assets/Project/Art/UI`.
- Final item/resource art goes under `Assets/Project/Art`.
- Authored gameplay contracts go under `Assets/Project/Design`.
- Do not put raw screenshots, mood boards, or temporary references in Prefabs or
  Runtime folders.
