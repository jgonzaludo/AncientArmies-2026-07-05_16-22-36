# Decisions

Dated record of decisions that shape the project. Newest first. A decision
stays in force until a later entry supersedes it.

---

## 2026-09-21 — Chunks B, C, D: battles end, panic inputs, officer grades

Built the same day as the morale approval below; this entry records what
shipped and the calls made while building it.

**Routing is permanent.** `routedCanRally` (MoraleConfig) is off: routers run
off the map and are removed from the battle — not casualties, not
targets, never coming back. The rally code from Chunk A stays behind the
flag. A permanently routed formation counts as fallen for the collapse rule.

**Battles end by line collapse.** A team loses the moment `collapseFraction`
(0.5) of its formations are Routing or destroyed, counted from
`BattleSetup.formations` filtered by team — never an assumed 8. Ending the
battle at that moment is the latch. Both teams collapsing on the same check is
a draw. The old "no living soldiers" check is gone.

**Time limit.** `battleTimeLimit` 600 s, a stall backstop: the countdown is
small and low-contrast until the final `timerWarningSeconds` (60). On expiry
the side with more intact formations (alive and not Routing; BrokenRanks
counts as intact) wins; equal is a draw. Result text states the reason:
`… — RED LINE COLLAPSED`, `… — TIME`, `DRAW — …`.

**No Reform command.** The button and all player reform input are removed.
BrokenRanks reforms automatically for both teams through the shared
`CanStartReform` rule; `EnemyCommander.TryReformBroken` and its safe radius
are deleted, so both sides play by identical logic. A charged century is
uncontrollable until it is out of melee and reformed.

**Officer grades** live in `OfficerRanks`, the second ScriptableObject,
referenced from `BattleSetup` with the same loud-error-on-null rule. One entry
per `SoldierRole`: grade (open-ended integer, higher outranks lower, 5+
reserved for army-level officers) and `deathShock`. Legionary/Archer 0/0,
Tesserarius 1/5, Cornicen 1/5, Signifer 2/35, Optio 3/15, Centurion 4/30.
The commanding officer is the highest-grade living officer; succession is
implicit. `TemporaryOfficerRanks` is gone.

**Reform anchor.** With `reformOnStandardFirst` (on), a reform plants the
standard on the Signifer if he is alive in the main pack; else on the
highest-grade officer in the main pack; else on the pack's center. No special
leaderless behavior yet.

**Stop chasing.** A formation whose attack target routs or dies clears the
order and holds position and facing — ranged units included. Routing
formations are excluded from every order and charge target query (player,
AI, charge resolution). Charge pursuit is unchanged: pursuing packs still hunt
routers, which is where historical casualties came from.

**Panic inputs**, all through `FormationMorale.AddShock` or its decay, each
behind an enable bool in MoraleConfig:

- *Flank/rear* (`enableFlankShock`): a casualty's shock is multiplied by the
  zone of the killing blow — front 1, flank 2, rear 3. The zone rides with the
  blow from the attack through any deferred hit; arrows count as front.
- *Outnumbered* (`enableOutnumberShock`): shock decay becomes
  `shockDecayPerSecond / (1 + 1 × extra enemy formations engaged)`. No constant
  input — below the decay rate it would do nothing.
- *Contagion* (`enableContagion`): a formation routing adds 15 shock to every
  same-team formation within 30 m anchor to anchor.
- *Officer death* (`enableOfficerShock`): an officer killed (not one who fled)
  adds his `deathShock` to his formation, on top of the normal casualty shock.

**Display.** A wavering halo behind the badge (never over it — badge color is
faction identity): shown below `haloEnter` 40, hidden above `haloExit` 43,
yellow → red and pulsing 1 → 2.5 Hz as morale falls toward 15. The pulse
carries the signal so it survives colorblindness and red banners. Both teams.
A Routing formation has no banner. Officer grade numbers show above officers
of the selected or inspected formation only.

**Scale-back, same day.** `panicScale` 0.65 on all non-officer shock; ranged
kills weigh 0.5; panic intake capped at 8/s (queued, not discarded; officer
deaths bypass); ceilings 2.5 contact / 1.5 rest; officer death shock
24 / 28 / 12 / 4 / 4; halo band 50 / 53. Seeded 2× check: first rout 30 s after
contact at ~33 % losses with shock ≈ 1–6 — routs now come from the contact
ceiling, so the remaining pacing lever is lethality (`globalDamageScale`), not
panic.

**Design change, same day: routs come from encirclement and sudden shock, not
from frontal grinding.** Ceilings 1.4 contact / 1.0 rest (grind alone routs
at ~60 % losses); `frontShockWeight` 0.25; burst rule (casualties beyond 5 % of
starting strength inside 3 s weigh 2.0, higher of zone and burst weight);
surround shock 8/s per engaged zone beyond the first, unscaled and uncapped.
`FormationMorale` keeps per-source totals (`ShockSource`) and `BattleRecorder`
writes the breakdown and primary cause with every rout. Seeded 2× checks at
`globalDamageScale` 0.15: no-input battle — first rout 73.7 s after contact,
all seven routs at 54–62 % casualties with Grind primary, collapse at 112 s;
flank test — pinned century routed 7.8 s after the flank contact at 11 %
casualties, Surround primary.

**Combat by facing, 2026-09-22.** `CombatConfig` is the third ScriptableObject:
`globalDamageScale` 0.15, front ×0.5 / flank ×1.5 / rear ×2.0, arcs 60°; the
fields left `BattleSetup`, so no combat number is scene-pinned. Ranged damage
uses the same multipliers (arrows at half from the front — kept, historically
right). **Zones are decided by formation facing, never by agent position**:
a formed attacker facing roughly opposite the defender is frontal, roughly the
same way is rear, otherwise a flank; BrokenRanks and Routing attackers fall
back to the striking soldier's position. The same rule feeds damage, flank /
rear shock and surround counting (an enemy formation counts only with
`surroundMinEngagedFraction` 0.10 of our men in contact with it). Per-soldier
zone reads were tried and rejected: engaged lines interleave 1–2.5 m on
average (up to ~6 m), so positions lie. Officers get `healthMultiplier`
(Centurion / Signifer 2.0, Optio 1.5). `contactCeilingScale` 1.1. Seeded 2×
checks: adjacent lines — 21,638 hits all frontal, surround never fired, no
routs in 180 s, ~11 % casualties at 60 s and ~60 % at 180 s; flank test —
pinned century routed 31 s after flank contact at 13.75 % casualties,
Surround primary, Signifer / Optio / Tesserarius / Cornicen alive.

**Observed, not tuned.** The first no-input battle chain-routed at 5–12.5 %
losses (contact burst + zone weights + contagion stacking) and ended 36 s
after first contact at 7.7 % loser casualties — far under the 15–30 % target.
Tuning happens on device against the enable bools; nothing was adjusted to hit
the test targets.

---

## 2026-09-21 — Morale, routing, automatic recovery, and the win condition

**Morale is approved.** This supersedes the "No morale" clause in the standing
Scope convention below, which is updated to match. Terrain, progression,
multiplayer, and advanced enemy tactics still require explicit approval.

**Every formation has a Morale value from 0 to 100.** Casualties lower it.
Morale recovers with time out of melee, up to a **rest ceiling** set by the
formation's cumulative losses — a century that has lost more men cannot
recover as high. While in melee, a stricter **contact ceiling** applies, so a
slow, steady bleed eventually routs a unit even when no single moment of
losses would.

**All morale tunables live in a new `MoraleConfig` ScriptableObject, never as
fields on `BattleSetup`.** `BattleSetup` fields are pinned by `Battle.unity`,
so a changed C# default can silently do nothing; a ScriptableObject asset
avoids that. This is the project's first ScriptableObject.

**States follow the original four-state design.**

- **Ordered** and **Engaged** — unchanged.
- **Disordered** — the design name for the live `BrokenRanks` state. Loose,
  agent-native combat; soldiers keep fighting. This is what a century becomes
  after a charge. Behavior unchanged.
- **Routing** — a new state, entered when morale falls below 15. Soldiers flee
  and stop acquiring targets, the formation's badge disappears, and the
  formation cannot be ordered. It rallies automatically once morale is above
  35 and it is not in melee. Arrow fire does not block rallying directly, but
  casualties from it still lower morale. A routing formation with pursuers
  within melee range therefore cannot rally.

`Attacking`, `Withdrawing`, `Reforming`, and `Charging` are not changed by
this decision.

**The Reform button is removed; all recovery is automatic.** Disordered
formations reform on their own after disengaging (see *Automatic reform*
below). Routing formations rally on their own when morale recovers. Reforming
still requires disengagement from melee — the game decides when, not the
player.

**Automatic reform.** A Disordered formation reforms automatically whenever
it is **not in melee**. Only active melee blocks it: enemies nearby do not,
and arrow fire does not. The soldiers run to their slots and reform even with
enemies close. One shared rule serves both sides — it replaces the
`reformSafeRadius` check in `EnemyCommander.TryReformBroken`, and the AI gets
no rule of its own.

"In melee" uses the code's existing engagement check: a soldier is engaged
when an enemy is within `personalEngageRadius` (3 m), scanned every 0.25 s.
A formation is in melee while at least one soldier is engaged. A reform starts
when none are; a reform already under way keeps its existing abort rule (12 %
of survivors engaged, `reformAbortEngagedFraction`).

*Consequence, accepted:* a Disordered century after a charge cannot be
controlled until it is out of melee and has reformed. A charge is a
commitment.

**`MoraleConfig` wiring.** `BattleSetup` holds a serialized reference to the
`MoraleConfig` asset, assigned in `Battle.unity`. That reference is the only
morale-related field on `BattleSetup`. If it is null, the game logs a loud
error — it never silently falls back to default values.

**Win condition.** A side collapses when 50 % of its formations are Routing or
destroyed. The count is read at runtime from that side's actual formations —
never a hardcoded 8 — so a different army size works unchanged. **Collapse
latches:** once a side reaches it, the battle is decided, even if units rally
afterwards. This replaces the current rule, which ends the battle only when one
side has no living soldiers.

**Time limit.** A visible 10:00 countdown. When it expires, the side with more
intact formations wins. Draws are allowed.

**Not yet decided.** Deliberately left open, to settle before or during
implementation:

- Recovery rates, and the exact shape of the rest and contact ceilings.
- Whether a reform that aborts on melee contact retries immediately or after
  a delay. Soldiers running to their slots can pass within 3 m of an enemy,
  abort, and restart the moment contact clears.
- Where a routing formation flees to, and where it re-forms after rallying.
- Whether "50 %" means at least half, and how odd formation counts round.
- What counts as "intact" at the time limit — for example, whether a
  Disordered formation counts.
- Whether both sides collapsing on the same check is a draw.
- Moving the simulation (Formation pipeline, engagement scan, morale tick,
  attacks, projectiles, AI — all `Update`/`Time.deltaTime` today) to
  `FixedUpdate` or substepping it. Needed for speed-independent results and,
  later, PvP determinism; only soldier movement is fixed-step now.

---

## 2026-07-24 — Art pipeline hard reset: Tripo Studio Pro + Blender

**Generator.** **Tripo Studio Pro is selected as the first 3D generator.** Pro
was purchased and the Tripo → Blender DCC Bridge transfer was verified working.

**Removed from the pipeline.**

- **AssetHub is removed from the current pipeline.**
- **Meshy is removed from the current pipeline.**

Neither may be revived as the active pipeline without a new decision recorded
here.

**Blender owns production.** **Blender owns the canonical rigs and production
assets** — one canonical humanoid skeleton, skin weights, sockets, materials,
and the `.blend` source files. `.blend` is the editable source of truth; FBX is
the Unity handoff.

**Mixamo is optional.** Mixamo is an optional source for **generic motion
only**. Equipment-specific animation and all final cleanup happen in Blender.

**Tripo Unity Bridge is preview and testing only.** Installed as a local Unity
package at `ThirdPartyPackages/TripoUnityBridge/`, opened from
`Tools → Tripo Bridge`. Raw Tripo → Unity transfer is for verifying the bridge
and for quick previews. It is not a production path, and generated assets may
not enter production runtime folders without explicit approval.

**Production assets require manual approval.** Two human gates: Tripo output
approval before Blender, and model/animation approval before FBX export into
Unity. Automation handles validation and plumbing, never subjective creative
approval.

**Old documentation and art experiments were intentionally removed.** Every
first-party Markdown document, the entire `docs/` tree (reference sheets,
turnarounds, review renders, production logs, patch notes, art specifications),
the previous `ArtSource/` folder organization, all Meshy-generated assets and
Meshy skill installs, the `meshy_output/` directory, and the unreferenced
`Assets/_SpikeHumanoid` retarget spike were deleted rather than archived.
Documentation was rewritten from scratch against the actual repository.

**Git history preserves the removed work.** No `Archive/`, `Legacy/`, `Old/`,
or `_Deprecated/` directory exists or should be created. The tag
`pre-tripo-phase-1-hard-reset` marks the tree immediately before cleanup.

*Caveat, recorded honestly:* `ArtSource/` and `meshy_output/` were gitignored
and untracked, so git history does **not** contain them. Their deletion was
permanent and was confirmed as intentional. Roughly 1.5 GB of superseded
Blender masters and Meshy candidate exports are gone for good.

**Runtime placeholders retained.** The existing `Assets/Art/` legionary and
archer models, materials, animator controllers, banner sprites, and battlefield
textures were kept. They are visually superseded but every one of them is
referenced by `Battle.unity`, a prefab, or a runtime `Resources.Load` call —
the game does not run without them. They are temporary placeholders pending
Tripo/Blender production.

---

## Standing conventions

**Git.** Work on `dev`; `main` receives only owner-tested bundles. Versions are
`0.MINOR.PATCH` git tags on `main`. Commits follow Conventional Commits with
game scopes.

**The test gate.** Never commit, push, merge, or tag a gameplay change until
the owner has manually tested it in Play mode and explicitly approved it.

**Scope.** No terrain, progression, multiplayer, or advanced enemy tactics
without explicit approval. (Morale was approved on 2026-09-21.) Prototype
before polish; prefer the smallest implementation that proves the gameplay
thesis.

**Git LFS is not configured** and was deliberately not introduced during the
reset. See the note in `README.md`/`Docs/ART_PIPELINE.md` about file sizes —
this will need revisiting once real `.blend` masters land in `ArtSource/`.
