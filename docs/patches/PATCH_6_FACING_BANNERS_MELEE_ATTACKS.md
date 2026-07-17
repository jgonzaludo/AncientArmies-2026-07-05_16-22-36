# Patch 6 — Facing Authority, Formation Banners, Legionary Melee Rework

**Branch:** `v1-prototype` (from `7494288 v1.3.8`)
**Date:** 2026-07-15

## Part 1 — Authoritative auto-facing

`Formation.AnchorRot` was already what directional damage, slot orientation,
and soldier idle facing read; this patch makes every remaining surface read it
too, so nothing can display a second opinion about where a formation faces.

- **`Formation.IsAutoFacing`** — true while combat steers the anchor rotation:
  `Attacking` with a live `attackTarget` (chase/hold rotation) or `Engaged`
  (contact-centroid wheeling at `engagedReorientSpeedDeg`).
- **`Formation.GetRotateBlock()`** — explicit rotate availability:
  `None / AutoFacing / Broken / Busy / Destroyed`. `PlayerCommander.
  CanEnterRotateMode` consumes it, and rotate mode is force-exited the moment
  a selected formation loses eligibility mid-preview (target acquired,
  engagement, break ranks, defeat). The HUD's existing disabled styling greys
  the button for every non-`None` state; when auto-facing ends the button
  re-enables and the formation keeps its final facing (AnchorRot persists —
  there is no stored "manual facing" to snap back to).
- **`FormationArrow`** now points along `AnchorForward` (was the
  travel-heading guess `CurrentHeading`, which has been deleted) and hides
  entirely during Broken Ranks. Smoothing is inherited: every auto-facing
  path rotates AnchorRot via `RotateTowards`, so the arrow turns with the
  soldiers rather than snapping.

## Part 2 — Formation banners (replacing text labels)

`FormationLabel` (floating "Red Swords 3 / Ordered 50/50" text) is deleted.
`FormationBanner` replaces it: a billboarded swallow-tail pennant in the
faction color with a procedural white icon — gladius for melee, bow-and-arrow
for archers — floating 5.2 m above the troops. No colliders anywhere in the
banner, so formation tapping is untouched.

**Dominant-group tracking.** `Formation.ComputeDominantGroup` (static, like
`WheelStep`, so editor validation exercises the shipped path) runs union-find
connected components over the soldier proximity graph (link distance =
`spacing * clusterLinkFactor`, default 2.5×) on a `clusterInterval` (0.4 s)
timer — ≤ n²/2 sqr-distance checks for n ≤ 50, no per-update allocations
(grow-only static scratch arrays). Hysteresis: the cluster nearest the
previous center keeps ownership unless a rival is `clusterSwitchFactor`
(1.3×) larger, so 26/24 never flip-flops while 40/10 resolves instantly.
The banner follows `AnchorPos` while structured (Ordered / Attacking /
Withdrawing / Reforming) and `DominantGroupCenter` while Engaged or Broken,
with exponential smoothing (`1 − exp(−3·dt)`) between interval updates.

**Reform rally.** `IssueReform` now anchors on a forced-fresh
`DominantGroupCenter` instead of the mean of every survivor: the 40-soldier
majority stands still and the separated 10 are recalled to it. The rally
point is computed once at the moment Reform is issued and never re-derived
mid-reform.

**Lifecycle.** The banner spawns with the formation, hides the frame
`soldiers.Count == 0`, and is destroyed with it; battle Restart reloads the
scene, so banners are rebuilt with the re-rolled faction/unit visuals.

New serialized tuning on `Formation`: `clusterLinkFactor = 2.5`,
`clusterInterval = 0.4`, `clusterSwitchFactor = 1.3`.

Editor validation: `Ancient Armies / Validate Dominant Group`
(`ClusterValidation.cs`) — 40/10, 26/24 hysteresis, 25/25, 35/15 transfer,
cold start, single survivor, empty, intact block: **8/8 PASS**.

## Part 3 — Legionary melee attack rework

The two old strikes (short rising stabs that read as upward punches — the
old thrust's hand traveled 0.37 m forward while *rising*, never passing the
shield plane) are fully re-authored in Blender
(`RomanCharacterSystem_v016_MeleeAttackRework.blend`, exported over
`RomanLegionary_Basic.fbx`; previous FBX archived as
`Archive/RomanLegionary_Basic_pre_v016_backup.fbx`), plus one new variant:

| Clip | Frames @30fps | Impact | Motion |
|---|---|---|---|
| `ANIM_Roman_Attack_Thrust` | 0–19 (0.63 s) | f9 (0.30 s) | shield opens a narrow lane, torso+shoulder drive a straight forward thrust — blade tip reaches 1.0 m dead forward at chest height, retracts along the same line |
| `ANIM_Roman_Attack_OverShield` | 0–21 (0.70 s) | f12 (0.40 s) | shield stays planted; sword raised above the upper right, chops forward-down (blade tip z 1.66 → 0.40) |
| `ANIM_Roman_Attack_DiagonalSlash` | 0–23 (0.77 s) | f13 (0.43 s) | slight lane opening, high-right prep, compact diagonal cut to lower-left across the enemy body line |

All three book-end on the FrontRankGuard f0 pose with **0.00° deviation on
every bone** (verified by evaluating the authored actions against the staged
key poses), no root motion, quaternion keys only, shield hand planted
through the over-shield strike (≤ 9 cm sway from torso rotation). The
engaged guard loop itself (`ANIM_Roman_FrontRankGuard`) already satisfied
the stance spec and is untouched. Archer clips untouched.

**Variant selection + contact-frame damage (gameplay-owned).** `Soldier`
picks the variant at attack commit — weighted `meleeVariantWeights`
(serialized, default 0.5/0.25/0.25 thrust/over-shield/diagonal) with one
re-roll if a variant would play three times in a row — and defers the damage
by the authored per-variant contact delay (`MeleeImpactDelay` = 0.30/0.40/
0.43 s), mirroring the archer's pending-shot pattern: validation, cooldown,
and the committed-action lock all charge at commit; only the damage moment
moves. The pending hit lands only if attacker and target are both still
alive; a dying attacker cancels it. `RomanLegionaryVisualController` sets
`soldier.deferMeleeImpact = true` and displays `soldier.MeleeAttackVariant`
— the clip shown and the damage frame can never disagree. Capsule-fallback
soldiers keep instant damage. Attack cooldown (1.4 s) and damage values are
unchanged; the 0.85 s melee lock still covers the longest clip.

Animator: `AC_RomanInfantry` gains an `AttackDiagonalSlash` state (tag
`Attack`, no LocoScale binding) with transitions mirroring `AttackThrust`
under `AttackVariant == 2`.

## Camera

Pitch lowered again per direction: 42° → 40° (was 45° briefly mid-patch).
Ground-focus preserved: position `(0, 42, -50.64)` = focus − forward ·
(42 / sin 40°). `BattleCamera` pan clamps are pitch-derived and adapt
automatically.

## Files

- Modified: `Formation.cs`, `PlayerCommander.cs`, `FormationArrow.cs`,
  `Soldier.cs`, `RomanLegionaryVisualController.cs`, `BattleSetup.cs`,
  `AC_RomanInfantry.controller`, `RomanLegionary_Basic.fbx` (+ meta)
- Added: `FormationBanner.cs`, `Editor/ClusterValidation.cs`,
  `docs/art/reviews/renders/patch_6_melee_attacks/` (32 renders)
- Deleted: `FormationLabel.cs`
- ArtSource (local-only): `RomanCharacterSystem_v016_MeleeAttackRework.blend`

## Verification

- Static Roslyn compile: PASS (main + editor assemblies); in-editor compile
  clean (no `error CS` after the final reload).
- `Ancient Armies / Validate Dominant Group`: **8/8 PASS**.
- `Ancient Armies / Validate Facing Pipeline`: clean (no BAD BINDING — the
  new attack state correctly does not bind LocoScale — no MISSING MOTION,
  prefab axis-conversion nodes intact).
- Scripted Play Mode sweep (Battle scene, all PASS):
  - Rotate available when Ordered; `IssueFace` small-turn works.
  - Attack order → `Attacking`, `IsAutoFacing=true`, `RotateBlock=AutoFacing`,
    `CanEnterRotateMode=false`; `IssueFace` while auto-facing is ignored.
  - Arrow shown and **0.0°-aligned to AnchorForward** during auto-facing.
  - Break Ranks → `RotateBlock=Broken`, arrow GameObject inactive.
  - Reform → `Reforming/Busy`; dominant group = full formation when intact.
  - Target wiped → next frame `Ordered`, `RotateBlock=None`, facing retained.
  - Battle start and Restart: exactly 10 banners each time, no duplicates,
    correct colors/icons (screenshot-verified, incl. bow vs sword icons).
  - Staged 40/10 broken split: `DominantGroupCount≈40`, center on the
    40-group (not the midpoint), banner within 4 m of it; `IssueReform`
    anchored on the 40-group.
  - Live melee: all three attack states observed playing simultaneously
    (thrust 5 / over-shield 4 / slash 1 mid-swing across one sample);
    engaged fighters' last-variant spread 15/8/3 ≈ the 50/25/25 weights.
  - Console: zero gameplay errors/warnings across restarts, splits, combat,
    and full-battle wipes.
- Human eyeball items (device zooms, extended feel checks): tracked in
  `PATCH_6_FACING_BANNERS_MELEE_MANUAL_TESTS.md`.

## Known limitations

- The banner pennant is billboarded and constant world-size; at extreme
  minimum zoom it occupies a modest portion of a packed formation's screen
  area (readable, not occluding taps). Tune `FormationBanner.Width` if
  desired.
- The chibi rig's short arms cap the thrust's physical hand travel; the
  forward read comes from blade direction, torso/shoulder drive, and the
  1.0 m tip reach (validated in renders at review and gameplay angles).
- Mid-fight the over-shield chop passes just inside the shield's inner edge
  from some angles rather than strictly over its top corner — reads
  correctly at gameplay zoom.
