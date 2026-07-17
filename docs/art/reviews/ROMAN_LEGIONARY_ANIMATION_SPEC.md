# Roman Legionary Animation Specification

Date: 2026-07-13. Source: `ArtSource/Blender/Romans/RomanCharacterSystem_v012_AnimationRework.blend`
→ `Assets/Art/Characters/Romans/Legionary/Models/RomanLegionary_Basic.fbx` (13 takes, 30 fps,
Generic rig, all in place, no root motion). Controller: `AC_RomanInfantry.controller`.

Gameplay is authoritative everywhere: the Animator displays state, never decides
targets, damage, movement, slots, or death.

## Animator parameters

| Param | Type | Set by | Meaning |
|---|---|---|---|
| `Loco` | int | `RomanLegionaryVisualController.Update` | locomotion role: 0 RearRankIdle, 1 FormationMarch, 2 FrontRankGuard, 3 CombatAdvance, 4 CloseRanksShuffle, 5 BrokenIdle, 6 BrokenRun |
| `LocoScale` | float | Update | clip playback scale = clamp(speed / stats.moveSpeed, 0.6–1.4); applied to March/Advance/Shuffle/BrokenRun state speed |
| `Attack` trigger + `AttackVariant` int | | `Soldier.OnAttack` event | 0 = thrust (~60%), 1 = over-shield (~40%) |
| `Hit` trigger | | `Soldier.OnHurt` event | skipped if current state is tagged "Attack" (committed strike) |
| `Die` trigger + `DeathVariant` int | | `Soldier.OnDeath` event | 0 = DeathA, 1 = DeathB, 50/50 |
| `Pivot` trigger | | `Formation.OnFacingSnapped` event | only while Ordered and stationary |

## Clips

| Clip | Role / trigger | Loop | Length | Pose summary | Returns to |
|---|---|---|---|---|---|
| ANIM_Roman_RearRankIdle | ordered soldier not on the fighting edge; default state | yes | 2.4 s | upright, shield vertical at left side, sword lowered; breathing + subtle weight shift | — |
| ANIM_Roman_FrontRankGuard | exposed/engaged soldier, stationary | yes | 1.6 s | shield raised vertical in front-left covering torso, sword cocked ready, knees bent | — |
| ANIM_Roman_FormationMarch | ordered movement with a destination | yes | 0.87 s | damped Meshy walk: short strides, shield arm near-still, minimal bounce | — |
| ANIM_Roman_CombatAdvance | guard-role soldier while moving | yes | 0.87 s | march legs (70%) under constant guard upper body | — |
| ANIM_Roman_CloseRanksShuffle | Reforming, or moving while Ordered with no move destination (auto-close / post-rotate slot corrections) | yes | 0.87 s | half-stride steps, controlled at-side carry | — |
| ANIM_Roman_Attack_Thrust | Attack, variant 0 | no | 0.67 s | shield planted; sword drawn back 4f, thrust past shield right edge at f9 (~45%), retract, guard | current Loco role |
| ANIM_Roman_Attack_OverShield | Attack, variant 1 | no | 0.73 s | shield planted; sword raised high behind at f6, strike over/beside shield top at f11 (~50%), guard | current Loco role |
| ANIM_Roman_HitLight | Hit | no | 0.37 s | upper-body recoil at f3, feet planted, back to guard | current Loco role |
| ANIM_Roman_Death_BackOrSide | Die, variant 0 (Meshy legacy death) | no | 2.4 s | falls backward | none — no exit |
| ANIM_Roman_Death_ShieldSide | Die, variant 1 (new) | no | 1.33 s | staggers, knees buckle, collapses onto shield side, stable end pose | none — no exit |
| ANIM_Roman_PivotStep | Pivot (explicit Rotate command) | no | 0.53 s | weight dip + two small foot adjustments; no root rotation in clip (gameplay rotates the root) | current Loco role |
| ANIM_Roman_Idle | BrokenIdle fallback (Meshy Combat_Stance) | yes | 1.37 s | open crouched combat stance | — |
| ANIM_Roman_Run | BrokenRun fallback (Meshy run) | yes | 0.53 s | free individual run | — |

## Role selection (computed from real gameplay data, in `ComputeLoco`)

- `FormationState.BrokenRanks` → BrokenIdle / BrokenRun (no rank restrictions).
- Guard role = `Soldier.IsEngaged` OR `NearestEnemyDist <= Formation.edgeEngageRadius`
  OR (front rank AND formation Attacking/Engaged AND `NearestEnemyDist <= 12 m`).
  Front rank = `slotIndex < Formation.columns` (row 0 is always the fighting edge in
  the current facing; the slot grid is rebuilt in the anchor frame on every rotate).
- Guard + moving → CombatAdvance; guard + stationary → FrontRankGuard.
- Otherwise moving → CloseRanksShuffle when Reforming or (Ordered and
  `!Formation.HasMoveDestination`), else FormationMarch.
- Otherwise → RearRankIdle.
- Moving uses hysteresis (on > 0.35 m/s, off < 0.2 m/s).

## Transition policy

- Loco↔Loco: no exit time, 0.15 s.
- Loco→Attack/Hit: no exit time, 0.05 s (responsive).
- Attack→Loco: exit time 0.85, 0.1 s — quick return to guard.
- Hit/Pivot→Loco: exit time 0.9, 0.1 s.
- AnyState→DeathA/B: 0.05 s, no self-transition, death states have **no** exit —
  Death always wins; the controller never fires Hit/Attack after death
  (`Soldier.Alive` checks) so nothing pulls a corpse out of Death.

## Root-motion policy

All clips in place; importer bakes root rotation + XZ position into pose
(`lockRootRotation`, `lockRootPositionXZ`); Y kept original (deaths lower the Hips
bone, not the root). `Animator.applyRootMotion = false`.

## Variation (visual only)

Random idle start phase (`Play(state, 0, Random.value)`), animator speed ±3%,
attack/death variant selection. Gameplay timing untouched.

## Known limitations

- Poses were authored procedurally via pose-bank blending; contact frames read
  correctly from the gameplay camera but are not hand-polished frame by frame.
- The over-shield strike reads as a compact diagonal cut at the shield's top
  edge rather than a tall overhead chop (intentionally restrained; revisit if
  it reads too subtle in battle).
- CombatAdvance/Shuffle legs are derived from the march cycle; minor foot
  sliding may remain when formation speed varies (upper-body stability was
  prioritized per the task).
- No layered upper/lower body split: a hit during an attack shows only the
  flash (reaction skipped while the "Attack"-tagged state plays).
- Broken Ranks uses the legacy Meshy clips (Idle/Run) as the documented
  fallback; a dedicated broken-ranks library is future scope.
- If a corpse despawns before Death_BackOrSide's 2.4 s finishes, the clip is cut
  (~2.35 s window) — pre-existing, visually negligible.
