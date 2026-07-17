# Ancient Armies — V1.1 Changes

## Purpose

A focused patch on top of V1 with two goals:

1. **Coordinated enemy AI** — upgrade the red army from three independent
   nearest-target bots to one lightweight commander coordinating its formations
   in a plausible, non-suicidal way: active, coordinated, role-aware,
   responsive, understandable. Not sophisticated.
2. **Miscellaneous battlefield changes** — 5v5 armies with a randomized unit
   mix every battle, much more room between the two sides, and a wider default
   camera view.

Nothing else changed. Combat, formations, ranged behavior, input, camera feel,
UI, and the battle lifecycle are untouched from `docs/V1_RELEASE.md`.

## Battlefield Changes (non-AI)

- **5 formations per side** (`formationsPerSide`, tunable on BattleSetup) in a
  single line, 16 units apart (`lineSpacingX` 14 → 16).
- **Random composition each battle:** every formation slot independently rolls
  Archers or Swordsmen at 50/50 (`archerChance`). Restart reloads the scene, so
  every round fields a new mix. Extreme rolls (e.g. five archers on one side)
  are possible and legitimate. Archer slots spawn 6 units behind the line.
- **Way more room between armies:** battle lines moved from z ±14 to z ±24
  (`lineZ`), giving ~48 units of no-man's land; the ground grew from 100×60 to
  120×80 to match. Move-order clamps and camera pan bounds widened accordingly.
- **Zoomed-out default:** camera starts at orthographic size 26 (was 20) so the
  whole 5-formation line is visible; max zoom-out raised 30 → 36.
- Soldier counts per formation are unchanged (50 melee / 40 archers), so a 5v5
  battle now fields roughly 400–500 soldiers. See Known Limitations.

## Previous AI Behavior (V1)

`EnemyCommander` ticked once per second; each red formation independently
picked the **nearest** living blue formation and attacked it, retargeting only
when idle or when its target died. Result: blind dogpiling onto whatever
happened to be closest, no role awareness beyond the shared preferred-range
system, and no reaction to threats against its own archers.

## New AI Behavior (V1.1)

`EnemyCommander` is now a strategic decision-maker for the whole red army. It
still ticks once per second and still issues only ordinary `IssueAttack`
orders — the existing formation state machine executes everything. Each pass:

1. Gather living red and blue formations (reused lists, no per-tick allocation).
2. Identify blue formations threatening red archers.
3. For each red formation that is *available* (Ordered or Attacking — never
   Engaged/BrokenRanks/Reforming/Withdrawing), score every blue formation.
4. Idle formations (no valid target) take the best-scoring target immediately.
5. Formations with a valid order keep it unless a rival target beats its score
   by a clear margin (see Retargeting Rules).

Because `IssueAttack` records the target on the formation immediately,
formations decided later in the same pass see earlier assignments as
saturation — coordination falls out of one shared scoring pass with no extra
bookkeeping.

## Target Scoring Logic

All factors share one currency (world units — "worth N meters of distance"),
tunable on the `EnemyCommander` component:

| Factor | Default | Applies to | Effect |
|---|---|---|---|
| Distance | −1/unit | all | closer targets score higher; prevents cross-map choices |
| Saturation | −18 per assigned ally | all | each friendly formation already attacking the target subtracts a penalty (archer assignments count 0.5×) |
| Archer threat | +40 | melee only | target is near (≤18 units) or attacking a red archer formation |
| Attacking me | +15 | all | target's own attack order is aimed at the scoring formation |
| Finishing | up to +10 | all | scaled by the target's missing health fraction; a modest nudge, never the strategy |
| In shooting range | +12 | archers only | target is already inside max bow range — shoot without walking |

Penalties reduce scores but never disqualify a target, so the army can always
act (last-enemy concentration works by construction).

## Melee Formation Priorities

1. Nearby reachable targets (distance term).
2. **Intercept threats to friendly archers** (+40 — the dominant bonus).
3. Enemies attacking them (+15).
4. Spread pressure instead of stacking (saturation −18 per ally already on it).
5. Slight preference for finishing wounded formations (≤ +10).
6. Keep existing fights (persistence margin; Engaged formations are never re-tasked).

## Archer Formation Priorities

1. Targets already inside bow range (+12) — no unnecessary walking.
2. Nearby targets otherwise (distance term); the existing preferred-range
   system still stops them ~12 units out, facing and firing.
3. Avoid piling onto oversaturated targets (same saturation penalty).
4. Slight finishing preference.
5. No archer-threat bonus: archers do not try to "protect" anyone; protection
   is the melee formations' job. No kiting, no line-of-sight simulation.

## Retargeting Rules

- **Target dies →** the formation is treated as idle at the next tick (and the
  formation itself already clears dead targets mid-tick) and takes the best
  available target. It never attacks dead formations.
- **Idle with living enemies →** assigned the best target within one tick.
- **Valid current order →** kept unless a rival target's score beats the
  current target's score by `switchMargin` (default 25). In practice only an
  archer emergency (+40) or a grossly better option crosses that bar — small
  score drift never causes switching.
- **Engaged / Broken Ranks / Reforming / Withdrawing →** never re-tasked; the
  formation state machine resolves these, exactly as in V1.

## Explicit Non-Goals

No planned flanking, pincers, retreats, reserves, morale, predictive movement,
terrain awareness, formation shape changes, reinforcements, difficulty levels,
behavior trees, utility-AI frameworks, ML, minimax, or GOAP. The commander is
one file with one scoring function.

## Known Limitations

- **Runtime behavior is not verified** — no Play Mode testing was performed
  (per instructions); the manual test suite below is the acceptance gate.
- In-editor compile could not be confirmed in-session (the editor was
  suspended/unreachable overnight); all scripts compile with **zero errors**
  under Unity 6000.4.8f1's own Roslyn with the project's exact references.
  Opening the project will import and compile normally; if Unity still has the
  scene open from a previous session, accept **Reload** on the
  "modified externally" dialog — the disk version is correct.
- An Engaged red melee formation will not break off to rescue archers; only
  *available* formations respond to archer threats.
- Archer threat detection is distance/order based (≤18 units or an explicit
  attack order); a fast player formation can reach archers between ticks.
- With random compositions, a side can roll all archers or all melee. The AI
  handles both (no melee → no protection response; no archers → protection
  logic is inert), but such rounds play very differently — restart re-rolls.
- ~400–500 soldiers now spawn in a 5v5; this is noticeably heavier than V1's
  280. Unverified on older phones — reduce `meleeCount`/`archerCount` or
  `formationsPerSide` in the Inspector if needed.
- Saturation counts *assignments*, not damage output; two half-dead red
  formations count the same as two fresh ones.

---

# Manual Test Suite

General setup: open `Assets/Scenes/Battle.unity`, Play Mode or device,
landscape. **Compositions are random each round** — if a test needs a specific
unit mix (e.g. red archers present), Restart until the roll provides it.
Formation labels name each unit ("Red Archers 1", "Blue Swords 2", …).

## Test 1: Initial Target Distribution

- **Setup:** Fresh battle. Press START and issue no orders.
- **Steps:** Watch which blue formations the red formations move toward
  (their facing/marching directions and eventual contacts show assignments).
- **Expected:** Red formations spread across *different* blue targets rather
  than all converging on the single nearest one. Roughly opposite formations
  pair off. Red archer formations advance only to firing range and shoot.
- **Failure:** Three or more red formations converge on one blue formation at
  battle start while other blue formations are comparably close and unpressured.

## Test 2: Melee Target Saturation

- **Setup:** Battle where red has at least two melee formations.
- **Steps:** Let one red melee formation commit to a blue formation. Watch the
  second red melee formation's choice while another blue formation is also
  available at a similar distance.
- **Expected:** The second red melee generally picks the unpressured target.
- **Legitimate exceptions:** the pressured target is much closer (saturation
  is worth ~18 units of distance, not a ban); it threatens red archers; it is
  nearly dead; or it is the only enemy left.

## Test 3: Archer Protection

- **Setup:** A round where red rolled at least one archer and one melee
  formation. Battle active.
- **Steps:** March a blue melee formation straight at the red archers (or
  order an attack on them), while at least one red melee formation is not
  engaged.
- **Expected:** Within a tick or two, an available red melee formation turns
  to intercept your formation — the +40 bonus dominates its scoring even if it
  had another valid marching order. The red army does not simply ignore the
  attack on its archers.
- **Failure:** All available red melee keep ignoring a formation that is
  visibly cutting through the red archers. (An *Engaged* red melee staying in
  its own fight is correct, not a failure.)

## Test 4: Archer Preferred Range

- **Setup:** Round with red archers. Press START.
- **Steps:** Watch a red archer formation attack.
- **Expected:** Approaches only until ~12 units from its target, stops with
  obvious ground between the formations, faces the target, fires arcs of
  arrows. Does not creep into melee.
- **Failure:** Archers walk to sword range while their target is alive.

## Test 5: Target Death and Retargeting

- **Setup:** Active battle.
- **Steps:** Destroy (or let the AI destroy) a blue formation currently
  targeted by one or more red formations. Watch those red formations.
- **Expected:** Within a few seconds each acquires a new living blue target
  (possibly after a brief auto-reform if untouched by enemies). Nothing idles
  permanently; nothing attacks the destroyed formation's remains.

## Test 6: No Enemies Left Unnecessarily Untouched

- **Setup:** Mid-battle with several blue formations alive: one heavily
  engaged with red forces, another untouched.
- **Steps:** Free up a red formation (e.g. its target dies) and watch its next
  assignment.
- **Expected:** The freed formation generally pressures the unengaged blue
  formation rather than stacking onto the already-swarmed one, unless the
  swarmed one is much closer, nearly dead, or threatening red archers.

## Test 7: Last Enemy Standing

- **Setup:** Reduce blue to a single living formation.
- **Steps:** Watch all remaining red formations.
- **Expected:** Every available red formation converges on the final blue
  formation and finishes the battle. Saturation penalties lower the score but
  never block the only remaining target.
- **Failure:** Any red formation idles while the last blue formation lives.

## Test 8: Target Stability

- **Setup:** Active battle, no player interference for ~30 seconds.
- **Steps:** Watch red formations' movement directions over many commander
  ticks.
- **Expected:** Formations hold their assignments; no visible oscillation or
  re-aiming every second between two similar targets. Meaningful changes
  (target death, archer emergency) still cause prompt reassignment.

## Full V1.1 Acceptance Battle

1. Press **START**.
2. Observe initial red assignments: formations fan out toward *different*
   blue targets.
3. Confirm the army does not behave like independent nearest-target bots
   (no full-army dogpile at the opening).
4. Confirm melee pressure is distributed plausibly across your line.
5. Confirm red archers stop at range, face their targets, and fire.
6. Send one of your formations at the red archers.
7. Confirm an available red melee formation reacts and intercepts within a
   couple of seconds.
8. Kill one red-targeted blue formation (or lose one of yours).
9. Confirm the affected red formations retarget promptly.
10. Fight down to one final blue formation.
11. Confirm multiple red formations concentrate on it and finish the battle,
    then the correct result banner shows and RESTART yields a fresh round
    with a **new random composition**.

**Intended feeling:**

> The enemy is still simple, but it now behaves like one small army rather
> than several unrelated autonomous units.
