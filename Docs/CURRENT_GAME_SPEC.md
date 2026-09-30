# Current game specification

What Ancient Armies actually does today. Everything here is implemented and
running in `Assets/Scenes/Battle.unity`. Nothing here is aspirational.

## Shape of a match

A single symmetric land battle: Blue (player) against Red (autonomous AI).
The battle runs Pre → Active → Ended. Press **START** to go Active; a
persistent **RESTART** button reloads the scene at any time before the end.
The battle ends by **line collapse**: a team loses the moment 50 % of its
formations are Routing or destroyed (counted from its actual formations, never
an assumed 8). That moment is the latch — nothing afterwards reopens it. A
visible **10:00 countdown** is the stall backstop: small and faint until the
last 60 s, then bright and large. At 0:00 the side with more intact formations
(alive and not Routing; BrokenRanks counts) wins, equal is a draw. The result
states its reason internally (`BLUE WINS — RED LINE COLLAPSED`, `RED WINS —
TIME`, `DRAW — BOTH LINES COLLAPSED`), but **the end screen shows only the
winner**: `BLUE WINS`, `RED WINS` or a grey `DRAW`.

A **tide bar** at the top center shows the balance of the battle while it is
Active: blue fills from the left, red is the rest, a tick marks even. Each side's
power is the living men of formations that have not routed, each weighted by its
morale (never below 25 %); the fill eases toward the value rather than jumping.
The 10:00 countdown sits just under it, clearly readable from the start (shown
in the pre-battle too) and larger and amber in the final 60 s. `showTideBar` in `MoraleConfig` switches it off.

## Scale and army composition

Per side, fixed and symmetric — no random rerolls:

| Element | Value |
| --- | --- |
| Centuries per side | 8 (6 melee + 2 archer) |
| Soldiers per century | 80 |
| Total soldiers | 1,280 |
| Melee frontage | 10 wide × 8 deep |
| Archer frontage | 10 wide |
| Melee spacing | 1.15 m (near shoulder-to-shoulder) |
| Archer spacing | 1.75 m (visibly looser) |

Deployment is a Roman-style three-line arrangement: four melee centuries in the
front line, two archer centuries 20 m behind it, and two melee reserve
centuries 40 m behind the front. The two armies start 240 m apart on a
410 × 340 m field. `ValidateLineGaps` warns if any two neighbouring formations
would overlap.

Formations physically march to their deployment positions during the Pre
phase; combat cannot start because soldiers only fight while the battle is
Active.

## Century roles

A century is 80 soldiers **total** — specialists occupy slots, they are not
added on top. Roles are reserved in data (`UnitStats.BuildCenturyRoles`):

- **Centurion** — front-right corner slot, melee centuries only. Re-pinned to
  that corner after every slot remap. Never promoted or respawned.
- **Signifer** — right of front centre. **Cornicen** — behind the signifer.
- **Optio** — rear centre. **Tesserarius** — in the ranks.
- Remainder: **Legionary** or **Archer**.

All roles currently spawn the generic legionary or archer visual; the
composition system reserves the positions for specialist prefabs later.

## Controls (locked)

Mobile-first touch grammar. Mouse input in the editor mirrors it 1:1.

| Input | Result |
| --- | --- |
| Tap friendly formation | Select it **exclusively** (a routing formation cannot be selected) |
| Tap the sole selected formation | Deselect |
| **Double**-tap a formation | Add it to the current selection (keep double-tapping to build a group) |
| Tap empty ground | Clear all selection |
| Tap enemy formation | Inspect it |
| Drag from a selected formation | Command drag — move order, with live destination slot dots |
| Second finger during a move drag | Twist the final facing; it **locks** once past the deadzone |
| Drag onto an enemy | Attack order (line + ring preview instead of dots) |
| Drag on empty ground | Pan the camera |
| Pinch / scroll | Zoom |

A selected formation that routs leaves the selection immediately.

Touch targets are deliberately forgiving: tap padding and command-grab
tolerance are zoom-aware and expressed in 1080p-reference pixels, so they stay
finger-sized regardless of screen density.

HUD command buttons: **CHARGE** and **ROTATE**, plus persistent **RESTART**,
**BANNERS** (cycles banner visibility) and **SCENES**. There is no Reform
button: recovery is automatic. A selection of broken centuries shows
"Regroups when clear of melee" instead of commands.

## Formation states

`Ordered · Attacking · Engaged · BrokenRanks · Withdrawing · Reforming · Charging · Routing`

- **Ordered** — full slot discipline; the parade state.
- **Attacking** — closing with an explicit target. Melee closes to contact;
  ranged formations stop at their preferred range and wheel to face.
- **Engaged** — melee contact. Entered automatically when any soldier is
  engaged; exits after 1.5 s with no contact.
- **Withdrawing** — any move order issued while Engaged is a disengagement
  attempt; the formation backs away without wheeling through the melee.
- **BrokenRanks** ("Disordered" in design language) — zero slot steering.
  Soldiers act as individuals with a wide acquisition radius, leashed to a
  rally point. **It takes no orders.** It stays loose for `pursuitMaxSeconds`
  (9 s) — a charge's pursuit lasts exactly that long. Then it regroups: if at
  most 15% of its men have an enemy within 3 m (and 2 s have passed since any
  aborted reform) it reforms by walking back to the standard; if it is still
  locked in melee it **re-forms in place** — slots rebuilt around the main pack
  where it stands, planted on the Signifer / senior officer, facing the nearest
  enemy — and becomes Engaged again, with orders available. Both teams use
  this one rule. A charge is a commitment, but only for 9 s.
- **Reforming** — survivors walk back to a rebuilt slot grid at the frozen
  rally anchor. Completes at 75 % in place (or a 25 s timeout); **aborts back
  to Broken** if 30 % of survivors are melee-engaged. Arrow fire alone never
  aborts a reform.
- **Charging** — a committed sprint (1.3× speed) at a nearby enemy within 45 m.
  Grants a 1.5× impact bonus for 2 s after first contact, then dissolves into
  **pursuit** on the broken-ranks machinery.
- **Routing** — entered from any state when morale falls below 15.
  **Permanent.** Soldiers stop acquiring targets and never attack. Each flees
  away from the nearest enemy formation, bent toward its own army's back
  edge, at 1.2× move speed, with no leash, and **runs off the map** — a
  router that crosses the field edge leaves the battle for good (not a
  casualty). The formation takes **no orders** from the player, the AI, or
  any HUD button, has no banner, and is never an attack or charge target;
  enemy soldiers still cut down routers within reach and pursuing packs
  still hunt them — that is the pursuit. It counts as fallen for the collapse
  rule. (`routedCanRally` in MoraleConfig re-enables the Chunk A rally: routers
  then stop at the field edge and may rally above 35 morale.)

A reform plants the standard on the **Signifer** if he is alive in the main
pack (Romans formed on the standard), else on the highest-grade officer in the
main pack, else on the pack's center. "Main pack" is the dominant cluster, so
stragglers never drag the rally point into empty ground.

## Rotation maneuvers

The Rotate command classifies the shortest signed yaw delta:

- **≤ 20°** — small turn: facing snaps, everyone keeps their slot, soldiers
  pivot in place and redress.
- **≥ 135°** — about-face: facing snaps and a nearest-slot remap reinterprets
  the ranks in place; the old rear becomes the new front.
- **Otherwise** — inner-flank wheel: the whole slot grid rotates rigidly around
  the inner front corner. The outer flank walks the long arc; nobody crosses
  the block. A wheel pauses while more than 40 % of soldiers are mid-attack.

Rotate is unavailable while auto-facing (chasing or engaged), while broken or
routing, or while otherwise busy — the button reflects exactly this.

## Morale

Every formation has morale from 0 to 100, ticked 4× per second while the
battle is Active. All values live in `MoraleConfig.asset`.

- **Base morale** is capped by losses against starting strength. Out of melee
  the rest ceiling is 100 − 1 × loss%. In melee (any soldier with an enemy
  within 3 m) the contact ceiling is 100 − 1.1 × loss%, so a frontal grind
  alone routs a century only near 77% losses. **Routs are meant to come
  from being surrounded or from sudden shocks, not from grinding.**
- **Shock debt** is panic. Each casualty adds 3 × its percent of starting
  strength (3.75 per man in an 80-man century) **times the zone of the killing
  blow**: front ×0.25 (steady frontal losses are muted), flank ×2, rear ×3; a
  ranged kill weighs ×0.5 instead. **Burst rule:** once more than 5% of
  starting strength (4 of 80) has fallen inside a rolling 3 s window, further
  casualties weigh ×2 whatever their zone (the higher weight applies). All
  of it is then multiplied by `panicScale` 0.65 and rate-limited to 8 panic
  per second — the excess queues and lands over the following ticks, never
  discarded. The debt decays
  by 5 per second at all times, **divided by (1 + extra enemy formations
  engaged)** — 2-on-1 halves decay, 3-on-1 thirds it. Losing 10 of 80 men at
  once in melee routs a fresh century; losing them over a few seconds does
  not.
- **Surrounded**: every tick adds 8 panic per second for each engaged zone
  beyond the first. Zones (front, left flank, right flank, rear) come from
  the facing of each enemy formation that is actually fighting us — at least
  10% of our living men in contact with it — so a neighbour brushing the
  line's edge is not a second side. Unscaled and outside
  the panic cap. Two zones builds panic steadily (decay is 5/s); three or
  more breaks a unit within seconds.
- **Contagion**: when a formation routs, every same-team formation within
  30 m (anchor to anchor) takes 15 shock. A steady unit shrugs it off; a
  wavering one goes over.
- **Officer death**: an officer killed (not one who fled) adds his
  `deathShock` to his formation on top of the casualty shock — Tesserarius
  and Cornicen 4, Optio 12, Centurion 24, Signifer 28 (losing the standard
  hits hardest) — unscaled and outside the panic cap. A fresh century never routs from one officer death; a
  wavering one does from losing its Centurion or standard.
- **Reported morale** = base − shock debt. Below 15 the formation routs.
- **Recovery**: +3 base per second, only out of melee and only once 3 s have
  passed without a casualty.
- Each panic input has an enable switch in `MoraleConfig`
  (`enableFlankShock`, `enableOutnumberShock`, `enableContagion`,
  `enableOfficerShock`, `enableSurroundShock`, `enableArrowShockWeight`,
  `enableShockCap`) so a problem can be isolated on device without code.

## Officers

Every century spawns a Signifer, Cornicen, Optio and Tesserarius; melee
centuries also spawn a Centurion (archer centuries have no Centurion). Grades
live in `OfficerRanks.asset`: Centurion 4, Optio 3, Signifer 2, Tesserarius 1,
Cornicen 1, line soldiers 0. Officers are tougher: spawn health ×2.0 for the
Centurion and Signifer, ×1.5 for the Optio. The commanding officer is the highest-grade
survivor; succession is implicit. Grade numbers are drawn above the officers
of the selected or inspected formation only.

## Combat

- **Directional damage by facing** (`CombatConfig.asset`): front ×0.5, flank
  ×1.5, rear ×2.0, with 60° front and rear arcs. The zone of a hit is the
  attacking formation's facing relative to the defender's — roughly opposite
  is front, roughly the same way is rear, in between is a flank — never the
  striking soldier's position (engaged lines interleave). Attackers with no
  formed facing (BrokenRanks, Routing) use the soldier's position instead.
  Arrows use the same multipliers, so they do half damage from the front.
  Head-on fights bleed slowly; flank, rear and the pursuit of routers stay
  deadly.
- **Personal space:** every soldier's body collider is 0.5 m radius
  (`CombatConfig.soldierColliderRadius`), so any two soldiers — friend or
  enemy, in any state — stay at least 1.0 m apart; visuals are unchanged.
- A **global damage scale of 0.15** stretches battles. It preserves every
  relative combat relationship — only the attrition rate changes.
- **Melee**: 12 damage on a 4 s cadence, 1.7 m reach. Gameplay still chooses
  one of three attack variants (thrust / over-shield / diagonal slash), but
  with placeholder capsules there is no clip to sync to, so damage resolves
  immediately rather than on an authored contact frame. At most 3 attackers
  per target spreads strikes along the contact line.
- **Ranged**: archers fire as a formation. A volley window opens every 8 s for
  1.5 s, and each archer looses once inside it at a deterministic offset —
  readable volleys with lulls, not 80 independent drizzling timers. Volley
  clocks are staggered per century. 10 damage, 48 m max range, 40.8 m
  preferred, 2.5 m minimum (inside that they defend with a sidearm). Target
  scoring applies a graduated spread penalty so 160 archers distribute across
  the enemy line instead of massacring one man.
- Per-soldier **skill** is a fixed variation, not hidden per-hit dice.

## Formation cohesion behaviors

- **Auto-close** — ordinary attrition leaves empty slots; the grid rebuilds for
  the surviving headcount every 2 s once 2 casualties accumulate. Suspended
  during melee, during a Rotate maneuver, and while a century is firing.
- **Rank replacement** — during ordered melee, a coherent soldier one row back
  is promoted into a vacated front slot. Cascading front-to-back feeds men into
  the fight progressively while the rest stays structured.
- **Edge engagement** — unengaged soldiers within 6 m of an enemy get loosened
  slot weight and longer reach, so melee edges bend and wrap without the whole
  formation dissolving.
- **Defensive pivot** — an engaged formation slowly wheels toward the contact
  after a 1.2 s delay. Heavily engaged formations (≥ 35 % engaged) are
  **pinned**: the tactical flank stays exposed until the formation genuinely
  turns.
- **Dominant group** — union-find clustering over soldier proximity, with
  hysteresis, picks the main body. It anchors the banner and the pursuit
  anchor.

## Enemy AI

`EnemyCommander` runs the Red side through the same public command API the
player uses — it has no privileged access. It scores targets and runs separate
routines for the front line, the archers, and the reserves.

## Presentation

**The build currently runs on placeholder primitives.** All character art,
models, and animations were stripped pending Tripo/Blender production:

- Soldiers are **capsules** (melee wider than archers) with a cube weapon that
  doubles as the facing indicator, tinted by team and unit type. No Animator,
  no rig, no equipment meshes.
- The ground is a **flat sand colour** — no texture, no tiling, so nothing
  repeats at max zoom-out.
- Arrows are small dark spheres.

Everything else is unchanged:

- Fixed orthographic camera, 40° pitch, pan-and-zoom clamped to the field. It
  starts framed on the player's deployment zone.
- Formation banners with a cycleable visibility mode, tracking the dominant
  group.
- Ground arrows and live destination-slot previews for orders.
- A merged-billboard **impostor LOD** hides individual soldier visuals at far
  zoom. Simulation is untouched — only presentation goes dormant.
- **Wavering halo**: an additive 4× glow plus a thick ring behind the badge,
  shown below 50 morale and hidden again above 53, #FFD400 → #FF2A00, pulsing
  1.5 → 3 Hz in both alpha (0.7–1) and scale (1–1.4) as morale falls toward
  15, with a one-time flash when a formation first enters the band. Never
  dimmed by banner dimming; the badge itself is untouched. Both teams.
  Routing formations show no banner at all.
- **Officer grade markers** above the officers of the selected or inspected
  formation.
- **Debug morale overlay** (tuning only, `showDebugMorale`): each formation's
  morale and state near its banner.

## Deliberately not in this build

Terrain, progression, multiplayer, and advanced enemy tactics are all out of
scope. Do not add them without explicit approval.
