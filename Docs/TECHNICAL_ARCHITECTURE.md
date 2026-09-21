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
- Directional damage configuration and `globalDamageScale` (pinned by
  `Battle.unity` since 2026-09-21)
- `moraleConfig` — the serialized reference to the `MoraleConfig` asset, and
  the only morale field it holds. If it is unassigned, `Awake` logs an error
  and morale stays off for the battle; there is no fallback to defaults.
- Per-team soldier registries (`blue`, `red` — lists of **soldiers**, not
  formations) and the `formations` list (both teams; destroyed formations stay
  in it with zero soldiers)
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
- **Morale-driven transitions** — it owns a `FormationMorale` (below) and every
  state change morale causes: `EnterRouting`, the rally through
  `CanStartReform` → `OfficerRallyPoint` → `BeginReform`, and the flee
  steering routing soldiers read (`GetFleeVelocity`, `KeepRouterInField`).
  `BeginReform` is the reform body shared by `IssueReform` and the rally.
- `CanStartReform` — **the shared automatic-reform start rule**: only active
  melee blocks a reform (engaged fraction ≤ `reformStartEngagedFraction`), with
  `reformRetryCooldown` after an aborted reform. Chunk A uses it for the rally
  only; `IssueReform` and `EnemyCommander.TryReformBroken` do not call it yet.

Its `Update` runs a fixed pipeline: maneuver → anchor movement → engagement →
morale → flee threat → volley → dominant group → engaged facing → state
machine → rank replacement → auto-close. The state machine checks for a morale
break before anything else.

Two algorithms are `static` specifically so editor validation can exercise the
exact shipped code path: `WheelStep` and `ComputeDominantGroup` (union-find
clustering with hysteresis). `ComputeDominantGroup` optionally reports which
points belong to the winning cluster; the rally uses that to find officers
inside the main pack.

### Morale — `FormationMorale` and `MoraleConfig`

`FormationMorale` is a plain C# class, one per formation, created by
`Formation` on its first frame and ticked every `tickInterval` while the
battle is Active. It is pure math: `Formation` feeds it the alive count and
`engagedCount` and reads `Value` back. It never changes formation state.

- **Base** is capped by a ceiling built from losses against starting strength
  (`TotalSpawned`). In melee (`engagedCount > 0`) the stricter contact ceiling
  applies; on rally, `rallyLossForgiveness` of the losses so far stops counting
  against the contact ceiling. Only recovery raises base: out of melee, and
  `recoveryLossCooldown` after the last casualty.
- **ShockDebt** — casualties add to it, and it decays at all times.
  `AddShock` is the single entry point for sudden pressure.
- **Value** = base − shock debt, clamped to 0–100. The rout and rally
  thresholds read it.

`MoraleConfig` holds every morale tunable. It is loaded through a serialized
reference on `BattleSetup` assigned in `Battle.unity`, not through
`Resources.Load`. The asset is `Assets/AncientArmies/Settings/MoraleConfig.asset`.
Because the numbers live in the asset, the scene pins only the reference.
Changing a C# default in `MoraleConfig.cs` affects only newly created assets —
edit the existing asset to tune.

`TemporaryOfficerRanks` hardcodes the rally priority (Signifer > Centurion >
Optio > Tesserarius > Cornicen) and the debug grade numbers. It is marked
temporary until the officer grade system arrives.

### Individual simulation — `Soldier`

A lightweight agent that blends slot attraction with local combat. Owns health,
target acquisition, attack cadence, melee variant selection, and a
**committed-action lock** that suspends slot correction while a soldier is
visibly striking or reacting. Exposes presentation-only events (`OnAttack`,
`OnHurt`, `OnDeath`) plus flags that animated visuals set to defer damage and
projectile release to authored contact frames.

While its formation is Routing, a soldier replaces its target-or-slot blend
with the formation's flee velocity, skips the leash, and ends each physics
step with `KeepRouterInField` so separation and collisions cannot push it off
the map. `DropTarget` releases its target and any staged hit or drawn shot the
moment the century routs.

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

It commands formations **only** through the public `Issue*` API. Routing
formations cannot be selected, and `PruneSelection` drops a selected century
the frame it routs.

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

Gameplay-authoritative, visuals subordinate. **All character art is currently
placeholder primitives** — `SoldierFactory` builds a capsule body plus a cube
weapon directly, with no prefab, Animator, or rig involved.

- `FormationBannerManager` / `FormationBannerController` — banners tracking the
  dominant group, with visibility modes
- `FormationArrow`, `FormationDestinationPreview` — order feedback
- `FormationImposterRenderer` — merged-billboard LOD at far zoom
- `BattleVisuals`, `BattlefieldDecor` (flat sand colour), `Projectile` (sphere)
- `MoraleDebugOverlay` — **debug only**. An `OnGUI` readout of each
  formation's morale and state, plus temporary officer grade numbers. It
  installs itself on scene load and nothing references it, so deleting the
  file removes it. Toggled by `MoraleConfig.showDebugMorale`.

`Soldier` retains the hooks animated visuals used — `deferMeleeImpact`,
`deferRangedRelease`, `suppressTint`, `suppressFallRotation`. Nothing sets them
now, so they stay `false` and damage/release resolve immediately. They are the
seam for reattaching animated visuals later; `Soldier.CacheVisualParts` already
handles both a single `VisualRoot` child and the Body/Weapon primitive pair.

## Data structures

- `UnitStats` — `[System.Serializable]` plain class, not a ScriptableObject.
  Instances live as serialized fields on `BattleSetup`. It also owns
  `BuildCenturyRoles`, the data-driven century composition.
- `MoraleConfig` — **the project's first and only ScriptableObject** (see
  Morale above).
- `Team`, `SoldierRole`, `BattlePhase`, `FormationState` (now including
  `Routing`, appended last), `OrderType`, `FormationManeuverState`,
  `RotateBlock`, `ChargeBlock` — plain enums.

**There are no Addressables and no save system.**
Runtime asset loading is `Resources.Load` by name from exactly one call site:
`FormationBannerController` (banner sprites). Soldier visuals, the arrow, and
the ground are all built from primitives and code-created materials.

## Runtime asset layout

```
Assets/Art/UI/Banners/Resources/    UI_Banner_{Blue,Red}_{Melee,Ranged}
Assets/Settings/                    URP pipeline assets, renderers, volumes
Assets/AncientArmies/               clean destination tree for NEW work
Assets/AncientArmies/Settings/      MoraleConfig.asset
```

The four banner sprites are the only art assets left in the project — they are
UI readability, not character art. Everything a soldier or the battlefield
renders is generated at runtime, so the game has no character-art dependency
at all until Tripo/Blender production lands in
`Assets/AncientArmies/Art/Characters/`.

## Validation and test structure

There is **no automated test suite** — no play-mode or edit-mode tests, despite
`com.unity.test-framework` being present.

Validation is editor tooling in `Assets/Editor/`, run manually from menus:

| Script | Checks |
| --- | --- |
| `FormationManeuverValidation.cs` | Wheel/redress geometry via the shipped `WheelStep` |
| `ClusterValidation.cs` | Dominant-group clustering via the shipped `ComputeDominantGroup` |
| `FacingValidation.cs` | Live soldier-vs-anchor facing deviation (Play mode) |
| `BannerSpriteImport.cs` | Banner sprite import settings |
| `McpAutoReconnect.cs` | Unity MCP editor connection |

Behavioral verification is **manual Play mode testing in `Battle.unity`**.
The Unity CLI (`unity command …`, through `com.unity.pipeline`) can also drive
a running editor — enter Play mode, run C# with `eval`, read the console —
which is how the Chunk A morale runs were scripted.

## Unity packages

Notable dependencies beyond Unity's built-in modules:

`com.unity.render-pipelines.universal` (17.4.0), `com.unity.inputsystem`
(1.19.0), `com.unity.ai.navigation`, `com.unity.timeline`, `com.unity.ugui`,
`com.unity.visualscripting`, `com.unity.test-framework`,
`com.coplaydev.unity-mcp` (git dependency), `com.unity.pipeline`
(0.7.0-exp.1, editor bridge for the Unity CLI), and the local
`com.tripo3d.unitybridge` (`file:../ThirdPartyPackages/TripoUnityBridge`).

Note: AI Navigation is installed but the game does not use a NavMesh — soldier
movement is custom steering plus the `BattleGrid` partition.
