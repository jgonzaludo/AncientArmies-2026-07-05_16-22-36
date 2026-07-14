# Manual Tests: Spacing, Projectile, Archer Range, and Terrain Polish (v1.3.1)

Run in the Unity editor, `Battle` scene, Play Mode, landscape Game view.
Restart between tests where noted (Restart re-rolls army composition).

## A. Intra-formation spacing

- [ ] **A1 — Melee looser:** At battle start, melee soldiers stand in clean
  ranks with a small visible gap between shoulders (spacing 1.15, was 1.05).
  No soldier overlaps a neighbor while Ordered.
- [ ] **A2 — Archers tighter:** Archer formations read looser than melee but
  tighter than before (1.75, was 1.85). Both teams identical.

## B. Formation edge-to-edge gaps

- [ ] **B1 — No touching formations at spawn:** Restart repeatedly (5+ rolls)
  until a side fields two adjacent archer formations. Their edges must show a
  clear walkable gap (≥ ~1.9 m), never merging into one blob (the v1.3
  screenshot bug).
- [ ] **B2 — All combinations:** Across restarts, check melee–melee,
  melee–archer, and archer–archer neighbors: every pair has daylight between
  footprints; the whole line stays centered on the field and inside the
  120-wide plane at max zoom-out.
- [ ] **B3 — No console warnings:** The spawn-time layout validator prints no
  "Formation gap violation" warnings across 5+ restarts.

## C. Friendly anti-clumping

- [ ] **C1 — March without stacking:** Order a formation across the map;
  while moving, no two friendly soldiers visually occupy the same spot.
  Column order stays predictable (no jitter or oscillation).
- [ ] **C2 — Broken ranks:** Break ranks into a crowd; soldiers spread to a
  loose swarm instead of stacking, but may pack tighter than ordered ranks.
- [ ] **C3 — Melee unaffected:** Engaged soldiers still reach and strike
  enemies at the front line — enemy contact distance is unchanged; the fight
  doesn't get pushed apart.
- [ ] **C4 — At rest:** An idle Ordered formation shows no drifting or
  vibrating soldiers (separation must not fight slot positions).

## D. Terrain

- [ ] **D1 — Light grass:** The battlefield reads as a light, warm
  yellow-green field — clearly brighter than the old muddy green, with NO
  black/dark speckles and no 3D tufts.
- [ ] **D2 — Tiling:** At default zoom and at min zoom (5.5), no obvious
  repeating pattern or visible tile seams.
- [ ] **D3 — Fallback:** (Optional) Temporarily remove
  `TEX_Battlefield_Grass_Light` from Resources → field falls back to a bright
  procedural green, not magenta/grey; restore afterwards.

## E. Arrows

- [ ] **E1 — Orientation:** Zoom to an archer volley: every arrow points
  head-first along its flight path for the whole flight — never sideways,
  never tumbling. Check both short flat shots and long shots.
- [ ] **E2 — Arc:** Long-range volleys (~40+ m) fly a pronounced readable
  arc (up to ~7 m apex); close shots (~10 m) fly visibly flatter. Arrows tilt
  up on launch and tip downward on descent.
- [ ] **E3 — Landing:** Arrows still damage on impact exactly as before
  (hit flash on the target); no arrows freezing mid-air or spinning at spawn.

## F. Archer range

- [ ] **F1 — Long-range opening:** Start a battle with archers on both
  sides; archers begin firing after only a small advance (max range 48, was
  20) — clearly before melee lines meet.
- [ ] **F2 — Preferred stand-off:** An archer formation ordered to attack a
  distant enemy stops around 40 m out (preferred 40.8) and fires; it doesn't
  march into melee.
- [ ] **F3 — AI parity:** Enemy (red) archers open fire at the same distances
  as player archers.
- [ ] **F4 — Broken-ranks targeting:** Break an archer formation's ranks near
  max range: archers still acquire and shoot targets ~30–48 m away (the old
  26 m leash no longer blocks ranged acquisition).
- [ ] **F5 — Flight time:** A max-range shot takes ~2.4 s and is easy to
  follow; close shots feel unchanged.

## G. Regression sweep

- [ ] **G1 — Combat numbers untouched:** Kill-time feel of melee vs melee is
  unchanged (damage/cooldown/health untouched by this patch).
- [ ] **G2 — Formations:** Move / attack / face / break / reform orders all
  behave as in v1.2/v1.3; pivot-in-place still triggers.
- [ ] **G3 — Restart:** Mid-match restart works; new layout obeys B1–B3.
- [ ] **G4 — Console:** No errors or warnings during a full battle.
