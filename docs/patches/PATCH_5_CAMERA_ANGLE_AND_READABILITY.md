# Patch 5 — Camera Angle and Battlefield Readability

**Branch:** `v1-prototype` (after Patch 4 `00ac745`) · **Date:** 2026-07-15
**Play Mode:** not used.

## Previous camera (measured before changing)

- Runtime-authoritative setup in `BattleSetup.EnsureEnvironment` (scene camera
  values are overwritten every load): position `(0, 42, −30)`, rotation
  Euler `(55, 0, 0)` → ground-relative viewing depression **55°**, yaw 0,
  roll 0, orthographic, size 26 default, zoom 5.5–36 (`BattleCamera`),
  near 0.3 / far 200. Center-screen ground focus: `z = −0.59` (ray math).
- Pan: `BattleCamera` clamped the CAMERA position to hardcoded
  `x ∈ [−40, 40], z ∈ [−66, 8]` (tuned for the 55° offset of 29.4 m).
- Screen-to-ground input: ray-plane (`GroundPoint`) and a pitch-aware pan
  delta (`ScreenDeltaToGroundDelta` divides by the live `sin(tilt)`), both
  angle-agnostic. Labels billboard to `Camera.main` each frame.
- URP shadow distance: 50 (both RP assets) — far units already lost shadows
  at 55° (camera-to-unit up to ~77 m).

## Changes

- **Pitch 55° → 42°** (13° lower; inside the 35–50° target band). Position
  recomputed to preserve the exact center-screen ground focus:
  `pos = focus − forward · (height / sin(pitch))` → `(0, 42, −47.24)`.
  Yaw/roll unchanged (0/0), orthographic and fixed-angle preserved, zoom
  limits untouched (5.5–36), orthographic size untouched, near/far
  untouched (max scene distance ~113 m < 200 far plane).
- **Pan bounds are now pitch-aware:** `BattleCamera` derives its camera-
  position clamps once in `Awake` from the actual pitch/height so the
  **ground focus** is what gets clamped (`focusHalfX 40 / focusHalfZ 38`
  serialized; camera z clamp becomes `[−38−h/tanθ, 38−h/tanθ]` ≈
  `[−84.6, −8.6]` at 42°). No per-frame recalculation; battlefield edges
  reachable at every zoom; behavior identical in both scenes (the rig is
  runtime-added after the camera transform is set).
- **Shadow distance 50 → 90** on `PC_RPAsset` and `Mobile_RPAsset` so units
  keep shadows at the new (longer) camera distances. Only rendering-distance
  change; no new post-processing, no cascades change.
- **No input changes required:** taps, drags, Rotate previews, and command
  lines all flow through ray-plane ground intersection or the pitch-aware
  pan conversion (verified by code inspection). **No label/indicator changes
  required:** labels billboard dynamically; selection discs and facing
  arrows are flat ground geometry unaffected by the viewing angle.

## Composition evidence

`docs/art/reviews/renders/patch_5_camera_angle/` — before (55°) and after
(42°) at standard (ortho 12), closest (5.5), and max zoom-out (36), same
ground focus, mixed legionary + archer vignette. At 42° the close view
shows shields, tunics, quivers, bows, and sandals instead of helmet domes;
the max-zoom overview keeps the whole field visible with no horizon
exposure (orthographic top-down oblique cannot expose one at 42°).

## Performance

One trigonometric derivation at Awake; everything else is data changes.
The larger shadow distance is the only rendering-cost change (documented).

## Values (old → new)

| Item | Old | New |
|---|---|---|
| Euler X (pitch) | 55° | 42° |
| Ground depression | 55° | 42° |
| Position | (0, 42, −30) | (0, 42, −47.24) |
| Focus (center-screen) | (0, 0, −0.59) | (0, 0, −0.59) — preserved |
| Ortho size default / min / max | 26 / 5.5 / 36 | unchanged |
| Zoom smoothing | 0.12 | unchanged |
| Pan clamps | camera x±40, z −66..8 (hardcoded) | focus ±40 / ±38, camera clamp derived from pitch |
| Near / far clip | 0.3 / 200 | unchanged |
| Shadow distance | 50 | 90 |
