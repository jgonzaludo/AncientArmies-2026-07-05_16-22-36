# Meshy Legionary Candidate Review

Date: 2026-07-12
Generator: Meshy multi-image-to-3D (`ai_model: latest` / Meshy-6), textured, no PBR.
Inputs: `docs/art/references/meshy_inputs/legionary_{front,side,back}.png` — exact
thirds of the authoritative turnaround, no redraws, no flips (verified sword-right /
shield-left before generation). Style prompt embedded as texture prompt.
Cost: 30 credits per candidate (90 total). Balance after: 3,010.

## Candidates

| | Task ID | Tris | Raw height | File |
|---|---|---|---|---|
| A | `019f58c8-f60c-78ac-a6c5-3684044286cb` | 679,214 | 1.899 m | `ArtSource/Generated/Meshy/RomanLegionary/meshy_output/20260712_200633_roman-legionary_candA_019f58c8/Meshy_RomanLegionary_Candidate_A.glb` |
| B | `019f58c9-1277-7451-a45e-15cf2a0159cf` | 713,740 | 1.899 m | `.../candB_019f58c9/Meshy_RomanLegionary_Candidate_B.glb` |
| C | `019f58c9-2891-7452-8b43-33d56a0ac82e` | 712,610 | 1.899 m | `.../candC_019f58c9/Meshy_RomanLegionary_Candidate_C.glb` |

Screenshots: `docs/art/reviews/renders/meshy_candidates/` (per-candidate
front/side/three-quarter/iso + `compare_front_ABC.png`).

## Scoring (1–10)

| Criterion | A | B | C |
|---|---|---|---|
| Overall silhouette | 9 | 9 | 9 |
| Head size & shape | 9 | 9 | 9 |
| Body proportions (≈2.6 heads) | 9 | 9 | 9 |
| Torso width | 9 | 9 | 8 |
| Leg length | 9 | 9 | 9 |
| Arm thickness | 8 | 9 | 8 |
| Helmet shape | 9 | 8 | 8 |
| Armor shape | 9 | 8 | 8 |
| Face simplicity | 9 | 9 | 7 |
| Sword shape | 8 | 8 | 8 |
| Shield shape | 9 | 8 | 9 |
| Side-view depth | 9 | 9 | 9 |
| Back-view completeness | 9 | 9 | 9 |
| Style match (front strap layout) | **9** | 7 | 7 |
| Cleanup difficulty (lower = better score) | 8 | 8 | 8 |
| Modularization difficulty | 8 | 8 | 8 |
| Rigging suitability | 8 | 8 | 8 |
| Gameplay-camera readability | 9 | 9 | 9 |
| **Total** | **157** | **153** | **150** |

## Defects noted

- **A**: sword slightly slim at the guard; scabbard strap hangs down the center
  of the back skirt (minor oddity, hidden at game angle).
- **B**: chest straps form an X on the FRONT — the reference reserves the X-cross
  for the back view and shows a single baldric on the front; pteruges leather
  tone drifts browner than the reference red.
- **C**: same front X-cross issue; glossier, more plasticky highlights; small
  "o" mouth where the reference has a neutral frown; cheek guards read like
  hair curtains.

All three: single fused ~700k-tri mesh with one baked texture — modular
separation and retopology required regardless of choice (expected).

## Selection: **Candidate A**

Chosen for the reference-correct single-baldric front, cleanest helmet and
cheek-guard forms, matte finish closest to the family reference's polish, and
lowest triangle count. Geometry/silhouette quality is equivalent across
candidates, so the style-accuracy differences decide it. Selected on geometry
and silhouette, not texture quality — the baked texture will be replaced by
shared flat materials during cleanup.

## Orientation check

Imported candidates already face Blender −Y (documented modeling forward),
stand on Z=0 after regrounding, and scale cleanly to 1.875 m helmeted
(scale factor 0.9872 from raw 1.899 m).
