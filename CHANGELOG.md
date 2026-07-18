# Changelog

All notable changes to Ancient Armies. Format follows Keep a Changelog;
versions are git tags on `main` per `docs/VERSIONING.md`.

## [Unreleased]

### 0.12.0 candidate — Charge (on `dev`, awaiting owner Play Mode test)

#### Added
- CHARGE replaces Break Ranks: the century sprints as a loose pack (1.3×) at
  a target within 45 m, hits with a 1.5× melee impact bonus for a 2 s window
  after first contact (stacks with flank/rear multipliers; archers may
  charge as a last resort but get no bonus), then dissolves into pursuit on
  the broken-ranks machinery — individuals hunting, the pack flowing to the
  next enemy within 35 m and never map-chasing, under a tighter moving 18 m
  leash whose center follows the pack.
- Plant-the-standard reform: a pursuing century's banner rides with the
  dominant cluster (historically the signifer's standard moved with the
  men); pressing REFORM strikes it into the ground wherever the pack stands
  now, and the survivors form up there. A reform that aborts leaves the
  standard planted.
- Charging legionaries run (broken-ranks sprint animation) instead of
  marching; the charge shows the movement banner icon.
- The button greys out with "No enemy in charge range" when nothing is
  within reach — a formation can never dissolve itself for free.
- Enemy AI charges sparingly: only at targets already badly under strength
  or broken, and only within charge reach — discipline is a resource.

### 0.11.0 candidate — Controls & UI revamp (on `dev`, awaiting owner Play Mode test)

#### Added
- Move drags now preview the real thing: the 80 destination slot dots follow
  the finger live (same math the released order issues, so the preview never
  lies); attack drags keep the red line + target ring.
- Place-and-twist facing: while dragging a move, a second finger aims the
  formation's final facing — the dots rotate under it, the facing locks past
  a small deadzone, and lifting the second finger keeps it. No rotate button
  needed mid-order (the button remains for stationary rotates).
- Double-tap grouping: tap = exclusive select; a quick second tap on another
  century adds it to the group; tapping a selected century removes it; empty
  ground clears.

#### Fixed
- Removed the full-width bottom bar whose invisible background swallowed unit
  taps near the bottom of the screen. Commands are standalone floating
  rounded buttons bottom-right; formation info lives on a compact
  non-interactive chip bottom-left. Only button backgrounds are raycast
  targets anywhere in the HUD.

### 0.10.0 candidate — Mobile presentation (on `dev`, awaiting owner Play Mode test)

#### Added
- Far-zoom imposter LOD: below ~34 reference-px soldier height each century
  swaps its 80 animated soldiers for one merged mesh of team-colored
  billboard quads (16 draws for the whole battle). Quads flash white on
  hits, fall flat and shrink on deaths, brighten while selected; swaps are
  staggered across formations and pending archer shots are released before
  the animator sleeps so no validated arrow is ever swallowed.
- Mid-zoom shadow tier: soldier shadow casting (a full second draw of the
  army) turns off below ~50 reference-px soldier height, well before the
  imposter swap.

#### Fixed
- Max zoom-out no longer shows off-field ground: the camera derives its true
  fit-max zoom from field size, aspect, and pitch (≈95 at 19.5:9 vs the old
  hard 120), and pan bounds now shrink with zoom so the visible rectangle
  can never slide past the battlefield edge.
- Banners no longer drift off their centuries on high-DPI phones: soldier
  screen height and all declutter paddings/steps are normalized to a
  1080p-reference pixel space.
- The 80-circle destination preview now renders on device: replaced the
  runtime-instanced draw (its shader variant is stripped from builds) with
  one combined mesh on the proven non-instanced path.
- Buttons render as true rounded rectangles at every size: the 9-slice
  sprite's pixelsPerUnit now matches the canvas (100) with the border equal
  to the corner radius.
- Touch slop and formation tap padding no longer shrink physically on dense
  screens (expressed in 1080p-reference pixels / screen-height fractions).

### 0.9.1 candidate — Banner strength readout (on `dev`)
- Removed the 80/80 strength text from banners; the health bar (now 3.1 ×
  0.45) carries remaining strength alone.

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
