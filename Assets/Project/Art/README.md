# Farion Art Asset Contract

`Assets/Project/Art` contains only runtime-ready assets imported by Unity.
Editable sources such as `.blend`, layered paint files, bake intermediates, and
vendor originals belong under the repository-level `ArtSource` folder.

## Path Contract

Runtime art uses:

`Art/<AssetType>/<Domain>/<AssetName>`

- `AssetType`: `Audio`, `Materials`, `Models`, `Shaders`, `Textures`, `UI`, or
  `VFX`.
- `Domain`: the broad visual family, such as `Celestial`, `Space`, or
  `Spacecraft`.
- `AssetName`: the owning asset when several assets share a domain.

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
- Icons: `IC_<Purpose>`
- Prefabs: `PF_<Domain>_<Purpose>`

Texture map suffixes use `BaseColor`, `Normal`, `Roughness`, `AO`, `Height`,
and `Emission`. Normal, roughness, AO, and height maps are linear data.
Base-color and emission maps are sRGB color data.

Procedural helper maps use purpose-specific suffixes. `SurfaceNoise` and
`EjectaMask` are linear data, not visible albedo. Celestial surface noise wraps;
localized ejecta masks clamp at their edges.

Materials must not cross domain boundaries. Celestial materials belong only to
celestial renderers; gameplay props use gameplay materials even while sharing
the same shader family.

`Fleet` is reserved for the shared gameplay/progression identity.
`CapitalShip` identifies the physical ship in code and asset names. The first
capital ship's display name is Fleet, so `SM_CapitalShip_Fleet_A` is correct;
its owning art folder is `CapitalShipFleet`.
