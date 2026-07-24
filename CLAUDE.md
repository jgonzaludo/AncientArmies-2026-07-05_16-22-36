# Ancient Armies — agent working rules

Unity 6 (6000.4.8f1), URP, mobile-first landscape, fixed isometric-style
orthographic camera. Mobile-first real-time tactical game.

## Source of truth

Read these before substantial work; they override any assumption:

- `Docs/CURRENT_GAME_SPEC.md` — implemented gameplay, controls, states
- `Docs/TECHNICAL_ARCHITECTURE.md` — systems and responsibilities
- `Docs/ART_DIRECTION.md` — visual target
- `Docs/ART_PIPELINE.md` — the active art pipeline and its approval gates
- `Docs/DECISIONS.md` — recorded decisions and their dates

Do not expand scope beyond the current spec without explicit approval.

## Project structure

```
Assets/Scripts/       all runtime gameplay code (flat, one class per file)
Assets/Editor/        editor-only validation tools; never referenced at runtime
Assets/Scenes/        Battle.unity is the only scene in Build Settings
Assets/Art/           current runtime art actually referenced by the game
Assets/Settings/      URP pipeline assets and volume profiles
Assets/AncientArmies/ clean destination tree for NEW production work
ArtSource/            art production source, outside Assets/ — never shipped
ThirdPartyPackages/   local Unity packages (Tripo Unity DCC Bridge)
```

## Core invariants

These are design law, not preferences:

- Player control is formation-level.
- Soldiers are real, individually simulated entities.
- Formations are a behavioral and tactical harness, not a rendering trick.
- Ordered formations are predictable; combat creates local disorder.
- Breaking ranks increases individual freedom and chaos.
- Reforming requires sufficient disengagement from melee.
- Dead soldiers are removed; survivors close ranks when reforming.
- Outcomes are understandable, not dominated by invisible randomness.

## Gameplay code boundaries

- `Formation` owns the state machine, the anchor (position + facing), and the
  slot grid. It is the only writer of `AnchorPos` / `AnchorRot`.
- `Soldier` owns individual movement and combat. It reads formation state
  through `GetSlotWeight` / `GetAcquireRadius` — it never drives the formation.
- `PlayerCommander` and `EnemyCommander` only ever call the public `Issue*`
  command methods. They never mutate formation internals directly.
- `BattleSetup` owns spawning, the battle lifecycle, and the team registries.
- Visual controllers (`Roman*VisualController`, banners, arrows, previews,
  impostors) are presentation only. Gameplay stays authoritative: never let a
  visual read drive a gameplay decision, and never gate gameplay timing on
  Animator state names.

Do not rewrite these systems to add a feature. Extend at the seams.

## Validating changes

- Compile: the Unity Editor is the authority. `Assets/Editor/*Validation.cs`
  holds the existing static checks; run them from the editor menus.
- Behavior: **Play mode in `Assets/Scenes/Battle.unity`.** Verify in Unity
  rather than assuming code works.
- **`Battle.unity` serializes values that override C# field defaults.** Before
  changing a default in a `MonoBehaviour`, grep the scene for the field — if
  it is pinned there, editing the C# default alone changes nothing. Adding a
  *new* field is the reliable way to make a C# default win.

## Naming conventions

- Scripts: `PascalCase.cs`, one public type per file, matching the filename.
- Runtime prefabs loaded by name: `VIS_*` (soldier visuals), `PROP_*` (props),
  `UI_*` (sprites), `TEX_*` (textures), `MAT_*` (materials), `AC_*` (Animator
  Controllers).
- Anything loaded via `Resources.Load` must sit in a `Resources/` folder and
  keep its exact name — renaming silently breaks runtime loading. Current
  string loads live in `SoldierFactory`, `Projectile`, `BattlefieldDecor`, and
  `FormationBannerController`.

## Unity `.meta` handling

- Every asset has exactly one `.meta`. Delete or move an asset and its `.meta`
  **together**, never one alone.
- Never leave an orphaned `.meta`, and never delete a `.meta` while keeping the
  asset — both break GUID references in scenes and prefabs.
- Before deleting anything under `Assets/`, confirm it is unreferenced: grep
  its GUID across scenes, prefabs, materials, controllers, and ScriptableObjects.
- Never hand-edit a GUID in an existing `.meta`.

## Art pipeline rules

- Tripo Studio Pro generates; Blender owns rigs and masters; Unity receives FBX.
- **Raw Tripo output is not production-ready.** It is a starting point.
- **`.blend` is the editable source of truth** for characters, rigs, and
  animation. Unity assets are exports, not originals.
- **Production character exports normally enter Unity as FBX.**
- **Generated assets may not enter production runtime folders without explicit
  approval.** The Tripo Unity Bridge drops assets in for preview and testing
  only; approved production art is placed deliberately.
- Never put raw Tripo exports, reference images, Blender working files, Mixamo
  downloads, or review renders under `Assets/` — they belong in `ArtSource/`.
- **Do not revive Meshy or AssetHub as the active pipeline without an explicit
  decision** recorded in `Docs/DECISIONS.md`.

## Do not modify casually

- `ProjectSettings/` — especially input, graphics, and quality settings
- `Packages/manifest.json` and `Packages/packages-lock.json`
- `Assets/Settings/` — URP renderer and pipeline assets
- `ThirdPartyPackages/` — vendored third-party code; do not edit its source
- `Assets/Scenes/Battle.unity` — serialized tuning lives here

## Git and the test gate

Work on `dev`. Versions are `0.MINOR.PATCH` git tags on `main`. Commits follow
Conventional Commits with game scopes.

**Never commit, push, merge, or tag a gameplay change until the owner has
manually tested it in Play mode and explicitly approved it.** Ask first even
for docs-only changes.

## Knowledge graph

`graphify-out/` (local-only, not committed) holds a prebuilt knowledge graph of
this codebase. Query it before grepping broadly:

- `graphify query "<question>"` — traversal answer
- `graphify-out/GRAPH_REPORT.md` — communities, god nodes, audit trail

Refresh after substantial code changes: `graphify . --update`.
