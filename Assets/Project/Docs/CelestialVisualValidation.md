# Celestial Visual Validation

This is the final quality gate for celestial art and rendering changes. Keep
camera, field of view, exposure, and time of day fixed while comparing revisions.
Change one profile group at a time so a regression can be traced to its owner.

## Automated integrity gate

Run `Farion > Validation > Validate Project` before a build or visual review.
The build preprocessor runs the same validation automatically.

The celestial checks cover:

- required surface, planet, atmosphere, ocean, and star profiles;
- the material-to-shader contract for terrestrial, moon, and star rendering;
- surface-material base-color, normal, roughness, AO, height, and emission import settings;
- moon helper-map and ocean-normal import settings;
- the URP package-owned BlueNoise256 atmosphere input;
- active ocean and atmosphere renderer features, shader references, and pass order;
- build-scene profile, renderer, camera, scaled-space star, and adaptive surface
  patch references.

An automated pass confirms structural integrity. It does not replace the Game
View review below.

## Controlled Game View review

Use `SC_Expedition` and capture the same views before and after tuning.
Do not evaluate a surface from only one distance.

### Terrestrial planet and lava

1. Close surface: confirm texel density reads as ground detail rather than a
   repeated carpet, and normals do not dominate the albedo.
2. Medium altitude: confirm surface-material color, roughness, AO, and height
   remain coherent without obvious triplanar seams.
3. Orbit: confirm large land masses remain readable and the material does not
   collapse into uniform high-frequency noise.
4. Confirm basalt desert regions contain plausible sand/regolith/rock
   composition; they must not become lava simply because the biome is basaltic.
5. Lava: confirm the selected overlay appears only where the volcanic state
   mask is active. `Mixed` is the default black-crust/molten blend; `Molten`
   remains an alternate more-liquid art set.
6. Cross every LOD threshold slowly and confirm the silhouette changes smoothly
   while material boundaries remain fixed.
7. Move the planet farther from and nearer to the star, rebuild its visual, and
   confirm long-term temperature/aridity and biome coverage change coherently.
8. Use `Log Surface Coverage Report` on `CelestialBodyVisual`; accept only a
   report with zero missing biome, material, and visual-material weights.

### Near-surface terrain stability

1. Approach until `CelestialSurfacePatchSystem > Surface Mode Active` becomes
   true. Confirm the `Terrain Mesh` renderer is disabled. At medium altitude,
   `Collision Authority` must remain `GlobalFallback` and its collider must stay
   enabled even though adaptive patches are visible.
2. Confirm `Active Patch Count` is non-zero, `Deepest Active Level` reaches the
   profile maximum near the observer, and only the local neighborhood owns
   enabled patch colliders. `LocalAuthoritative` is valid only when
   `Local Collision Coverage Ready` is true; in that state the global collider
   must be disabled.
3. On an enabled patch, confirm its `MeshFilter.sharedMesh` and
   `MeshCollider.sharedMesh` are the same object.
4. Stand still for at least ten seconds, then walk slowly across patch
   boundaries. The ground must not disappear, pulse, or shift vertically, and
   lighting must not reveal seams.
5. Fly through the entry and exit altitudes slowly. The global sphere and patch
   terrain must hand over once per crossing without overlap or rapid toggling.
6. Repeat the descent above normal landing speed. Collision authority must stay
   `GlobalFallback`; the ship must not cross the visible surface while patch
   meshes are still staging.
7. Enter and exit the spacecraft on the surface. The collision observer must
   switch between the spacecraft and explorer without an authority gap.
8. Inspect ridges from ground, medium, and grazing views. Peaks may be steep,
   but single-cell pyramids, razor edges, and normals that change at patch
   borders are failures.

### Moon

1. Inspect a cratered close view for believable flat-versus-steep normal
   response.
2. Inspect a medium view for ejecta rays that support the craters without
   appearing painted over the entire sphere.
3. Orbit the camera around the poles and texture seams.
4. Cross every moon LOD threshold slowly and confirm crater readability is
   preserved without a visible material-scale change.

### Ocean

1. Orbit view: confirm the ocean is clipped by terrain and never draws through
   the far side of the planet.
2. Coastline view: confirm shallow color and depth opacity transition without a
   bright halo or a hard circular mask.
3. Grazing view: confirm Fresnel and specular reflection follow the light while
   the two normal maps break up the highlight without creating a noisy carpet.
4. Cross the water surface in both directions and confirm the underwater pass
   changes without a flash, missing frame, or inverted depth.

### Atmosphere

1. Orbit limb: confirm the halo is smooth, thin enough to preserve the planet
   silhouette, and free of stepping or sparse-dot artifacts.
2. Daylight horizon: confirm the atmosphere is visible but does not wash out
   terrain contrast.
3. Terminator and night side: confirm scattering follows the star direction
   and falls off cleanly instead of lighting the full ring uniformly.
4. Move the camera slowly and confirm blue-noise jitter removes bands without
   visible crawling.

### Star

1. Confirm the star remains in the physical light direction while the visual
   proxy stays inside the camera far clip.
2. Confirm the photosphere has a readable limb, granulation, broad cells, and
   restrained sunspots without obvious UV seams.
3. Confirm motion is slow enough to avoid shimmer at distance.
4. With bloom enabled, confirm the glow supports the disc instead of clipping
   it into a flat white circle.

## Acceptance rule

A celestial change is complete only when the automated integrity gate passes
and all relevant controlled views pass in Game View. If a view fails, record
the body, camera distance, failing layer, and screenshot before changing a
profile. Tune the owning profile first; do not compensate in an unrelated
renderer, light, or scene object.
