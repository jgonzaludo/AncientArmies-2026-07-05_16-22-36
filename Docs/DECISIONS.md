# Decisions

Dated record of decisions that shape the project. Newest first. A decision
stays in force until a later entry supersedes it.

---

## 2026-10-08: Interim soldier art: KayKit characters (CC0)

**Decision (owner, via the KayKit device-test brief):** replace the capsule
placeholders with the free CC0 KayKit Adventurers Knight and the Rig_Medium
animation packs, for a device test of the rig, animations and performance
before any custom art. This is an interim test, not a change to the long-term
art pipeline: Tripo + Blender remains the recorded production route and the
asset-pipeline gate is still undecided (KayKit's shared 23-bone skeleton is
also the candidate base for a future custom body; Mannequin_Medium is kept in
`Assets/ThirdParty/KayKit` for that). Raw files sit in `Assets/ThirdParty/KayKit`
with both licenses. Git LFS is still not configured (about 11 MB added).

How it is built and wired is documented in `TECHNICAL_ARCHITECTURE.md`
(Presentation layer) and the clip table in `CURRENT_GAME_SPEC.md`. Switch back to
the capsules with `KayKitVisualConfig.useKayKitVisual = false`.

Measured in the Unity editor on a Mac (NOT a phone), seed 12345, battle at 2x:
mid zoom 358k triangles / 547 batches / ~49 fps (capsules: 351k / 537 / ~47 fps,
so the frame is dominated by the simulation, not the models); near zoom 664k /
267 / ~72 fps; far zoom (impostors) 20k / 109 / ~94 fps. Before merging the
sword and shield into the body mesh mid zoom was 1.23M triangles / 1763 batches
/ ~35 fps. Phone numbers are still to be measured.

---

## 2026-09-30: Ordering: destination + facing, destination fix, target lock

**Implemented (step 1, awaiting device test):**
- **Ghost march tried and removed.** Letting marching formations pass through friendly soldiers stopped archers being shoved (up to 20 m to 0.4 m) but caused visible clipping and bursts when the unit turned solid again. Owner decision: ghosting is not a feature. Melee-on-melee contact stays physical. Archers caught in a marching unit's path are for route planning and a rescue rule to solve (step 2).
- **Destination fix.** A move order never ends inside a friendly formation's footprint (where it stands or is headed). Toggle: `CombatConfig.destinationFix`.
- **Target lock.** An Attacking formation keeps marching at its ordered target while a corner brushes a bystander. It stops to fight only when contact with non-target enemies reaches 40% of its men for 1.5 s, or it is surrounded; contact with the ordered target always engages. Tunables: `targetLock`, `targetLockBlockedFraction`, `targetLockBlockedSeconds` (CombatConfig). Without routing it only delays a blocked unit (~5 s in the `lock` test), it cannot yet go around.

**Implemented (step 2, awaiting device test):**
- **Route planning.** `RoutePlanner` bends a straight leg around friendly blocks (soft) and enemy blocks (extra 7 m margin, so a plain move stays out of contact range), shorter side first, with string-pulling. `Formation.SteerAlongRoute` follows it, re-planning every 0.75 s or when the goal moves; an attack goes straight at its target but bends around bystanders. No route found = straight line. Toggle: `CombatConfig.routePlanning`.
- **Path display.** `RouteLines`: every moving friendly unit draws one quiet line with an arrowhead (enemy routes are never drawn); selected units draw brighter and thicker; units heading to the same place (anchors within 40 m, destinations within 25 m, same order kind) share ONE arrow from the group centre. The line shows the remaining route, so it trims as the unit walks and vanishes on arrival. Destination ghost slot dots and facing arrows already existed.
- **Facing.** Destination + facing were already stored (`DestinationFacing`, second-finger twist) and applied on arrival; unchanged.
- **AI plans.** `EnemyCommander.plan`: Random (seeded) / Frontal / FlankOne / DoubleFlank. Flankers leave the lane scorer, march a routed course to a staging point 28 m outside the enemy line abreast of it, then attack the nearest block from the side; released early if caught in melee or routing. Test (seed 12345, 2x, idle Blue): DoubleFlank produced 1318 flank and 242 rear hits against 0 for Frontal.
- **Yield to marching friends.** A standing friendly soldier within 2.6 m of a marching friendly block sidesteps out of its line (up to 3 m/s) instead of being pushed or carried along; it returns to its slot by itself after the block passes. Added after the `b2f` (rear centuries ordered through archers and the front line) test showed soldiers dragged up to 70 m even with routing: peak displacement 66 m -> 7.7 m, 0 of 480 soldiers more than 3 m from their start afterwards. Toggle: `CombatConfig.yieldToMarching` (`yieldDistance`, `yieldSpeed`).
- **Planner upgrade.** Shortest path over a visibility graph of obstacle outlines (replaces the two-step detour), so a solid friendly line is walked around by its end. Friend margin 2 m.
- **Drift fix.** The arrival turn onto the stored facing wheeled around the block's front corner, shifting the centre by up to ~6 m (archers). Arrival turns now pivot on the block's centre; the tactical Rotate command keeps its corner wheel. `drift` scenario: placed spot vs real anchor 6.1 m -> 0.2 m, soldiers 0.05 m from slots.
- **Facing locked.** A move order keeps the facing the unit had when it was ordered (the block still turns to march, then turns back on arrival); only the player's twist changes it. Before, the default was the direction of travel.
- **Recall.** A soldier more than 2 m from its slot and not in melee contact abandons everything else and runs back at 1.25x speed.
- **Roomy routes.** The planner tries clearance tiers widest first (friend/enemy margin 10/12 m, then 6/8, 3/5, 1/3) and takes the widest one whose route is not longer than 2.6x the straight distance + 30 m, so blocks keep room from friends and brush them only when nothing roomier exists. Same planner, yield, recall and re-slot rules apply to the enemy AI (verified with a mirrored Red back-to-front run: 0.0 m drag).
- **Recall beats the bow.** A displaced soldier's recall now overrides the shooting lock (a drawn bow zeroed it, which is why archers stayed put) and cancels the pending shot. Standing friends also give way to a friend running back to its own slot (sidestep across the returner's path to its slot).
- **Re-slot stragglers.** A soldier more than 3 m from its slot and not moving for 2 s (its slot walled in by its own ranks, gaps narrower than a body) triggers the minimal-displacement re-slot used by reform. In the `drift` test the stuck archer returned within ~20 s.
- **Path-line stability.** Arrow groups are fixed by the order (same command batch, destinations within 25 m), not by current positions, so arrows no longer split, merge or jump as units drift apart; the group's line keeps the first member's route shape carried to the group centre. Routes are kept while still clear instead of re-planned every second; the visibility graph has 16 outline points per obstacle plus string-pulling, so routes are a few straight legs. Line height 0.4 m, thicker.
- **Facing preserved in the order UI.** The drag preview (slot dots + arrow) shows each unit's own CURRENT facing until the second finger twists; it no longer turns to the direction of travel, and the issued order keeps that facing.
- **Targets, smooth routes, victory screen (2026-10-30).** Sticky targets and latched
  recall: reversals among men with a live target 1.48% -> 0.60% of moving samples (seed
  12345, 2x). Route smoothing: path kinks over 25 degrees 3 -> 0, max turn 52 -> 22
  degrees, arrival and drag unchanged. Edge-contact swing-in built but DISABLED: even
  with a two-sided-contact guard it caused a Surround rout at <1% casualties in the
  `edge` test; needs another pass before it ships. Victory screen: reason + totals +
  per-formation table. Test hygiene: the harness now runs with cameras ON (watchable).
- Not done: hold-and-bait AI plan; AI repath-on-friendly-block uses the same 0.75 s replan.

**Tests:** scenarios `pass` (melee marches through friendly archers) and `lock` live in `ScenarioRunner`. Without route planning `pass` shows archers displaced 20 m max (8 m average); with it the average drops to 0.5 m (max still 23 m for one unit in this stress case).

---

## 2026-09-21: Chunks B, C, D: battles end, panic inputs, officer grades

Built the same day as the morale approval below; this entry records what
shipped and the calls made while building it.

**Routing is permanent.** `routedCanRally` (MoraleConfig) is off: routers run
off the map and are removed from the battle, not casualties, not
targets, never coming back. The rally code from Chunk A stays behind the
flag. A permanently routed formation counts as fallen for the collapse rule.

**Battles end by line collapse.** A team loses the moment `collapseFraction`
(0.5) of its formations are Routing or destroyed, counted from
`BattleSetup.formations` filtered by team, never an assumed 8. Ending the
battle at that moment is the latch. Both teams collapsing on the same check is
a draw. The old "no living soldiers" check is gone.

**Time limit.** `battleTimeLimit` 600 s, a stall backstop: the countdown is
small and low-contrast until the final `timerWarningSeconds` (60). On expiry
the side with more intact formations (alive and not Routing; BrokenRanks
counts as intact) wins; equal is a draw. Result text states the reason:
`…, RED LINE COLLAPSED`, `…, TIME`, `DRAW, …`.

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
order and holds position and facing, ranged units included. Routing
formations are excluded from every order and charge target query (player,
AI, charge resolution). Charge pursuit is unchanged: pursuing packs still hunt
routers, which is where historical casualties came from.

**Panic inputs**, all through `FormationMorale.AddShock` or its decay, each
behind an enable bool in MoraleConfig:

- *Flank/rear* (`enableFlankShock`): a casualty's shock is multiplied by the
  zone of the killing blow, front 1, flank 2, rear 3. The zone rides with the
  blow from the attack through any deferred hit; arrows count as front.
- *Outnumbered* (`enableOutnumberShock`): shock decay becomes
  `shockDecayPerSecond / (1 + 1 × extra enemy formations engaged)`. No constant
  input, below the decay rate it would do nothing.
- *Contagion* (`enableContagion`): a formation routing adds 15 shock to every
  same-team formation within 30 m anchor to anchor.
- *Officer death* (`enableOfficerShock`): an officer killed (not one who fled)
  adds his `deathShock` to his formation, on top of the normal casualty shock.

**Display.** A wavering halo behind the badge (never over it, badge color is
faction identity): shown below `haloEnter` 40, hidden above `haloExit` 43,
yellow → red and pulsing 1 → 2.5 Hz as morale falls toward 15. The pulse
carries the signal so it survives colorblindness and red banners. Both teams.
A Routing formation has no banner. Officer grade numbers show above officers
of the selected or inspected formation only.

**Scale-back, same day.** `panicScale` 0.65 on all non-officer shock; ranged
kills weigh 0.5; panic intake capped at 8/s (queued, not discarded; officer
deaths bypass); ceilings 2.5 contact / 1.5 rest; officer death shock
24 / 28 / 12 / 4 / 4; halo band 50 / 53. Seeded 2× check: first rout 30 s after
contact at ~33 % losses with shock ≈ 1–6, routs now come from the contact
ceiling, so the remaining pacing lever is lethality (`globalDamageScale`), not
panic.

**Design change, same day: routs come from encirclement and sudden shock, not
from frontal grinding.** Ceilings 1.4 contact / 1.0 rest (grind alone routs
at ~60 % losses); `frontShockWeight` 0.25; burst rule (casualties beyond 5 % of
starting strength inside 3 s weigh 2.0, higher of zone and burst weight);
surround shock 8/s per engaged zone beyond the first, unscaled and uncapped.
`FormationMorale` keeps per-source totals (`ShockSource`) and `BattleRecorder`
writes the breakdown and primary cause with every rout. Seeded 2× checks at
`globalDamageScale` 0.15: no-input battle, first rout 73.7 s after contact,
all seven routs at 54–62 % casualties with Grind primary, collapse at 112 s;
flank test, pinned century routed 7.8 s after the flank contact at 11 %
casualties, Surround primary.

**Combat by facing, 2026-09-22.** `CombatConfig` is the third ScriptableObject:
`globalDamageScale` 0.15, front ×0.5 / flank ×1.5 / rear ×2.0, arcs 60°; the
fields left `BattleSetup`, so no combat number is scene-pinned. Ranged damage
uses the same multipliers (arrows at half from the front, kept, historically
right). **Zones are decided by formation facing, never by agent position**:
a formed attacker facing roughly opposite the defender is frontal, roughly the
same way is rear, otherwise a flank; BrokenRanks and Routing attackers fall
back to the striking soldier's position. The same rule feeds damage, flank /
rear shock and surround counting (an enemy formation counts only with
`surroundMinEngagedFraction` 0.10 of our men in contact with it). Per-soldier
zone reads were tried and rejected: engaged lines interleave 1–2.5 m on
average (up to ~6 m), so positions lie. Officers get `healthMultiplier`
(Centurion / Signifer 2.0, Optio 1.5). `contactCeilingScale` 1.1. Seeded 2×
checks: adjacent lines, 21,638 hits all frontal, surround never fired, no
routs in 180 s, ~11 % casualties at 60 s and ~60 % at 180 s; flank test,
pinned century routed 31 s after flank contact at 13.75 % casualties,
Surround primary, Signifer / Optio / Tesserarius / Cornicen alive.

**Reform reworked, 2026-09-29.** Device play showed BrokenRanks almost never
reformed: a charge's pursuit kept retargeting and reform needed zero soldiers
in contact. Now: pursuit and any loose (BrokenRanks) spell last
`pursuitMaxSeconds` = 9; reform may start with up to 15 % of the men in
contact (`reformStartEngagedFraction`) and aborts at 30 % (was 12 %, which
would have cancelled every reform); and a century still locked in melee after
9 s **re-forms in place** (slots rebuilt around the pack, facing the enemy,
state Engaged) instead of deadlocking, historically a unit in a fight could
not walk away, and turning your back was a rout. This is an exception to
"reforming requires disengagement", which now governs only the walk back to
the standard. `engagedSlotWeight` (CombatConfig, 0.3, was hardcoded 0.12)
holds clashing lines together; measured interleave did not clearly improve
(avg 0–4.5 m, max ~7 m), so the squish is not mainly a slot-weight effect.

**Personal space, 2026-09-29.** Soldier body colliders are 0.5 m radius
(`CombatConfig.soldierColliderRadius`, was 0.35), applied at spawn to every
soldier, friend and enemy, every state, for a 1.0 m minimum gap. Visuals are
not scaled. Physics already collided all soldiers, so this relaxes no rule
(the "enemies are never pushed" rule only ever covered steering nudges).
Seeded 2× lines run: enemy interleave dropped from avg 0.3–4.5 m (max ~7 m) to
avg 0–1.0 m (max 2.4 m); idle reserve speed 0.000 m/s (no jitter); but a
180 s frontal fight now costs ~12–13 % casualties instead of ~30 %, with no
routs, fewer soldiers reach contact, so pace needs a `globalDamageScale`
retune on device.

**Observed, not tuned.** The first no-input battle chain-routed at 5–12.5 %
losses (contact burst + zone weights + contagion stacking) and ended 36 s
after first contact at 7.7 % loser casualties, far under the 15–30 % target.
Tuning happens on device against the enable bools; nothing was adjusted to hit
the test targets.

---

## 2026-09-21: Morale, routing, automatic recovery, and the win condition

**Morale is approved.** This supersedes the "No morale" clause in the standing
Scope convention below, which is updated to match. Terrain, progression,
multiplayer, and advanced enemy tactics still require explicit approval.

**Every formation has a Morale value from 0 to 100.** Casualties lower it.
Morale recovers with time out of melee, up to a **rest ceiling** set by the
formation's cumulative losses, a century that has lost more men cannot
recover as high. While in melee, a stricter **contact ceiling** applies, so a
slow, steady bleed eventually routs a unit even when no single moment of
losses would.

**All morale tunables live in a new `MoraleConfig` ScriptableObject, never as
fields on `BattleSetup`.** `BattleSetup` fields are pinned by `Battle.unity`,
so a changed C# default can silently do nothing; a ScriptableObject asset
avoids that. This is the project's first ScriptableObject.

**States follow the original four-state design.**

- **Ordered** and **Engaged**: unchanged.
- **Disordered**: the design name for the live `BrokenRanks` state. Loose,
  agent-native combat; soldiers keep fighting. This is what a century becomes
  after a charge. Behavior unchanged.
- **Routing**: a new state, entered when morale falls below 15. Soldiers flee
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
still requires disengagement from melee, the game decides when, not the
player.

**Automatic reform.** A Disordered formation reforms automatically whenever
it is **not in melee**. Only active melee blocks it: enemies nearby do not,
and arrow fire does not. The soldiers run to their slots and reform even with
enemies close. One shared rule serves both sides, it replaces the
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
error, it never silently falls back to default values.

**Win condition.** A side collapses when 50 % of its formations are Routing or
destroyed. The count is read at runtime from that side's actual formations,
never a hardcoded 8, so a different army size works unchanged. **Collapse
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
- What counts as "intact" at the time limit, for example, whether a
  Disordered formation counts.
- Whether both sides collapsing on the same check is a draw.
- **Decided 2026-09-29: keep the simulation in `Update` for now.** Moving it
  (Formation pipeline, engagement scan, morale tick, attacks, projectiles, AI
  all `Update` / `Time.deltaTime` today; only soldier movement is fixed-step)
  to `FixedUpdate` or substepping is a large change to the most tuned code in
  the game, and it buys nothing while play is single-player at 30–120 fps
  (results only diverge at large frame steps, e.g. the 10× test). Revisit when
  any of these happens: PvP or replays (both machines must compute identical
  battles), a need to test above 2×, or device frame rates low enough to change
  outcomes. Cheap interim option if lag ever matters: lower
  `Time.maximumDeltaTime` so a lag spike slows the game instead of taking one
  huge step.
---

## 2026-07-24: Art pipeline hard reset: Tripo Studio Pro + Blender

**Generator.** **Tripo Studio Pro is selected as the first 3D generator.** Pro
was purchased and the Tripo → Blender DCC Bridge transfer was verified working.

**Removed from the pipeline.**

- **AssetHub is removed from the current pipeline.**
- **Meshy is removed from the current pipeline.**

Neither may be revived as the active pipeline without a new decision recorded
here.

**Blender owns production.** **Blender owns the canonical rigs and production
assets**: one canonical humanoid skeleton, skin weights, sockets, materials,
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
referenced by `Battle.unity`, a prefab, or a runtime `Resources.Load` call,
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
reset. See the note in `README.md`/`Docs/ART_PIPELINE.md` about file sizes,
this will need revisiting once real `.blend` masters land in `ArtSource/`.
