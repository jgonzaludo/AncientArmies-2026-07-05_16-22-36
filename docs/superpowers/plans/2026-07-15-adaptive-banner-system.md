# Adaptive Formation Banner System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Patch 6 procedural pennant with the supplied four-banner artwork, presented adaptively by zoom (Close/Compact/Expanded), with visibility modes, info-hold reveal, health bar + count, state icons, screen-projected facing wedge, and screen-space overlap avoidance — all reading the existing dominant-cluster anchor and facing authority.

**Architecture:** Two new runtime-built classes in the project's no-prefab style: `FormationBannerController` (one per formation, added by `BattleSetup.CreateFormation`, builds a billboarded SpriteRenderer/TextMesh hierarchy, owns per-banner presentation) and `FormationBannerManager` (one per battle, added by `BattleSetup`, owns detail-level computation with hysteresis, visibility mode + persistence, info-hold input, and interval-driven overlap resolution). Simulation stays authoritative: controllers read `Formation` (`DominantGroupCenter`, `AnchorForward`, `IsAutoFacing`, `State`, health/count, `IsSelected`) plus `PlayerCommander.InspectedEnemy` for targeting. Sprites are prepared offline from the supplied PNG by a Python script and loaded via `Resources.Load<Sprite>`.

**Tech Stack:** Unity 6 URP runtime-built visuals (SpriteRenderer + TextMesh + quads, MaterialPropertyBlocks), Python/Pillow for sprite extraction, PlayerPrefs for the visibility setting, static Roslyn compile gate + Unity MCP Play Mode verification.

## Global Constraints

- Preserve the supplied art exactly (gold frame, red/blue interior, white icons, double-notched edge, gold pointer); no restyling, no visible checkerboard.
- Sprite names exactly: `UI_Banner_Red_Melee`, `UI_Banner_Blue_Melee`, `UI_Banner_Red_Ranged`, `UI_Banner_Blue_Ranged`; identical canvas dimensions.
- One logical banner per living formation; never anchored to an individual soldier; no second cluster implementation — read `Formation.DominantGroupCenter`.
- Facing wedge reads `Formation.AnchorForward` only (screen-projected); the baked gold pointer never rotates; wedge hidden while Broken.
- Detail thresholds in **soldier screen pixels** (not raw ortho size), serialized, with hysteresis; transitions blend 0.15–0.25 s via opacity/scale without rebuilding hierarchy.
- Do not touch legionary or archer animations. No morale system exists — per CLAUDE.md it may not be added without approval, so the morale bar is explicitly N/A (documented), not faked.
- No fog of war exists — every living enemy formation is "visible/discovered" by definition (documented).
- No routing state exists in `FormationState` — Broken covers the disrupted case (documented).
- Mobile perf: no per-frame allocations/LINQ/Find*, interval-driven logic, cached references, reused buffers.
- Old label presentation fully gone (Patch 6 already deleted `FormationLabel`); nothing parses display strings — class/faction come from `formation.team` / `formation.stats.isRanged`.

---

### Task 1: Extract the four sprites from the supplied image

**Files:**
- Source: `/Users/josephgonzaludo/Downloads/ChatGPT Image Jul 15, 2026, 01_36_28 PM.png` (copy to `ArtSource/UI/banner_source.png`, local-only)
- Create: `Assets/Art/UI/Banners/Resources/UI_Banner_{Red,Blue}_{Melee,Ranged}.png` (+ Unity metas via import utility)
- Create: `Assets/Editor/BannerSpriteImport.cs` (idempotent import-settings utility, Patch6Integration pattern)
- Scratch: `scratchpad/extract_banners.py`

**Interfaces:**
- Produces: four `Sprite` assets loadable as `Resources.Load<Sprite>("UI_Banner_Red_Melee")` etc., true alpha, equal canvases, pointer included.

- [ ] **Step 1:** Python/Pillow script: split the image into four equal-width panels around the detected banner bounding boxes; remove the checkerboard by exterior flood fill over the two checker grays (sampled from corners, tolerance ~14), reconstructing soft-shadow alpha in the fill band as `alpha = 1 - lum/checkerLum` (color black); interior whites are protected because the fill only walks the exterior. Pad each crop to one shared canvas size (max box + 12 px margin), centered.
- [ ] **Step 2:** Visually verify each output PNG over a magenta test background (Read the composited previews) — no checker remnants, no clipped outline/pointer.
- [ ] **Step 3:** `BannerSpriteImport.cs`: `[InitializeOnLoad]` + `MenuItem("Ancient Armies/Run Banner Sprite Import")` sets, for the four textures: `TextureImporterType.Sprite`, single mode, `alphaIsTransparency=true`, `mipmapEnabled=false`, bilinear, `TextureImporterCompression.Compressed`, then reimports; logs to `Logs/banner_sprite_import.log`; no-ops when already set.
- [ ] **Step 4:** Refresh Unity, confirm log, commit.

### Task 2: Formation order events + banner anchor bias

**Files:**
- Modify: `Assets/Scripts/Formation.cs`

**Interfaces:**
- Produces: `public event System.Action OnOrderIssued;` raised in `IssueMove`, `IssueAttack`, `IssueFace`, `IssueBreakRanks`, `IssueReform` (after validation, before return). Consumed by Task 3 for close-zoom order feedback.

- [ ] Add the event + `OnOrderIssued?.Invoke();` at the end of each successful Issue* body; compile; commit (folded into Task 3's commit if trivial).

### Task 3: FormationBannerController + FormationBannerManager

**Files:**
- Delete: `Assets/Scripts/FormationBanner.cs` (+ meta)
- Create: `Assets/Scripts/FormationBannerController.cs`
- Create: `Assets/Scripts/FormationBannerManager.cs`
- Modify: `Assets/Scripts/BattleSetup.cs` (`CreateFormation` adds `FormationBannerController`; `Awake` ensures one `FormationBannerManager`)

**Interfaces:**
- Manager produces: `FormationBannerDetailLevel { Close, Compact, Expanded }`, `FormationBannerVisibilityMode { Adaptive, AlwaysVisible, SelectedOnly }`, `static FormationBannerManager Instance`, `DetailLevel CurrentLevel`, `bool InfoHeld`, `VisibilityMode Mode` (+ `CycleMode()` persisting to PlayerPrefs key `"FormationBannerVisibilityMode"`), `float SoldierScreenPixels`, and `RegisterBanner/UnregisterBanner(FormationBannerController)`.
- Controller consumes: `Formation` (anchor, cluster, state, facing, health, counts, `IsSelected`), `PlayerCommander.InspectedEnemy`, manager state; builds hierarchy `BannerRoot → [BaseBanner sprite, SelectionOutline sprite, CountText TextMesh, StateIcon quad, HealthBar bg+fill quads, FacingWedge quad]`.
- Manager overlap output: per-banner `screenLift` (vertical world offset) smoothed by controllers.

Key algorithms (implemented exactly):
- **Soldier pixels:** `(cam.WorldToScreenPoint(p + up*1.8) - cam.WorldToScreenPoint(p)).magnitude` at the camera focus point, recomputed every frame (two projections — cheap).
- **Hysteresis:** enter Close ≥ `closeEnterPixels` (32), leave < `closeExitPixels` (28); enter Expanded ≤ `expandedEnterPixels` (12), leave > `expandedExitPixels` (16). Defaults serialized; runtime-tuned after Play Mode measurement so all three levels are reachable within BattleCamera zoom 5.5–36.
- **Visibility resolve (per banner):** hidden if formation dead; else `AlwaysVisible` → show; `SelectedOnly` → selected/targeted/Broken(critical); `Adaptive` → Compact/Expanded always (enemy formations are always "discovered"; documented), Close → selected, targeted (`InspectedEnemy`), Broken, Reforming, order-feedback timer active, or `InfoHeld`.
- **Order feedback:** single `orderFeedbackUntil = Time.unscaledTime + orderFeedbackSeconds` timestamp set from `OnOrderIssued` — no coroutines, no stacking.
- **Detail content:** Close(selected) → base sprite at `closeScale` + selection outline + facing wedge; Compact → + health bar + state icon; Expanded → + count text, `expandedScale`. All blends via cached MPB alpha + localScale lerp over `transitionSeconds` (0.2).
- **Facing wedge:** screen angle from `WorldToScreenPoint(anchor+fwd) - WorldToScreenPoint(anchor)`; wedge is a billboarded triangle orbiting the banner edge; hidden when `State == BrokenRanks`; alpha-muted (×0.6) while `IsAutoFacing`.
- **State icons:** procedural 48px white textures (chevrons=Moving[Ordered+HasMoveDestination or Attacking], crossed lines=Engaged, crack=Broken, inward arrows=Reforming; Ordered=none), static-cached.
- **Anchor:** structured → `AnchorPos - AnchorForward * rearOffset`; Engaged/Broken → `DominantGroupCenter`; smoothed `1-exp(-k dt)`; `+Vector3.up * hoverHeight`; plus smoothed overlap lift.
- **Overlap:** manager every `overlapInterval` (0.15 s): project visible banner anchors to screen, sort by priority (selected 3 > targeted 2 > Broken/critical 1.5 > friendly 1 > enemy 0.5), greedy sweep — if within `overlapPaddingPx` horizontally and vertically of a higher-priority banner, push up one `overlapStepPx`; beyond `maxStack` (3) in one bin → fade lowest priorities to 0.25 alpha. Screen px converted back to world lift via `orthoSize*2/Screen.height`. Reused arrays, no allocations after warmup.
- **Screen-constancy:** banner world scale = `baseScale * (cam.orthographicSize / referenceOrthoSize)` clamped to `[minWorldScale, maxWorldScale]`, so screen size stays readable across zoom without becoming a giant card.

- [ ] Write both classes with all serialized tuning (§21 list; bar dims, scales, thresholds, durations, offsets, overlap values, count font size, selected multiplier/sorting).
- [ ] Wire `BattleSetup` (manager + controller per formation), compile, commit.

### Task 4: HUD settings button + info-hold input

**Files:**
- Modify: `Assets/Scripts/BattleHUD.cs` (small top-left cycle button `BANNERS: ADAPTIVE/ALWAYS/SELECTED` below the hint text)
- Modify: `Assets/Scripts/FormationBannerManager.cs` (poll `Keyboard.current.iKey.isPressed` as the `ShowBattleInformation` hold — same direct-device pattern PlayerCommander uses; `SetInfoHeld(bool)` public for a future mobile button)

- [ ] Add button + label refresh; persistence round-trip via PlayerPrefs; compile; commit.

### Task 5: Play Mode verification, tuning, docs

- [ ] Scripted sweep via Unity MCP: correct sprite per faction/class (4 combos), zoom through all three levels (assert hysteresis no-flicker by oscillating ortho size near a threshold), close-zoom hiding + selected visible + order feedback, 40/10 split banner ownership + reform recall (reuse Patch 6 staging), facing wedge angle vs `AnchorForward` under manual rotate/auto-face/broken/reform, overlap lift with stacked formations, restart → no duplicates, console clean.
- [ ] Screenshot evidence at each zoom level; tune thresholds/scales to make all levels reachable and readable at 1080p.
- [ ] Patch doc `docs/patches/PATCH_7_ADAPTIVE_BANNERS.md` + manual test suite; graphify update; final commit.

## Self-Review

- §1 art → Task 1; §2 core → Task 3 (one logical banner, no soldier anchoring); §3 zoom/hysteresis/transitions → Task 3 manager; §4–6 levels → Task 3 controller content table; §7 modes + persistence → Tasks 3/4; §8 info-hold → Task 4; §9 hierarchy/data-driven sprite map (dictionary keyed by (team,isRanged), extensible) → Task 3; §10 health bar → Task 3 (morale N/A per constraints); §11 icons → Task 3; §12 facing → Task 3 wedge; §13 positioning/reform → existing Patch 6 systems + rear bias; §14 presentation → billboarded world-space, no colliders/raycasts; §15 overlap → manager; §16 selection → outline + scale + sorting; §17 broken/reforming (routing N/A) → controller states; §18 order feedback → Task 2 event + timer; §19 architecture → controller/manager split; §20 perf constraints → design above; §21 tuning → serialized fields; §22 migration → already done in Patch 6 (verify no leftovers); §23–24 → Task 5.
- No morale/fog/routing/centurion systems exist: documented as N/A rather than silently faked (CLAUDE.md forbids expanding scope).
