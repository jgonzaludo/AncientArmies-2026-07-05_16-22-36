# Patch 4 — Combat-Aware Reform and Coherent Formation Maneuvers

**Branch:** `v1-prototype` (from `badee95 v1.3.6`) · **Date:** 2026-07-15
**Play Mode:** not used. Pre-existing untouched: `.agents/`, `skills-lock.json`.

## Inspection record (pre-change authority flow)

- Slot correction: `Soldier.FixedUpdate` blends an `Arrive(slot)` desire with
  a combat desire by `Formation.GetSlotWeight(state)` (Ordered 1.0,
  Attacking 0.95, Engaged 0.12/0.35/0.75 by contact, Broken 0.05,
  Withdrawing/Reforming 1.0), integrated through a capped-acceleration
  Rigidbody velocity. **No action-lock concept existed** — a soldier mid
  strike/draw/stab/hit was pulled toward its slot at full strength: the
  root cause of combat sliding (Problem A).
- Rotate: `IssueFace` snapped `AnchorRot` and globally nearest-slot
  remapped for EVERY angle. For 90° turns this teleports the slot grid
  sideways and every soldier walks a straight line to a rotated
  destination — the liquid-melt look (Problem B). Reform completion
  required EVERY soldier within 0.6 m (one straggler stalled it).
- Auto-close ran even while Engaged, globally remapping slots mid-fight.
- Attacks are gameplay-timed events (damage at the attack tick; archer
  projectile released at the visual release frame via the Patch 1/2
  pending-shot system) — so committed windows are derivable from gameplay,
  never from Animator state names.

## Problem A — action priority and correction rules

**Authoritative source (`Soldier`, gameplay-derived only):**
`IsInCommittedCombatAction` (= lock timer active OR a pending bow shot),
`IsImmediatelyThreatened` (= existing engagement scan), `Alive`,
`CanPerformStrongSlotCorrection`, `CanPerformWeakSlotCorrection`.
No second state machine — the same events that deal damage set the locks.

**Committed windows (centralized consts, seconds):** melee strike 0.85;
archer sidearm stab 0.7; bow draw = the whole pending-shot window
(attack tick → release frame) + 0.35 follow-through; capsule-fallback shot
0.5; hit reaction 0.45. Death: existing behavior already zeroes movement
permanently (kinematic body, update exit).

**Application:** the soldier's whole steering desire is scaled by a
recovery factor — 0 while locked, ramping back to 1 over 0.3 s
(no snapping, no overshoot; the existing capped acceleration preserves
arrival behavior). In-reach combat has near-zero desire anyway, so combat
is unaffected; a drawing archer or striking legionary is simply planted.
Same-team separation stays ≥40% active while locked so overlaps still
resolve gently. The broken-ranks slot weight (0.05) is untouched.
Weak correction "between attacks" emerges from the ramp + the existing
Engaged slot weight (0.12–0.35) rather than a third mechanism.

**Reform flow:** Reform request → free soldiers correct immediately; locked
soldiers hold their CURRENT position while their assigned slot stays
pending (slots are evaluated live, so on release they walk to the current
slot, not a stale one). **Completion rule: ≥85% of living soldiers within
0.6 m of slot, or the existing 12 s timeout** — no single locked/straggling
soldier blocks completion. Attacks are never cancelled by Reform.

**Auto-close priority:** now runs only in Ordered/Attacking (never while
Engaged — the existing column-local rank promotion covers vacancies in
melee without global remaps) and never during an active Rotate maneuver.
Its existing 2 s interval provides reassignment hysteresis.

## Problem B — formation maneuvers

`FormationManeuverState { None, SmallTurn, Wheel, AboutFace, Redressing }`
with one formation-level progress value; no per-soldier maneuver objects.

**Classification** (shortest signed yaw delta, `Mathf.DeltaAngle`):
- **|Δ| ≤ 20° — small turn:** facing snaps, slot ownership unchanged
  (no nearest-slot remap anymore), soldiers pivot in place (Patch 1 facing +
  pivot clip) and briefly redress.
- **20° < |Δ| < 135° — inner-flank wheel:** pivot = inner front corner
  (right turn → front-right, left turn → front-left, from
  `FootprintHalfExtents` in formation space). Each frame the anchor frame
  rotates rigidly around the frozen pivot
  (`WheelStep: rot = Δq·rot; pos = pivot + Δq·(pos−pivot)`), so every slot
  follows its exact arc — no Lerp to rotated endpoints, no straight paths
  through the block, slot ownership frozen. Angular speed =
  `wheelOuterSpeed (2.3 m/s) / outerRadius`, duration clamped 0.8–10 s.
  Anchor movement, engaged-facing wheel, and auto-close are suspended while
  the wheel owns the anchor. A new move order supersedes the maneuver.
- **|Δ| ≥ 135° — about-face:** facing snaps and the existing nearest-slot
  remap reinterprets ranks in place — validated: **0.00 m displacement**,
  old front row becomes the new rear rank, no duplicate slots. Front/rear
  roles, guard eligibility, and the visual front-rank check
  (`slotIndex < columns`) all follow the remap automatically; directional
  damage keeps reading canonical `AnchorForward` (values unchanged).

**Combat locks during a wheel:** progression pauses while >40% of living
soldiers are in committed actions (`wheelPauseLockedFraction`); locked
soldiers keep their positions and attacks, and rejoin toward the CURRENT
(live-evaluated) maneuver slots when released. The Rotate command is never
lost — the wheel resumes when enough soldiers free up.

**Completion/redressing:** wheel ends within 0.5° of the commanded facing →
Redressing; small turns/about-faces enter Redressing immediately.
Redressing ends when ≥85% of living soldiers are within 0.6 m of slot or
after 4 s (timeout fallback) → back to normal ordered behavior (auto-close
re-enabled). No final snapping — normal steering closes the last gap.

**Backpedaling prevention:** small turns and about-faces move nobody;
wheels move soldiers forward along arcs (facing tracks movement via
Patch 1); no reverse-walk paths are ever generated. No backpedal clip added.

## Performance

One formation-level maneuver state (5 floats + enum); slots derived from
the anchor frame (no new buffers); `LockedFraction`/`FractionNearSlots` are
allocation-free O(soldiers) loops on existing lists; no physics queries, no
LINQ, no per-frame searches. Correction scaling is two floats per soldier.

## Static validation (Ancient Armies → Validate Formation Maneuvers)

Real `WheelStep`/slot/`AssignNearestSlots` code paths on temp objects,
four layouts (melee 50×10 @1.15, archer 30×10 @1.75, 5×10, 8×5) ×
wheels ±45/±90/120 plus 180° about-face: **all OK** — spacing preserved
(rigid to <0.001 m), exact final facing, correct inner pivot (inner flank
0.5–2.1 m vs outer arc 6.7–29.8 m), no NaN/infinity, no duplicate slots,
about-face displacement 0.00 m with correct rank reversal. Boundary
classification: 10°/20° small, 45–120° wheel, 135°/180° about-face.
Action-lock behavior is runtime-only → covered by the manual checklist.

Static Roslyn compile: exit 0. Editor compile clean (one deprecated-API
warning in the validator fixed immediately; Console otherwise clean).

## Values (all serialized/tunable)

smallTurnMaxDeg 20 · aboutFaceMinDeg 135 · wheelOuterSpeed 2.3 m/s ·
wheelPauseLockedFraction 0.4 · redressCompleteFraction 0.85 ·
redressTimeoutSeconds 4 · locks: 0.85/0.7/0.35/0.5/0.45 s · ramp 0.3 s ·
reform completion 85% @0.6 m or 12 s.

No damage, health, cooldown, range, spacing, count, AI-targeting, or
morale values changed; the melee-defers-to-archers behavior is untouched.
