# Roman Legionary End-to-End Report

Date: 2026-07-12. Covers the full basic-legionary pipeline: reference →
Meshy generation → Blender cleanup/rig/animation transfer → FBX export →
Unity import → prefab → code integration. Detailed step logs live in
`ROMAN_LEGIONARY_PRODUCTION_LOG.md` and the per-phase reports in this
directory.

## Completion Status

****COMPLETE** — integrated, battle-tested, user-feedback pass applied.**

## References Used

- `docs/art/references/full_reference_sheet.png` — **single controlling
  authority** (complete turnaround + exploded part panels + palette).
- `docs/art/references/basic_legionary_turnaround.png` — earlier turnaround
  (source of the first Meshy input crops).
- `docs/art/references/roman_unit_family_reference.png` — Step 1 concept
  reference (original palette sampling source).

## Blender Source Files

All under `ArtSource/Blender/Romans/`:

- `RomanCharacterSystem_v001.blend` — Step 2 pipeline scene (collections,
  review cams/lights, scale guides, shared materials).
- `RomanCharacterSystem_v002_Blockout.blend` — primitive-pipeline blockout.
- `RomanCharacterSystem_v003_BaseMesh.blend` — primitive-pipeline base mesh.
- `RomanCharacterSystem_v004_LegionaryUnrigged.blend` — primitive-pipeline
  unrigged assembly (rejected at visual review).
- `Archive/RomanLegionary_PrimitivePrototype_Rejected.blend` — archived
  rejected primitive prototype, with `REJECTED_PROTOTYPE_NOTES` defect list.
- `RomanCharacterSystem_v008_MeshyRestart.blend` — clean restart file for the
  first Meshy pass (Step 2 scene + shared materials, `05_MESHY_SOURCE` added).
- `RomanCharacterSystem_v009_MeshyCleanedUnrigged.blend` — cleaned unrigged
  legionary from the first Meshy candidate.
- `Archive/RomanLegionary_BadModularization_Archive.blend` — archived failed
  modularization attempt.
- `RomanCharacterSystem_v010_ModularRepair.blend` — repaired MOD_* module
  library (authoritative copies of the repaired modules).
- `RomanCharacterSystem_MeshyFinalReference_Source.blend` — imported
  final-reference Candidate B raw sculpt, positioned/scaled (feet at Z=0,
  facing −Y, 1.875 m).
- `RomanCharacterSystem_v011_Animated.blend` — final production file: rigged,
  skinned, animated, material-classified; FBX export source.

## Final Asset Summary

- Meshy final-reference Candidate B, remeshed to a single fused skinned mesh:
  **22,610 tris**.
- **6 shared flat `MAT_Roman_*` materials** on the mesh (no textures; per-face
  color classification replaced the baked Meshy texture).
- **24-bone Mixamo-style rig** from the Meshy auto-rig service.
- Sword, shield, helmet, and armor weights hardened to rigid (single-bone)
  skinning so props do not deform.
- **MOD_* module library** (14 mask-separated patch shells) plus the blank
  body carried over in the Blender source for future unit variants —
  display/source only, not exported.

## Dimensions

- Helmeted height: **1.875 m** (scale 0.9872 applied to raw 1.887 m).
- Feet at Z=0, centered at X=0.
- Faces **−Y in Blender**, which exports to **+Z forward in Unity** (matches
  `Soldier` forward convention).

## Geometry Budget

- Final: **22,610 tris** vs. the provisional **~6.5k budget** — a documented
  deviation. The visual quality gate failed at 6.5k (silhouette and part
  definition collapsed), so the higher count was accepted.
- Mobile impact **pending device profiling** (see Performance); revisit with
  a further remesh or LODs only if profiling demands it.

## Rig

- 24 deform bones, Mixamo-style hierarchy, from Meshy auto-rig (5 credits).
- **No attachment sockets.** Sword and shield are skinned rigidly to the hand
  bones rather than parented to `ATTACH_*` empties. This deviates from the
  original hand-rig plan (`ATTACH_*` socket scheme in the naming standard),
  which was superseded by the user-directed Meshy auto-rig + library-animation
  pipeline.

## Materials

Eight shared materials per `ROMAN_MATERIAL_PALETTE.md` (authoritative for
Blender and Unity); the fused mesh uses six of them. Flat colors only, no
textures, shared assets (no per-soldier instances — required by the
`MaterialPropertyBlock` hit-flash/damage-darkening in `Soldier.cs`).

Revision 2026-07-12: `MAT_Roman_Skin` warmed to (0.95, 0.72, 0.47) and
`MAT_Roman_ClothRed` deepened to (0.60, 0.11, 0.09) to match
`full_reference_sheet.png`; Blender and Unity updated in the same pass.

## Animations

All clips: Meshy library actions, 30 fps, in-place (horizontal root travel
stripped from March/Run/Pivot), no root motion.

| Clip | Length | Wrap | Playback | Meshy source action |
|---|---|---|---|---|
| ANIM_Roman_Idle | 1.33 s | loop | 1x | 89 Combat_Stance |
| ANIM_Roman_March | 0.80 s | loop | 1x | walking (free with rig) |
| ANIM_Roman_SwordAttack | 2.40 s | once | **2x** | 240 Thrust_Slash |
| ANIM_Roman_Hit | 1.30 s | once | **2.5x** | 178 Hit_Reaction |
| ANIM_Roman_Death | 2.37 s | once | 1x | 8 Dead |
| ANIM_Roman_Pivot | 1.60 s | loop | — (**unwired by design**) | 572 Walk_Turn_Left |
| ANIM_Roman_Run | 0.50 s | loop | — (spare) | running (free with rig) |

## FBX Export

- Path: `Assets/Art/Characters/Romans/Legionary/Models/RomanLegionary_Basic.fbx`
  (2.5 MB, 7 takes).
- Settings: **−Z forward, Y up, FBX_SCALE_UNITS**, all actions baked.

## Unity Import

- Rig: **Generic** (not Humanoid), no root motion.
- Clips configured per the table above (Idle/March loop; SwordAttack/Hit/Death
  play once; Pivot/Run present but unwired).
- Materials remapped by name to the existing shared `MAT_Roman_*.mat` assets
  (mechanical remap — identical names on both sides).

## Prefabs

- `VIS_Roman_Legionary_Basic` — visual prefab in a `Prefabs/Resources` folder
  (loaded via `Resources.Load`).
- `AC_RomanInfantry` — Animator Controller (Speed float; Attack/Hit/Die
  triggers).

## Code Changes

- `Assets/Scripts/Soldier.cs` — added `OnAttack`/`OnHurt`/`OnDeath` events and
  a `suppressFallRotation` flag so an animated visual owns the death pose;
  overall death timing unchanged.
- `Assets/Scripts/SoldierFactory.cs` — melee soldiers instantiate
  `VIS_Roman_Legionary_Basic` as a visual child; the capsule+weapon primitive
  path remains as fallback (archers unchanged; missing/renamed prefab falls
  back cleanly). Roman visuals get a `Color.white` base tint so hit-flash and
  damage-darkening work against the shared materials without team recolor
  (Romans are never team-recolored).
- `Assets/Scripts/RomanLegionaryVisualController.cs` — new presentation-only
  adapter: drives Speed float + Attack/Hit/Die triggers,
  `CullUpdateTransforms` culling, no root motion, no gameplay logic.
- Static Roslyn compile of Assembly-CSharp: exit 0, no errors.

## Gameplay Validation

Tested 2026-07-12 in play mode on the real Battle scene (no balance/AI edits during the test):

- **Spawn**: 250 melee soldiers received the Roman visual (RomanLegionaryVisualController instances = 250 = all melee); archers remained capsules by design.
- **Fallback**: `Resources.Load` path verified; renaming the prefab away restores capsules (see manual checklist).
- **Combat**: battle ran to late game (450 → 61 alive); targeting, movement, hit flash, damage darkening, death removal, selection, and labels all functioned with the new visuals.
- **Death**: animator death clip plays; procedural fall-rotation correctly suppressed; corpses sink and despawn on the existing timing.
- Screenshots: `renders/unity_integration/battle_test.png` (formations pre-battle) and `battle_combat.png` (melee engaged).

Post-test polish applied after user review: left-arm (shield) swing damped ~65% in March/Run and ~50% in Idle; March playback 0.85×; melee `moveSpeed` 3.2 → 2.7 (user-directed; serialized in Battle.unity — this save also baked `meleeSpacing: 0.95` / `archerSpacing: 1.7` at their existing code-default values, no behavioral change).

## Performance

Editor desktop (M-series, not mobile — provisional):

- Pre-battle, 250 animated SkinnedMeshRenderers + 200 capsule archers: **~72 FPS**.
- Mid-combat at 113 alive: ~283 FPS; at 61 alive: ~460 FPS.
- Console: zero gameplay errors (only benign editor/Metal notices).
- No LOD/optimization work performed (per policy: evidence first). Mobile profiling remains the open gate for the 22.6k-tri budget deviation; first lever if needed is shadows, then animator culling already set to CullUpdateTransforms.

## Known Issues

- Pivot clip imported but **unwired by design** (pivot-in-place currently
  reads fine without a dedicated clip).
- Single fused skinned mesh: **no per-module runtime swaps** — the MOD_*
  module library lives only in the Blender source for future variants.
- Back-skirt (pteruges) detail reads busy at extreme closeup; fine at gameplay
  camera distance.
- Death clip is 2.40 s vs. the ~2.35 s gameplay removal window — slight
  overlap, accepted as visually negligible.
- Helmeted height 1.875 m vs. the 1.8 m gameplay capsule — documented
  mismatch; the **collider is authoritative**, the visual is slightly taller.
- Meshy credits consumed: **~265 total** across both Meshy pipelines
  (candidates, remesh, auto-rig, library animations).

## Scope Confirmation

- No other units produced (basic legionary only).
- No enemy factions touched.
- No balance, AI, or formation-behavior changes.
- No LOD work.
- No commit/push made by this task.
