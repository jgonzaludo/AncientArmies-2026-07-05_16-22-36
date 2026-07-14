# Roman Legionary Manual Test Checklist

Manual verification for the animated Roman legionary visual
(`VIS_Roman_Legionary_Basic`). Companion to
`ROMAN_LEGIONARY_END_TO_END_REPORT.md`.

## Desktop Editor

- [ ] Scale: legionary visual matches the 1.8 m capsule gizmo (helmet may sit
      slightly above — collider is authoritative).
- [ ] Orientation: soldier faces +Z; visual and movement direction agree.
- [ ] Idle animation plays when stationary.
- [ ] March animation plays while moving; feet don't obviously slide.
- [ ] SwordAttack fires on attack (2x speed).
- [ ] Hit reaction fires when damaged (2.5x speed).
- [ ] Death animation plays; body removed on the normal gameplay timing.
- [ ] Hit flash and damage darkening visible on the Roman mesh.
- [ ] Selection ring visible under animated legionaries.
- [ ] Formation labels readable over animated legionaries.
- [ ] Archers still render as capsules (unchanged).
- [ ] Fallback: rename the prefab away in Resources — melee soldiers spawn as
      capsules with no errors; restore afterwards.

## Full Battle (5v5, ~400–500 soldiers)

- [ ] Frame rate acceptable in editor at full soldier count.
- [ ] Formations still readable at gameplay camera distance.
- [ ] Front/flank/rear engagements readable with animated soldiers.
- [ ] Deaths readable (soldiers visibly fall before removal).
- [ ] No console spam (missing bindings, animator warnings, etc.).

## iPhone Build (LATER — not yet tested, do not claim results)

- [ ] iOS build succeeds with the new FBX/prefab.
- [ ] Frame rate on device at full battle count.
- [ ] Thermals after 10 minutes of play.
- [ ] Memory usage acceptable.
- [ ] Battery drain note.

## Regression

- [ ] Restart mid-match works.
- [ ] Defeated-label timing unchanged.
- [ ] Archer projectile arcs unchanged.
