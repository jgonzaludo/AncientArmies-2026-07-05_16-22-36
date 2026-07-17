# Patch 4 Manual Tests — Combat-Aware Reform and Formation Maneuvers

Play Mode, `TestSkirmish` (TEST 1) for isolation, `Battle` for scale.
None marked passed until performed.

## Reform

- [ ] Outside combat: free soldiers reform normally, no snapping, completes
- [ ] During legionary attacks: attackers stay planted, strikes finish,
      correction resumes within ~0.3 s after
- [ ] During archer draw: no sliding at full draw; the arrow still releases
      and the projectile behaves as before; correction resumes in recovery
- [ ] During dagger stab: stab stays planted; Phase 2 dagger states stay
      correct; correction resumes after
- [ ] Hit reaction: correction pauses ~0.45 s, no teleport, resumes
- [ ] Death: dead soldiers are never dragged; Reform still completes (85%
      rule) with dead/locked members present

## Automatic close-ranks

- [ ] In melee: vacancies fill via column promotion only (no global shuffle,
      no fighters dragged)
- [ ] Out of melee: compaction still works after ≥2 losses
- [ ] Never runs during a Rotate maneuver

## Small turn (≤20°)

- [ ] Soldiers pivot mostly in place (pivot step), no wheel, no translation
- [ ] Ground arrow and final soldier facing agree

## Left and right wheels (20°–135°)

- [ ] Right turn wheels around the front-right corner, left around front-left
- [ ] The block stays rectangular through the whole turn (no liquid melt)
- [ ] Outer flank marches the long arc; inner flank barely steps
- [ ] Nobody cuts through the middle or backpedals long distances
- [ ] Facing arrow rotates smoothly with the turn

## About-face (≥135°)

- [ ] Soldiers turn in place (near-zero displacement)
- [ ] Old rear rank is now the front (guard stances move to the new front)
- [ ] No giant wheel, no front/rear position exchange

## Combat-locked wheel

- [ ] Order a wheel, then let the enemy hit the formation: the wheel pauses
      while fighters are committed, attacks are not cancelled, and the turn
      resumes/completes when combat eases — command is not lost
- [ ] A new move order cleanly supersedes an in-progress wheel

## Full battle

- [ ] No visible combat sliding (the headline fix)
- [ ] No permanent maneuver lock; redressing always ends (≤4 s)
- [ ] No repeated console errors; performance acceptable at 5v5
- [ ] Front/flank/rear damage feel unchanged (values untouched)
