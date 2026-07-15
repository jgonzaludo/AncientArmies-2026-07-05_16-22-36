# Patch 3 Manual Tests — Animation Quality

Play Mode, `TestSkirmish` (TEST 1) for isolation or `Battle`. None marked
passed until performed.

## Legionary attacks

- [ ] Both attacks start and end in the same guard (no pose pop into/out of
      attacks; transitions to guard are clean)
- [ ] Over-shield: compact strike above the shield, blade leads, shield
      stays put, fast protected recovery — no punch/baseball silhouette
- [ ] Thrust: shield opens narrowly, blade drives nearly straight, shield
      closes over the recovery — one continuous motion
- [ ] No shield deformation, no root drift, attacks play at normal speed
      while march speed varies (locomotion multiplier isolation)

## Archer direct shot

- [ ] Full cycle readable: reach → arrow appears → to bow → nock → draw →
      **string hand anchors beside the cheek/jaw** → brief stable aim →
      release → recovery → firing-ready
- [ ] No floating hand in front of the face at full draw (the fixed defect)
- [ ] Hand arrow appears/disappears at the right moments; projectile timing
      unchanged (spawns on release)
- [ ] No hand-through-head, no backward lean, archer faces the target

## Archer high arc

- [ ] Same anchored draw mechanics with the bow elevated
- [ ] Clean return to ready; no excessive backward arch

## Archer knife

- [ ] Guard reads defensive: dagger at the ribs, point at the threat
- [ ] Stab is a compact blade-first thrust (~0.7 s), not a punch; fast
      retraction to guard
- [ ] Phase 2 dagger visibility (sheath ↔ hand) still correct through
      draws, stabs, hits, and deaths

## Regression

- [ ] Repeated ranged↔melee cycles keep equipment states correct
- [ ] Hits and deaths interrupt attacks cleanly (death always wins)
- [ ] Full battle: no missing clips, no console errors, no perf regression

## Known-limitation acknowledgements (not failures)

- [ ] Bow does not bend / string does not visibly follow the hand (no string
      rig on the approved model — documented)
- [ ] At extreme zoom the nocked arrow doesn't quite reach the bow grip at
      the deepest draw (short prop — documented)
