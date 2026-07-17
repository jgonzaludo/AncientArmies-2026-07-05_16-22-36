# Roman Archer + Overnight Batch — Manual Test Checklist

Date: 2026-07-13. Open `Assets/Scenes/Battle.unity`, Play Mode, landscape.
Restart until the roll gives at least one archer formation per side. Runtime
behavior has NOT been play-tested (compile + static validation only); this
checklist is the acceptance gate for the whole overnight batch.

## A. Archer visuals

### A1 — Spawn and identity
- [ ] Archer formations spawn the new Roman archer model (helmet, scale
      cuirass, quiver, bow in the RIGHT hand — matches the reference art;
      the written "left hand" in the brief contradicted its own images).
- [ ] Blue archers: blue tunic/sleeves; red archers: red. Everything else
      (helmet, brass, leather, bow, quiver, skin, eyes) identical between teams.
- [ ] Melee soldiers still use the legionary visual; if the archer prefab is
      renamed away, archers fall back to capsules cleanly.

### A2 — Idles
- [ ] Out of combat: relaxed disciplined idle, bow at the side (subtle motion).
- [ ] Enemy in bow range, between shots: firing-ready stance (slight blade,
      bow forward); NO permanent nocked arrow, NO held full draw.

### A3 — Walk / shuffle / pivot
- [ ] Movement uses the formation walk (light, controlled, bow close);
      no dedicated run — urgent moves just play faster.
- [ ] Reform / auto-close corrections use the shuffle.
- [ ] Explicit ROTATE shows the pivot step; the root does the turning.

### A4 — Firing cycle (watch several shots up close)
- [ ] Full cycle reads: reach to quiver → hand-arrow appears → nock → draw to
      cheek → brief hold → release: hand-arrow disappears AND the flying arrow
      appears at that moment (not at the start of the animation).
- [ ] The projectile is now an arrow mesh oriented along its flight (not a sphere).
- [ ] Long-range shots use the high-arc variant (bow raised ~30°, chin up)
      and the projectile's tall arc reads consistent with it.
- [ ] Fire rate and damage feel unchanged from before (cooldown untouched;
      only the visible release moment moved).
- [ ] A whole formation does not release on the same exact frame (small
      per-soldier phase variation).

### A5 — Hit reaction
- [ ] A hit mid-draw cancels the visible draw, the arrow still flies
      immediately (no lost shots), and the flash shows.
- [ ] No archer flies back or leaves its slot.

### A6 — Knife fallback
- [ ] When enemies reach sidearm range (~2.5 m), the archer draws the hand
      knife (the belt scabbard stays put — a separate hand blade appears),
      takes the defensive guard, and stabs compactly on its sidearm attacks.
- [ ] Leaving melee range returns to bow stances; knife disappears.

### A7 — Deaths
- [ ] Both variants appear (backward Meshy fall; forward fold-to-knees).
- [ ] Corpses stay near the death spot, sink, and despawn as before.

## B. Spacing (+~10%)

- [ ] Melee blocks slightly looser than before (1.05 pitch) but still read
      as dense shoulder-to-shoulder walls vs archers (1.85, clearly looser).
- [ ] Auto-close, reform, rotation, and touch targets all work at the new
      pitch (compare against the V1.2 D1–D7 tests).
- [ ] No collider jitter or overlapping soldiers at rest.

## C. Camera closer zoom

- [ ] Pinch-in now reaches ortho size 5.5 (previously 8) — noticeably closer;
      soldier animation detail readable.
- [ ] Max zoom-out unchanged (36). Smoothing/pan feel unchanged.
- [ ] At full zoom-in: no clipping into soldiers/ground, selection and drag
      orders still work, UI stays in the safe area, pan bounds behave.

## D. Legionary attack polish (validation patches)

- [ ] Thrust: shield visibly steadier than before (sway was 15 cm, now ~5 cm);
      attack still lands with the same rhythm.
- [ ] Over-shield: slightly slower/weightier (0.73 s → 0.87 s), impact reads
      around mid-clip; both attacks start and end in the same guard.

## E. Performance & console

- [ ] 5v5 with archer formations: no console errors, no missing-clip warnings,
      frame rate comparable to the legionary-only build.
- [ ] No material instance explosion (archers tint via per-slot
      MaterialPropertyBlocks on the same 8 shared materials).
- [ ] Arrow projectiles don't leak (destroyed on landing as before).

## Known limitations (accepted, documented)

- Bow does not flex at full draw (rigid); the draw pose carries the read.
  Follow-up: draw shape key or limb bones.
- The nocked hand-arrow is angle-correct at full draw, slightly off-angle
  during the reach/nock frames.
- High-arc selection uses nearest-enemy distance as a proxy for target
  distance (edge cases possible in mixed melees).
- An archer that dies mid-draw loses that arrow (pending shot cancelled) —
  rare, slight nerf vs the old instant-spawn.
- Knife hand-blade appears only in melee mode; belt scabbard never empties.
