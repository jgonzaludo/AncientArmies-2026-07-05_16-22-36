# V1 Incremental Overhaul — Working Plan (Phase 0 baseline)

Branch `v1-prototype` @ `e0381bf`. Working tree has one pre-existing user
modification (`ProjectSettings/ProjectSettings.asset`) — preserved, not
committed by this run. Untracked `.agents/`, `skills-lock.json` untouched.

## Actual architecture map (verified in code)

| System | File | Notes |
|---|---|---|
| Formation FSM, slots, anchor, facing | `Formation.cs` | States: Ordered/Attacking/Engaged/BrokenRanks/Withdrawing/Reforming. `AnchorRot` is authoritative tactical facing (Patch 6). Slots derived from `AnchorPos/AnchorRot`. Rotate = SmallTurn/Wheel/AboutFace maneuvers. `DominantGroupCenter` cluster system (Patch 6). |
| Soldier sim | `Soldier.cs` | Slot attraction + local combat blend; deferred melee impact + variants; visual-facing resolver (pending shot > combat > velocity > formation forward). Separation = brute force over team registry (every 4th tick). Targeting = brute force over enemy registry (0.3 s). |
| Input/selection | `PlayerCommander.cs` | V1.2 exclusive tap select, drag = move/attack (group offsets preserved), rotate mode = preview arrow drag, pinch/pan. |
| Facing arrow | `FormationArrow.cs` | Reads `AnchorForward`; hidden while broken (Patch 6). |
| Banners | `FormationBannerController/Manager.cs` | Patch 7 adaptive sprites; broken banners currently follow dominant cluster (this overhaul pins them to a fixed rally anchor instead — locked decision). |
| HUD | `BattleHUD.cs` | Runtime-built; Break/Reform/Rotate buttons; rounded-rect 9-slice already. |
| Spawn/lifecycle | `BattleSetup.cs` | 5 formations/side random mix; melee 50, archer 30. **Scene gotcha:** `Battle.unity` serializes these fields — this overhaul introduces NEW field names so C# defaults apply without scene edits. Victory = registry empty. |
| Enemy AI | `EnemyCommander.cs` | Single-layer scored target assignment; no phases/reserves/lanes. Rewritten in Phase 7. |
| Camera | `BattleCamera.cs` | Pitch-aware pan clamps from serialized `focusHalfX/Z`; zoom 5.5–36. |
| Combat math | `BattleSetup.GetDirectionalMultiplier` | Uses defender `AnchorForward` — already tactical-facing based. |

## Rotation/facing writers (complete list)
`Formation` (AnchorRot: IssueFace maneuvers, chase rotation, UpdateEngagedFacing, wheel), `Soldier.UpdateFacing` (visual only), `Soldier.DeathAnim` (fall), visual controllers (animation only). No conflicting writers found — Patch 6 established single ownership. This overhaul adds: stored `DestinationFacing` per order, arrival rotation, defensive-pivot gating.

## Auto-compaction/reform writers
`Formation.UpdateAutoClose` (ordered attrition compaction — will be gated off while firing/fighting), `UpdateRankReplacement` (front-rank promotion — kept), autopilot auto-reform in `UpdateStateMachine` (removed; commander decides), `IssueReform` (rebuilt on fixed rally anchor).

## Phase order and deviations
Phases 0–8 as prompted. Deviations forced by the real codebase:
- New serialized field names for army scale (scene-serialization override trap).
- Broken-banner behavior changes from Patch 7's dominant-cluster tracking to a fixed rally anchor (locked decision in this prompt; cluster code retained for potential future use, no longer drives broken banners).
- `BattlePhase.Pre` becomes the physical deployment phase.
- Composition roles are data (`FormationComposition` on `UnitStats`) spawning the existing prefabs; no officer art.
- Spatial grid (`BattleGrid`) added for 1,280-soldier target scans/separation.

## Parallel work split (subagents on isolated files)
- EnemyCommander rewrite (Phase 7) — `EnemyCommander.cs` only.
- Banner/HUD pass (Phase 5) — `FormationBannerController.cs`, `BattleHUD.cs`.
- Spatial grid (Phase 6F) — new `BattleGrid.cs`.
- Destination previews (Phase 2F) — new `FormationDestinationPreview.cs`.
Core (`Formation.cs`, `Soldier.cs`, `PlayerCommander.cs`, `BattleSetup.cs`, `BattleCamera.cs`) is edited only by the primary session.
