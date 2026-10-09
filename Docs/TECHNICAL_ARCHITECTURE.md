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

- `BattlePhase` (`Pre → Active → Ended`), `BattleTime`, and the result text.
  `Update` ends the battle on **line collapse** (`HasCollapsed`: fallen =
  Routing or destroyed ≥ `collapseFraction` of that team's formations) or on
  the time limit (`CountIntact` decides), with the reason in `ResultText`.
- `DamageZone` (`Front / Flank / Rear`) and `GetDirectionalZone`, decided by
  the two formations' **facings** (dot product against `CombatConfig`'s arcs;
  left/right flank by the side the attacker's facing comes from), with a
  soldier-position fallback only for BrokenRanks / Routing attackers.
  `ZoneIndexOf` gives the same answer per enemy formation for surround
  counting. The damage multiplier derives from the zone, and the zone rides
  with every blow to the casualty's morale shock
- Unit stat blocks (`meleeStats`, `archerStats`) and all battlefield-scale
  tuning: field extents, army separation, line offsets, spacing, deployment
- `moraleConfig`, `officerRanks` and `combatConfig` — the serialized
  references to the three ScriptableObject assets, the only such fields it
  holds. Combat numbers (`globalDamageScale`, the directional multipliers and
  arcs) live in `CombatConfig`; nothing about combat is scene-pinned any more. If either is
  unassigned, `Awake` logs an error and that system stays off for the battle;
  there is no fallback to defaults.
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
  `IssueBreakRanks`, plus `RedirectMove` for in-flight edits. There is no
  reform command; `BeginReform` runs only from the state machine.
- **Blocking queries** — `GetRotateBlock` / `GetChargeBlock` return *why* a
  command is unavailable, so the HUD button state and the actual outcome can
  never disagree.
- **Per-soldier behavior knobs** — `GetSlotWeight` and `GetAcquireRadius` are
  how formation state reaches individual soldiers.
- Cohesion subsystems: `UpdateAutoClose`, `UpdateRankReplacement`,
  `UpdateManeuver` (wheel/redress), `UpdateEngagedFacing` (defensive pivot),
  `UpdateVolley`, `UpdateDominantGroup`.
- **Morale-driven transitions** — it owns a `FormationMorale` (below) and every
  state change morale causes: `EnterRouting` (which also applies contagion to
  same-team formations in range), the automatic reform from BrokenRanks
  through `CanStartReform` → `ReformAnchorPoint` → `BeginReform`, the optional
  rally (`routedCanRally`), and the flee steering routing soldiers read
  (`GetFleeVelocity`; `KeepRouterInField` only when rally is on).
- `CanStartReform` — **the one automatic-reform rule for both teams**: only
  active melee blocks a reform (engaged fraction ≤ `reformStartEngagedFraction`),
  with `reformRetryCooldown` after an aborted reform. The AI issues no reform
  orders.
- `ReformInPlace` — after `pursuitMaxSeconds` in BrokenRanks with contact still
  above `reformStartEngagedFraction`, rebuild the slot grid around the main
  pack where it stands (planted on `ReformAnchorPoint`, facing the nearest
  enemy formation) and return to Engaged; the walk-back reform runs instead
  when contact has dropped.
- `ReformAnchorPoint` — Signifer in the main pack if `reformOnStandardFirst`,
  else the highest-grade officer in the main pack, else the pack's center;
  `CommandingOfficer` is the highest-grade survivor.
- `NotifyDeath(soldier, zone)` feeds the casualty's zone-weighted shock and
  the officer's `deathShock`; `NotifyEscaped` removes a router that left the
  map without counting a casualty (`EscapedCount`).
- `UpdateEngagement` also counts our engaged soldiers per enemy formation:
  the number of distinct enemy formations is the outnumber input, and the
  formations with at least `surroundMinEngagedFraction` of our men in contact
  contribute their facing-based zone to the surround count.
- Targets: `NearestEnemyFormation` skips Routing formations unless asked to
  include them (pursuit and pack retargets do); `IssueAttack` and
  `IsChargeable` refuse routing targets; a chased target that routs or dies
  clears the order and the formation holds.

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
battle is Active. It is pure math: `Formation` feeds it casualties
(`RecordCasualty` with the zone weight), extra pressure (`AddShock`), the
alive count, `engagedCount` and the number of enemy formations engaged, and
reads `Value` back. It never changes formation state.

- **Base** is capped by a ceiling built from losses against starting strength
  (`TotalSpawned`). In melee (`engagedCount > 0`) the stricter contact ceiling
  applies; on rally, `rallyLossForgiveness` of the losses so far stops counting
  against the contact ceiling. Only recovery raises base: out of melee, and
  `recoveryLossCooldown` after the last casualty.
- **ShockDebt** — panic. Zone-weighted casualties, officer deaths and
  contagion add to it through `AddShock`, the single entry point for sudden
  pressure; it decays at all times, divided by
  `1 + outnumberDecaySlowdown × (enemy formations engaged − 1)`. Everything
  through `AddShock` is multiplied by `panicScale` and rate-limited by
  `maxShockPerSecond`: the excess waits in `QueuedShock` and lands on later
  ticks, never discarded. Officer deaths pass `bypass` — unscaled, uncapped.
  Ranged kills carry a `ranged` flag from `Projectile` through `TakeDamage`
  and weigh `arrowShockWeight` instead of a zone multiplier. Casualty weights
  (front 0.25, flank, rear, the 3 s burst rule) are resolved inside
  `RecordCasualty(zone, ranged)`. Surround shock comes from
  `Formation.UpdateEngagement`'s zone count (front / left flank / right flank /
  rear, by the anchors of the enemy formations in contact) and is unscaled
  and uncapped. Every addition is tallied per `ShockSource`; `PrimaryCause`
  and the totals are written by `BattleRecorder` with each rout (`Grind` =
  net base morale forced down by the ceiling).
- **Value** = base − shock debt, clamped to 0–100. The rout and rally
  thresholds read it.

`MoraleConfig` holds every morale tunable. It is loaded through a serialized
reference on `BattleSetup` assigned in `Battle.unity`, not through
`Resources.Load`. The asset is `Assets/AncientArmies/Settings/MoraleConfig.asset`.
Because the numbers live in the asset, the scene pins only the reference.
Changing a C# default in `MoraleConfig.cs` affects only newly created assets —
edit the existing asset to tune.

`OfficerRanks` is the second ScriptableObject
(`Assets/AncientArmies/Settings/OfficerRanks.asset`, referenced from
`BattleSetup.officerRanks`): one entry per `SoldierRole` with an open-ended
integer grade and a `deathShock`, plus `reformOnStandardFirst`. Grades are
command structure, not morale, which is why it is a separate asset.
`TemporaryOfficerRanks` is gone.

### Individual simulation — `Soldier`

A lightweight agent that blends slot attraction with local combat. Owns health,
target acquisition, attack cadence, melee variant selection, and a
**committed-action lock** that suspends slot correction while a soldier is
visibly striking or reacting. Exposes presentation-only events (`OnAttack`,
`OnHurt`, `OnDeath`) plus flags that animated visuals set to defer damage and
projectile release to authored contact frames.

While its formation is Routing, a soldier replaces its target-or-slot blend
with the formation's flee velocity and skips the leash. With rally off (the
default) it runs off the map and `Escape` removes it from the battle once it
is 2 m past the field edge; with rally on, `KeepRouterInField` holds it inside
after separation and collisions. `DropTarget` releases its target and any
staged hit or drawn shot the moment the century routs. `TakeDamage` carries
the `DamageZone` of the blow (staged melee hits remember theirs; projectiles
are always Front) so the death reports its zone to the formation.

`SoldierFactory` sets every soldier's capsule collider radius from
`CombatConfig.soldierColliderRadius` (0.5 m): the global personal-space knob,
independent of the visual size. It builds soldiers and resolves their visual prefab via
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
formations cannot be selected or drag-targeted, and `PruneSelection` drops a
selected century the frame it routs.

### Enemy AI — `EnemyCommander`

Drives the Red side through the same public command API, with target scoring
and separate routines for the front line (`RunFront`), the archers
(`RunArchers`), and the reserves (`RunReserves`). No privileged access to
formation internals. Its Blue roster excludes Routing formations, so nothing
it does targets a router, and it issues no reform orders — reform is
automatic in `Formation`.

### UI — `BattleHUD`

Builds the entire uGUI canvas from code: selection info panels, the CHARGE /
ROTATE command buttons with live interactivity, banner-mode and scene
switchers, start and end overlays, the tide bar (`BattleSetup.BattlePower` per team,
morale-weighted, presentation-only), the countdown (faint until
`timerWarningSeconds`), and a `SafeAreaFitter` for notched devices.

### Camera — `BattleCamera`

Orthographic rig: pan and zoom clamped to computed field bounds. Configured by
`BattleSetup` at 40° pitch with a negative near-clip plane (legal for
orthographic cameras) so the tilted frustum does not clip ground at full zoom
out.

### Presentation layer

Gameplay-authoritative, visuals subordinate. **Soldiers are KayKit animated
characters** (interim art, see `DECISIONS.md` 2026-10-08), with the old capsule
+ cube placeholders kept behind a switch (`KayKitVisualConfig.useKayKitVisual`
in `Assets/AncientArmies/Visuals/KayKit/Resources/`, loaded by name so
`Battle.unity` is untouched).

KayKit visual pipeline:

- Raw CC0 files live in `Assets/ThirdParty/KayKit` (Knight, Mannequin_Medium
  for the future custom body, four Rig_Medium animation FBXs plus the
  Simulation one for Cheering, props, licenses). One shared 23-bone skeleton.
- `Assets/Editor/KayKitBuild.cs` (`task=KayKitBuild.Build` through the file
  trigger) regenerates everything under `Assets/AncientArmies/Visuals/KayKit`:
  importer settings (Generic, avatar copied from the Knight, root motion
  baked out, only the used clips kept, loop flags set), the **merged skinned
  mesh** (the Knight's skinned parts, minus Cape and HelmetVisor, merged into
  ONE mesh on the shared bones; for melee the sword and shield are rigidly
  skinned into it as well, so a soldier is one draw call; the archer mesh is
  body only and carries the bow as a separate prop), Unity mesh LODs
  (`MeshLodUtility`), two **team textures** (one atlas per team: the dark
  cloth/armour tiles and the shield tiles take the team hue; skin, leather,
  gold and bright steel stay), three materials (blue, red, bow; URP Lit, no
  specular or reflections, a faint emission from the same texture because the
  scene's ambient is very dark), two animator controllers (one state per
  clip, no transitions: the visual component cross-fades by state hash), and
  the two prefabs `VIS_KayKit_Melee` / `VIS_KayKit_Archer`.
- No `MaterialPropertyBlock` anywhere (it would break the SRP Batcher): team
  colour is purely the two team materials; the old HP tint is therefore off
  (`suppressTint`).
- `SoldierFactory.TryCreateKayKit` instantiates the prefab as the identity
  child `VisualRoot` (the child the impostor swap already looks for) and adds
  `KayKitSoldierVisual` to the soldier root.
- `KayKitSoldierVisual` (presentation only, on the root so it keeps running
  while the model is asleep) maps simulation state to clips — idle, blocking
  when engaged, walk/run scaled by speed, stab/chop/diagonal by
  `MeleeAttackVariant`, block-hit vs hit by `LastHitZone`, death then pose,
  bow aim/draw/release, cheer for the winning team — with a random start
  offset per soldier. The only thing it writes is the bow release, via a
  timer, because archers run with `deferRangedRelease`; melee runs with
  `deferMeleeImpact` (the existing variant and impact-delay logic).
- `KayKitCrowdLod` (zoom tiers from the banner manager's 1080p soldier pixel
  height, hysteresis): >=110 px mesh LOD0, 70-110 LOD1, 45-70 LOD2 and
  animator every 2nd frame, 30-45 LOD3, animator every 3rd frame and every
  2nd man not drawn, <=30 px the impostor takes over (raised from 12/15).
  Men outside the view (plus margin) stop animating and drawing. Animators
  are `CullCompletely`; renderers do not update when offscreen; skin quality
  is 2 bones.

- `FormationBannerManager` / `FormationBannerController` — banners tracking the
  dominant group, with visibility modes; the wavering halo lives here (a 4×
  glow plus a thick ring behind the badge, additive `HaloAdditive` shader
  loaded from Resources, hysteresis `haloEnter`/`haloExit`, pulse 1.5–3 Hz in
  alpha and scale, a one-time flash on entering the band, never dimmed), and
  a Routing formation renders no banner
- `OfficerGradeMarkers` — grade numbers above the officers of the selected or
  inspected formation (self-installing `OnGUI`, reads only)
- `FormationArrow`, `FormationDestinationPreview` — order feedback
- `FormationImposterRenderer` — merged-billboard LOD at far zoom
- `BattleVisuals`, `BattlefieldDecor` (flat sand colour), `Projectile` (sphere)
- `MoraleDebugOverlay` — **debug only**. An `OnGUI` readout of each
  formation's morale and state, plus temporary officer grade numbers. It
  installs itself on scene load and nothing references it, so deleting the
  file removes it. Toggled by `MoraleConfig.showDebugMorale`.

`Soldier` exposes the seams animated visuals use — `deferMeleeImpact`,
`deferRangedRelease`, `suppressTint`, `suppressFallRotation`, `OnAttack` /
`OnHurt` / `OnDeath`, `LastHitZone` / `LastHitRanged`. `KayKitSoldierVisual`
sets them when the KayKit visual is on; with the switch off they stay `false`
and damage/release resolve immediately. `Soldier.CacheVisualParts` handles
both a single `VisualRoot` child and the Body/Weapon primitive pair.

## Data structures

- `UnitStats` — `[System.Serializable]` plain class, not a ScriptableObject.
  Instances live as serialized fields on `BattleSetup`. It also owns
  `BuildCenturyRoles`, the data-driven century composition.
- `MoraleConfig`, `OfficerRanks` and `CombatConfig` — **the project's three
  ScriptableObjects** (see Morale above). `OfficerRanks` also carries a
  per-role `healthMultiplier` applied at spawn (`Soldier.ScaleMaxHealth`).
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
Assets/AncientArmies/Settings/      MoraleConfig.asset, OfficerRanks.asset, CombatConfig.asset
```

The four banner sprites are the only art assets left in the project — they are
UI readability, not character art. Everything a soldier or the battlefield
renders is generated at runtime, so the game has no character-art dependency
at all until Tripo/Blender production lands in
`Assets/AncientArmies/Art/Characters/`.

## Knowledge graph workflow

`graphify-out/` holds a code + docs knowledge graph (allowlisted by
`.graphifyignore`: C# under `Assets/`, `Docs/*.md`, `CLAUDE.md`; art and Unity
YAML never enter it). Git hooks rebuild it on commit and checkout. How to use
it — query before reading files, update before trusting it with uncommitted
edits — is the "Context recovery" section of `CLAUDE.md`.

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
| `ScenarioFileTrigger.cs` | File-driven scenario runs (see Test harness) |

Behavioral verification is **manual Play mode testing in `Battle.unity`**.
### Test harness (editor and development builds)

Scripted battles need no editor bridge. Write `Temp/scenario_request.txt`:

```
presets=P0:; P1:shockMultiplier=1.5,globalDamageScale=0.15
seed=12345
speed=2
renderOff=true
aiOn=true
```

`Assets/Editor/ScenarioFileTrigger.cs` (polls while the editor is unfocused)
enters Play mode and calls `ScenarioRunner.Run`, which runs the presets back
to back — overrides are any `MoraleConfig` field or `globalDamageScale`,
applied in memory and restored afterwards — then writes
`Temp/scenario_done.txt` and exits Play mode. The single word `refresh` in the
request file reimports and recompiles instead; compile errors land in
`Temp/scenario_compile.txt` (empty = clean) and `Temp/scenario_ready.txt` is
rewritten on every script load.

- `SimSeed` seeds `UnityEngine.Random` (the simulation's only random source)
  in `BattleSetup.Awake` before anything spawns: same seed + settings = same
  battle **at the same speed**.
- `BattleRecorder` logs in simulation time — first contact per formation
  pair, every state change with morale / shock / alive / casualty %, officer
  deaths, the end and its reason — and writes one JSON per battle with a
  summary to `Temp/BattleLogs/` (editor) or `persistentDataPath/BattleLogs/`
  (device; the path is printed to the console).
- `SimSpeed.Set(multiplier, renderOff)` / `Reset()`.
- `scenario=flank` (pin + flank) and `scenario=lines` (two full lines, no
  manoeuvre; casualty and interleave snapshots at 60 / 120 / 180 s) are the
  targeted checks; the summary also carries front / flank / rear hit counts.

**Safe maximum speed is 2×.** Almost all simulation (Formation pipeline,
engagement scan, morale tick, attacks, projectiles, AI) runs in `Update` on
`Time.deltaTime`; only soldier movement is fixed-step. Seed 12345 gave first
rout 6.8 s after contact at 2× and 179.9 s at 10× — large frame steps change
outcomes. The dev HUD speed button is therefore 1× / 2× only. Moving the sim
to fixed steps is logged under "Not yet decided" in `DECISIONS.md`.

## Unity packages

Notable dependencies beyond Unity's built-in modules:

`com.unity.render-pipelines.universal` (17.4.0), `com.unity.inputsystem`
(1.19.0), `com.unity.ai.navigation`, `com.unity.timeline`, `com.unity.ugui`,
`com.unity.visualscripting`, `com.unity.test-framework`,
and the local
`com.tripo3d.unitybridge` (`file:../ThirdPartyPackages/TripoUnityBridge`).

Note: AI Navigation is installed but the game does not use a NavMesh — soldier
movement is custom steering plus the `BattleGrid` partition.
