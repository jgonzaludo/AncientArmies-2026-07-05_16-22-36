# Camera Close-Zoom Report

Date: 2026-07-13 · Branch: `v1-prototype` · Not committed (working tree only)

## Change

| Field (`Assets/Scripts/BattleCamera.cs`) | Old | New |
|---|---|---|
| `zoomMin` (orthographic size) | 8 | 5.5 (−31.25%) |
| `zoomMax` | 36 | 36 (unchanged) |
| `zoomSmoothTime` | 0.12 | 0.12 (unchanged) |
| `panSmoothTime` / `glideDamping` / `maxGlideSpeed` | 0.08 / 5.5 / 40 | unchanged |
| Pan bounds (`minX/maxX/minZ/maxZ`) | −40/40/−66/8 | unchanged |

Goal: let the player pinch in close enough to appreciate the new soldier art.
At orthographic size 5.5 the visible slice is 11 world units tall — roughly one
formation block fills the screen.

Only `BattleCamera.cs` was edited. The pinch pipeline is untouched:
`PlayerCommander` feeds `ZoomBy(factor)` → `targetZoom = Clamp(targetZoom *
factor, zoomMin, zoomMax)` → `LateUpdate` eases `orthographicSize` via
`SmoothDamp(…, zoomSmoothTime)`. The new floor simply changes the clamp.

## Scene serialization status

`BattleCamera` is **not serialized in `Battle.unity`**. Verified two ways:

- The script's meta GUID (`af1a66321bd4f455195487caed01e94f`) has zero
  occurrences in the scene file.
- The component is added at runtime by `BattleSetup.EnsureEnvironment()`
  (`BattleSetup.cs:208-209` — `AddComponent<BattleCamera>()`), after setting
  the camera's projection, position, and default framing.

So the C# defaults are authoritative and **no scene edit was needed** for this
task (unlike the spacing fields). The scene camera's serialized
`orthographic size: 5` is irrelevant — `EnsureEnvironment` overwrites it to 26
in `Awake` before `BattleCamera.Awake` captures `targetZoom`, and 26 sits
comfortably inside the new [5.5, 36] clamp.

## Clipping analysis (static, no Play Mode)

Camera rig: fixed at y = 42, pitch 55° down, orthographic, near clip 0.3,
far clip 200 (`BattleSetup.EnsureEnvironment`).

- Orthographic zoom changes the frustum's half-height only — the **camera
  never moves when zooming**, so zooming in cannot push geometry across the
  near plane.
- Distance from the camera to the ground along the view axis is
  42 / sin(55°) ≈ 51.3 units; the tallest battlefield geometry (soldiers,
  ~2 units) is still ≥ ~48 units from the camera along the view axis —
  vastly beyond the 0.3 near plane. **No near-clip change needed.**
- Far plane: the farthest ground corner from the camera position
  (±60, 0, +40 on the 120×80 field) is ≈ 101 units away — well inside 200.
- Pan bounds are position bounds, not view bounds, so they are unaffected by
  zoom level; at min zoom the player can frame any point the old bounds
  allowed, just tighter.

## What was NOT changed

- Camera angle (55°), height, projection, or position logic — no rotation, no
  Cinemachine.
- `zoomMax` (36, the documented wide framing), zoom smoothing, pan feel, glide.
- `PlayerCommander.cs` (pinch gesture source) — read-only, untouched.
- Near/far clip planes (analysis above shows no need).

## Manual tests (Play Mode, not performed here)

1. **Pinch to both extremes**: pinch in until it stops (size 5.5) and out until
   it stops (36); motion should ease smoothly with no snap at either clamp.
2. **Clipping at min zoom**: at full pinch-in over a melee scrum, confirm no
   soldier heads/weapons vanish (near-plane) and ground renders everywhere.
3. **Selection and drag while zoomed in**: tap-select, move-drag, and
   attack-drag a formation at min zoom — screen-space touch slop should feel
   more forgiving, not less (world-units-per-pixel shrinks).
4. **UI safe area**: HUD buttons/labels should be unaffected (screen-space UI),
   but confirm formation labels remain legible and don't overwhelm the frame at
   min zoom.
5. **Pan bounds at min zoom**: pan to all four edges while fully zoomed in —
   confirm you can still reach both army lines and nothing feels walled off.
6. **Release glide**: flick-pan at min zoom — glide should settle naturally
   (unchanged damping, now covering more screen distance per world unit).
