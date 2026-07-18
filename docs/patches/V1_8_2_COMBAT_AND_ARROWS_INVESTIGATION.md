# v1.8.2 — Investigation & Fix Record (arrow damage, volley spread, spacing, arrow duplication)

Three parallel read-only investigations preceded this patch. Root causes and
what was changed — kept here so these mistakes are not repeated.

## 1. Arrows felt spongy
Front shots on a 35 HP melee soldier took **4–5 arrows** (10 dmg × skill
0.85–1.13 × front 1.0). Flank/rear were already ≤3. **Authoritative value was
scene-pinned**: `Battle.unity` serializes `archerStats.attackDamage: 10` —
editing the C# default alone would have been a silent no-op.
**Fix:** `attackDamage 10 → 14` in BOTH `Battle.unity` and the code default.
14 yields exactly 3 front arrows across the whole skill band (11.9–15.8 per
hit). Side effects: flank 2, rear 1–2, archers-vs-archers 2 front.

## 2. All archers volleyed one man
Chain: ranged acquire radius ≈48 m → `CollectEnemies` ring-order truncation
at 96 keeps only the nearest cluster → 80 co-located archers see identical
candidate lists → score = nearest-first with NO ranged spread term (the
attacker-count system was melee-gated on both the increment and the penalty)
→ identical argmin for everyone → hysteresis locks the volley → victim dies →
all re-converge on the next nearest.
**Fix (deterministic, allocation-free):** `rangedAttackerCount` maintained in
`SetTarget`/`Die` (mirrors the proven melee path) + graduated score penalty
`×(1 + 0.35 × count)` + a fixed per-soldier salt (0.97–1.03 from slot) to
break residual lockstep. Arrows now distribute proportionally to load.

## 3. Deployment "still touching" after v1.8.1
The v1.8.1 offsets (32/16) WERE live and the flat-footprint gaps (4.4 m) were
real. Two things made it still look wrong:
- **Off-field spill:** armySeparation 240 put the reserve rear rank at
  z = −156.6 with fieldHalfZ = 140 — 16.6 m past the ground edge, jamming the
  rear lines against the void under the camera clamps.
- **Camera projection:** at the 40° pitch a ~1.9 m soldier model consumes
  ~2.3 m of perceived ground depth, so a 4.4 m flat gap reads as ~2 m.
**Fix:** `fieldHalfZ 140 → 170` (whole army on-field with margin, keeps the
240 separation / ~46 s contact), `archerLineOffset 16 → 20`,
`reserveLineOffset 32 → 40` (≈8.6 m flat gaps ≈ 6 m perceived).
**Trap encountered while fixing:** the first in-editor `SaveScene` pinned
STALE load-time values (18/140) for the previously-unserialized fields — the
in-memory component does NOT pick up new C# defaults across a domain reload.
Every changed field was explicitly re-stamped before the final save. Rule:
after changing defaults of fields the open scene doesn't serialize, stamp
them explicitly in-editor before ANY scene save.

## 4. Double rotate arrow + "triangle artifact"
All arrows share one 3-triangle mesh. `FormationArrow` (white) gated only on
`IsSelected`, so: selecting a MOVING formation showed it at the anchor beside
the yellow destination arrow (the "artifact"), and rotate mode stacked the
yellow rotate arrow on top of it at the same pivot/radius (the "two arrows").
**Fix — single-arrow ownership:** exactly one direction arrow per context:
- rotate session active → ONLY the yellow rotate arrow (FormationArrow and
  the destination-preview arrow hide via `PlayerCommander.IsRotating(f)`);
- Move order preview showing → ONLY the yellow destination arrow;
- selected + stationary + not rotating → ONLY the white facing arrow.
Slot circles keep showing during rotate (they rotate live under the drag).

## 5. "Deployment facing" report
No facing bug found in code: spawn yaws (blue 0 / red 180), the white arrow,
and the banner wedge all read `AnchorForward` correctly; the bad-looking
default `DestinationFacing` is always overwritten before anything renders it.
The perceived issue was almost certainly the duplicate-arrow clutter above —
re-evaluate after this patch.

## Notable live divergence (not changed)
`meleeStats.moveSpeed`: scene pins **2.7**, code default says 3.2 — the scene
wins. Left as-is (shipped behavior); reconcile deliberately later.
