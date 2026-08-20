# Farion Art Asset Contract

`Assets/Project/Art` contains only runtime-ready assets imported by Unity.
Editable sources such as `.blend`, layered paint files, bake intermediates, and
vendor originals belong under the repository-level `ArtSource` folder.

## Path Contract

Runtime art uses:

`Art/<AssetType>/<Domain>/<AssetName>`

- `AssetType` is a real Unity asset type, never a subject: `Fonts`, `Icons`,
  `Materials`, `Models`, `Shaders`, `Textures`, `VFX`.
- `Domain` is the visual family: `Celestial`, `Space`, `Spacecraft`,
  `SurfaceDecoration`, `ResourceNodes`, `UI`, `Lighting`, `VFX`.
- `AssetName` is the owning asset when several assets share a domain.

`UI` is a domain, not an asset type. UI textures live in `Textures/UI`, UI icons
in `Icons`, UI materials in `Materials/UI`. There is no `Art/UI` folder; a
subject-named asset-type folder would give every UI asset two valid homes.

No folder under `Assets` other than `Assets/Project/Resources` may be named
`Resources` — Unity force-includes such folders in every build.

Keep the hierarchy shallow. Do not split an asset's class and display name into
separate nested folders. Use `Spacecraft/CapitalShipFleet`.

Current spacecraft examples:

- `Models/Spacecraft/CapitalShipFleet`
- `Models/Spacecraft/PlayerStarterShuttle`
- `Materials/Spacecraft/CapitalShipFleet`
- `Materials/Spacecraft/PlayerStarterShuttle`
- `Textures/Spacecraft/CapitalShipFleet`
- `Textures/Spacecraft/PlayerStarterShuttle`

Generic assets may stop at the domain or capability level. Shared spacecraft
thruster effects therefore remain under `VFX/Spacecraft/Thrusters`; they do not
belong to one ship.

## Runtime naming

- Materials: `MAT_<Domain>_<Purpose>`
- Static meshes: `SM_<Domain>_<Purpose>_<Variant>`
- Textures: `TX_<Domain>_<Surface>_<Map>`
- Volume textures: `TX3D_<Domain>_<Purpose>`
- Volume profiles: `VP_<Purpose>`
- Icons: `IC_<Group>_<Purpose>`
- Fonts (generated TMP assets): `FONT_<Family>_<Weight>_SDF`
- Prefabs: `PF_<Domain>_<Purpose>`
- Visual effect graphs: `VFX_<Domain>_<Purpose>`

Every runtime asset carries its type prefix. A file that still holds a vendor's
download name (`pine_tree_01_bark_diff_4k.png`) has not finished intake and does
not belong under `Art`.

Texture map suffixes use `BaseColor`, `Normal`, `Roughness`, `AO`, `Height`,
and `Emission`. `BaseColorAlpha` marks a base-color map with opacity packed into
its alpha channel. Normal, roughness, AO, and height maps are linear data.
Base-color and emission maps are sRGB color data.

Procedural helper maps use purpose-specific suffixes. `SurfaceNoise` and
`EjectaMask` are linear data, not visible albedo. Celestial surface noise wraps;
localized ejecta masks clamp at their edges.

Materials must not cross domain boundaries. Celestial materials belong only to
celestial renderers; gameplay props use gameplay materials even while sharing
the same shader family.

Vendor originals under `Fonts/Source` are the one exception to the prefix rule:
they keep the foundry's file name so a font can be traced back to its release.

## Domain terms

`SurfaceDecoration` is the term for scattered surface props — the runtime types
are `SurfaceDecorationProfile`, `SurfaceDecorationPlacement`, and
`SurfaceDecorationRenderer`, and the art folders match them. It is not
`SurfaceFlora`: boulders and rocks are decoration, not flora. Its buckets are
biome affinity (`Arid`, `Frozen`, `Temperate`) plus `Shared` for props used by
more than one biome.

`Fleet` is reserved for the shared gameplay/progression identity.
`CapitalShip` identifies the physical ship in code and asset names. The first
capital ship's display name is Fleet, so `SM_CapitalShip_Fleet_A` is correct;
its owning art folder is `CapitalShipFleet`.

The player's ship is a `Shuttle` in both code and assets. `StarterShip` is not
used.
