# Roman Legionary Production Log

End-to-end production log for the basic Roman legionary (Steps 3–16 of the
Roman character pipeline). Names follow
`docs/art/specifications/ROMAN_ASSET_NAMING_STANDARD.md`; materials follow
`docs/art/reviews/ROMAN_MATERIAL_PALETTE.md`.

---

## Preflight (Phase 0)

* **Date:** 2026-07-12
* **Task:** end-to-end basic legionary production (Steps 3–16).

### Git

* Branch: `v1-prototype`.
* Pre-existing untracked dirs `ArtSource/` and `docs/art/` (Step 1–2
  outputs) — **must not be reverted**.
* No modified tracked files at start.
* No commits will be made by this task.

### Blender

* Version: **5.1.2**.
* Source: `ArtSource/Blender/Romans/RomanCharacterSystem_v001.blend`.
* Step 2 scene verified complete: 10 collections, references loaded,
  review cams/lights, scale guides, PIPELINE_NOTES, `90_EXPORT` empty.

### Unity

* Version: **6000.4.8f1**; active scene: Battle
  (`Assets/Scenes/Battle.unity`).
* Key integration facts:
  * Soldier forward is **+Z**; root scale 1.
  * Capsule: radius 0.35, height 1.8, center (0, 1, 0).
  * Melee formation spacing 0.95 m; archers 1.7 m.
  * ~400–500 soldiers per battle.
  * Runtime camera: orthographic size 26, Euler (55, 0, 0).
  * `MaterialPropertyBlock` `_BaseColor` used for hit-flash and
    damage-darkening in `Soldier.cs` — shared materials must not be
    instanced per soldier.

### Version plan

`v002_Blockout` → `v003_BaseMesh` → `v004_LegionaryUnrigged` →
`v005_Rigged` → `v006_Animated` → `v007_ExportReady`.

FBX export target:
`Assets/Art/Characters/Romans/Legionary/Models/RomanLegionary_Basic.fbx`.
(Note: this task-specified path deviates from
`ROMAN_ASSET_NAMING_STANDARD.md` §3, which lists
`Assets/Art/Roman/Models/Roman_LegionaryBasic.fbx`; the task-specified
path is used for this run.)

### Resolved assumptions

* Shield emblem = shallow raised geometry with brass material
  (`MAT_Roman_Brass`), removable.
* Fitted-shorts underlayer = material region on the base body
  (`MAT_Roman_ClothNeutral`), not separate geometry.
* Animation rate: 30 FPS.
* Unity **Generic** rig (not Humanoid).
* In-place clips, no root motion.

### Known constraint carried from Step 2 report

Prompt/task truncation note: the integration tail follows
`docs/art/specifications/ROMAN_UNITY_INTEGRATION_SPEC.md` as authority.

### Status

Current phase: **Phase 1 (blockout) in progress on main thread.**

---

## Blockout (Phase 1)

_(pending)_

## Base Mesh (Phase 2)

_(pending)_

## Clothing (Phase 3)

_(pending)_

## Armor (Phase 4)

_(pending)_

## Helmet (Phase 5)

_(pending)_

## Weapons (Phase 6)

_(pending)_

## Materials (Phase 7)

_(pending)_

## Unrigged Assembly (Phase 8)

_(pending)_

## Rig (Phase 9)

_(pending)_

## Skinning (Phase 10)

_(pending)_

## Animation (Phase 11)

_(pending)_

## Export (Phase 12)

_(pending)_

## Unity Import (Phase 13)

_(pending)_

## Integration (Phase 14)

_(pending)_

## Performance (Phase 15)

_(pending)_

---

## RESTART — Meshy multi-image-to-3D pipeline (2026-07-12)

The primitive-built prototype (v002–v004) was **rejected by the user** at the
Phase 9 visual review: proportions were close but surface design, part
integration, and overall polish did not match the reference art (full defect
list in the archived file's `REJECTED_PROTOTYPE_NOTES`).

Production restarted per new direction: **Meshy multi-image-to-3D** generates
the base sculpt from the turnaround crops; Blender MCP cleanup turns the best
candidate into the modular game-ready unrigged legionary. The previous
"no Meshy" constraint from the original brief was explicitly overridden by
the user, who supplied a Meshy API key.

* Rejected prototype archived: `ArtSource/Blender/Romans/Archive/RomanLegionary_PrimitivePrototype_Rejected.blend`
* Clean production file: `ArtSource/Blender/Romans/RomanCharacterSystem_v008_MeshyRestart.blend`
  (Step 2 scene + shared materials retained; primitive model objects removed;
  temp collection `05_MESHY_SOURCE` added)
* Meshy inputs: `docs/art/references/meshy_inputs/legionary_{front,side,back}.png`
  (exact thirds of the turnaround, no redraw/flip; verified sword-right/shield-left)
  * Limitation: side view hides most of the scutum; back view shows only its
    inner edge — shield geometry may need manual rebuild after generation.
* Meshy account: balance 3,100 credits at start; 3 candidates × multi-image-to-3D
  (textured, no PBR) in flight.
* This session: no rigging, no animation, no Unity changes (visual-gate task only).

---

## FINAL-REFERENCE PIPELINE (2026-07-12, evening)

User supplied `full_reference_sheet.png` (complete turnaround + exploded parts +
palette) as the single controlling authority and directed a Meshy-based restart,
including **Meshy auto-rig + library animations** (superseding the earlier
hand-rig plan).

### Generation & selection
- 4 multi-image candidates from front/left/back/front-3q crops (3× Lanczos
  upscaled after HTTP 400 on sub-256px inputs). 30cr each.
- Candidate B selected (159/180; sheet-correct straps, no invented props) —
  `FINAL_REFERENCE_MESHY_CANDIDATE_REVIEW.md`.
- Remesh B → 22,610 tris (5cr). Raw 616k source preserved.

### Rig & animations (Meshy)
- Auto-rig at 1.875 m (5cr): 24-bone Mixamo-style skeleton, skinned fused mesh.
- Library clips (3cr each): 89 Combat_Stance→Idle, 240 Thrust_Slash→SwordAttack,
  178 Hit_Reaction→Hit, 8 Dead→Death, 572 Walk_Turn_Left→Pivot; walking/running
  free with rig → March/Run. All 30 fps.
- Credit total this pipeline: 145cr (balance 2,860).

### Blender assembly (v011_Animated + MeshyFinalReference_Source)
- Rigged GLB imported: RIG_RomanLegionary + GEO_RomanLegionary (22,610 tris).
- Per-face color classification → 6 shared MAT_Roman_* slots (no textures);
  palette revision: Skin (0.95,0.72,0.47), ClothRed (0.60,0.11,0.09).
- 7 Actions transferred (Blender 5 slotted-action API), horizontal root travel
  stripped from March/Run/Pivot (in-place, no root motion).
- Module library: 14 MOD_* parts (mask-separated patch shells, display/source
  only, not exported) + carried-over blank body; components-layout render
  produced. Icosphere placeholder from a Meshy GLB removed.
- FBX exported: `Assets/Art/Characters/Romans/Legionary/Models/RomanLegionary_Basic.fbx`
  (2.5 MB, 7 takes, -Z fwd/Y up, FBX_SCALE_UNITS).

### Unity integration (code)
- `Soldier.cs`: added OnAttack/OnHurt/OnDeath events + suppressFallRotation
  flag (animated visuals own the death pose; overall death timing preserved).
- `SoldierFactory.cs`: melee soldiers instantiate `VIS_Roman_Legionary_Basic`
  (Resources.Load) as a visual child; capsule+weapon primitives remain the
  fallback (archers unchanged; missing prefab → old path). Roman visuals get
  Color.white base tint so hit-flash/damage-darkening work without team recolor.
- `RomanLegionaryVisualController.cs`: presentation-only adapter (Speed float +
  Attack/Hit/Die triggers, CullUpdateTransforms, no root motion).
- Static Roslyn compile of Assembly-CSharp: **exit 0, no errors**.
- Known friction: editor App Nap keeps dropping the MCP bridge (per project
  memory); work batched into responsive windows.
