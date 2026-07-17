# Roman Legionary Animation Rework — Manual Test Checklist

Date: 2026-07-13. Open `Assets/Scenes/Battle.unity`, Play Mode, landscape.
Compositions are random — Restart until the roll gives the units a test needs.
Runtime behavior has NOT been play-tested by the implementing task (compile +
static validation only); this checklist is the acceptance gate.

## Test 1: Material colors

Start a battle with red and blue melee units.

- [ ] Blue soldiers: blue tunic and blue shield face.
- [ ] Red soldiers: red tunic and red shield face.
- [ ] Metal (helmet, segmentata, blade) silver on BOTH teams.
- [ ] Brass (shield rim/boss/emblem, fittings) gold on both.
- [ ] Skin tan, leather brown, eyes black on both.
- [ ] Hit flash: brief white flash on damage.
- [ ] Damage darkening: wounded soldiers visibly darker, palette hues retained.
- [ ] After a hit flash the faction color returns (no soldier stuck white/grey).
- [ ] No per-soldier material instances (Frame Debugger / memory profiler: only
      the 8 shared MAT_Roman_* materials; tints via per-slot MaterialPropertyBlocks).

## Test 2: Rear-rank idle (before contact)

- [ ] Rear soldiers stand upright, shields resting vertically at their sides.
- [ ] Swords lowered; no shield waving; no attack animations.
- [ ] Soldiers not perfectly synchronized (phase offsets) but not chaotic.

## Test 3: Formation march

Move a formation across open ground.

- [ ] Short controlled strides, upright torso, shield steady at the side.
- [ ] No casual-walk arm swing; no big vertical bounce.
- [ ] Feet track formation slots; no visual drifting off the gameplay roots.

## Test 4: Front-rank guard

Attack an enemy formation and watch the approach + contact.

- [ ] Front-rank soldiers raise shields (guard) as the enemy comes within ~12 m.
- [ ] Rear ranks stay in the calm at-side idle.
- [ ] Shield covers the torso; sword held ready beside/behind it.
- [ ] After each attack the soldier returns to guard quickly.

## Test 5: Thrust attack

Watch several attacks closely (zoom in).

- [ ] Shield stays essentially planted; barely opens.
- [ ] Sword thrusts around the shield's right edge; compact motion.
- [ ] No whole-body lunge, no shield flail, no root displacement.
- [ ] Quick retraction to guard.

## Test 6: Over-shield attack

- [ ] Sword rises behind/above shoulder, strikes over/at the shield's top edge.
- [ ] Shield remains raised throughout; motion compact.
- [ ] Clean return to guard.

## Test 7: Rear-rank behavior during contact

Watch ranks 2–3 immediately after melee begins.

- [ ] They do NOT all play attack animations at first contact.
- [ ] They hold controlled postures until promoted forward, directly contacted,
      or near the fight (edge-engagement).
- [ ] Note: mechanically, rear ranks only acquire targets within ~3 m
      (personalEngageRadius) while unengaged — if you see rear soldiers dealing
      damage at range with no animation, record it as a gameplay issue.

## Test 8: Combat advance

Observe small forward corrections near contact (rank promotion, auto-close).

- [ ] Engaged/near fighters move in a shield-up shuffle, not a relaxed march.
- [ ] No sprint pose; no large foot sliding.

## Test 9: Hit reaction

- [ ] Normal hits: short readable upper-body recoil + white flash.
- [ ] Soldier does not fly back or leave its slot; feet planted.
- [ ] A soldier hit mid-attack finishes the strike (flash only) — expected.

## Test 10: Death variants

Observe multiple deaths.

- [ ] Both variants appear (backward fall and shield-side collapse).
- [ ] Final poses differ; bodies stay near the death location.
- [ ] No spinning, no launch, no return to idle; corpses sink and despawn as before.
- [ ] "Defeated" label behavior unchanged (stays at death site).

## Test 11: Rotate command

Select an ordered formation away from combat; use ROTATE ~180°.

- [ ] Pivot footwork plays (weight dip + small steps).
- [ ] Gameplay root still performs the actual turn; no double rotation.
- [ ] Shield stays controlled. (Known design note: backward MOVE orders still
      turn soldiers around — unchanged by this task.)

## Test 12: Reform

Break ranks, withdraw, press REFORM.

- [ ] Soldiers converging on slots use the close-ranks shuffle (also used for
      auto-close corrections while Ordered without a move order).
- [ ] Shields return to disciplined side-carry when done.
- [ ] Reform mechanics unchanged.

## Test 13: Performance

One formation → 5v5 → full 400–500-soldier load.

- [ ] No repeated Console errors or missing-clip warnings.
- [ ] Frame rate comparable to the pre-rework build (same clip count per
      Animator; tint updates are event-driven, not per-frame).
- [ ] No material-instance explosion (Profiler memory / Frame Debugger).
- [ ] No animation stalls or culling pops on attack (if pops: widen skinned
      mesh localBounds rather than update-when-offscreen).
- [ ] Do not claim iPhone performance without testing on device.

## Results

Record findings under each test; failures go to
`ROMAN_LEGIONARY_ANIMATION_REWORK_LOG.md` → Unresolved issues.
