# Patch 1 Manual Tests — Facing and Locomotion Synchronization

Unity editor, `Battle` scene, Play Mode, landscape Game view.
None of these are marked passed until actually performed.

## Test 1: Legionary movement direction

Move a melee formation north, south, east, west, and all four diagonals.

- [ ] Soldier bodies face the direction they are moving
- [ ] Shield stays on the left, sword on the right
- [ ] No sideways gliding, no backward-looking march
- [ ] No sudden 180° jitter when starting/stopping

## Test 2: Archer movement direction

Repeat all eight directions with an archer formation — including while an
enemy is visible in the distance (inside 48 m bow range).

- [ ] Archers face their movement even with a distant target in range
- [ ] Bows held correctly, quivers behind
- [ ] No sideways walking toward a far-away target (the old twist bug)

## Test 3: Explicit rotation

With a stationary Ordered formation: north→south, south→north, east→west,
west→east, and each diagonal. **Also test before pressing Start** (Pre phase).

- [ ] Ground arrow updates to the commanded direction
- [ ] Soldiers visually turn to face the arrow (pivot step plays when idle)
- [ ] Final facing persists after the pivot animation ends
- [ ] Idle does not snap back to the old direction
- [ ] Moving afterwards marches off correctly in any direction
- [ ] Pre-battle Rotate turns the models, not just the arrow

## Test 4: Archer target facing

Let archers fire at enemies ahead, left, right, behind, and diagonal.

- [ ] Archer turns horizontally toward the target before/during the ready stance
- [ ] Stays stable through the whole draw and release (no mid-draw wobble,
      even when other enemies die or targets change nearby)
- [ ] No sideways or backward firing; no vertical root tilt
- [ ] Does not rotate away during release recovery
- [ ] Projectile behavior unchanged (damage lands as v1.3.2)

## Test 5: Melee target facing

Engage enemies from front, left, right, and rear.

- [ ] An actively fighting soldier faces its own opponent
- [ ] A marching soldier does NOT twist toward a distant acquisition
- [ ] Rear ranks without targets stay formation-facing
- [ ] Front/flank/rear damage multipliers behave exactly as before
      (they read formation facing, not soldier visuals)

## Test 6: Legionary march speed

Watch normal movement at default zoom, min zoom (5.5), and max zoom.

- [ ] Foot cycles track ground speed far more closely (no obvious skating)
- [ ] Faster march = faster feet; slowing = slower feet
- [ ] Feet stop promptly when the soldier stops (idle within a beat)
- [ ] No excessive foot flutter at full speed (playback is clamped at 2.4×)

## Test 7: Archer walk speed

- [ ] Walk cycle matches movement; no gliding
- [ ] Attack draw/release and knife clips play at normal speed while walking
      speed varies (LocoScale binds only Walk/Shuffle)
- [ ] Ordered archers with enemies in range hold the firing-ready stance
      between shots instead of rear-rank idle

## Test 8: Broken-ranks / urgent movement

Break ranks and let soldiers chase.

- [ ] Faster movement produces faster foot cycling (legionary uses the run clip)
- [ ] No new run animation exists for archers (faster walk playback only)
- [ ] No attack/hit/death clip is accelerated
- [ ] Soldiers face their meaningful movement direction

## Test 9: Reform regression

Use Reform during and outside combat.

- [ ] Reform behavior itself is unchanged (this patch touched none of it)
- [ ] Small slot corrections play as shuffle, not full-speed march
- [ ] The forced-reform-interrupts-combat issue still exists (Patch 4 scope)
- [ ] Facing stays coherent during reform (movement-facing with hysteresis)

## Test 10: Preserved melee/archer behavior

- [ ] Melee units still do not unnecessarily engage targets that friendly
      archers are already handling at range
- [ ] Targeting, AI, damage, and ranges are unchanged

## Test 11: Full battle

Run a full 5v5 battle to the end.

- [ ] No repeated console errors with ~400+ soldiers
- [ ] Facings stay stable (no spinning formations, no liquid rotation)
- [ ] No missing clips / materials / prefab visuals; capsule fallback still
      works if a VIS prefab is removed from Resources (spot-check optional)
- [ ] No obvious Animator CPU regression (Profiler spot-check)
- [ ] Dead soldiers keep their death-entry facing (no post-death rotation)

## Editor utility

- [ ] Menu **Ancient Armies → Validate Facing Pipeline** runs and reports
      clean bindings + prefab arrangement (works in and out of Play Mode)
