# Formation Spacing Adjustment Report

Date: 2026-07-13 · Branch: `v1-prototype` · Not committed (working tree only)

## Change

| Field | Old | New | Increase |
|---|---|---|---|
| `meleeSpacing` | 0.95 | 1.05 | +10.5% |
| `archerSpacing` | 1.7 | 1.85 | +8.8% |

Goal: give the new Roman legionary / archer models slightly more visual breathing
room per soldier without changing formation footprint semantics, combat tuning,
or column counts.

## Files changed

1. `Assets/Scripts/BattleSetup.cs` — C# defaults for `meleeSpacing` and
   `archerSpacing` (lines 50 and 52).
2. `Assets/Scenes/Battle.unity` — the serialized `BattleSetup` component
   **overrides C# defaults** (known project gotcha), so both fields were also
   updated in the scene via Unity MCP `execute_code`
   (`FindAnyObjectByType<BattleSetup>()` → set both fields → `SetDirty` →
   `EditorSceneManager.SaveScene`). Verified on disk after save:

   ```
   Assets/Scenes/Battle.unity:331  meleeSpacing: 1.05
   Assets/Scenes/Battle.unity:332  archerSpacing: 1.85
   ```

## How spacing flows through the game (verified by code trace)

`BattleSetup.CreateFormation` copies the per-type value onto each runtime-built
formation: `f.spacing = stats.isRanged ? archerSpacing : meleeSpacing;`
(`BattleSetup.cs:166`). From there, `Formation.spacing` is read in exactly one
place — `Formation.BuildSlots` (`Formation.cs:116-136`) — which derives
everything else. That single choke point is why every downstream system picks
up the new values automatically:

| System | Where | Why it's automatically covered |
|---|---|---|
| Slot grid geometry | `BuildSlots` (`Formation.cs:126-127`) — slot x/z offsets are multiples of `spacing` | Direct consumer; rank/file gaps scale with the new value. |
| Bounding radius | `BuildSlots` (`Formation.cs:133`) — `BoundingRadius = sqrt(maxSq) + spacing` | Recomputed on every `BuildSlots` call. Consumed by `FormationArrow.cs:25` (arrow placement), `PlayerCommander.cs:351` (attack-drag target diameter `BoundingRadius * 2.1`), and `PlayerCommander.cs:451` (rotate preview arrow). All grow proportionally — touch/drag targets get slightly *more* forgiving. |
| Footprint (touch target) | `BuildSlots` (`Formation.cs:134`) → `FootprintHalfExtents` → `DistanceToFootprint` → `InteractionDistance` (`Formation.cs:153-167`) | The formation-level tap target rectangle is derived per rebuild; no cached copies exist. |
| Auto-close | `UpdateAutoClose` (`Formation.cs:373`) calls `BuildSlots(soldiers.Count)` | Compaction rebuilds the grid at the formation's own (new) spacing. |
| Reform | `IssueReform` (`Formation.cs:237`) calls `BuildSlots(n)` | Same — the reformed rectangle uses the new spacing. |
| Rotation (pivot-in-place) | `IssueFace` → `AssignNearestSlots` uses `GetSlotWorldPos`, which reads `slotOffsets` built by `BuildSlots` | Slot reassignment operates on the already-scaled offsets. |
| Rank replacement | `UpdateRankReplacement` (`Formation.cs:398-418`) compares soldier positions to `GetSlotWorldPos` | Same derived data. Note the *thresholds* (`promoteVacancyDist` 1.6, `promoteCoherenceDist` 2.2) were NOT scaled — see below. |
| Soldier steering | `Soldier.cs:77` steers toward `formation.GetSlotWorldPos(slotIndex)` | Same derived data. |

No other script reads `meleeSpacing`, `archerSpacing`, or `Formation.spacing`
(verified by grep across `Assets/Scripts`).

## What was NOT changed

- Collider radius, strike/ranged ranges, damage, cooldowns, move speeds.
- `formationColumns` (10), soldier counts (50 melee / 40 archers), formations per side.
- Rank-replacement thresholds (`promoteVacancyDist` 1.6, `promoteCoherenceDist` 2.2):
  at 1.05 melee spacing these still exceed one slot pitch, so promotion cascades
  keep working; not retuned.
- Engagement radii (`personalEngageRadius` 3, `disengageRadius` 9, etc.).
- `Formation.spacing` C# default (1.3) — dead in practice; always overwritten by
  `CreateFormation`.
- Scene YAML was not hand-edited; the save went through the editor (SaveScene
  serializes every in-memory field, and both spacing fields were set explicitly
  in the same pass).

## Manual checks to run (Play Mode, not performed here)

1. **Density comparison (D1-style)**: spawn a battle, eyeball melee blocks at
   1.05 vs. memory/screenshots of 0.95 — soldiers should read as individually
   distinguishable but still a continuous front; archers still visibly looser
   than melee.
2. **Touch targets**: tap each formation near its edge and between soldiers —
   selection should be at least as forgiving as before (footprint grew ~10%).
3. **Auto-close**: let a melee formation take 2+ casualties while Ordered —
   gaps should squeeze shut at the new (slightly looser) pitch, no overlap.
4. **Reform**: break ranks, disengage, Reform — rebuilt rectangle should use
   the new spacing with no soldier overlap or stragglers.
5. **Rotation**: rotate an ordered formation — pivot-in-place slot reassignment
   should look unchanged, just on the slightly wider grid.
6. **Melee contact**: verify two melee fronts still make contact cleanly
   (strikeRange 1.7 vs. melee spacing 1.05 — front ranks still overlap reach).
