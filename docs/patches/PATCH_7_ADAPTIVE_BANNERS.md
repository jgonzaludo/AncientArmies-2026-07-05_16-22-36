# Patch 7 — Adaptive Formation Banner System

**Branch:** `v1-prototype` (after Patch 6)
**Date:** 2026-07-15

Replaces the Patch 6 procedural pennant with the supplied four-badge artwork,
presented adaptively by zoom, selection, and formation state. Simulation stays
authoritative: banners read `Formation` (anchor, `DominantGroupCenter`,
`AnchorForward`, state, health, counts, selection) and never own logic.

## Art

`ArtSource/UI/banner_source.png` (local-only) → four true-alpha sprites in
`Assets/Art/UI/Banners/Resources/`:

`UI_Banner_Red_Melee`, `UI_Banner_Blue_Melee`, `UI_Banner_Red_Ranged`,
`UI_Banner_Blue_Ranged` — equal 281×473 canvases.

Extraction (`scratchpad/extract_banners.py`, Pillow): the checkerboard is baked
into the pixels, so removal is an exterior flood fill over the two sampled
checker grays (254/245), with the soft drop shadow reconstructed as true alpha
(`a = 1 − lum/bgLum`, black) via a second flood over grayish-darker border
pixels. Interior whites (sword/bow icons) are safe because only the exterior
region is walked. Preview verified over magenta: no checker remnants, pointers
and outlines intact.

Import settings (`BannerSpriteImport.cs`, idempotent, menu
`Ancient Armies/Run Banner Sprite Import`): Sprite (2D and UI), Single,
alpha-is-transparency, no mipmaps, Bilinear, Compressed, 100 ppu.

## Architecture

- **`FormationBannerManager`** (one, on the BattleSetup object): adaptive
  detail level from **soldier screen-pixel height** measured at the camera's
  center-screen ground focus (resolution/aspect independent) with hysteresis
  (Close ≥70 px enter / <62 exit; Expanded ≤34 enter / >40 exit). The spec's
  30/14 px starting points assume soldiers can shrink below 14 px, but this
  camera's zoom range (BattleCamera 5.5–36 ortho) only spans ~28–186 soldier
  px at 1080p, so the bands were recalibrated to make all three levels
  reachable with the default view (ortho 26 ≈ 39 px) landing in Compact —
  measured live in Play Mode, not guessed; persisted visibility
  mode (`Adaptive/AlwaysVisible/SelectedOnly`, PlayerPrefs
  `FormationBannerVisibilityMode`); `ShowBattleInformation` hold (I key via
  the Input System keyboard device, plus `SetInfoHeld(bool)` for a future
  mobile button); greedy screen-space overlap resolution every 0.15 s
  (priority: selected 3 > targeted 2 > broken 1.5 > friendly 1 > enemy 0.5;
  vertical lift steps; >3 stacked → extras fade to 25%).
- **`FormationBannerController`** (one per formation, added by
  `CreateFormation`, replaces `FormationBanner`): builds the hierarchy once
  (badge sprite via a data-driven `(Team, isRanged)` map — extensible for
  future classes, never parsed from display strings; selection outline;
  health bar; procedural state icons; Expanded soldier count TextMesh;
  facing wedge) and blends alpha/scale over 0.2 s on any change. Anchors:
  structured → `AnchorPos − AnchorForward·rearOffset` (front line stays
  clear); Engaged/Broken → `DominantGroupCenter`. Screen-constancy world
  scale `detailScale · clamp(orthoSize/26, 0.45, 1.35)`. No colliders — taps
  pass through.

## Behavior

- **Close** (soldiers ≥ ~30 px): ordinary unselected banners hide. Shown for:
  selected, targeted (`InspectedEnemy`), Broken, Reforming, recent order
  (`Formation.OnOrderIssued` → 1.5 s single-timestamp feedback, no coroutine
  stacking), or while the info key is held. Selected marker is the reduced
  badge + outline + facing wedge only.
- **Compact** (~14–30 px): badge + health bar + state icon + wedge.
- **Expanded** (below ~14–20 px): adds the remaining-soldier count, slightly
  larger scale.
- **State icons** (procedural white glyphs): chevrons = moving/attacking/
  withdrawing, crossed lines = Engaged, cracked frame = Broken, inward arrows
  = Reforming, none = idle Ordered.
- **Facing wedge**: rotates to the screen-projected `AnchorForward` (the same
  authority as soldiers, ground arrow, and directional damage), orbiting the
  badge; hidden while Broken; alpha-muted 0.6 while `IsAutoFacing`. The baked
  gold pointer in the artwork never rotates — it marks the formation anchor.
- **HUD**: `BANNERS: ADAPTIVE/ALWAYS/SELECTED` cycle button (top-left).

## Explicit N/A (scope honesty)

- **Morale bar**: no morale system exists and CLAUDE.md forbids adding one
  without approval — the bar slot is not faked. Add it when morale ships.
- **Fog of war / discovery**: none exists; every living enemy formation is
  "visible" by definition, so banners cannot leak hidden information.
- **Routing**: not a `FormationState`; Broken covers disrupted presentation.
- **Centurion/leader cluster tiebreak**: no leader entity exists; hysteresis
  (1.3×) + largest-cluster ordering covers near-even splits.

## Serialized tuning

Manager: close enter/exit px, expanded enter/exit px, overlap interval,
padding X/Y px, step px, max stack. Controller: hover height, rear offset,
follow smoothing, transition seconds, close/compact/expanded scales,
reference ortho size, zoom-compensation clamps, selected scale multiplier,
selected sorting boost, order-feedback seconds, bar width/height, wedge size,
count character size.

## Files

- Added: `FormationBannerController.cs`, `FormationBannerManager.cs`,
  `Editor/BannerSpriteImport.cs`, 4 sprites under
  `Assets/Art/UI/Banners/Resources/`
- Modified: `Formation.cs` (`OnOrderIssued`), `BattleSetup.cs`,
  `BattleHUD.cs`
- Deleted: `FormationBanner.cs` (Patch 6 procedural pennant)

## Verification (Play Mode, all PASS)

- Static Roslyn compile clean; in-editor compile clean.
- 4 sprites load at 281×473; all 10 formations map to the correct
  faction/class badge (10/10).
- Three levels reachable and hysteretic across the zoom range: ortho 8→Close
  (128 px), 20→Compact (51 px), 26→Compact (39 px, default), 32/36→Expanded
  (32/28 px); no flicker oscillating near a threshold.
- Close: unselected banners hide (10→1 shown after selecting one); selected
  shows the reduced marker with gold outline; archer formations correctly
  bannerless — screenshot `pm7_close_selected.png`.
- Compact: badge + green health bar + state icon (chevron=moving, ×=engaged)
  + wedge — screenshot `pm7_compact.png`.
- Expanded: badge + health bar + remaining count ("30"/"1"/"16") — screenshot
  `pm7_expanded.png`.
- Facing wedge enabled and rotated to the screen-projected `AnchorForward`
  while formed (localRotZ tracked facing); hidden after Break Ranks; broken
  banner itself stays visible.
- Dominant-cluster anchoring: banners sit over 1–2 soldier remnants, never the
  empty midpoint (inherited Patch 6 system).
- Visibility mode cycles Adaptive→AlwaysVisible→SelectedOnly and persists to
  PlayerPrefs (`FormationBannerVisibilityMode`).
- Restart: exactly 10 controllers + 10 banner roots, no duplicates; mode
  restored from PlayerPrefs.
- Console: zero gameplay errors across zoom, selection, break/reform, restart
  (only the benign editor off-screen-render GUI warning).

## Remaining limitations

- Morale bar deferred (no morale system — see N/A above).
- Overlap avoidance is a greedy vertical sweep sized for ~10 formations; if
  formation count grows large, add spatial binning (noted in the manager).
- Info-hold is bound to the `I` key via the keyboard device; a mobile hold
  button can call `SetInfoHeld(bool)` when the touch layout is finalized.
- The facing wedge is a small procedural triangle; a bespoke arrow sprite
  would read slightly better at Expanded zoom.
