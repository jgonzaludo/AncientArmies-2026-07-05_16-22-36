# Patch: Spacing, Projectile, Archer Range, and Terrain Polish

**Version:** v1.3.1 (patch on top of the v1.3 archer/legionary visual release)
**Branch:** `v1-prototype` (upstream `origin/v1-prototype` exists)
**Date:** 2026-07-14
**Status:** IN PROGRESS

## Versioning decision

Repo history uses `v0.1, v0.2, v1, v1.1, v1.2` (last commit `09c936a v1.2
combat and control`). The working tree additionally contains the entire
uncommitted v1.3 body of work (Roman archer + legionary visual overhaul,
spacing/camera tuning). This patch builds directly on those files, so the
push is delivered as **two commits**:

1. `v1.3 roman archer, legionary animation rework, camera and spacing tuning`
   — the pre-existing uncommitted feature work this patch depends on.
2. `v1.3.1 spacing, projectile, archer range, and terrain polish` — only the
   files this patch touches.

## Pre-patch authoritative values (verified on disk, scene wins over C#)

| Value | C# default | Battle.unity serialized |
|---|---|---|
| meleeSpacing | 1.05 | 1.05 (line 331) |
| archerSpacing | 1.85 | 1.85 (line 332) |
| archer rangedRange | 20 | 20 (line 324) |
| archer rangedPreferredRange | 16 | 16 (line 325) |
| archer projectileSpeed | 13 | 13 (line 327) |
| lineSpacingX (formation center pitch) | 16 | 16 (line 336) |
| lineZ | 24 | 24 |
| meleeCount / archerCount / columns | 50 / 40 / 10 | same |
| Projectile flightTime clamp | 0.35–1.6 s | (code only) |
| Projectile arcHeight | clamp(dist × 0.22, 0.6, 3.5) | (code only) |

## Root cause: adjacent formations visually merging

`BattleSetup.SpawnSide` places formation centers at a fixed
`lineSpacingX = 16` pitch. Real footprint widths (front row is the widest):
`width = (min(count, columns) − 1) × spacing + spacing`.

- Melee @1.05: 11.0 wide → neighbor gap +5.0 ✔
- Archer @1.85: 18.5 wide → **archer–archer gap −2.5 (overlap)** ✘ — the bug
  in the report screenshot.

## Planned changes

### 1. Intra-formation spacing (symmetric ±0.10)
- meleeSpacing 1.05 → **1.15**
- archerSpacing 1.85 → **1.75**
- Applied to both C# defaults and Battle.unity serialized values.

### 2. Guaranteed formation edge-to-edge gap
- New `Formation.LineHalfWidth(count, columns, spacing)` — closed form of the
  front-row half-width that `BuildSlots` bakes into `FootprintHalfExtents.x`
  (single source of footprint truth; the validator cross-checks the real
  `FootprintHalfExtents` after spawn).
- `SpawnSide` rolls the whole line first, then places centers sequentially:
  `centerDist = max(lineSpacingX, halfW_a + gapFactor × max(spacing_a, spacing_b) + halfW_b)`,
  recentered on x = 0. `formationGapFactor = 1.1` (≥ 1.0 required by spec,
  1.10 target). Applies to every spawn (initial + restart reload, both teams,
  all melee/archer combinations).
- Post-spawn `ValidateLineGaps` warns if real footprints violate the gap.
- Post-change worst case (5 archers @1.75): line ≈ 87 wide, fits the 120-wide
  field and the max-zoom-out framing.

### 3. Friendly-soldier anti-clumping (Soldier.cs, subagent)
- Same-team soft separation: min distance 0.85 × spacing ordered /
  0.65 × broken-or-engaged, linear ramp, capped ~1.2 m/s, deterministic
  tiebreak, allocation-free, staggered neighbor refresh. Enemy contact and
  all combat numbers untouched.

### 4. Grass replacement (subagent)
- Old runtime grass (dark muddy two-green Perlin mottle + 700 dark tufts)
  REPLACED by a light warm yellow-green seamless tiled albedo
  (`TEX_Battlefield_Grass_Light`, Meshy-generated if the API offers image
  generation, procedural fallback otherwise) under a single
  `MAT_Battlefield_Grass_Light`. No 3D tuft geometry, no dark speckles.

### 5. Arrow orientation + arc (Projectile.cs, subagent + axis measurement)
- Root cause of sideways arrows: imported PROP_Archer_Arrow local axis to be
  measured in-editor; single documented `ArrowAxisCorrection` applied once to
  the visual child. Root rotation = `LookRotation(trajectory tangent)`.
- Initial rotation set analytically from the launch tangent (no first-frame
  garbage).
- arcHeight: clamp(dist × 0.22, 0.6, 3.5) → **clamp(dist × 0.15, 1.2, 7)**.
- flightTime clamp max 1.6 → **2.4 s** (sub-20 m shots keep identical timing).

### 6. Archer range
- Spec: `newMax = max(20 × 2, archerWidth 17.5 × 6.5 = 113.75)` — 113.75 is
  impractical (field 120 × 80; melee lines spawn 48 apart, archer lines 60).
- **Capped for battlefield practicality at 48** = the melee-line spawn
  separation: archers can bombard the opposing line after a small advance,
  preserving maneuver and readable flight. Documented cap per spec.
- rangedRange 20 → **48**; rangedPreferredRange = 0.85 × 48 = **40.8**;
  projectileSpeed 13 → **20** (48 m extreme shot ≈ 2.4 s flight).
- Player/AI parity: EnemyCommander, acquire radius, high-arc threshold, and
  stop-at-preferred all read `stats.rangedRange`/`rangedPreferredRange` — no
  hardcoded copies found (verified by grep). Broken-ranks acquire leash
  checked/fixed for ranged units (subagent).

## Verification plan
- Static Roslyn compile (Unity's csc against Assembly-CSharp.csproj refs).
- Unity editor recompile + Console check via MCP; scene edits via
  execute_code → SetDirty → SaveScene (every changed field set explicitly).
- Before/after battlefield screenshots for the grass swap.
- Manual test suite: `SPACING_PROJECTILE_RANGE_TERRAIN_MANUAL_TESTS.md`.

## Results

All planned changes landed. Highlights and deviations:

- **Spacing/gap:** C# + scene both updated (melee 1.15, archer 1.75,
  formationGapFactor 1.1 serialized). New `Formation.LineHalfWidth` +
  footprint-aware `SpawnSide` + post-spawn `ValidateLineGaps` warning guard.
  Melee–melee neighbors keep the familiar 16-pitch (gap +4.5 m); archer
  neighbors now compute 19.4 m pitch (edge gap 1.925 m, was −2.5 m overlap).
- **Separation:** implemented in Soldier.cs as a cached, 4-tick-staggered,
  capped (1.2 m/s) same-team push — ~31k sqr-distance checks/tick at
  250v250, zero allocations. Deterministic entity-ID tiebreak for
  coincident soldiers.
- **Ranged leash fix (found & fixed):** `AcquireTarget` discarded candidates
  beyond the 26 m broken-ranks anchor leash — it would have silently blocked
  the new 48 m range. Ranged units now use
  `max(brokenLeash, rangedRange + 2)`.
- **Grass:** Meshy text-to-image (task `019f5ed8-08f0-7421-a670-a23766192b03`,
  nano-banana-2, 6 credits) → border-crop → FFT periodic-smooth seamless →
  color-grade. Final 1024²: mean #9AAE65, luminance 0.52–0.78 (no dark
  speckles possible). Runtime material `MAT_Battlefield_Grass_Light`,
  tufts and dark mottle deleted. Before/after: `renders/terrain_before.png`
  / `renders/terrain_after.png` (rendered through the shipping code path).
- **Arrow axis (measured):** the imported PROP_Archer_Arrow head is the
  zero-radius tip at mesh-local −Y; the importer bakes Euler(270,0,0) on the
  prefab root. `ArrowAxisCorrection = Euler(270,0,0)` on the visual child
  maps head → +Z; the projectile root carries pure
  `LookRotation(trajectory tangent)` incl. an analytic launch tangent
  (no first-frame garbage). Arrow visual restructured to root+child so the
  correction can't fight the per-frame rotation (the old code stomped the
  prefab rotation — the actual cause of the sideways flight).
- **Range:** 48 max / 40.8 preferred / speed 20 in code + scene. All
  consumers verified stat-driven (EnemyCommander bonus, acquire radius,
  high-arc threshold 0.6×, stop-at-preferred).

### Verification
- Static Roslyn compile (Unity 6000.4.8f1 csc, full Assembly-CSharp): exit 0.
- Editor recompile confirmed (ScriptAssemblies 2026-07-14 00:34), Console:
  0 errors, 0 warnings.
- Scene YAML re-read from disk after SaveScene: all six values confirmed.
- Runtime gameplay NOT verified in Play Mode (per constraints) — see
  `SPACING_PROJECTILE_RANGE_TERRAIN_MANUAL_TESTS.md`.

## v1.3.2 hotfix: arrow damage reliability

Playtest found arrows dealing no damage to melee units until close range,
while hitting enemy archers fine. Cause: `Projectile.Spawn` led the target by
only half the flight time and `Update` required the target to be within
~1 m of that fixed guess at landing. Advancing melee (~3 m/s over a 2.4 s
flight) drifted ~3.6 m past the prediction — silent miss; stationary archers
sat exactly on it — hit. Fix: the arc now tracks the live target every frame
and the landing always applies the full fire-time damage; only the target
dying mid-flight cancels the hit (the arrow falls where they fell). Damage
per shot is unchanged and range-independent, as designed.

### Repo policy note
`ArtSource/` (1.7 GB, individual files >100 MB) added to .gitignore as
local-only; exported game-ready assets live in `Assets/Art/`. `.agents/` and
`skills-lock.json` (user tooling state) intentionally left untracked.
