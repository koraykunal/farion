# Farion

A quiet expedition game about readable spaceflight, planetary survival, and
fleet operations. Unity 6000.4.5f1, URP, FishNet listen-server co-op, FMOD.

## Repository layout

| Path | Owns |
| --- | --- |
| `Assets/Project` | Everything authored for this game — code, art, design data, prefabs, scenes, localization, tests, docs |
| `Assets/Plugins`, `Assets/FishNet`, `Assets/com.rlabrecque.steamworks.net` | Vendored third parties, reinstalled rather than edited |
| `ArtSource` | Editable art sources kept out of the Unity import pipeline: `.blend`, paint files, bake intermediates, vendor originals |
| `FMODProject` | FMOD Studio authoring project; Unity holds only the built banks |
| `Packages`, `ProjectSettings` | Unity package manifest and project settings |

`Assets/Project` is split by runtime ownership, one folder per assembly area:
`App`, `Audio`, `Core`, `Gameplay`, `Multiplayer`, `Rendering`, `Simulation`,
`UI`, plus `Editor` and `Tests`. Authored content mirrors that shape under
`Design`, `Prefabs`, and `Art`.

## Documentation

| Document | Read it for |
| --- | --- |
| [`Assets/Project/Docs/Architecture.md`](Assets/Project/Docs/Architecture.md) | Assembly graph, namespace and naming rules, runtime ownership |
| [`Assets/Project/Docs/AssetIntake.md`](Assets/Project/Docs/AssetIntake.md) | Where a new item, icon, model, or definition goes |
| [`Assets/Project/Art/README.md`](Assets/Project/Art/README.md) | Art path and file-naming contract |
| [`Assets/Project/Docs/SceneSetup.md`](Assets/Project/Docs/SceneSetup.md) | Authored scene composition |
| [`Assets/Project/Docs/UIArchitecture.md`](Assets/Project/Docs/UIArchitecture.md) | Screen routing, focus, localization |
| [`Assets/Project/Docs/BetaRoadmap.md`](Assets/Project/Docs/BetaRoadmap.md) | Planned work — never describe it as implemented |
| [`PRODUCT.md`](PRODUCT.md), [`DESIGN.md`](DESIGN.md) | Product register and visual design tokens |
| [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) | Third-party licences |

## Verification

The project has no CI; verification runs Unity in batchmode. The editor must be
closed.

```sh
UNITY="C:/Program Files/Unity/Hub/Editor/6000.4.5f1/Editor/Unity.exe"

# compile
"$UNITY" -batchmode -quit -nographics -projectPath . -logFile build.log

# authored scene and prefab validation
"$UNITY" -batchmode -quit -nographics -projectPath . -logFile validate.log \
  -executeMethod Farion.Editor.Validation.FarionProjectValidator.ValidateFromMenu

# tests -- omit -quit, and drop -nographics for PlayMode
"$UNITY" -batchmode -nographics -projectPath . -runTests \
  -testPlatform EditMode -testResults edit.xml -logFile edit.log
```

`FarionProjectValidator` is the only automated check that covers authored scenes
and prefabs. Run it after any change to a scene, prefab, or localization table.
