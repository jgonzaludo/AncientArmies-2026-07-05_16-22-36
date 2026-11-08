# Combat stats and time to kill

Pulled 2026-10-08 from the values the game actually runs (the **scene-pinned**
`Battle.unity` unit stats, `CombatConfig.asset`, `OfficerRanks.asset`, and the
constants in `Soldier.cs`). The C# defaults in `BattleSetup` differ from the
scene for several fields (e.g. Swordsmen `moveSpeed` 3.2 in code, **2.7** in the
scene), the scene wins.

## Inputs

| Unit | HP | Move speed | Damage | Cooldown | Reach | Projectile |
|---|---|---|---|---|---|---|
| Swordsmen | 35 | 2.7 | 12 | 4.0 s | 1.7 m strike | - |
| Archers | 22 | 3.0 | 10 | 6.5 s | bow 48 m, min 2.5 m; sidearm 1.6 m | 20 m/s, flight 0.35–2.4 s |

- **Per hit** = `attackDamage × skill × zoneMultiplier × globalDamageScale`.
  - `skill` is fixed per soldier, 0.85–1.15 (average 1.0).
  - `zoneMultiplier` (CombatConfig): front **0.5**, flank **1.5**, rear **2.0**
    (front arc ±60°, rear arc ±60°, from formation facing).
  - `globalDamageScale` = **0.15** (the single pacing dial).
  - An archer's sidearm hits at 0.4× the damage; charge impact adds
    `ChargeDamageMultiplier` to the first blows (not included below).
- **Cadence:** cooldown × a random 0.9–1.15 (mean ×1.025). Sword impact lands
  0.30 / 0.40 / 0.43 s after the swing starts (stab / chop / slice, rolled
  50 / 25 / 25 %). Arrows are gated by the formation's volley: 1.5 s window every
  8 s, so each archer fires **once per 8 s** even though its cooldown is 6.5 s.
  An arrow always lands unless the target dies in flight (zone multiplier is
  baked in at fire time).
- **Officer health:** Centurion ×2, Signifer ×2, Optio ×1.5, Tesserarius ×1,
  Cornicen ×1 (applied to the unit's base HP). Archer centuries have no
  Centurion.
- At most 3 melee attackers per target (`MaxMeleeAttackersPerTarget`); archers
  spread their targets.

## Damage per hit

| Attacker | Front | Flank | Rear | Cadence |
|---|---|---|---|---|
| Sword (legionary), skill 0.85 / 1.0 / 1.15 | 0.76 / 0.90 / 1.03 | 2.29 / 2.70 / 3.10 | 3.06 / 3.60 / 4.14 | 4.1 s |
| Archer sidearm | 0.26 / 0.30 / 0.34 | 0.76 / 0.90 / 1.03 | 1.02 / 1.20 / 1.38 | 6.7 s |
| Arrow | 0.64 / 0.75 / 0.86 | 1.91 / 2.25 / 2.59 | 2.55 / 3.00 / 3.45 | 8 s |

## Time to kill: ONE attacker, average skill, from the first hit to death

| Target | HP | Hits (sword) front / flank / rear | Sword TTK | Hits (arrow) | Arrow TTK |
|---|---|---|---|---|---|
| Legionary, Tesserarius, Cornicen (melee century) | 35 | 39 / 13 / 10 | 156 s / 49 s / 37 s | 47 / 16 / 12 | 368 s / 120 s / 88 s |
| Optio (melee century) | 52.5 | 59 / 20 / 15 | 238 s / 78 s / 57 s | 70 / 24 / 18 | 552 s / 184 s / 136 s |
| Signifer, Centurion (melee century) | 70 | 78 / 26 / 20 | 316 s / 102 s / 78 s | 94 / 32 / 24 | 744 s / 248 s / 184 s |
| Archer, Tesserarius, Cornicen (archer century) | 22 | 25 / 9 / 7 | 98 s / 33 s / 25 s | 30 / 10 / 8 | 232 s / 72 s / 56 s |
| Optio (archer century) | 33 | 37 / 13 / 10 | 148 s / 49 s / 37 s | 44 / 15 / 11 | 344 s / 112 s / 80 s |
| Signifer (archer century) | 44 | 49 / 17 / 13 | 197 s / 66 s / 49 s | 59 / 20 / 15 | 464 s / 152 s / 112 s |

(Steady state: `(hits − 1) × cadence`; add 0.4 s for the first sword impact and
the arrow's flight time, distance / 20 m/s, for the first arrow.)

Per-attacker damage rate (front, average skill): sword **0.22 HP/s**, arrow
**0.094 HP/s** per archer, sidearm 0.045 HP/s. A flank or rear hit is 3–4× a
front hit, which is what makes flanking decisive.

## Measured in battle

The battle recorder now records every death: victim `unit/role`, the cause
(`arrow` = killed by a ranged hit, `sword` = any melee blow, including an
archer's sidearm), time from the victim's first damage to death, and total hits
taken. It is in the JSON log under `summary.kills`. Seed 12345, AI DoubleFlank
vs an idle Blue army, 240 s (mixed attackers and zones, so these are far below
the single-attacker figures above):

| Victim | Killed by | Kills | Avg first-damage-to-death | Range | Avg hits taken |
|---|---|---|---|---|---|
| Swordsmen / Legionary | sword | 72 | 64 s | 16–135 s | 34 |
| Swordsmen / Legionary | arrow | 44 | 65 s | 9–142 s | 32 |
| Archers / Archer | sword | 10 | 44 s | 30–67 s | 25 |
| Swordsmen / Centurion | sword / arrow | 2 / 2 | 42 s / 72 s | 13–79 s | 84 / 60 |
| Swordsmen / Signifer | sword / arrow | 1 / 1 | 80 s / 71 s | - | 89 / 88 |
| Swordsmen / Tesserarius | sword | 3 | 81 s | 34–106 s | 32 |
| Swordsmen / Cornicen | arrow | 1 | 120 s | - | 33 |

## What this says

- A single front-on legionary takes **about 2.6 minutes** to kill with one
  sword and **about 6 minutes** with one archer's arrows. Fights are decided by
  numbers on the target and by direction, not by single duels.
- `globalDamageScale` scales every figure linearly (0.15 → 0.30 halves every
  TTK). That is the first dial if fights feel too slow on device.
- Arrows are weak per archer (about 0.4× a swordsman's rate) but ignore the
  3-attacker cap and reach 48 m; the 8 s volley window is what holds their rate
  down, not the 6.5 s cooldown.
