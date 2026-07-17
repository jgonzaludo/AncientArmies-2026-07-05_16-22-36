# Patch 1 — Facing and Locomotion Synchronization

**Branch:** `v1-prototype` (HEAD `2d6ae0a v1.3.3`)
**Date:** 2026-07-14
**Status:** implemented, statically verified, awaiting manual tests
**Git safety:** pre-existing dirty files at start: `ProjectSettings/ProjectSettings.asset` (modified,
untouched by this patch), untracked `.agents/`, `skills-lock.json` (untouched). No commit, no push,
no staging, no branch switch performed.

## Inspected

Scripts: `Soldier.cs`, `Formation.cs`, `SoldierFactory.cs`, `PlayerCommander.cs`,
`FormationArrow.cs`, `RomanLegionaryVisualController.cs`, `RomanArcherVisualController.cs`,
`BattleSetup.cs` (movement, rotation, Rotate command, facing storage).
Prefabs: `VIS_Roman_Legionary_Basic.prefab`, `VIS_Roman_Archer_Basic.prefab` (YAML + live instantiation).
Models: `RomanLegionary_Basic.fbx(.meta)`, `RomanArcher_Basic.fbx(.meta)` (axis import, clip table).
Animators: `AC_RomanInfantry.controller`, `AC_RomanArcher.controller` (all states, tags,
speed-parameter bindings, motions).
Clip measurements: performed in-editor (edit mode, `clip.SampleAnimation` on a temp prefab
instance, stance-foot world-velocity sampling at 60 samples/clip, then destroyed).

## Actual hierarchy (both unit types)

```text
<Formation>_S<slot>          ← gameplay root: CapsuleCollider, Rigidbody (FreezeRotation),
│                              Soldier. THE dynamically rotated transform (UpdateFacing).
├── SelectionDisc
└── VisualRoot               ← VIS_Roman_*_Basic instance, localRotation = identity, Animator
    │                          + visual controller. Never rotated at runtime by anything.
    ├── GEO_Roman*           ← mesh node: constant importer axis conversion,
    │                          localRotation Euler(-90, 0, 0), scale 0.01
    └── RIG_Roman*           ← armature root: the SAME constant conversion; bones +
                               SkinnedMeshRenderer live below (archer adds
                               bone-parented hand props). No other rotated nodes.
```

The formation-facing ground arrow is a separate world-space object reading
`Formation.CurrentHeading` (→ `AnchorForward` when idle) every LateUpdate.

## Axis findings

- Both models' visible forward at prefab identity **is local +Z** — verified empirically:
  toe-minus-heel vectors at idle t=0 = (0.09, 1.00) legionary / (0.00, 1.00) archer in XZ.
- The only rotated nodes are the two constant importer conversion nodes per prefab
  (`GEO_*` mesh + `RIG_*` armature siblings, each −90°X / 0.01 scale;
  `bakeAxisConversion: 0`, the standard Blender-FBX arrangement). Blender −Y facing →
  `-Z Forward, Y Up` export → Unity +Z holds.
- **No competing runtime corrections exist and none were added.** Melee and archer use the
  identical arrangement. The pivot clips have **0.0° net hips yaw** (measured first vs last
  frame), so they cannot undo or double the root's turn.
- No prefab, FBX, or animator asset changes were needed; assets are untouched by this patch.

## Root causes

1. **Rotate command turns the arrow, not the models:** `PlayerCommander` is not phase-gated
   (Rotate works before Start is pressed), but `Soldier.Update` returned early unless
   `Phase == Active`, so `UpdateFacing` never ran pre-battle. The arrow reads formation state
   directly in LateUpdate and turned regardless.
2. **Soldiers not facing movement / melee facing odd directions / archers walking sideways:**
   old `UpdateFacing` faced **any live acquisition target unconditionally** — with archer
   range 48 m an archer marching across the field twisted toward a distant target (sideways
   skating); melee near an acquisition faced it even when the real behavior was marching.
3. **Archer mid-draw wobble:** facing followed the *current* acquisition target, while the
   deferred shot flies to the *pending* target captured at attack time — a retarget during
   the draw rotated the archer away from the arrow it was about to loose.
4. **Locomotion gliding:** the loco states are correctly bound to the `LocoScale` speed
   parameter in both controllers (verified: legionary March/Advance/Shuffle/BrokenRun,
   archer Walk/Shuffle; stationary loops and one-shots unbound), but the value fed to it was
   `clamp(speed / stats.moveSpeed, 0.6, 1.4)` — i.e. playback ≈ 1.0 at full march speed.
   Measured implied ground speeds of the clips at 1× playback are far lower (below), so feet
   moved ~3.5× slower than the ground.

## Measured clip reference speeds (stance-foot, 1× playback)

| Clip | length | implied ground speed |
|---|---|---|
| ANIM_Roman_FormationMarch | 0.833 s | **0.76 m/s** |
| ANIM_Roman_CombatAdvance | 0.833 s | 0.55 m/s |
| ANIM_Roman_CloseRanksShuffle | 0.833 s | 0.43 m/s |
| ANIM_Roman_Run | 0.500 s | 2.54 m/s |
| ANIM_Archer_FormationWalk | 1.033 s | **0.66 m/s** |
| ANIM_Archer_CloseRanksShuffle | 1.033 s | 0.35 m/s |

Gameplay ground speed while marching ≈ 2.6–2.7 m/s (anchor 2.6, soldier caps 2.7/3.0 —
unchanged by this patch).

## Changes

### Soldier.cs — facing resolver (gameplay root remains the single facing authority)

The visual prefab is an identity child of the gameplay root, the root is the only
dynamically rotated transform, and the visual controllers rotate nothing — this already
matches the "one authoritative facing pipeline" goal, so the fix is in the resolver rules,
not the hierarchy. Priorities now:

1. **Dead** — `Update` exits, `suppressFallRotation` visuals keep death-entry facing (unchanged).
2. **Pending shot (archer mid-draw)** — face `pendingShotTarget` (the shot actually being
   released) until release/cancel; immune to retargeting. 480°/s.
3. **Active combat** — melee: face target when `IsEngaged` or target ≤ 1.5× strikeRange
   (fighting reach), NOT a distant acquisition while marching. Ranged: face target in bow
   range only while not meaningfully moving (the firing line), 360–480°/s.
4. **Meaningful movement** — face actual `Rigidbody` velocity (the authoritative gameplay
   steering output), 360°/s, gated by hysteresis: start facing movement above 0.45 m/s,
   release below 0.25 m/s — slot-correction noise cannot flip the stance.
5. **Idle** — face `formation.AnchorForward` (canonical facing = same source as the ground
   arrow), 240°/s (≈ the 0.53 s pivot clip's pace for a quarter turn).
   Dead zone: near-zero directions retain the last valid facing.

`UpdateFacing` now also runs when `Phase != Active`, so a pre-battle Rotate turns the models.
Interpolation stays `Quaternion.RotateTowards` (frame-rate independent, shortest path).

**Documented gameplay coupling:** `AcquireTarget`'s mild ahead-preference (behind-score
×1.6) reads `transform.forward` — the rule is untouched, but its *input* facing is now
saner (a marching soldier prefers enemies ahead of its march). Directional damage reads
**formation** `AnchorForward` only (`BattleSetup.GetDirectionalMultiplier`) and is
unaffected by anything in this patch.

### Visual controllers — locomotion playback calibration

`LocoScale = clamp(actualSpeed / clipReferenceSpeed, min, max)` per loco category, with the
measured reference speeds above as serialized defaults:

- Legionary: march/advance max 2.4×, shuffle max 2.2×, broken run max 1.6×, min 0.6×.
- Archer: walk/shuffle max 2.2×, min 0.6×.
- Stationary loops return 1 (they don't bind LocoScale anyway).
- `Animator.speed = Random(0.97–1.03)` per-soldier variety retained (±3% on everything,
  pre-existing, documented).

Archer `FiringReady` gating now matches gameplay shootability (any state except
Withdrawing/Reforming with an enemy inside bow range) instead of only Engaged/Attacking —
an Ordered formation firing back no longer looks asleep between shots.

### New editor tooling

`Assets/Editor/FacingValidation.cs` — menu **Ancient Armies → Validate Facing Pipeline**:
audits LocoScale bindings (loco states bound, nothing else), prefab single-conversion-node
arrangement, and (in Play Mode) per-formation worst soldier facing deviation. Menu-driven
only; no per-frame cost.

## Performance

No new per-frame allocations, searches, GetComponents, or physics queries. The resolver
adds one sqrMagnitude + a few comparisons per soldier per frame; playback calibration is a
switch + divide. All references and parameter hashes were already cached.

## Known limitations

- The authored march/walk strides are compact (0.63 m / 0.68 m per cycle), so at full
  gameplay speed the clamped playback (2.4× / 2.2×) still under-covers ground by ~30%.
  Obvious skating is removed; perfect foot-locking needs longer authored strides —
  **Patch 3 scope**, deliberately not touched here.
- Withdrawing formations backpedal by design; soldiers now face their movement (i.e. the
  retreat direction). Formation-maneuver geometry is **Patch 4 scope**.
- Melee sword-path quality, bow-string alignment, dagger equipment states: Patches 2–3.
- Runtime behavior is NOT verified in Play Mode (constraint); manual tests below.

## Verification

- Static Roslyn compile (Assembly-CSharp + Assembly-CSharp-Editor): see final report.
- Unity import/recompile + Console check: see final report.
- No damage / range / speed / AI / targeting / spacing / reform / pivot-geometry / camera
  values changed — the only gameplay-adjacent change is *when a soldier's transform faces
  its target*, per the facing rules above.

## Future roadmap (context only — NOT acted on)

- **Patch 2:** rigid shield attachment, correct dagger model, Stowed/Drawing/Active/Returning
  equipment states, reusable weapon-display logic.
- **Patch 3:** attack animation quality (slash/thrust readability), hand-to-string and
  nock alignment, bow flex, dagger anims, march/walk stride re-authoring.
- **Patch 4:** reform priority vs combat animations (`CanPerformSlotCorrection`),
  historically plausible formation turns (in-place turns, edge wheels, 180° reversals).
- **Patch 5:** lower camera pitch (Clash-of-Clans-like readability) with bounds/raycast updates.
