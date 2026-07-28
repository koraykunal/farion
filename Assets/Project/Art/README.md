# Farion Art Asset Contract

`Assets/Project/Art` contains art-authoring inputs and runtime-ready assets.

## Folder ownership

- `Audio`: source clips, mixers, and audio clip-set assets.
- `Materials`: runtime materials grouped by visual domain.
- `Models`: imported runtime meshes. Authoring files belong under `Source`.
- `Shaders`: Farion-owned shaders grouped by rendering domain.
- `Source`: Blender files and other editable authoring sources.
- `Textures`: runtime textures grouped by visual domain.
- `UI`: icons and UI textures.

## Runtime naming

- Materials: `MAT_<Domain>_<Purpose>`
- Static meshes: `SM_<Domain>_<Purpose>`
- Textures: `TX_<Domain>_<Surface>_<Map>_<Resolution>`
- Icons: `IC_<Purpose>`

Texture map suffixes use `BaseColor`, `Normal`, `Roughness`, `AO`, `Height`,
and `Emission`. Normal, roughness, AO, and height maps are linear data.
Base-color and emission maps are sRGB color data.

Procedural helper maps use purpose-specific suffixes. `SurfaceNoise` and
`EjectaMask` are linear data, not visible albedo. Celestial surface noise wraps;
localized ejecta masks clamp at their edges.

Materials must not cross domain boundaries. Celestial materials belong only to
celestial renderers; gameplay props use gameplay materials even while sharing
the same shader family.
