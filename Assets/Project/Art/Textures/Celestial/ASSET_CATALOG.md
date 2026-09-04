# Celestial Surface Texture Catalog

Runtime texture names are semantic. The original source identifiers are kept
here so a texture set can be traced back to its imported source package.

| Runtime surface | Source identifier | Intended use | Array readiness |
| --- | --- | --- | --- |
| `Snow` | `Snow009A` | Packed snow and frozen crust | Ready |
| `Rock` | `Rock058` | Dry rock and rocky highlands | Ready |
| `Ice/Fractured` | `Ice001` | Broken or fractured ice | Ready; AO is optional |
| `Ice/Smooth` | `Ice003` | Broad smooth ice fields | Ready; AO is optional |
| `Lava/DarkCrust` | `Lava001` | Black crust-dominant lava | Ready as an alternate volcanic overlay |
| `Lava/Molten` | `Lava004` | Liquid molten lava | Ready as an alternate volcanic overlay |
| `Lava/Mixed` | `Lava005` | Balanced molten and dark crust | Ready; active volcanic overlay |
| `Sand/Coarse` | `Ground080` | Coarser disturbed sand | Ready |
| `Sand/Fine` | `Ground093A` | Fine smooth sand | Ready |
| `Rock/Mossy` | `TCom_Rock_Mossy_2K` (TextureCan) | Temperate basin ground: mossy rock, replaces the retired `Ground068` soil set | Ready |
| `Rock/CliffSnow` | `TCom_Rock_CliffSnow_2K` (TextureCan) | Snow-capped cliff rock for cold steep slopes (`SO_SnowRockSurfaceMaterial`) | Ready |
| `Temperate/Grass` | `Grass005` | Grass-covered ground | Ready as an alternate surface material |
| `Temperate/RockGreen` | `Rock063` | Green-toned temperate rock | Blocked: source maps are 2048x1024 |
| `Temperate/RockNeutral` | `Rock059` | Neutral temperate rock | Ready |

Moon helper maps are stored under `Moon`. `TX_Celestial_Moon_SurfaceNoise` is a
linear multi-channel procedural modulation map; `TX_Celestial_Moon_EjectaMask`
is a linear, clamped crater-ray mask. Neither asset is base-color artwork.

Surface material source sets are stored under `SurfaceMaterials`; biome
definitions do not own texture sets. Ocean wave normals are stored under
`Ocean` and follow the same linear, repeatable normal-map import contract as
surface normals. Atmosphere ray-march
jitter uses URP's package-owned `BlueNoise256/LDR_LLL1_0` texture. The former
local `TX_Celestial_Atmosphere_BlueNoise` image is a legacy sparse dither
pattern, not statistically distributed blue noise, and must not be assigned to
the atmosphere profile.

Source URLs and license evidence should be added to this catalog before a
third-party pack is distributed with a public build.
