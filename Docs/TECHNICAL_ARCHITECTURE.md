# Technical architecture

The architecture actually present in the repository. Systems not listed here
do not exist.

## Runtime environment

- Unity **6000.4.8f1**, Universal Render Pipeline **17.4.0**
- Input System **1.19.0** only (`activeInputHandler: 1` — the legacy input
  manager is off)
- Landscape orientation, mobile-first
- All gameplay code is in `Assets/Scripts/`, flat, one public type per file.
  There is no assembly definition — gameplay compiles into `Assembly-CSharp`.

## Scenes

| Scene | Role |
| --- | --- |
| `Assets/Scenes/Battle.unity` | The game. **The only scene in Build Settings.** |
| `Assets/Scenes/TestSkirmish.unity` | Smaller sandbox |
| `Assets/Scenes/SampleScene.unity` | URP template leftover |

`Battle.unity` is nearly empty by design: `BattleSetup` constructs the ground
plane, both armies, the camera rig, and the HUD at runtime. The scene's real
content is **serialized tuning values** that override C# field defaults.

## Major systems

### Battle lifecycle and spawning — `BattleSetup`

The root manager and a singleton (`BattleSetup.Instance`). Owns:

- `BattlePhase` (`Pre → Active → Ended`) and the result text
- Unit stat blocks (`meleeStats`, `archerStats`) and all battlefield-scale
  tuning: field extents, army separation, line offsets, spacing, deployment
- Directional damage configuration and `globalDamageScale`
- Per-team soldier registries (`blue`, `red`) and the `formations` list
- `SpawnSide` → `CreateFormation` → `SoldierFactory.Create`
- `EnsureEnvironment` — ground plane, camera configuration, camera rig
- `ValidateLineGaps`, a layout guard that warns on formation overlap

It attaches `EnemyCommander` and `FormationBannerManager` to itself on Awake.

### Formation harness — `Formation`

The largest and most connected type in the codebase (the knowledge graph ranks
it the top god node at 96 edges). It owns:

- The **state machine**: `FormationState` × `OrderType` × `FormationManeuverState`
- The **anchor** — `AnchorPos` / `AnchorRot`, the single authoritative facing.
  Directional damage, slot positions, soldier idle facing, and the ground arrow
  all read it. Nothing else writes it.
- The **slot grid** — `BuildSlots` generates offsets from `columns` and
  `spacing`; `FootprintHalfExtents` and `BoundingRadius` fall out of it.
- **Commands**: `IssueMove`, `IssueAttack`, `IssueCharge`, `IssueFace`,
  `IssueBreakRanks`, `IssueReform`, plus `RedirectMove` for in-flight edits.
- **Blocking queries** — `GetRotateBlock` / `GetChargeBlock` return *why* a
  command is unavailable, so the HUD button state and the actual outcome can
  never disagree.
- **Per-soldier behavior knobs** — `GetSlotWeight` and `GetAcquireRadius` are
  how formation state reaches individual soldiers.
- Cohesion subsystems: `UpdateAutoClose`, `UpdateRankReplacement`,
  `UpdateManeuver` (wheel/redress), `UpdateEngagedFacing` (defensive pivot),
  `UpdateVolley`, `UpdateDominantGroup`.

Its `Update` runs a fixed pipeline: maneuver → anchor movement → engagement →
volley → dominant group → engaged facing → state machine → rank replacement →
auto-close.

Two algorithms are `static` specifically so editor validation can exercise the
exact shipped code path: `WheelStep` and `ComputeDominantGroup` (union-find
clustering with hysteresis).

### Individual simulation — `Soldier`

A lightweight agent that blends slot attraction with local combat. Owns health,
target acquisition, attack cadence, melee variant selection, and a
**committed-action lock** that suspends slot correction while a soldier is
visibly striking or reacting. Exposes presentation-only events (`OnAttack`,
`OnHurt`, `OnDeath`) plus flags that animated visuals set to defer damage and
projectile release to authored contact frames.

`SoldierFactory` builds soldiers and resolves their visual prefab via
`Resources.Load` by name.

### Spatial queries — `BattleGrid`

A static uniform spatial partition (`TeamGrid` per side) rebuilt every
`FixedUpdate` from the team registries. It is what makes 1,280 soldiers viable:
`NearestEnemy`, `CollectEnemies`, and `CollectFriends` stay O(local density)
instead of O(n²). Neighbor queries use shared static scratch buffers — main
thread only, no per-frame allocation.

### Input — `PlayerCommander`

A pointer state machine (`Idle · Pending · CameraPan · CommandDrag ·
RotateDrag`) built on the Input System. Handles tap selection with double-tap
group building, command drags with live destination previews, two-finger
place-and-twist facing, camera pan, and pinch zoom. Touch slop and hit padding
are resolution- and zoom-aware.

It commands formations **only** through the public `Issue*` API.

### Enemy AI — `EnemyCommander`

Drives the Red side through the same public command API, with target scoring
and separate routines for the front line (`RunFront`), the archers
(`RunArchers`), and the reserves (`RunReserves`). No privileged access to
formation internals.

### UI — `BattleHUD`

Builds the entire uGUI canvas from code: selection info panels, the CHARGE /
REFORM / ROTATE command buttons with live interactivity, banner-mode and scene
switchers, start and end overlays, and a `SafeAreaFitter` for notched devices.

### Camera — `BattleCamera`

Orthographic rig: pan and zoom clamped to computed field bounds. Configured by
`BattleSetup` at 40° pitch with a negative near-clip plane (legal for
orthographic cameras) so the tilted frustum does not clip ground at full zoom
out.

### Presentation layer

Gameplay-authoritative, visuals subordinate:

- `RomanLegionaryVisualController`, `RomanArcherVisualController` — animator
  driving, keyed off gameplay state, never the reverse
- `EquipmentVisualSlot`, `CenturionCommandGestures`
- `FormationBannerManager` / `FormationBannerController` — banners tracking the
  dominant group, with visibility modes
- `FormationArrow`, `FormationDestinationPreview` — order feedback
- `FormationImposterRenderer` — merged-billboard LOD at far zoom
- `BattleVisuals`, `BattlefieldDecor`, `Projectile`

## Data structures

- `UnitStats` — `[System.Serializable]` plain class, not a ScriptableObject.
  Instances live as serialized fields on `BattleSetup`. It also owns
  `BuildCenturyRoles`, the data-driven century composition.
- `Team`, `SoldierRole`, `BattlePhase`, `FormationState`, `OrderType`,
  `FormationManeuverState`, `RotateBlock`, `ChargeBlock` — plain enums.

**There are no ScriptableObjects, no Addressables, and no save system.**
Runtime asset loading is `Resources.Load` by name only, from four call sites:
`SoldierFactory` (soldier visuals), `Projectile` (arrow prop),
`BattlefieldDecor` (ground texture), `FormationBannerController` (banner
sprites).

## Runtime asset layout

```
Assets/Art/Characters/Romans/
  Legionary/   Models/ Animations/ Prefabs/Resources/
  Archer/      Models/ Animations/ Prefabs/Resources/
  Shared/Materials/    MAT_Roman_* (skin, iron, brass, leather, wood, cloth)
Assets/Art/Environment/Resources/   TEX_Battlefield_*
Assets/Art/UI/Banners/Resources/    UI_Banner_{Blue,Red}_{Melee,Ranged}
Assets/Settings/                    URP pipeline assets, renderers, volumes
Assets/AncientArmies/               clean destination tree for NEW work
```

Every asset under `Assets/Art/` is currently referenced by a scene, prefab,
material, or controller, or is loaded by name at runtime. They are temporary
placeholders pending Tripo/Blender production, but the game does not run
without them.

## Validation and test structure

There is **no automated test suite** — no play-mode or edit-mode tests, despite
`com.unity.test-framework` being present.

Validation is editor tooling in `Assets/Editor/`, run manually from menus:

| Script | Checks |
| --- | --- |
| `FormationManeuverValidation.cs` | Wheel/redress geometry via the shipped `WheelStep` |
| `ClusterValidation.cs` | Dominant-group clustering via the shipped `ComputeDominantGroup` |
| `FacingValidation.cs` | Facing and directional-damage arcs |
| `Patch6Integration.cs`, `CenturionAssetIntegration.cs` | Asset wiring |
| `BannerSpriteImport.cs` | Banner sprite import settings |
| `McpAutoReconnect.cs` | Unity MCP editor connection |

Behavioral verification is **manual Play mode testing in `Battle.unity`**.

## Unity packages

Notable dependencies beyond Unity's built-in modules:

`com.unity.render-pipelines.universal` (17.4.0), `com.unity.inputsystem`
(1.19.0), `com.unity.ai.navigation`, `com.unity.timeline`, `com.unity.ugui`,
`com.unity.visualscripting`, `com.unity.test-framework`,
`com.coplaydev.unity-mcp` (git dependency), and the local
`com.tripo3d.unitybridge` (`file:../ThirdPartyPackages/TripoUnityBridge`).

Note: AI Navigation is installed but the game does not use a NavMesh — soldier
movement is custom steering plus the `BattleGrid` partition.
