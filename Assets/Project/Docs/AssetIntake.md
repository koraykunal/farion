# Farion Asset Intake

Use this document before importing item, resource, UI, or progression design
assets. Keep source art, runtime art, authored data, and prefabs separate.

Raw authoring files live under the repository-level `ArtSource` folder, outside
Unity's `Assets` tree. Keep `.blend` files, bake intermediates, paint sources,
and original vendor textures there. Only runtime-ready models, materials, and
textures belong under `Assets/Project/Art`.

## Item And Progression Data

Runtime gameplay data is authored as ScriptableObject definitions. Every one
is named subject-first, `SO_<Subject><DefinitionType>`, so a folder listing
reads as a list of subjects. The type suffix is the runtime type without its
`Definition`/`Profile` boilerplate. Shared defaults are prefixed `Default`,
and content belonging to the starting system is prefixed `Starting`.
`Test` never appears in a shipped asset name.


- Item definitions: `Assets/Project/Design/Gameplay/Inventory`
  - `SO_<ItemName>Item.asset`
  - owns display name, category, form, tech domain, stack size, and future icon
    reference.
- Resource node definitions: `Assets/Project/Design/Gameplay/ResourceNodes`
  - `SO_<ResourceName>Node.asset`
  - owns prompt text, yielded item, reserve range, harvest amount, and visual
    prefab reference.
- Processing recipes: `Assets/Project/Design/Gameplay/Processing`
  - `SO_<InputName>Recipe.asset`
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
`SM_CapitalShip_Fleet_A` and `PF_CapitalShip_Fleet`.

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

- Gameplay item icons: `Assets/Project/Art/Icons/Items`
- Menu and navigation icons: `Assets/Project/Art/Icons/Menu`
- Ship and suit system icons reserved for later HUD/system panels:
  `Assets/Project/Art/Icons/Systems`
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

- Shared frames, masks, panels, and controls:
  `Assets/Project/Art/Textures/UI/Common`
- HUD-specific textures: `Assets/Project/Art/Textures/UI/HUD`
- Brand marks and logos: `Assets/Project/Art/Textures/UI/Branding`
- Feature-specific screen art: `Assets/Project/Art/Textures/UI/Screens/<Feature>`;
  create the feature folder only when a runtime asset exists.

Naming:

- `UI_HUD_<ElementName>.png`
- `UI_Panel_<ElementName>.png`
- `Logo_<BrandOrMode>.png`

## UI Prefabs

Reusable authored prefabs live under:

- System overlays and roots: `Assets/Project/Prefabs/UI/Foundation`
- Full screens: `Assets/Project/Prefabs/UI/Screens`
- Reusable widgets (buttons, dropdowns, slots, tooltips):
  `Assets/Project/Prefabs/UI/Widgets`
- Flight and gameplay HUDs: `Assets/Project/Prefabs/UI/Hud`

The interaction prompt is part of the authored gameplay HUD; do not create a
parallel interaction-prefab folder unless a second composition actually reuses it.

Do not create a new screen prefab when a shared widget prefab plus a scene-level
binding is enough. Put reusable visuals in Common, gameplay-only presenters in
Gameplay, and full-screen compositions in Screens.

## UI Fonts And Authored Settings

- Editable font files and licenses: `Assets/Project/Art/Fonts/Source` and
  `Assets/Project/Art/Fonts/OFL.txt`.
- Generated TextMesh Pro font assets: `Assets/Project/Art/Fonts/Generated`.
- Theme assets: `Assets/Project/Design/UI/Styling`.
- Game-flow UI settings: `Assets/Project/Design/UI/Flow`.

`Assets/Project/UI` contains runtime code only. Do not place sprites, fonts,
prefabs, screenshots, or ScriptableObject assets beside runtime assemblies.

## Resource Node Visuals

Preferred runtime format:

- Models: `FBX`
- Textures: `PNG` or `TIF`
- Materials: Unity `.mat`
- Prefab: one authored prefab per resource node type.

Folders:

- Models: `Assets/Project/Art/Models/ResourceNodes`
- Textures: `Assets/Project/Art/Textures/ResourceNodes`
- Materials: `Assets/Project/Art/Materials/ResourceNodes`
- Prefabs: `Assets/Project/Prefabs/Gameplay/ResourceNodes`

Create model and texture folders only when imported runtime assets actually
exist; Unity primitive-based prefabs do not need empty placeholder folders.

Naming:

- `SM_Resource_<ResourceName>_<Variant>.fbx`
- `TX_Resource_<ResourceName>_<Map>.png`, where `<Map>` is `BaseColor`, `Normal`,
  or `MetallicSmoothness`
- `MAT_Resource_<ResourceName>.mat`
- `PF_<ResourceName>Node.prefab`

`<ResourceName>` matches the item and node definition subject, so the iron
deposit is `IronOre`, not `IronDeposit`. Variants are single letters starting
at `A`; one material is shared by every variant of a resource unless the
variants genuinely need different surfaces.

Resource node prefabs should include their visual hierarchy and interaction
collider. Gameplay values still come from `ResourceNodeDefinition`, not from
hard-coded prefab scripts.

### Importing A Resource Node Model

Do the whole intake from an editor script run in batch mode. Hand editing prefab
or material YAML is not supported here: the offsets are measured from the
imported mesh, and the numbers below are outputs, not constants. Two tools live
under `Assets/Project/Editor/Authoring`:

- `FarionResourceModelIntake` does the import, and its constants at the top of
  the file are what you retarget per resource. `Run` performs the whole intake;
  `RebuildPrefab` redoes only the material and prefab, which is what you want
  after changing an offset or a correction.
- `FarionResourcePreviewRender` renders the finished prefab from three angles
  against a ground plane at the spawner's height and writes PNGs to
  `FARION_PREVIEW_DIR`. Set `FARION_PREVIEW_PREFAB` to point it at another node.
  Run it without `-nographics`.

Look at those renders before booting the game. The first two iron models both
shipped defects that the renders caught immediately and that numbers alone did
not: one lay on its side, the other stood on edge like a coin.

1. Put the FBX at `Assets/Project/Art/Models/ResourceNodes` under the name
   above. Keep the authoring file in `ArtSource`.
2. Set the model importer to import no materials, cameras, lights, visibility,
   blend shapes, constraints, or animation, and leave secondary UVs off:
   resource nodes are streamed and never lightmapped. Their material comes from
   the prefab, not from the FBX.
3. Take the three PBR maps into `Assets/Project/Art/Textures/ResourceNodes`
   under `TX_Resource_<ResourceName>_<Map>.png`. Prefer the generator's sibling
   PNG exports over the maps embedded in the FBX; the embedded copies are often
   lossy JPEG rewrites of the same bake. Import base colour as sRGB, the normal
   map as `NormalMap`, and pack metallic into R and inverted roughness into A of
   a single linear `MetallicSmoothness` map, because URP's Lit shader reads only
   `_MetallicGlossMap`. Turn streaming mipmaps on for all three.
4. Bind them to `MAT_Resource_<ResourceName>.mat` with `_METALLICSPECGLOSSMAP`
   and `_NORMALMAP` on, `_EMISSION` off, and GPU instancing enabled. Up to 192
   nodes stream in at once, so they must batch.
5. Rebuild `PF_<ResourceName>Node.prefab` as a root that carries only the
   trigger `BoxCollider`, plus a `VisualRoot` child that carries the mesh, the
   material, and a convex `MeshCollider` so the deposit is solid. Both objects
   sit on the `Interactable` layer, which collides with `Explorer` but not with
   `CelestialSurface`, so a solid node blocks the player without fighting the
   terrain.
6. Normalise scale at intake. Uniformly scale `VisualRoot` so the model's
   largest dimension is `2` metres, and centre it horizontally on the deposit
   point. Generator exports arrive at arbitrary scale, so this is what keeps
   one resource's variants — and different resources — the same size in game.
7. Check which way is up before measuring anything. Unity only applies an axis
   conversion when the FBX header declares one, so a Z-up model exported as Y-up
   arrives standing on its edge and every number downstream is measured off the
   wrong silhouette. `SM_Resource_IronOre_A` needs `-90` on X. Apply the
   correction to the model instance before reading its bounds and the rest of
   the intake follows from it.
8. Set `VisualRoot`'s local Y so the model sinks `0.15` into the ground:
   `-surfaceOffset - 0.15 - boundsCenter.y + boundsExtents.y`, using the
   oriented renderer bounds after correction and normalisation. `surfaceOffset`
   is the value on the scene's `ResourceDepositRuntimeSpawner`, not the `0.85`
   default in the component: all three spawners in `SC_WorldZone` currently run
   `1.96`. Check the scene before trusting the default, and size the trigger
   from the same bounds plus `0.4` of padding. The burial has to stay ahead of
   the tilt the factory applies, or a flat-bottomed model lifts one edge clear
   of the ground: `3` degrees across a one metre half-width is about `0.05`.
9. Relink `visualPrefab` on the `ResourceNodeDefinition` from the script rather
   than trusting the prefab's root file id to survive, and clear `tintByBiome`.
   That flag exists only so untextured placeholder meshes stay distinguishable,
   and it overwrites authored albedo through a material property block.

`ResourceNodeFactory` gives every spawned node a yaw and a small tilt derived
from its deposit seed, so one mesh does not read as a field of clones. Nothing
has to be authored for that, but it does mean source art must look correct from
any horizontal angle: a model with a detailed front and a flat back will show
that back to the player about as often as not.

Source art expectations, in the order they cost time:

- Export with the correct up axis, and apply scale and rotation first. The first
  iron model arrived with a `100` scale on its FBX root and a Z-up header, so
  Unity added the `-90` X conversion itself. The replacement declared Y-up while
  still being modelled Z-up, so nothing was corrected and the rock stood on its
  edge.
- Model the deposit with a flat base that reaches past the visible silhouette.
  Generated rock clusters tend to come out as a dome on a cut plane, which is
  correct here, but only if that plane is the bottom.
- Name the mesh datablock after the asset. `output_unwrapped` and `Mesh_0` come
  straight from auto-unwrappers and generators, become the mesh name inside
  Unity, and cannot be renamed there.
- Model the deposit around its own centre with the visible bulk above it. The
  prefab offsets against the spawner, so an off-centre pivot has to be undone
  by hand.
- Bake emission only when the surface actually glows. The first iron FBX shipped
  a fully black `Baked_Emit.png`, which was still enough for Unity to turn the
  `_EMISSION` keyword on with a white emission colour. Until the map was bound
  the deposit rendered as a solid white blob in game, with its albedo invisible
  underneath. A black emission map is worth deleting, not shipping.
- Ship a normal map. Resource nodes are looked at from two metres away while
  harvesting, and the silhouette alone does not carry that distance.
- Check that the metallic bake contains something. Iron's arrived uniformly
  black, so the packed map's red channel does no work and the deposit is
  dielectric everywhere; only the smoothness in its alpha channel matters. That
  reads correctly for ore locked in rock, but it is worth knowing on purpose
  rather than by accident, and a resource that should look like exposed metal
  will need the bake fixed at the source.
- `2048x2048` is the working resolution for a two-metre prop. The first iron
  model came in at `4096x4096`, which made its FBX 32 MB and its two PNGs
  another 36 MB, for maps the importer then caps at `2048` in memory anyway.

Once every node type uses an authored model, drop
`ResourceDepositRuntimeSpawner.surfaceOffset` to zero, fold the offset into each
prefab, and delete `tintByBiome` along with its factory branch. Until that
happens, per-deposit scale variation cannot be added: scaling the node root
would also scale the `surfaceOffset` compensation baked into `VisualRoot`, and
the deposit would float or sink by up to half a metre.

## Character Animation

The explorer is animated as presentation only. The motor keeps ownership of
movement, jumping, and network authority; the Animator never moves anything.

Folders:

- Clips: `Assets/Project/Art/Animations/Characters/<Character>/Clips`
- Controller: `Assets/Project/Art/Animations/Characters/<Character>/Controllers`

Naming: `AN_<Character>_<Motion>_<Variant>.fbx`, `AC_<Character>.controller`.

Download Mixamo clips as FBX for Unity, without skin, 30 fps, no keyframe
reduction, In Place on, and one FBX per clip. Right-hand strafes and turns are
not downloaded: `FarionPlayerAnimationIntake` emits them as mirrored clips out
of the left-hand source file.

Run `Farion.Editor.Authoring.FarionPlayerAnimationIntake.Run` in batchmode after
dropping new clips in. It sets every clip to humanoid with the avatar copied
from `SM_PlayerExplorer_A`, rebuilds `AC_PlayerExplorer` from scratch, and binds
the controller plus `PlayerExplorerAnimator` onto `PF_PlayerExplorerCore`, which
the offline and network variants inherit. Editing the controller by hand works
until the next run of the intake, which overwrites it.

`PlayerExplorerAnimator` reads the motor's body-relative surface velocity, never
`Rigidbody.linearVelocity`, so a spinning planet or a moving ship does not make
a standing character run. Its clip speed fields carry the metres per second each
Mixamo clip was authored for; they are the tuning knob for foot sliding.

## Design References

Use external design boards for exploration, but import only the assets that the
game needs:

- Final UI exports go under `Assets/Project/Art/UI`.
- Final item/resource art goes under `Assets/Project/Art`.
- Authored gameplay contracts go under `Assets/Project/Design`.
- Do not put raw screenshots, mood boards, or temporary references in Prefabs or
  Runtime folders.
