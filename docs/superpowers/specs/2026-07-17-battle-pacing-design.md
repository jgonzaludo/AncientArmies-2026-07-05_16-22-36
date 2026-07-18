# Battle Pacing Design — Total War-style tempo (v1.9 + v1.10)

Decisions from brainstorming (2026-07-17): full battles 10–15 min at Total War
pace; individual engagements grind 1–3 min; kills stay lethal (3–4 hits) and
CADENCE is the pacing lever, never HP sponging; tuning ships first (v1.9),
morale/rout ships next (v1.10).

## Why battles were fast

Kill throughput = fighters ÷ (hits-per-kill × cooldown). With spread targeting
removing overkill waste, a 1.4 s melee cooldown let a century-vs-century
frontal fight resolve in ~12 s, and archer output could delete a century in
~10 s. Battles ended only by annihilation, so pacing had no second lever.

## v1.9 — Cadence tuning + volley fire (THIS PATCH)

| Value | Old | New | Rationale |
|---|---|---|---|
| melee `attackCooldown` | 1.4 s | 4.0 s | front-line kill every ~14 s → century grind ~1.5–2 min; guard loop fills the gaps (shield-line "measuring") |
| archer `attackDamage` | 14 | 10 | revert; 4 front arrows / 3 flank / 2 rear per kill |
| archer `attackCooldown` | 3.0 s | 6.5 s | must stay under the volley interval (with the 0.9–1.15 jitter) so every archer is ready each volley; effective rate is governed by the volley interval |
| NEW `Formation.volleyInterval` | — | 8 s | ranged formations fire as a formation, not a drizzle |
| NEW `Formation.volleyWindow` | — | 1.5 s | arrows loose across a jittered window → reads as a TW volley, not one frame |

Volley mechanics: each ranged formation runs a volley clock (staggered per
formation so centuries don't sync with each other). A soldier's ranged attack
additionally requires the volley window to be open and its personal
deterministic jitter (from slot index) to have elapsed within the window.
Melee is untouched by volley logic. Archer century output: ~45–55 s of
sustained fire to destroy a century → softens a charge ~20–25%, support arm
not deletion arm.

All three stat values are scene-pinned in `Battle.unity` — the patch stamps
the scene component AND the C# defaults (per the scene-serialization rule).

Expected outcome: annihilation battles run ~7–9 min after first contact;
engagements 1.5–2 min; flanking maneuvers become executable mid-fight.

## v1.10 — Morale, rout, rally (NEXT PATCH, approved design)

- Century morale 0–100. Drains: own casualties (weighted higher while flanked
  or rear-attacked), sustained arrow fire, a nearby friendly century routing
  or being destroyed. Slow regen while disengaged.
- Break point → `Routing` state: flees toward its own map edge at run speed,
  no orders accepted, banner shows rout treatment (morale bar finally fills
  the banner slot reserved in the adaptive-banner patch).
- **Rally (TW-style):** a routing century that escapes pursuit (no enemy
  within a radius for N seconds after fleeing far enough) halts, slowly
  reforms, becomes commandable. Breaking at rock-bottom morale or breaking
  repeatedly = **shattered**: flees off-field permanently.
- Rout-proximity morale drain produces cascading collapses; victory = enemy
  army dead OR routed (typically ~30–40% casualties), then a pursuit phase.
- Same rules for both armies; AI commander gains rout-awareness triggers.

## Non-goals

Fatigue, block/miss dice (invisible randomness), per-soldier morale, and any
HP inflation — rejected during brainstorming.
