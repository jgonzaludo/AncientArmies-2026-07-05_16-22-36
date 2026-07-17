# Ancient Armies — V1 Release

## V1 Goal

The first real playable battle: a 3v3 match against an autonomous enemy army where
unit type, positioning, facing, and player control clearly affect the outcome.

> The game should now feel like an actual small tactical battle against an opponent
> rather than a formation systems sandbox.

## Included Features

1. **Larger formations** — 50 swordsmen per melee formation, 40 archers per archer
   formation, 10 columns wide. Six formations, 280 individually simulated soldiers.
2. **Autonomous enemy AI** (`EnemyCommander`) — the red army picks targets, approaches,
   attacks, and retargets when its target formation is destroyed. 2 melee + 1 archer
   per side.
3. **Ranged engagement model** — archer formations stop at a tunable preferred firing
   range, face the target, and fire, instead of walking toward melee range.
4. **Directional combat** — front / flank / rear attack detection with tunable damage
   multipliers (1.0x / 1.5x / 2.0x). Facing is now tactically meaningful.
5. **Forgiving mobile command gestures** — dpi-aware touch slop and a zoom-aware
   grab tolerance around selected formations, so command drags don't need
   pixel-perfect origins.
6. **Buttery camera** — smoothed pan with a short natural glide after release,
   eased pinch/scroll zoom, battlefield bounds.
7. **Minimal visual/UI cleanup** — lighter matte grass, rounded consistent buttons
   with clear hierarchy, height-based UI scaling, safe-area support.

All V0 systems are preserved: formation-level control, individual soldiers, ordered
movement, melee with local disorder, Break Ranks, withdrawal, Reform with survivors
closing ranks, health/death with damage darkening, Start / victory / defeat / draw /
Restart, and the tap/drag mobile control grammar.

## Explicit Non-Goals (unchanged from the V1 contract)

No morale, cavalry, terrain effects, hills/forests/rivers, formation shapes,
campaign/progression, unit levels, equipment, soldier customization, advanced enemy
tactics (coordinated flanking, retreats), multiplayer, polished models or animation,
or architectural rewrites. AI archers do not kite or skirmish.

## Important Implementation Decisions

- **AI is a thin commander, not a new system.** `EnemyCommander` ticks once per
  second and issues the *same* `IssueAttack` orders the player issues. Approach,
  archer preferred range, melee contact, disorder, and reforming all come from the
  existing Formation/Soldier systems, so AI and player formations behave identically.
  A red formation only receives a new order when it is idle (Ordered/Attacking) and
  its target is gone; Engaged/Reforming formations are left to resolve naturally.
- **Ranged engagement model lives in `UnitStats`:** `rangedRange` (max per-soldier
  shot distance, 14), `rangedPreferredRange` (anchor-to-anchor formation stop
  distance, 12), `rangedMinRange` (inside this a soldier defends with its sidearm at
  reduced damage, 2.5). The formation stops at preferred range and wheels to face the
  target; individual archers fire at anything inside `rangedRange`.
- **Directional combat is formation-level and deterministic.** The defender's
  canonical forward (its anchor rotation) is compared against the vector from the
  defender's anchor to the attacker's anchor. Within ±60° of forward = front (1.0x);
  within ±60° of directly behind = rear (2.0x); everything else = flank (1.5x). All
  five values are tunable on `BattleSetup`. The multiplier applies to melee strikes
  and projectiles alike, computed at the moment of each attack.
- **Red auto-reform is gated to idle.** V0's red-side auto-tidy would have halted AI
  attack marches; it now only fires when a red formation is Ordered with no target.
- **Camera is a separate rig (`BattleCamera`).** `PlayerCommander` feeds screen-space
  pan deltas (converted exactly to ground-plane deltas for the fixed ortho camera) and
  zoom factors; the rig owns target position/zoom, eases the real camera with
  `SmoothDamp`, and applies an exponentially decaying glide after release. Bounds are
  clamped on the target, so the camera always settles inside the battlefield.
- **Touch leniency:** drag-vs-tap slop is `max(22px, 6% of screen dpi)` (~1.5mm on a
  real phone). A command drag wins if it starts on a selected formation's soldiers
  *or* within `max(3m, 70px-equivalent)` of them; empty ground beyond that pans the
  camera. Only *selected* formations get the enlarged grab area, so the battlefield
  stays navigable.
- **Restart stays a scene reload** — the simplest reliable full reset; it rebuilds
  formations, soldiers, AI, selection, camera, and UI from scratch.
- Formation counts/columns are serialized on the `GameManager` in `Battle.unity`
  (`meleeCount: 50`, `archerCount: 40`, `formationColumns: 10`) and tunable in the
  Inspector.

## Final Controls

| Gesture | Action |
|---|---|
| Tap friendly formation | Select / add to selection |
| Tap selected friendly formation | Remove from selection |
| Tap enemy formation | Inspect (read-only panel) |
| Tap empty ground | Deselect all |
| Drag from a selected formation (or near it) to ground | Move / withdraw |
| Drag from a selected formation (or near it) onto an enemy | Attack |
| Drag from empty ground | Pan camera (with release glide) |
| Pinch / scroll wheel | Zoom (eased, clamped) |
| BREAK RANKS button | Loosen the formation harness |
| REFORM button | Rebuild formation once disengaged |
| ROTATE button + drag | Set formation facing |
| START / RESTART buttons | Battle lifecycle |

Mouse in the Editor mirrors touch: click = tap, click-drag = pan/command, scroll = pinch.

## Known Limitations

- **Runtime behavior has not been manually tested.** Per the V1 instructions, no Play
  Mode testing was done; the manual test suite below is the acceptance gate.
- **In-editor compile pass could not be confirmed in-session.** All scripts compile
  with zero errors using Unity 6000.4.8f1's own Roslyn compiler and the project's
  exact reference set/defines, and Unity imported the new scripts (valid .meta GUIDs).
  However, the open editor became unresponsive behind a "scene modified externally"
  modal during the pass. **First action when opening Unity: choose *Reload* if that
  dialog is still shown** (the on-disk scene is the correct V1 version), then let it
  compile normally.
- Archers do not back away if enemies close inside preferred range (no kiting —
  deliberate non-goal); inside `rangedMinRange` they defend with weak sidearm strikes.
- Directional bonuses use formation anchors, not per-soldier positions; in a swirling
  melee the modifier reflects where the formations are, not each soldier.
- Directional bonuses also apply to arrow fire (consistent formation-level rule) —
  archers shooting a formation's rear deal 2x.
- Red formations independently pick the *nearest* target, so they may gang up on one
  blue formation. This is acceptable V1 behavior, not a bug.
- ~280 dynamic rigidbodies; performance verified nowhere but the Editor is expected
  to be fine — older phones may need `meleeCount`/`archerCount` reduced in the
  Inspector.
- A draw remains possible if the last soldiers on both sides die within the same
  half-second end-check window.

---

# Manual Test Suite

General setup for all tests unless stated otherwise: open `Assets/Scenes/Battle.unity`,
enter Play Mode (or run on device), landscape orientation.

## A. Battle Lifecycle

### A1 — Pre-battle Start state
- **Setup:** Launch the scene. Do not press START.
- **Steps:** Observe both armies for ~10 seconds. Try tapping formations. Drag from
  empty ground; pinch/scroll.
- **Expected:** Both armies stand in ordered ranks and do nothing. No fighting, no
  movement, no projectiles. Formations cannot be selected. Camera pan and zoom still
  work. The START button is visible bottom-center.
- **Failure:** Any soldier moves or attacks before START; formations selectable; no
  START button.

### A2 — Start button
- **Setup:** Pre-battle state.
- **Steps:** Press START.
- **Expected:** The button disappears, the control hint appears top-left, and the
  battle becomes active — formations can be selected and commanded.
- **Failure:** Button stays; game remains inert; UI errors.

### A3 — AI activation on Start
- **Setup:** Press START and immediately watch the red army.
- **Steps:** Do nothing else for ~15 seconds.
- **Expected:** Within a second or two, all three red formations begin acting: the two
  red sword formations march toward the nearest blue formations; red archers advance
  only until roughly 12 units from their target, then stop and start shooting arrows.
- **Failure:** Any red formation stands idle for more than ~3 seconds after START
  while enemies exist.

### A4 — Blue victory
- **Setup:** Active battle.
- **Steps:** Fight deliberately well (use rear/flank attacks, focus fire) until every
  red soldier is dead.
- **Expected:** "BLUE WINS" banner in blue tint, RESTART button below it. All combat
  stops; surviving soldiers hold position; red AI does nothing (there is no red army
  left). The final battlefield state remains visible behind the banner.
- **Failure:** No banner; fighting continues; soldiers keep dying after the end.

### A5 — Red victory
- **Setup:** Active battle.
- **Steps:** Press START and let the AI fight an uncommanded blue army, or feed blue
  formations into bad engagements until all blue soldiers are dead.
- **Expected:** "RED WINS" banner in red tint, RESTART available, combat stopped,
  final state visible.
- **Failure:** Same failure indicators as A4.

### A6 — Draw (if it occurs)
- **Setup:** Hard to force; occurs only if the last soldiers on both sides die
  almost simultaneously.
- **Steps:** If observed, note the banner.
- **Expected:** "DRAW" in white. RESTART available.
- **Failure:** A winner declared when both armies are dead.

### A7 — Restart
- **Setup:** Any ended battle.
- **Steps:** Press RESTART.
- **Expected:** The scene reloads to the exact pre-battle state: six full-strength
  formations in starting positions, full health (no darkened soldiers), nothing
  selected, no orders or AI targets carried over, camera at the default framing,
  START button showing.
- **Failure:** Missing soldiers, leftover corpses/arrows/markers, red army starts
  acting before START, camera stuck where it was.

### A8 — Repeated restart reliability
- **Setup:** —
- **Steps:** Play (or fast-forfeit) and RESTART five times in a row.
- **Expected:** Every restart is identical and clean; no accumulating slowdown,
  console errors, duplicated UI, or ghost objects.
- **Failure:** Anything degrades or errors by the fifth cycle.

## B. Larger Formations

### B1 — Formation size and shape
- **Setup:** Pre-battle.
- **Steps:** Count ranks: select nothing, just look.
- **Expected:** Each sword formation is 10 wide × 5 deep (50 soldiers); each archer
  formation is 10 × 4 (40). Labels read 50/50 and 40/40. The battle visually reads as
  a small army (~280 soldiers).
- **Failure:** Old small formations (18/12); overlapping spawns.

### B2 — Ordered movement at size
- **Setup:** Active battle, select a blue sword formation.
- **Steps:** Order a move across the battlefield; watch the trip.
- **Expected:** The formation wheels toward the destination and marches as a coherent
  10-wide block. Soldiers keep their slots (small jostling is fine).
- **Failure:** The block smears apart, soldiers left behind, severe accordion effects.

### B3 — Melee, casualties, and closing ranks
- **Setup:** Send one blue sword formation frontally into a red sword formation.
- **Steps:** Let them fight until ~15 soldiers have died, withdraw (drag away), then
  press REFORM once available.
- **Expected:** Front ranks fight while rear ranks feed forward; soldiers darken as
  they take damage and fall when killed. After reform, survivors rebuild a smaller
  clean rectangle with **no permanent gaps** — dead soldiers leave no holes.
- **Failure:** Rear soldiers never join; gaps remain after reforming; dead soldiers
  still count in the label.

## C. Enemy AI (melee)

### C1 — Target acquisition and approach
- **Setup:** Press START, don't command anyone.
- **Steps:** Watch each red sword formation.
- **Expected:** Each picks the nearest living blue formation, wheels, and marches at
  it in good order.
- **Failure:** Idle red melee; marching to empty ground.

### C2 — Attack and sustained fighting
- **Setup:** Continue from C1.
- **Steps:** Let a red formation reach a blue one.
- **Expected:** It engages exactly like a player-commanded attack: front-line melee,
  local disorder near contact, rank replacement feeding soldiers in.
- **Failure:** Red stops short and stands passive; red soldiers don't fight back.

### C3 — Retargeting after a kill
- **Setup:** Active battle where the AI destroys a blue formation (or sacrifice one).
- **Steps:** Watch the red formation that finished the kill.
- **Expected:** Within a few seconds it picks another living blue formation and moves
  to attack it. If it took losses and no enemy is near, it may briefly reform first,
  then attack.
- **Failure:** It stands idle permanently while blue formations remain.

### C4 — No idle while enemies remain
- **Setup:** Long battle.
- **Steps:** Periodically scan all living red formations.
- **Expected:** Every red formation is always doing something meaningful: marching,
  fighting, briefly reforming, or shooting. Nothing idles for more than a few seconds.
- **Failure:** Any red formation permanently inactive with blue soldiers alive.

## D. Enemy AI (archers)

### D1 — Preferred-range stop
- **Setup:** Press START; watch the red archer formation.
- **Steps:** Track its advance toward the nearest blue formation.
- **Expected:** It advances only until roughly 12 units from its target (clear,
  obvious ground visible between the two formations — several soldier-lengths), stops,
  faces the target, and starts loosing arrows. It does **not** keep creeping forward.
- **Failure:** Archers walk to within melee distance; never stop; never fire.

### D2 — Facing and firing
- **Setup:** From D1.
- **Steps:** Observe the stopped archer block.
- **Expected:** The formation wheels so its front faces the target; arrows arc from
  the block to the target formation; individual enemy soldiers flash/darken and die.
- **Failure:** Firing while facing the wrong way; no projectiles; no damage.

### D3 — Archer retargeting
- **Setup:** Let the red archers' target formation be destroyed.
- **Steps:** Watch the red archers afterward.
- **Expected:** Within a few seconds they pick another living blue formation, reposition
  only if needed to get in range, and resume firing.
- **Failure:** Archers idle permanently after their target dies.

## E. Player Archers

### E1 — Attack order and preferred-range stop
- **Setup:** Active battle. Select the blue archer formation.
- **Steps:** Drag from the archers onto a distant red formation.
- **Expected:** Attack confirmed (red line + target ring pulse). The archers march
  toward the target and stop at the same ~12-unit preferred range with obvious space,
  face the target, and begin firing arcs of arrows. They do not continue closing.
- **Failure:** They walk all the way in; stop but never fire; ignore the order.

### E2 — Target death and re-command
- **Setup:** From E1, let the target formation die (help with your swords).
- **Steps:** Watch the archers, then give them a new attack order.
- **Expected:** When the target is destroyed they stop firing and hold (player archers
  await orders — only red archers self-retarget). A new attack order works immediately.
- **Failure:** Firing at nothing; refusing new orders.

### E3 — Move order still exact
- **Setup:** Select blue archers.
- **Steps:** Drag to empty ground.
- **Expected:** They walk to that exact spot (no ranged stop distance on plain moves).
- **Failure:** Stopping short of a plain move destination.

## F. Directional Combat

For all three tests: same-type formations (sword vs sword) so the comparison is fair.
Damage tuning: front 1.0x, flank 1.5x, rear 2.0x.

### F1 — Frontal attack (baseline)
- **Setup:** Active battle. Select a blue sword formation positioned directly in
  front of a red sword formation (red's forward faces it).
- **Steps:** Attack it head-on. Time roughly how fast each side loses soldiers.
- **Expected:** A grinding, roughly even fight — both sides at 1.0x. Neither melts.
- **Failure:** A frontal fight is dramatically one-sided between equal formations.

### F2 — Flank attack
- **Setup:** Move a blue sword formation to a red formation's left or right side
  (≈90° off its forward arrow) while it faces elsewhere (e.g. it is marching at a
  different blue formation).
- **Steps:** Attack from the side. Compare kill rate with F1.
- **Expected:** Blue kills noticeably faster than the frontal baseline (1.5x out,
  while red still deals 1.0–1.5x back depending on where blue's own anchor faces).
  Clearly better than F1, clearly worse than F3.
- **Failure:** No visible difference from a frontal fight.

### F3 — Rear attack
- **Setup:** Maneuver a blue sword formation directly behind a red formation that is
  engaged or marching away.
- **Steps:** Attack from behind. Compare with F1/F2.
- **Expected:** The strongest result: blue deals 2.0x and shreds the red formation
  visibly faster than both earlier tests. **Rear > Flank > Front** must be obvious.
- **Failure:** Rear attack no better than flank/front.

### F4 — Equal-formations comparison
- **Setup:** Fresh battle. Use your two sword formations against the two red sword
  formations.
- **Steps:** Send one blue formation frontally into one red formation; simultaneously
  maneuver the other blue formation into the second red formation's rear. Watch both
  fights side by side.
- **Expected:** The rear-attacking formation wins its fight much faster and with far
  fewer losses than the frontal one. The outcome difference is readable without any
  numbers on screen.
- **Failure:** Both fights look the same.

## G. Input Leniency

### G1 — Command drag directly on formation
- **Steps:** Select a blue formation; start a drag with the finger/cursor on one of
  its soldiers; release on empty ground.
- **Expected:** Move order issued (line preview + destination marker during the drag).

### G2 — Command drag from the formation's edge
- **Steps:** Select a formation; start the drag right at the outer edge of the
  soldier block (between/beside the outermost soldiers).
- **Expected:** Still a command drag, never a camera pan.

### G3 — Command drag slightly outside the soldiers
- **Steps:** Select a formation; start the drag a finger-width (up to ~3m world
  space / ~70px) outside the visible soldiers.
- **Expected:** Still a command drag. The selected formation's grab area is forgiving.
- **Failure for G1–G3:** The camera pans instead of showing the command line.

### G4 — Camera pan from empty ground
- **Steps:** With a formation still selected, start a drag on clearly empty
  battlefield far from any selected formation.
- **Expected:** Camera pans. Selection is unaffected.
- **Failure:** Command drags firing from clearly empty ground (grab area too large).

### G5 — Small accidental finger movement (touch slop)
- **Steps:** Tap a blue formation but let the finger wobble a couple of millimeters
  during the tap.
- **Expected:** Still treated as a tap: the formation selects. No camera nudge, no
  accidental command.
- **Failure:** Tiny wobbles turn taps into pans/drags.

### G6 — Attack drag
- **Steps:** Select a formation; drag from it (or near it, per G3) onto a red
  formation; release.
- **Expected:** Line turns attack-styled over the enemy, enemy gets a target ring,
  release issues the attack with a confirmation pulse.

### G7 — Move drag with cancel
- **Steps:** Start a command drag, then drag back onto the originating formation and
  release.
- **Expected:** The command is cancelled — no move order issued.

**Interpretation rule:** a drag that begins on or within a finger-width of a
*selected* formation = command; a drag that clearly begins on empty ground = camera.

## H. Camera

### H1 — Pan responsiveness and weight
- **Steps:** Drag the camera around; move the finger in different speeds.
- **Expected:** The world follows the finger almost 1:1 with a slight, pleasant
  weight (~80ms of smoothing). No lag that makes it feel like pulling through mud;
  no rigid instant snapping.

### H2 — Release glide and settling
- **Steps:** Pan briskly and release mid-motion.
- **Expected:** The camera glides a short natural distance and settles cleanly within
  roughly half a second. It must not sail on (slippery) or stop dead on the exact
  release frame (rigid).

### H3 — Repeated direction changes
- **Steps:** Shake the camera left-right-left rapidly, then release.
- **Expected:** No overshoot oscillation or rubber-banding; motion stays controlled.

### H4 — Pinch zoom / scroll zoom
- **Steps:** Pinch on device (scroll in Editor) in and out repeatedly.
- **Expected:** Zoom changes continuously and eases toward the pinch — no abrupt
  jumps or stepping.

### H5 — Zoom limits
- **Steps:** Zoom fully in and fully out.
- **Expected:** Zoom halts smoothly at a sensible closest view and a widest view
  (orthographic size 8–30); no flipping, no infinite zoom-out.

### H6 — Battlefield boundaries
- **Steps:** Pan hard toward each edge, including with glide.
- **Expected:** The camera stops at the field bounds; the battlefield can never be
  completely lost off-screen; glide respects the same bounds.

## I. UI and Presentation

### I1 — Grass
- **Expected:** The battlefield reads as a light, clean, pleasant green — matte,
  no specular shine, not dark or muddy. Blue and red units pop against it.

### I2 — Buttons
- **Expected:** All buttons (START, RESTART, BREAK RANKS, REFORM, ROTATE) share one
  family: rounded corners, consistent size and spacing, readable white labels, clear
  pressed/disabled states. START/RESTART use the blue accent; command buttons are
  neutral dark.

### I3 — Mobile text sizing
- **Setup:** Run on a phone (or a phone-aspect Game view, e.g. 19.5:9 landscape).
- **Expected:** The hint line, info panel, and button labels are comfortably readable;
  nothing looks blown-up or microscopic; the bottom panel doesn't crowd the screen.

### I4 — Safe area
- **Setup:** Phone with a notch/punch-hole, landscape.
- **Expected:** The hint text and bottom panel controls stay inside the safe area —
  nothing hides under the notch or the rounded corners.

### I5 — Selection circles and formation arrow
- **Steps:** Select a formation; move it; rotate it.
- **Expected:** Every living selected soldier has a clean white ground circle; exactly
  one arrow per selected formation shows facing/heading; circles vanish on deselect
  and never appear under dead soldiers.

### I6 — Damage darkening
- **Steps:** Watch any prolonged fight.
- **Expected:** Soldiers visibly darken toward black as health drops, flash white on
  hits, fall over and sink away on death. (Unchanged V0 behavior — must still work.)

## J. Full V1 Acceptance Battle

- **Setup:** Fresh launch. Landscape. Battle scene.
- **Steps:**
  1. Press **START**.
  2. Observe the red army activate on its own: two sword formations marching, archers
     advancing to range and firing.
  3. Select a blue sword formation and **move** it — confirm ordered march.
  4. Select your archers and order an **attack** on an approaching red sword formation.
  5. Verify the archers **stop at range** with obvious space and shoot continuously.
  6. Send your first sword formation **frontally** into one red sword formation and
     let the grind develop.
  7. Take your second sword formation on a walk **around** the other red formation
     and hit it from the **side or rear**.
  8. Compare the two melees: the flank/rear fight should collapse visibly faster in
     your favor than the frontal grind.
  9. Kill one red formation completely and watch the AI **retarget** survivors onto
     your remaining formations within a few seconds.
  10. Use Break Ranks / withdrawal / Reform as needed — all V0 tools must still work
      mid-battle.
  11. Fight to a conclusion. Verify the correct **victory/defeat banner**.
  12. Press **RESTART** and confirm a completely clean new pre-battle state.
  13. Press START again and confirm the second battle runs identically.
- **Expected result:** It feels like fighting an actual opponent in a small tactical
  battle — positioning, facing, unit type, and your decisions clearly determine the
  outcome.
- **Failure:** Any AI formation idles permanently; archers behave like melee with
  bows; directional attacks make no visible difference; restart is dirty.
