# Step 2 Blender Scene Setup Report

Date: 2026-07-12
Scope: Blender production-scene setup and pipeline validation only.
No modeling, rigging, animation, export, or Unity modification occurred.

## Completion Status

**PASS** — all 27 validation checks passed; file saved and versioned.

## Files Created or Modified

| Item | Path | Action |
|---|---|---|
| Blender source file | `ArtSource/Blender/Romans/RomanCharacterSystem_v001.blend` | Created (new folder + new file) |
| This report | `docs/art/reviews/STEP_2_BLENDER_SCENE_SETUP_REPORT.md` | Created |
| References README | `docs/art/references/README.md` | Updated (stale "ACTION REQUIRED" note resolved — both PNGs now on disk) |
| `PIPELINE_NOTES` | Text block inside the .blend | Created (113 lines) |

No Unity asset, script, scene, prefab, material, or project setting was modified.

## Unity Inspection

Read-only inspection of the live editor (edit mode) plus static file/code reads.
Labels: **LIVE** = confirmed from running editor via MCP, **SERIALIZED** = from
asset YAML, **CODE** = from C# source.

| Fact | Value | Source |
|---|---|---|
| Unity version | 6000.4.8f1 | `ProjectSettings/ProjectVersion.txt` |
| Active scene | `Battle` (`Assets/Scenes/Battle.unity`, build index 0) | LIVE |
| Camera projection (runtime) | **Orthographic** — forced in `BattleSetup.EnsureEnvironment()` on Awake | CODE `BattleSetup.cs:197-210` |
| Camera projection (serialized) | Perspective, FOV 60, size 5, far 1000 — **dead on arrival**, overridden at runtime | SERIALIZED `Battle.unity` |
| Camera transform | position (0, 42, −30), rotation Euler(55°, 0, 0) | LIVE + CODE (identical) |
| Camera orthographic size | 26 default; `BattleCamera` zoom range **8–36** | CODE `BattleSetup.cs`, `BattleCamera.cs:20-21` |
| Camera near/far (runtime) | 0.3 / 200 | CODE `BattleSetup.cs` |
| Soldier forward axis | **local +Z** (`LookRotation`, `transform.forward`, weapon offset +Z) | CODE `Soldier.cs:176,204,288`, `SoldierFactory.cs:62` |
| World up axis | +Y (gravity (0, −9.81, 0)) | LIVE |
| Soldier root scale | (1, 1, 1) — never scaled | CODE `SoldierFactory.cs` |
| Capsule collider | center (0, 1, 0), radius 0.35, height **1.8 m** | CODE `SoldierFactory.cs:28-32` |
| Placeholder visual | capsule child at y=1, localScale (0.7, 0.85, 0.7) melee / (0.55, 0.85, 0.55) archer → ~1.7 m tall | CODE `SoldierFactory.cs:41-49` |
| Melee formation spacing | **0.95 m** (V1.2 dense spacing) | LIVE + CODE `BattleSetup.cs:50` (field absent from scene YAML → C# default applies) |
| Archer formation spacing | 1.7 m | LIVE + CODE `BattleSetup.cs:51` |
| Battle scale | 5v5 formations; 50 melee / 40 archer per formation → ~400–500 soldiers; battlefield 120 × 80 m | SERIALIZED + CODE |
| Other scale anchors | arrow spawn height 1.3 m, aim height 0.8 m, strike range 1.7 m, personal engage 3 m | CODE |

**Inferred rather than serialized:** runtime camera values (orthographic, size 26,
far 200) and melee/archer spacing come from C# defaults executed at runtime —
they are not in the scene file. Everything labeled LIVE was read from the editor
in edit mode.

**Mismatch note (documented, not fixed):** the current placeholder visual is
~1.7 m tall vs. the intended 1.75 m bare / ~1.85–1.90 m helmeted character, and
the 1.8 m collider is slightly shorter than a helmeted Roman. This is fine: the
gameplay root, collider, and combat radii remain authoritative; the visual model
will be a child and must not redefine gameplay spacing. At 0.95 m melee spacing
the visual footprint (body + shield) must stay within ~1.1–1.2 m diameter as
per the integration spec — but shields at dense spacing will read tighter than
the spec's 1.3 m assumption. Flagged for the first in-engine visual test.

## Blender Configuration

| Setting | Value |
|---|---|
| Blender version | 5.1.2 |
| Unit system | Metric |
| Unit scale | 1.0 (1 BU = 1 m) |
| Length unit | Meters |
| Render engine | EEVEE (`BLENDER_EEVEE`) |
| World background | Neutral gray (0.18) |
| Source file | `ArtSource/Blender/Romans/RomanCharacterSystem_v001.blend` |

File history: Blender was opened on its default startup scene (unsaved
Cube/Camera/Light). No prior `RomanCharacterSystem_*.blend` existed, so v001
was claimed. The file was saved once before changes and again after validation.
Default Cube, Camera, Light, and the empty default "Collection" were removed;
no other data existed in the file.

## Collection Structure

| Collection | Contents | Empty as expected? |
|---|---|---|
| 00_REFERENCE | 11 guide/reference objects | populated (by design) |
| 10_BASE_BODY | — | ✅ empty |
| 20_CLOTHING | — | ✅ empty |
| 30_ARMOR | — | ✅ empty |
| 40_HELMETS | — | ✅ empty |
| 50_WEAPONS_SHIELDS | — | ✅ empty |
| 60_ACCESSORIES | — | ✅ empty |
| 70_RIG | — | ✅ empty |
| 80_REVIEW | 5 cameras + 2 lights | populated (by design) |
| 90_EXPORT | — | ✅ **completely empty** |

No objects sit in the Scene Collection root.

## References

| Image | Path | Pixels | Status |
|---|---|---|---|
| Unit family | `docs/art/references/roman_unit_family_reference.png` | 1448 × 1086 | ✅ loaded as image empty `REF_RomanUnitFamily` (4.0 × 3.0 m display) |
| Legionary turnaround | `docs/art/references/basic_legionary_turnaround.png` | 1774 × 887 | ✅ loaded as image empty `REF_LegionaryTurnaround` (4.6 × 2.3 m display) |

Both are image **empties** (never rendered, never exportable), native aspect
preserved (no stretching/cropping), transforms locked, selection disabled,
front-side display only so back/side review cameras are unobstructed. They sit
at y = +1.5 (behind the modeling origin, since modeling forward is −Y).

Reference limitations for future modeling: the turnaround is an illustration,
not a true orthographic plate — the front/back views are slightly
three-quarter, the sword arm changes angle between views, and the combat pose
(sword + shield) is **not** the modeling pose (relaxed A-pose). Suitable for
approximate proportion matching only; do not distort it to force alignment.

## Scale Guides

All in `00_REFERENCE`, wireframe/empty display, render-hidden, transforms locked.

| Guide | Exact value | Placement |
|---|---|---|
| REF_GroundPlane | 4 × 4 m, surface at Z = 0 | world origin |
| REF_BodyHeight_1_75m | 0 → **1.75 m** (verified 1.75) | x = −0.9 |
| REF_HelmetMaxHeight_1_90m | 0 → **1.90 m** (verified 1.8999999) | x = −1.15 |
| REF_HeadHeightGuide | 0 → **0.673077 m** (= 1.75 / 2.6, verified) | x = −1.4 |
| REF_HumanScaleGuide | wire box 0.60 × 0.35 × 1.75 m (non-character) | x = +1.2 |
| REF_Origin | plain-axes empty at (0, 0, 0) | origin |
| REF_Axis_Forward | arrow along **−Y** (modeling forward, provisional) | origin |
| REF_Axis_Right | arrow along +X | origin |
| REF_Axis_Up | arrow along +Z | origin |

## Cameras

All in `80_REVIEW`, clip 0.05–100.

| Camera | Type | Position | Rotation (XYZ°) | Ortho scale |
|---|---|---|---|---|
| CAM_Review_Front | Ortho | (0, −6, 0.95) | (90, 0, 0) | 3.8 |
| CAM_Review_Side | Ortho | (6, 0, 0.95) | (90, 0, 90) | 3.8 |
| CAM_Review_Back | Ortho | (0, 6, 0.95) | (90, 0, 180) | 3.8 |
| CAM_Review_ThreeQuarter | Ortho | (−4.78, −4.78, 2.71) | (75, 0, −45) | 3.8 |
| CAM_Review_IsometricGame | Ortho | (0, 5.16, 8.27) | (**35, 0, 180**) | **3.2** (single-soldier) |

At 16:9 render aspect, ortho scale 3.8 covers ~2.14 m vertically centered at
z = 0.95 → ground through ~2.0 m visible (> 1.90 m requirement).

**Coordinate conversion for CAM_Review_IsometricGame:** Unity camera
Euler(55°, 0, 0) looks along Unity +Z pitched 55° down. Unity +Z maps to
Blender −Y under the provisional convention, and a Blender camera looks along
its local −Z, so the equivalent Blender rotation is (90° − 55°, 0, 180°) =
(35°, 0, 180°) positioned on +Y looking down at the origin. The **actual**
Unity battle camera uses orthographic size 26 (zoom 8–36) framing a 120 × 80 m
field; the review camera intentionally uses 3.2 to frame one soldier. This
approximation is **not yet validated** — it must be confirmed with an exported
test object during the first FBX round-trip.

## Lighting

| Light | Type | Rotation (XYZ°) | Energy | Color |
|---|---|---|---|---|
| LIGHT_Review_Key | Sun | (50, 0, −40) — from front-left, above | 3.0 | (1.0, 0.98, 0.95) slightly warm |
| LIGHT_Review_Fill | Sun | (50, 0, 50) — from front-right, above | 1.0 | neutral white |

No rim light (not needed yet), no bloom/DoF/fog/atmosphere, neutral 0.18 gray
world. EEVEE real-time engine. Inspection-quality only.

## Axis and Origin Convention

| Convention | Value | Status |
|---|---|---|
| Blender up | +Z | firm |
| Blender modeling forward | −Y | **provisional** |
| Blender character right | +X | provisional |
| Unity target forward | +Z (soldier root) | confirmed from gameplay code |
| Unity target up | +Y | confirmed |
| Character origin | between feet, ground level, Z = 0 | firm |
| FBX conversion | −Y→+Z forward, +Z→+Y up | **unvalidated — must be tested with a probe object before rigging** |

## Validation Results

| # | Check | Result |
|---|---|---|
| 1 | File versioned in `ArtSource/Blender/Romans/` | ✅ PASS |
| 2 | Unit system Metric | ✅ PASS |
| 3 | Unit scale 1.0 | ✅ PASS |
| 4 | Ground at Z = 0 | ✅ PASS |
| 5 | Bare-head guide exactly 1.75 m | ✅ PASS |
| 6 | Helmet guide exactly 1.90 m | ✅ PASS (float32 1.8999999) |
| 7 | Head guide = 1.75 / 2.6 m | ✅ PASS (0.6730769) |
| 8 | All ten collections, exact names, exact order | ✅ PASS |
| 9 | All objects inside intended collections (none in root) | ✅ PASS |
| 10 | No meaningless default names | ✅ PASS |
| 11 | Reference images undistorted | ✅ PASS |
| 12 | Reference objects hidden from render | ✅ PASS |
| 13 | Reference objects excluded from export | ✅ PASS (no export preset exists; 00_REFERENCE is non-export by convention) |
| 14 | Five review cameras exist, aimed correctly | ✅ PASS |
| 15 | Review lighting neutral | ✅ PASS |
| 16 | PIPELINE_NOTES complete | ✅ PASS (113 lines) |
| 17 | 10_BASE_BODY empty | ✅ PASS |
| 18 | 20_CLOTHING empty | ✅ PASS |
| 19 | 30_ARMOR empty | ✅ PASS |
| 20 | 40_HELMETS empty | ✅ PASS |
| 21 | 50_WEAPONS_SHIELDS empty | ✅ PASS |
| 22 | 60_ACCESSORIES empty | ✅ PASS |
| 23 | 70_RIG empty | ✅ PASS |
| 24 | 90_EXPORT completely empty | ✅ PASS |
| 25 | No body/armor/clothing/weapon/shield/rig/animation created | ✅ PASS |
| 26 | Unity project assets unchanged | ✅ PASS |
| 27 | Unity never entered Play Mode | ✅ PASS |

Visual verification: viewport capture confirmed both references display
upright at correct aspect, guides cluster at origin, five camera frustums and
two sun lamps placed as specified. (A final *render* is intentionally blank —
every current object is render-hidden by design.)

## Issues and Warnings

**Blocking problems:** none.

**Non-blocking warnings:**

1. **Collection naming diverges from `ROMAN_ASSET_NAMING_STANDARD.md`.** The
   naming standard describes a `ROMAN/` → `ROMAN_Base/`… hierarchy; this task
   mandated the numbered `00_REFERENCE` … `90_EXPORT` structure, which now
   supersedes it in the working file. The naming standard should be amended to
   record the numbered top-level structure (module-level `BODY_/CLOTH_/…`
   object prefixes are unaffected). Until amended, treat the numbered layout
   as authoritative.
2. **Blender MCP screenshot tools failed** (bridge JSON serialization error in
   `get_screenshot_of_*`); worked around via OpenGL viewport render. Cosmetic,
   but worth knowing for future review-capture automation.
3. **Placeholder vs. spec height mismatch** (1.7 m visual / 1.8 m collider vs.
   1.75–1.90 m character) — documented above; no Unity change made or needed.
4. **Dense melee spacing (0.95 m) vs. integration-spec footprint assumption
   (1.3 m spacing)** — shield overlap at dense spacing must be evaluated at
   the first in-engine visual test.
5. Blender writes `RomanCharacterSystem_v001.blend1` backup files in the same
   folder — harmless; consider gitignoring `*.blend1`.

**Future export decisions (deliberately open):**

- FBX axis conversion (−Y→+Z forward) unvalidated; test with a probe object.
- No FBX export preset finalized; `90_EXPORT` empty.
- Whether CAM_Review_IsometricGame truly matches the game view must be
  verified after the first export round-trip.

**Missing reference files:** none — both references present and loaded.

**Unity values that could not be confirmed live:** runtime camera state and
V1.2 spacing fields are C#-default-driven (not serialized); they were
confirmed by code reading plus live component inspection in edit mode, but the
actual play-mode values were not observed because Play Mode was off-limits.

## Confirmation

- ✅ No character modeling occurred (no body, blockout, clothing, armor, weapon, shield, or prop geometry exists).
- ✅ No rigging occurred (no armatures in the file).
- ✅ No animation occurred (no actions in the file).
- ✅ No FBX export occurred (no preset, `90_EXPORT` empty).
- ✅ No Unity gameplay asset was modified (inspection was read-only).
- ✅ Unity Play Mode was never used (editor remained in edit mode throughout).
