# Patch 2 Manual Tests — Equipment State and Rigid Attachments

Unity editor, Play Mode, `Battle` or `TestSkirmish` scene (TEST 1 via the
SCENES button isolates a handful of soldiers). None marked passed until
actually performed.

## Test 1: Legionary shield idle

Watch rear-rank idle and front-rank guard at close zoom.

- [ ] Shield remains rigid — no bending, no width change, rim stable

## Test 2: Legionary shield locomotion

Formation walk, combat advance, close-ranks shuffle, pivot.

- [ ] Shield moves as one solid object — no stretching/twisting/scale change

## Test 3: Legionary shield attacks

Watch both sword attacks repeatedly (slow motion via low timeScale if handy).

- [ ] Shield rigid through anticipation, impact, and recovery
- [ ] Any awkward sword pose is a Phase 3 issue, NOT a shield deformation

## Test 4: Legionary hit and death

- [ ] Shield stays rigid and follows the body through hits and both deaths
- [ ] No mesh explosion, no detachment

## Test 5: Archer ranged state

- [ ] Dagger visible in the sheath at the left hip
- [ ] Nothing in the free hand (no flat slab, no floating arrow)
- [ ] Bow held normally; quiver on the back

## Test 6: Knife draw

Let an enemy reach an archer (TestSkirmish makes this easy).

- [ ] Sheathed dagger disappears as the hand pugio appears (~mid-draw)
- [ ] No prolonged double dagger, no prolonged no-dagger
- [ ] The hand weapon is a recognizable dagger (blade/grip/pommel), not a slab

## Test 7: Knife guard and attack

- [ ] Dagger in hand, sheath empty (leather backing reads as empty scabbard)
- [ ] Dagger stays rigid in the hand; no floating or independent rotation
- [ ] Stab pose quality is deferred to Phase 3

## Test 8: Return to ranged

Kite the enemy away and back several times (ranged ↔ melee ↔ ranged).

- [ ] Dagger returns to the sheath when melee ends (instant handoff — known)
- [ ] Never two daggers, never zero daggers after cycles
- [ ] Bow behavior unchanged; firing resumes correctly

## Test 9: Hit interruption

Take hits during ranged, draw, knife guard, and knife attack.

- [ ] Equipment state resolves correctly after each hit
- [ ] No duplicate or permanently hidden weapon

## Test 10: Death

Kill archers in ranged state, mid-draw, in knife guard, and mid-stab.

- [ ] Equipment state freezes at death (no post-death switching)
- [ ] No detached dagger, no Console errors

## Test 11: Full battle

- [ ] No repeated equipment errors in a 5v5 battle
- [ ] Blue archers' sheathed dagger tints with the faction palette (no red
      flecks on blue soldiers)
- [ ] No missing visuals, no per-soldier material instances (Frame Debugger
      spot-check optional), no combat behavior change
