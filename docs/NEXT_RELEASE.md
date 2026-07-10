# Ancient Armies — Next Release Roadmap

Future ideas deliberately **not** implemented in V1.2. Nothing in this file is
committed scope; it preserves direction so V1.2 decisions don't accidentally
foreclose it. Do not implement anything here without explicit approval.

---

# Proposed next release: V1.3 — Command Stances & Advanced Control

**Purpose:** give the player intentional automation and advanced command tools
without increasing ordinary control friction.

## 1. Auto Engage stance

Explicit per-formation behavior toggle:

- **HOLD** — the formation does not automatically acquire a new target.
- **ENGAGE** — an idle formation may automatically attack nearby enemies.

This must be a deliberate stance the player sets. Units must not all become
automatically aggressive by default without player control.

## 2. Auto Reform preference

Possible toggle:

- **AUTO REFORM ON** — a sufficiently disengaged, disordered formation
  automatically attempts to rebuild.
- **AUTO REFORM OFF** — waits for an explicit Reform command.

Important distinction: **Auto Reform is not Auto-Close.** Auto-Close (shipped
in V1.2) is normal formation competence — continuously compressing casualty
gaps while ordered. Auto Reform would be the full Reform action (recenter on
survivors, rebuild columns, reset state) fired automatically after
disengagement.

## 3. Intentional multi-select

V1.2 removed accidental additive tap selection; the selection container is
still a list, so a deliberate system can build on it. Options to evaluate:

- dedicated multi-select mode / selection mode button
- hold-to-add
- drag selection box
- lasso

Whatever ships must be explicitly discoverable. Never return to accidental
additive tap selection.

## 4. Drawn movement paths

Draw a path across the battlefield; the selected formation follows it.
Investigate only once ordinary command input is extremely reliable.

## 5. Drawn formation shapes

Long-term experiment: the player draws a line or shape and soldiers reorganize
into that front. Not near-term scope; preserved for exploration.

## 6. Separate Face Direction from Wheel Maneuver

- V1.2 shipped: **Rotate = face direction** (soldiers pivot in place; facing
  snaps; nearest-slot remap).
- Future: **FACE** (pivot in place) vs **WHEEL** (physically maneuver the
  formation footprint around a turning point) as distinct commands. Matters
  for advanced tactical movement once facing bonuses deepen.

---

# Proposed V2 after stability: Morale & Routing

Do not implement until combat and control are stable.

Morale should eventually connect:

- casualties → morale pressure
- flank attack → major morale pressure
- rear attack → severe morale pressure
- friendly formation destroyed nearby → morale shock
- successful disengagement and reform → partial recovery

Eventually:

- high morale → holds formation
- low morale → degraded control
- morale break → rout

---

# Smaller deferred items noted during V1.2

- Confirmation (or undo grace) for the mid-match RESTART button — it currently
  reloads instantly on tap.
- Optional: keep the front rank's world position fixed during auto-close
  compaction (rows currently recenter around the anchor).
- Per-soldier (rather than formation-anchor) directional combat evaluation.
- Archer kiting / skirmish behavior remains an explicit non-goal until designed.
