# Ancient Armies — V1.2 Combat & Control

## 1. Release Goal

Fix the largest problems found through real player testing of V1.1 without
redesigning anything. Two themes:

1. **Control** — selection must be simple and predictable; touch targets must
   be forgiving; the player must never fight the input system.
2. **Combat** — ordered formations must gain real mechanical value from being
   formations: density, mutual support, automatic gap closing, and forward
   pressure through their depth. Broken Ranks must stop being a universal
   "fight better" button.

Everything else (5v5 random armies, commander AI, directional combat, camera,
lifecycle) is preserved as shipped in V1/V1.1.

## 2. Player-Testing Problems That Motivated the Release

1. Selection was confusing and sticky: formations stayed selected after
   orders, so taps on other formations silently accumulated a multi-selection
   players did not realize they had.
2. Touch interaction was still too precise for mobile — selecting, starting
   command drags, and attacking required touching near-exact geometry.
3. Ordered melee formations left gaps open: dead soldiers left permanent holes
   until an explicit Reform.
4. Rear ranks stood frozen in their slots while usable combat space opened,
   making formation depth meaningless.
5. Soldiers at the edge of an uneven engagement stood idle with enemies
   visibly a step away.
6. Broken Ranks beat ordered formation in equal frontal melee for the wrong
   reasons — more free agents could reach and surround targets while ordered
   rear ranks idled.
7. Formation rotation performed a giant orbital wheel instead of soldiers
   turning in place.
8. "Defeated" text could follow the surviving attacker around the battlefield.
9. Archers stopped uncomfortably close to their targets.
10. The UI looked accidentally assembled; Restart was only available after a
    battle ended.

## 3. Final Implemented Behavior

### Selection (Change 1)

- **Tap friendly formation** → that formation becomes the *only* selection.
  Any previous selection is dropped automatically. Tapping the formation that
  is already the sole selection keeps it selected (no toggle-off).
- **Tap empty battlefield** → all selection clears immediately (circles,
  arrow, info panel, contextual buttons).
- **After a move order** → the order executes, then the formation
  auto-deselects.
- **After an attack order** → the order executes, then the formation
  auto-deselects.
- **Tap enemy formation** → read-only inspection panel (unchanged from V1).
- Implicit multi-selection no longer exists. The selection container remains a
  list internally so a deliberate multi-select mode can be added later without
  rearchitecting (see `docs/NEXT_RELEASE.md`).

### Touch targets (Change 2)

- Every formation now has a formation-level interaction footprint: its living
  soldiers **plus** the oriented rectangle of its slot grid **plus** generous
  zoom-aware padding (`max(2.5 m, 60 px)` for taps).
- Command drags from a selected formation are even more forgiving:
  `max(4 m, 100 px)` beyond the footprint still counts as grabbing the
  formation (V1 allowed `max(3 m, 70 px)` from soldiers only).
- When a tap falls inside several formations' padded footprints, the closest
  formation (by world-space distance to soldiers/footprint) wins — never a
  random one.
- Drag from clearly empty ground still pans the camera; drag-to-command is
  unchanged in grammar.

### Melee density (Change 3)

- Formation spacing is now per unit type, tunable on `BattleSetup`:
  `meleeSpacing = 0.95` (near shoulder-to-shoulder; soldier capsules are
  0.7 wide, so no collision instability) and `archerSpacing = 1.7`
  (visibly looser). V1 used 1.3 for everyone.
- Melee reads as a dense continuous front; archers read as a loose
  skirmish-line block.

### Auto-close (Change 4)

- Always active while a formation is trying to stay ordered (Ordered,
  Attacking, or Engaged states). Not a player toggle.
- When enough casualties accumulate (`autoCloseDeficit`, default 2), the slot
  grid is rebuilt every `autoCloseInterval` (default 2 s) for the surviving
  headcount — same anchor, same facing, same column count — and every soldier
  is assigned the nearest new slot.
- Effect: permanently empty slots disappear; rear soldiers flow forward and
  lateral holes squeeze shut over a few seconds. Engaged fighters ignore it
  (their slot attraction is ~0.12), so active combat still visibly deforms the
  unit — the formation does not snap into a perfect rectangle mid-fight.
- **Reform remains meaningful**: auto-close never recenters the anchor onto
  the survivors, never changes the column count downward beyond headcount, and
  never resets state. Rebuilding major disorder after disengagement is still
  Reform's job.

### Rear-rank pressure (Change 5)

- The V1 rank-replacement system is retained: in Engaged formations, when a
  front slot's owner dies or surges into combat, the coherent soldier one row
  behind is promoted into that slot, cascading a column forward one row per
  pass (every 0.6 s).
- Auto-close now complements it structurally: casualties shrink the grid from
  the back, so surviving depth keeps pressing toward the contact line instead
  of preserving empty historical rows.
- Rear soldiers that are not needed stay in ranks (strong slot constraint far
  from combat) — ordered combat remains visibly more structured than Broken
  Ranks.

### Edge engagement (Change 6)

- New local engagement allowance in Engaged formations, driven by each
  soldier's distance to the nearest enemy (computed by the existing 0.25 s
  engagement scan — no new queries):
  - Soldier in melee contact: slot weight 0.12, acquire radius 4.5 (as V1).
  - Soldier **near** the fight (nearest enemy ≤ `edgeEngageRadius`, default
    6 m): slot weight 0.35, acquire radius 4.5 — free to step out, attack a
    diagonal enemy, bend the local front, or wrap slightly around an exposed
    edge.
  - Soldier far from the fight: slot weight 0.75, acquire radius 3 — held in
    ranks (slightly stronger constraint than V1's 0.7).
- Closer to combat → more freedom; farther → more order. No global swarming.

### Local targeting (Change 7)

- A melee soldier with a living target inside 1.2× strike range never switches
  targets (committed fight; kills target flicker in dense melee).
- Otherwise the V1 hysteresis stands: keep the current target while it is
  within 1.25× acquire radius.
- New picks now carry a mild forward bias: an enemy behind the soldier counts
  as 1.6× farther when scored (squared-distance scale). A soldier no longer
  spins away from the fight in front of it for a marginally closer enemy at
  its back — but a lone rear threat is still acquired normally.

### Ordered vs Broken Ranks (Change 8)

- **No damage numbers changed.** No hidden ordered buff, no hidden broken
  penalty. The rebalance is purely behavioral: density (3), auto-close (4),
  rank feeding (5), edge participation (6), and targeting (7) let an ordered
  formation actually use its manpower.
- Broken Ranks is untouched: acquire radius 28, slot weight 0.05, leash 26. It
  remains the pursuit / surround / exploit mode.
- The comparative battles in the test suite (§6) are the measurement; results
  should be recorded there for future balance work.

### Defeated-text fix (Change 9)

- **Root cause**: a destroyed formation's `Update()` kept running. With
  `attackTarget` still set, `UpdateAnchorMovement` kept assigning
  `destination = attackTarget.AnchorPos` and marched the empty formation's
  anchor after the survivor; the label is a child of the formation object, so
  "Destroyed" text traveled with it. (An Engaged empty formation also flipped
  back to Attacking after 1.5 s of "no contact", re-entering the chase.)
- **Fix (Option A)**: a formation with zero soldiers is now inert — its update
  clears `attackTarget` and returns before any movement, freezing the anchor
  at the final battlefield position. The label shows "Defeated" there, holds
  ~2.5 s, fades over ~3 s, then hides. It can never follow anything.

### Rotation = pivot in place (Change 10)

- `Rotate` now means **face direction**. On confirming a rotation, the
  formation's canonical facing snaps to the new direction and every soldier is
  reassigned the slot (in the new frame) nearest to where it already stands.
  Soldiers pivot roughly in place; the footprint never orbits the anchor.
- For a 180° about-face the grid is symmetric, so soldiers simply turn around
  where they stand (front rank becomes rear rank). Other angles produce small
  local shuffles as the rectangle re-forms around the same anchor.
- Facing is tactically real (front/flank/rear bonuses), so the snap also makes
  the moment your defensive arc changes unambiguous. A true wheel maneuver is
  deferred (see `docs/NEXT_RELEASE.md`).

### Archer range (Change 11)

- `rangedRange` 14 → **20**, `rangedPreferredRange` 12 → **16** (= 80% of max,
  inside the requested 75–85% band). Values live in `UnitStats` as before —
  no hardcoded ratio.
- Sequence is unchanged and applies to player and AI archers alike (both run
  the same Formation code): approach only if beyond preferred range, stop,
  wheel to face, fire; never creep closer while the target lives.
- With formation half-depths accounted for, front ranks now fight with roughly
  11–12 units of open ground — archers visibly read as a second line.
- No kiting; the min-range sidearm behavior is unchanged.

### UI cleanup (Change 12)

- Disabled buttons now dim their labels along with the background (previously
  bright text floated on faded buttons).
- Persistent top-right **RESTART** button (see Change 13), same rounded
  family as every other button.
- Hint line rewritten for the new selection grammar: *"Tap: select unit ·
  Drag from unit: move / attack (then deselects) · Drag ground: pan · Pinch:
  zoom"*.
- Info panel line spacing increased for readability.
- Deprecated `FindFirstObjectByType` call replaced (console warning removed).
- Layout, colors, safe-area handling, and the mobile-first structure are
  otherwise unchanged — this is a tidy-up, not a redesign.

### Mid-match Restart (Change 13)

- A small persistent RESTART button sits top-right (inside the safe area)
  before Start and during combat. After a battle ends it hides — the result
  banner already carries the primary RESTART button.
- Both buttons call the same scene-reload restart as V1 (the reliable full
  reset): armies re-spawn with a fresh random composition, health, AI,
  targets, selection, camera, and the Start state all reset.

## 4. Important Implementation Decisions

- **Spacing tunables live on `BattleSetup`, not `UnitStats`.** `Battle.unity`
  serializes both `UnitStats` blocks, and a *new* serialized field falls back
  to its single class default — melee and archers would have been forced to
  share one spacing value without another scene edit. New `BattleSetup` fields
  (`meleeSpacing`, `archerSpacing`) fall back cleanly to their per-field code
  defaults and stay Inspector-tunable. `Formation` objects are runtime-built,
  so applying spacing there is serialization-safe.
- **The archer range change required a scene edit** (`archerStats.rangedRange`
  was serialized as 14). It was made through the open Unity editor (MCP) and
  saved, so the scene on disk and the editor agree; `rangedPreferredRange` was
  written explicitly for both stats blocks in the same save.
- **Auto-close reuses `BuildSlots` + `AssignNearestSlots`** — no new slot
  system. The deficit threshold (2) and cadence (2 s) keep it from thrashing:
  single stragglers don't trigger rebuilds, and reassignment happens between
  engagement scans, not per frame.
- **Edge freedom is data the formation already computes.** The engagement scan
  already visited every enemy per soldier; it now records the nearest-enemy
  distance per soldier (`Soldier.NearestEnemyDist`) at zero additional cost,
  and slot weight / acquire radius read it.
- **Rotation snaps facing instantly** rather than animating the anchor
  rotation. Animating it would re-create the orbit (slots are anchor-relative)
  or need per-soldier slot interpolation; the snap plus nearest-slot remap is
  the smallest correct implementation. Soldiers still turn at their normal
  420°/s, so the visual read is "the unit turns around".
- **A destroyed formation is frozen, not destroyed.** Other systems
  (commander AI, selection pruning, hit tests, victory check) already skip
  zero-soldier formations, so freezing the object in place is the least
  invasive fix for the label bug and leaves the fade to the label itself.

## 5. Known Limitations

- **Runtime behavior has not been manually tested** (per instructions — no
  Play Mode). The test suite below is the acceptance gate.
- Auto-close recenters rows around the fixed anchor, so as rear rows empty the
  front slot line can drift back roughly half a spacing per two lost rows
  during a long grind. Engaged fighters ignore slots, and Attacking anchors
  keep closing, so contact is not lost — but the effect exists.
- Auto-close never moves the anchor to the survivors. A formation that
  drifted far from its anchor mid-melee walks back to anchor-based slots;
  rebuilding around the survivors' actual position is still Reform's job.
- Edge freedom updates on the 0.25 s engagement-scan cadence; a very fast
  collision can lag it by a beat.
- The melee target lock (no switching inside 1.2× strike range) means a
  soldier won't opportunistically swap to an almost-dead adjacent enemy.
- Directional bonuses remain formation-anchor-based, as in V1.
- The mid-match RESTART reloads the scene immediately, without a confirmation
  step — an accidental tap forfeits the round. Accepted for V1.2 simplicity.
- Ordered-vs-broken balance is intentionally **not** declared final; §6's
  comparison tests exist to gather data.
- Melee formations are denser, so total formation frontage shrank (~10 wide ×
  0.95 ≈ 8.5 units vs 11.7 before); two melee formations can now fit in gaps
  they previously couldn't. This is intended but changes battlefield feel.
- ~400–500 dynamic rigidbodies unchanged; older phones may still need counts
  reduced in the Inspector.

## 6. Manual Test Suite

General setup unless stated otherwise: open `Assets/Scenes/Battle.unity`,
enter Play Mode (or run on device), landscape. Compositions are random per
round — Restart until the roll provides the units a test needs. Press START
before tests that require an active battle.

### SELECTION TESTS

#### S1 — Exclusive selection
- **Setup:** Active battle, two living blue formations (A and B).
- **Steps:** 1. Tap Formation A. 2. Tap Formation B.
- **Expected:** After step 1, A has selection circles + arrow + info panel.
  After step 2, A's circles/arrow vanish and B is the only selected formation;
  the panel shows only B.
- **Failure:** Both formations show circles; panel says "2 formations
  selected"; A stays selected in any form.

#### S2 — Tap out clears everything
- **Setup:** A formation selected.
- **Steps:** Tap clearly empty battlefield (several finger-widths from any
  formation).
- **Expected:** Selection circles, formation arrow, info panel, and command
  buttons all disappear immediately.
- **Failure:** Any selection visual or panel remains; the tap selects a
  distant formation (padding too large).

#### S3 — Auto-deselect after move
- **Setup:** Select a blue formation.
- **Steps:** Drag from it to empty ground and release.
- **Expected:** Move order executes (pulse + formation marches); the
  formation immediately deselects (no circles, no panel).
- **Failure:** Formation stays selected after the order.

#### S4 — Auto-deselect after attack
- **Setup:** Select a blue formation.
- **Steps:** Drag from it onto a red formation and release.
- **Expected:** Attack confirmed (red line styling + target ring pulse), the
  formation attacks, and it immediately deselects.
- **Failure:** Formation stays selected after the order.

#### S5 — Re-tap the same formation
- **Setup:** Select formation A.
- **Steps:** Tap A again.
- **Expected:** A stays selected (tap-again is not a toggle-off; only tap-out
  or issuing an order deselects).
- **Failure:** A deselects on the second tap.

#### S6 — Enemy inspection unchanged
- **Setup:** Select a blue formation.
- **Steps:** Tap a red formation.
- **Expected:** Read-only enemy panel (no command buttons). Blue selection
  circles remain — inspection does not steal the selection.
- **Failure:** Command buttons appear for the enemy; selection is lost.

### TOUCH AREA TESTS

#### T1 — Tap resolution across the footprint
- **Steps:** Try selecting a blue formation by tapping: (a) directly on a
  soldier, (b) in a gap between soldiers, (c) at the block's outer edge,
  (d) slightly outside the visible footprint (about a finger-width).
- **Expected:** All four select the formation.
- **Failure:** (b)–(d) pan the camera or select nothing.

#### T2 — Command drag origins
- **Steps:** Select a formation, then start command drags from: (a) a
  soldier, (b) the center of the block, (c) near an edge, (d) the padded area
  just outside the block (~1–2 finger-widths).
- **Expected:** All four show the command line + destination preview (never a
  camera pan).
- **Failure:** Any of them pans the camera.

#### T3 — Overlap resolves to the closest formation
- **Setup:** Two blue formations near each other (move one next to another).
- **Steps:** Tap between them, slightly nearer one of them.
- **Expected:** The closer formation is selected; result is deterministic for
  the same tap point.
- **Failure:** The farther one gets selected, or repeated taps alternate.

#### T4 — Camera pan still works
- **Steps:** With a formation selected, drag starting on clearly empty ground
  far from it.
- **Expected:** Camera pans; selection unaffected.
- **Failure:** A command drag fires from clearly empty ground.

#### T5 — Cancel by dragging back
- **Steps:** Select a formation, start a command drag outward, drag back onto
  the formation, release.
- **Expected:** No order issued; the formation stays selected (only completed
  orders deselect).
- **Failure:** A move order to the formation's own position is issued.

### MELEE SPACING TEST

#### D1 — Density comparison
- **Setup:** Pre-battle (or fresh battle). A round with at least one melee and
  one archer formation per side (Restart until rolled).
- **Steps:** Zoom to compare a sword block and an archer block side by side.
- **Expected:** Swordsmen stand near shoulder-to-shoulder — a dense continuous
  block with minimal empty ground inside. Archers have obvious space between
  files. No overlapping/jittering soldiers in either.
- **Failure:** Both look equally spaced; melee capsules intersect or vibrate.

### AUTO-CLOSE TEST

#### D2 — Gaps close without Reform
- **Setup:** Active battle. Send one blue sword formation frontally into a red
  sword formation.
- **Steps:** Let the fight run until 10+ soldiers on one side have died. Watch
  the block behind the fighting front. Do not press Reform.
- **Expected:** Holes left by the dead do not persist as permanent empty
  slots: within a few seconds of losses accumulating, survivors shift to
  close meaningful gaps and rear rows compress forward. The block stays
  visibly imperfect while fighting continues — no perfect parade rectangle
  mid-melee.
- **Failure:** Permanent holes remain until Reform; or the formation
  teleports/snaps into a perfect grid during combat.

### REAR PRESSURE TEST

#### D3 — Depth feeds the fight
- **Setup:** Same frontal sword-vs-sword fight, formations 10 wide × 5 deep.
- **Steps:** Watch a single column of the blue formation for ~30 seconds.
- **Expected:** When the front fighter dies or surges forward, the soldier
  behind steps up into its place, and the rows behind compress in turn —
  pressure visibly ripples forward through the ranks. Unneeded rear soldiers
  hold formation rather than milling.
- **Failure:** Rear soldiers stand frozen in their original slots while empty
  space sits in front of them; or the whole rear dissolves into a mob.

### UNEVEN EDGE ENGAGEMENT TEST

#### D4 — Off-center collision
- **Setup:** Active battle. Command a blue sword formation to attack a red
  formation so the blocks meet offset by roughly half their width (attack from
  an angle).
- **Steps:** Watch the soldiers at the overhanging edges of both blocks.
- **Expected:** Edge soldiers within a couple of soldier-lengths of the fight
  step out, attack diagonal enemies, and bend/wrap the local front slightly.
  Soldiers on the far, uncontacted side hold ranks. The formation does not
  collapse into a swarm.
- **Failure:** Soldiers stand idle with enemies ~1–2 body-lengths away; or the
  entire formation abandons structure and swarms.

### ORDERED VS BROKEN RANKS TESTS

Run each on a fresh battle with equal sword formations (Restart until both
sides roll swords in usable positions). Command only the units under test;
keep other formations away. Record winner, approximate survivors, and visible
behavior below each test for future balance work.

#### B1 — Ordered vs Ordered (baseline)
- **Steps:** Send one blue sword formation frontally into one red sword
  formation. Issue no further commands.
- **Expected:** A grinding, roughly even fight; both fronts stay coherent;
  rear ranks feed in on both sides.
- **Record:** winner, survivors, duration impression.

#### B2 — Ordered vs Broken Ranks
- **Steps:** Repeat B1, but the moment the formations meet, press BREAK RANKS
  on the blue formation (red stays ordered — or invert and run twice).
- **Expected design direction:** The ordered side should generally win or at
  least not lose clearly: its density, gap closing, and rank feeding should
  match or beat the broken side's free-agent participation in sustained
  frontal contact. The broken side should look chaotic — pursuing, wrapping,
  clumping.
- **Record:** winner, survivors, whether broken visibly out-participated
  ordered.
- **Failure:** Breaking ranks is still an obvious universal upgrade in equal
  frontal melee.

#### B3 — Broken vs Broken
- **Steps:** Repeat with both formations set to Broken Ranks at contact.
- **Expected:** A chaotic brawl, notably messier than B1; no crash or stalls.
- **Record:** same data.

### TARGETING TEST

#### D5 — Plausible local fights
- **Setup:** Any dense melee.
- **Steps:** Follow several individual soldiers for ~15 seconds each.
- **Expected:** Each fights an adjacent/nearby enemy; no soldier runs across
  the formation's width past enemies to reach a distant target; no visible
  rapid target flicker (spinning between two enemies); no soldier ignores an
  enemy attacking it from a body-length away while walking to someone farther.
- **Failure:** Long-distance chases through the crowd, constant re-facing, or
  idle soldiers beside enemies.

### DEFEATED TEXT BUG TEST

#### D6 — Label stays put
- **Setup:** Active battle.
- **Steps:** Destroy one red formation completely (gang up on it). Watch where
  it died, and watch the surviving blue attacker march away afterward.
- **Expected:** "«Name» Defeated" appears at the destroyed formation's final
  battlefield position, holds a couple of seconds, fades out, and disappears.
  It does not move with — or ever re-attach to — any surviving formation.
- **Failure:** The text follows the survivor, jumps around the map, or never
  disappears.

### ROTATION TEST

#### D7 — Pivot in place
- **Setup:** Active battle. Select an Ordered blue formation away from combat.
- **Steps:** Press ROTATE, drag to face roughly the opposite direction (~180°),
  release. Watch the soldiers and the facing arrow.
- **Expected:** The facing arrow snaps to the new direction. Each soldier
  turns around approximately where it stands (the old front rank becomes the
  rear rank). The block as a whole stays in the same ground area — no giant
  wheeling arc around the center. Small local shuffles are fine.
- **Failure:** Soldiers march in big arcs around the formation center; the
  footprint translates or orbits; the arrow points wrong.

#### D8 — Rotation feeds directional combat
- **Steps:** After D7, order an enemy to attack (or wait for the AI) and
  confirm the front arc is where the new arrow points.
- **Expected:** Attacks into the new facing register as frontal (grinding),
  not rear.

### ARCHER RANGE TEST

#### D9 — Second-line archers (both sides)
- **Setup:** A round where each side rolled at least one archer formation.
- **Steps:** Order blue archers to attack a distant red formation; separately
  watch red AI archers advance on their own target.
- **Expected:** Both stop with *obvious* open ground to the target — clearly
  more than in V1.1 (front ranks roughly 11–12 units apart; several
  formation-depths of space), wheel to face it, and sustain fire from there.
  They do not keep creeping closer while the target lives.
- **Failure:** Archers advance into or near melee contact; stop but never
  fire; stop too far and fire nothing (arrows all fall short).

#### D10 — Plain moves still exact
- **Steps:** Give blue archers a move order (not an attack) to a spot.
- **Expected:** They walk to that exact spot — the preferred-range stop only
  applies to attack orders.

### UI TESTS

#### U1 — Buttons and disabled states
- **Steps:** Select formations in various states; look at BREAK RANKS /
  REFORM / ROTATE enabled and disabled; check START, banner RESTART, and the
  new top-right RESTART.
- **Expected:** One consistent rounded button family; disabled buttons dim
  both background *and* label; "Too close to enemy" reason still shows over a
  disabled REFORM; all labels readable at phone size; nothing overlaps the
  notch/safe area.
- **Failure:** Bright text on faded disabled buttons; misaligned or
  clipped controls; new clutter.

#### U2 — Hint and panel
- **Steps:** Read the top-left hint during battle; select one formation and
  read the info panel.
- **Expected:** Hint matches the new grammar (mentions deselect-after-order).
  Panel lines have comfortable spacing and remain readable.

### MID-MATCH RESTART TESTS

#### R1 — Restart before Start
- **Steps:** At the pre-battle screen, note the army composition, then press
  the top-right RESTART.
- **Expected:** Scene reloads cleanly to a fresh pre-battle state with a
  re-rolled composition (usually different units); START button present.

#### R2 — Restart during combat
- **Steps:** Mid-fight — with formations selected, orders active, and arrows
  in the air — press the top-right RESTART.
- **Expected:** Immediate clean reload: full-strength re-rolled armies, no
  corpses/arrows/markers, nothing selected, no stale orders or AI targets, no
  console errors, camera at default framing, START button showing.
- **Failure:** Leftover objects, red army acting before START, errors.

#### R3 — Restart after victory
- **Steps:** Finish a battle; use the banner RESTART. Also verify the
  top-right button is hidden on the end screen.
- **Expected:** Same clean reload; only one RESTART is offered on the end
  screen (the banner's).

#### R4 — Repeated restarts
- **Steps:** Restart five times in a row from mixed phases (pre, mid, ended).
- **Expected:** Every reload identical and clean; no accumulating slowdown,
  duplicate UI, or ghost objects.

## 7. Complete V1.2 Acceptance Test

1. Launch the game (landscape). Verify the top-right RESTART is visible even
   before START.
2. Press START.
3. Select Formation A (a blue formation) — circles + arrow + panel appear.
4. Tap Formation B. Verify **only B** is selected.
5. Tap empty battlefield. Verify all selection clears.
6. Select a formation with a forgiving tap just outside its soldiers.
7. Issue a move order. Verify the order executes and the formation
   **auto-deselects**.
8. Select it again; issue an attack on a red formation. Verify the attack
   confirms and the formation **auto-deselects**.
9. Watch an ordered sword-vs-sword melee develop.
10. Verify melee blocks are visibly dense (compare with an archer block).
11. Verify rear ranks advance as front soldiers die.
12. Verify casualty gaps close automatically without pressing Reform.
13. Create an off-center collision; verify edge soldiers join in and the local
    front bends without the formation mobbing.
14. Run one Ordered-vs-Broken-Ranks comparison; record the outcome (§6 B2).
15. Rotate a formation ~180°. Verify soldiers pivot in place and the arrow
    updates; no orbital wheel.
16. Watch blue and red archers; verify both stop clearly farther out than
    V1.1 and sustain fire from range.
17. Destroy a red formation. Verify the "Defeated" text stays at the death
    site and fades — it must not follow your surviving formation.
18. Press the top-right RESTART mid-battle. Verify a clean re-rolled round.
19. Play a round to a result banner; verify the banner RESTART also works.

**Intended overall result:** the game is easier to control (no selection
fighting, forgiving touch), clearer to read, and more believable in melee —
ordered formations act like formations and earn their advantage mechanically.

## 8. Differences From the Requested Design

- **Rotation facing snaps instantly** instead of turning gradually. The spec
  asked for pivot-in-place without prescribing anchor-facing animation; the
  snap plus nearest-slot remap is the smallest implementation that can never
  orbit, and it makes the front/flank/rear arc change unambiguous. Soldiers
  themselves still turn at a natural rate.
- **Tapping the already-selected formation keeps it selected.** V1's
  tap-to-toggle-off is gone; deselection is now only tap-out or
  order-auto-deselect. The spec defined tap = exclusive select; this is the
  most literal, least surprising reading.
- **Spacing tunables live on `BattleSetup` rather than `UnitStats`** (see §4)
  because of the scene-serialization fallback behavior; the spec asked only
  that values remain tunable, which they are (Inspector on the GameManager).
- **`rangedRange` was raised (14 → 20), not just the preferred distance.** At
  a max range of 14, no preferred distance both reads as "second line" and
  keeps rear archers in range. Preferred = 16 = 80% of the new max, inside the
  requested 75–85% relationship. Archer damage/cooldown are untouched.
- **The persistent RESTART hides on the end screen** where the banner's
  RESTART already exists — restart remains available in every phase, just
  never as two buttons at once.
- **Auto-close triggers on a casualty threshold (2) rather than any
  displacement** — soldiers who merely leave their slots to fight are handled
  by the existing rank-replacement promotion; rebuilding the grid for
  displacement as well would fight that system.
