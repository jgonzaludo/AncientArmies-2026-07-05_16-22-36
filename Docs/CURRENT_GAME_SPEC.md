# Current game specification

What Ancient Armies actually does today. Everything here is implemented and
running in `Assets/Scenes/Battle.unity`. Nothing here is aspirational.

## Shape of a match

A single symmetric land battle: Blue (player) against Red (autonomous AI).
The battle runs Pre → Active → Ended. Press **START** to go Active; a
persistent **RESTART** button reloads the scene at any time before the end.
The battle ends when one side has no living soldiers, reporting
`BLUE WINS` / `RED WINS` / `DRAW`. *A new win condition and time limit are
approved but not built — see [Approved, not yet implemented](#approved-not-yet-implemented).*

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
| Tap friendly formation | Select it **exclusively** |
| Tap the sole selected formation | Deselect |
| **Double**-tap a formation | Add it to the current selection (keep double-tapping to build a group) |
| Tap empty ground | Clear all selection |
| Tap enemy formation | Inspect it |
| Drag from a selected formation | Command drag — move order, with live destination slot dots |
| Second finger during a move drag | Twist the final facing; it **locks** once past the deadzone |
| Drag onto an enemy | Attack order (line + ring preview instead of dots) |
| Drag on empty ground | Pan the camera |
| Pinch / scroll | Zoom |

Touch targets are deliberately forgiving: tap padding and command-grab
tolerance are zoom-aware and expressed in 1080p-reference pixels, so they stay
finger-sized regardless of screen density.

HUD command buttons: **CHARGE**, **REFORM**, **ROTATE**, plus persistent
**RESTART**, **BANNERS** (cycles banner visibility) and **SCENES**. *Removing
REFORM is approved but not built — see [Approved, not yet implemented](#approved-not-yet-implemented).*

## Formation states

`Ordered · Attacking · Engaged · BrokenRanks · Withdrawing · Reforming · Charging`

- **Ordered** — full slot discipline; the parade state.
- **Attacking** — closing with an explicit target. Melee closes to contact;
  ranged formations stop at their preferred range and wheel to face.
- **Engaged** — melee contact. Entered automatically when any soldier is
  engaged; exits after 1.5 s with no contact.
- **Withdrawing** — any move order issued while Engaged is a disengagement
  attempt; the formation backs away without wheeling through the melee.
- **BrokenRanks** — zero slot steering. Soldiers act as individuals with a wide
  acquisition radius, leashed to a rally point. **Reform is the only command
  available.** *Approved but not built: this state is renamed "Disordered" in
  design language and recovers automatically — see [Approved, not yet implemented](#approved-not-yet-implemented).*
- **Reforming** — survivors walk back to a rebuilt slot grid at the frozen
  rally anchor. Completes at 75 % in place (or a 25 s timeout); **aborts back
  to Broken** if 12 % of survivors are melee-engaged. Arrow fire alone never
  aborts a reform.
- **Charging** — a committed sprint (1.3× speed) at a nearby enemy within 45 m.
  Grants a 1.5× impact bonus for 2 s after first contact, then dissolves into
  **pursuit** on the broken-ranks machinery.

Break Ranks freezes the rally anchor and facing where the ranks broke. A
pursuit has no pre-frozen rally — the standard moves with the pack until
Reform plants it where the men stand.

## Rotation maneuvers

The Rotate command classifies the shortest signed yaw delta:

- **≤ 20°** — small turn: facing snaps, everyone keeps their slot, soldiers
  pivot in place and redress.
- **≥ 135°** — about-face: facing snaps and a nearest-slot remap reinterprets
  the ranks in place; the old rear becomes the new front.
- **Otherwise** — inner-flank wheel: the whole slot grid rotates rigidly around
  the inner front corner. The outer flank walks the long arc; nobody crosses
  the block. A wheel pauses while more than 40 % of soldiers are mid-attack.

Rotate is unavailable while auto-facing (chasing or engaged), while broken, or
while otherwise busy — the button reflects exactly this.

## Combat

- **Directional damage** on the formation's canonical facing: front ×1.0,
  flank ×1.5, rear ×2.0, with 60° front and rear arcs.
- A **global damage scale of 0.25** stretches battles roughly 4×. It preserves
  every relative combat relationship — only the attrition rate changes.
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

## Approved, not yet implemented

Decided on 2026-09-21 — see `Docs/DECISIONS.md` for the full record and the
questions still open. **None of this is in the build yet.** When a piece
ships, move it into the sections above and delete it here.

- **Morale** — each formation has a Morale value from 0 to 100. Casualties
  lower it. It recovers with time out of melee, up to a rest ceiling set by
  cumulative losses; a stricter contact ceiling applies in melee. Tunables
  live in the `MoraleConfig` ScriptableObject, referenced from `BattleSetup`
  (a missing reference is a loud error, never a silent default).
- **Disordered** — the design name for `BrokenRanks`. Behavior unchanged:
  loose, agent-native combat, still fighting.
- **Routing** — a new state, entered below 15 morale. Soldiers flee and stop
  acquiring targets, the banner badge disappears, and the formation cannot be
  ordered. It rallies automatically above 35 morale when not in melee.
- **No REFORM button** — recovery is automatic for both sides, through one
  shared rule.
- **Automatic reform** — a disordered formation reforms whenever it is not in
  melee (no soldier with an enemy within 3 m). Nearby enemies and arrow fire
  do not block it. A reform under way still aborts at 12 % of survivors
  engaged. After a charge, a century is uncontrollable until it is out of
  melee and reformed.
- **Win condition** — a side collapses when 50 % of its formations are Routing
  or destroyed, counted from its actual formations (never assumed to be 8).
  Collapse latches: the result stands even if units rally afterwards.
- **Time limit** — a visible 10:00 countdown. On expiry, the side with more
  intact formations wins. Draws are allowed.

## Deliberately not in this build

Terrain, progression, multiplayer, and advanced enemy tactics are all out of
scope. Do not add them without explicit approval.
