# Changelog

All notable changes to Ancient Armies. Format follows Keep a Changelog;
versions are git tags on `main` per `docs/VERSIONING.md`.

## [0.9.0] — 2026-07-18

First tag under the new versioning scheme (see `docs/VERSIONING.md`).
Encompasses the combat-feel and pacing wave after the V1 incremental
overhaul (retro-anchored at `v0.8.0`).

### Added
- Volley fire: ranged formations loose in staggered 8 s formation volleys
  with a 1.5 s jittered window — Total War-style waves instead of a drizzle.
- Spread targeting: graduated ranged-attacker penalty + deterministic
  per-soldier salt distributes arrows across the enemy front (no more
  80-arrow pile-ons onto one man).
- Single-arrow ownership: exactly one direction arrow per formation context
  (white facing / yellow destination / yellow rotate), with the rotate arrow
  staying white until dragged, turning yellow while editing, and remaining
  yellow until the century physically settles on the new facing.

### Fixed
- Deployment lines no longer spill off the battlefield or read as touching:
  field depth 140→170, archer line offset 16→20, reserve offset 32→40
  (accounts for the 40° camera's ~2.3 m perceived-depth cost of model height).
- Removed the banner facing wedge that rendered as a stray white triangle on
  top of the badge artwork.
- Rotate mode no longer stacks a second arrow on the facing arrow, and
  selecting a moving century no longer spawns a stray arrow at its anchor.

### Tuned
- Total War pacing: melee attack cooldown 1.4→4.0 s (kills stay 3-4 hits;
  engagements grind ~1.5-2 min), archer damage 14→10 (4 front arrows per
  kill), archer cooldown 3→6.5 s under the 8 s volley clock. Century-vs-
  century archery now softens (~20-25% per charge) rather than deletes.
- All tuned values stamped into `Battle.unity` (scene-pinned) AND code.

### Docs
- `docs/VERSIONING.md` — binding branch/version/commit conventions.
- `docs/superpowers/specs/2026-07-17-battle-pacing-design.md` — pacing
  decisions + approved v0.10 morale/rout/rally design.
- `docs/patches/V1_8_2_COMBAT_AND_ARROWS_INVESTIGATION.md` — root-cause
  record for the arrow, spacing, and duplicate-arrow investigations.
