# Patch 3 — Animation Quality Repair

**Branch:** `v1-prototype` (from `3759b7e v1.3.5`)
**Date:** 2026-07-15 · **Play Mode:** not used.

## Pre-change audit (numeric, in-Blender sampling)

Clips at 30 fps, frame ranges as imported (legionary 13 takes, archer 13 takes).

### Legionary `ANIM_Roman_Attack_OverShield` (f0–26) / `ANIM_Roman_Attack_Thrust` (f0–20)

| Criterion | Measured | Verdict |
|---|---|---|
| Shared guard start/end, no snapping | first & last frames match FrontRankGuard f0 with **0.0° deviation on every bone** (attacks were authored from the guard pose bank) | PASS |
| Impact timing | OverShield impact f13 of 26 (≈0.43 s); Thrust impact f9 of 20 (≈0.3 s) — within the compact windows | PASS |
| No root motion / drift | Hips horizontal excursion **0.0000** in both clips | PASS |
| No scale keys | 0 scale fcurves | PASS |
| Interpolation | 0 linear keys — all bezier/auto | PASS |
| Shield rigidity | re-verified in Patch 2 (0.0001 cm across all actions) | PASS |

**Result: the v013 attack polish already satisfies every measurable Phase 3
legionary criterion — the clips were left untouched** (subjective blade-lead
readability was v013's validated target; fresh evidence renders below).

### Archer `ANIM_Archer_Attack_DirectShot` (f0–42) / `HighArc` (f0–44)

Phase profile (hand↔head and hand↔hand distances per frame): reach-to-quiver
f0–8 → arrow-to-bow f8–14 → draw f14–22 → **hold/aim f22–26 (stable beat)** →
release ~f28 (matches the controller's 0.66 release threshold) → recovery →
exact FiringReady return (0.0° first/last vs FiringReady).

**Defect found:** at full draw the string hand floated **0.41 m** from the
head — an under-drawn, "hand missing the string" look (hands only 0.64 m
apart).

**Fix applied:** draw deepened on the full-draw/hold/release keys (Direct
f22/26/28, HighArc f24/28/30) using a temporary two-bone IK bake
(`visual_transform_apply`, then immediate keyframe insert — honoring the
Blender 5 staged-key/slot traps), pulling the string hand back along the
arrow line while a copy-rotation hold preserved the hand's world orientation
(the nocked-arrow prop keeps aiming at the bow). Post-fix: anchor **0.38 m**
(reach-limited by the compact proportions), hand separation 0.64 → **0.89 m**
— a committed full-draw silhouette (bow arm extended, string hand beside the
cheek; render-verified). Boundary keys (nock f14/15) untouched, so retrieval
and nock phases are unchanged.

### Archer knife clips

| Criterion | Measured | Verdict |
|---|---|---|
| KnifeGuard: point faces threat | blade vs facing = **0.0°**, hand at rib height (z 0.73) | PASS |
| KnifeStab: blade-first, not a punch | during the thrust (f7–9) blade leads motion by only **10–29°**; retraction 150–170° (pulling back) | PASS |
| Compact stab | 13 cm forward reach over f6–10; 21 f = **0.70 s** (target 0.6–0.75) | PASS |
| Clean loop to guard | returns to start pose by f20 | PASS |

KnifeDraw untouched (Phase 2 handoff timing depends on it).

## Known limitations (documented, out of reach without rig changes)

- The bow has **no string bone or limb bones** (rigid-skinned by design since
  v005): bow bending and string-follows-hand cannot be authored on the
  existing approved rig, and Phase 3 forbids rig changes. The deepened draw +
  hand-arrow prop carry the read at gameplay zoom.
- The 0.55 m arrow prop is shorter than the new 0.89 m draw span, so the
  arrowhead sits short of the grip at full draw — invisible at gameplay
  distance; a longer draw-arrow variant is future polish.
- Anchor reached 0.38 m (not the 0.29 m target) — the two-bone chain is at
  its reach limit for this compact body.

## Unity integration

- Archer FBX re-exported (same conventions; previous FBX archived as
  `RomanArcher_Basic_pre_v007_backup.fbx`; blend saved as new
  `RomanArcher_v007_AnimationQuality.blend`; legionary saved as
  `RomanCharacterSystem_v015_AnimationQuality.blend`, clips unchanged, no
  legionary export needed).
- Import verified: 13 clips; prefab mesh references intact (name-stable).
- Animator transitions verified (YAML audit): both controllers keep 2
  AnyState→Death overrides, 0 missing motions, attacks not bound to the
  locomotion speed multiplier (Patch 1 utility), attack exits use exit-time,
  entries condition-only (prompt starts).
- No C# changes in this patch.

## Review captures

`docs/art/reviews/renders/patch_3_animation_quality/` — 46 images:
legionary over-shield & thrust at guard/anticipation/impact/recovery/final
(front + three-quarter), archer reach/nock/half-draw/full-draw/release/
recovery + high-arc full-draw/release + knife draw/guard/stab phases.
