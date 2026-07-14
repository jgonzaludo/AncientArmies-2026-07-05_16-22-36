# Final-Reference Meshy Candidate Review

Date: 2026-07-12
Primary authority: `docs/art/references/full_reference_sheet.png` (1448×1086,
copied unaltered from the upload; original preserved in ~/Downloads).

## Inputs

Geometry views (multi-image-to-3D, `ai_model: latest`, textured, no PBR):
front, left side, back, front-3/4 — cropped directly from the sheet, 3×
Lanczos-resampled (no redraw) to satisfy Meshy's minimum input size after the
native-resolution crops were rejected with HTTP 400. Right-side and back-3/4
crops plus 7 component panels (helmet/armor/shield/sword/clothing/base
body/materials) retained as scoring references — the API accepts max 4 images.

## Candidates (30 credits each; balance 3,005 → 2,885)

| | Task ID | Tris | Raw height |
|---|---|---|---|
| A | `019f5918-1602-7940-8a5c-d1c1f0e46231` | 683,662 | 1.887 m |
| B | `019f5919-c50a-7350-bd63-2a4917fd8ba6`* | 616,016 | 1.887 m |
| C | * | 651,822 | 1.887 m |
| D | * | 595,504 | 1.887 m |

(*full IDs in each candidate's `task.json` under
`ArtSource/Generated/Meshy/FinalReferenceLegionary/meshy_output/`)

Renders: `renders/final_reference_meshy_source/` — `compare_front_ABCD.png` +
per-candidate front/side/back/three-quarter/iso.

## Scores (1–10)

| Criterion | A | B | C | D |
|---|---|---|---|---|
| Overall silhouette | 9 | 9 | 9 | 9 |
| 2.6-head proportion | 9 | 9 | 9 | 9 |
| Head size/shape | 9 | 9 | 8 | 9 |
| Torso width | 9 | 9 | 9 | 9 |
| Arm/leg/hand/foot | 9 | 9 | 9 | 9 |
| Helmet shape (dome+brow band+top fitting) | 8 | **9** | 8 | 8 |
| Cheek guards | 9 | 9 | 8 | 8 |
| Armor volume / segmentata | 9 | 9 | 9 | 9 |
| Shoulder armor | 9 | 9 | 9 | 9 |
| Tunic/pteruges/sandals | 9 | 9 | 8 | 9 |
| Sword | 8 | 8 | 8 | 8 |
| Shield shape/curvature/emblem | 9 | 9 | 9 | 9 |
| Back-view accuracy (X-cross straps) | 8 | **9** | 8 | 7 (extra disc+pouch invented) |
| Three-quarter accuracy | 9 | 9 | 9 | 9 |
| Face simplicity | 9 | 9 | 8 | 9 |
| Isometric readability | 9 | 9 | 9 | 9 |
| Cleanup difficulty (higher=easier) | 8 | 8 | 8 | 8 |
| Rigging suitability | 8 | 8 | 8 | 8 |
| **Total** | 157 | **159** | 153 | 154 |

## Major defects

- A: helmet ear bosses slightly oversized; otherwise clean.
- B: none significant. Slimmest file, no invented details.
- C: brow band rides low, slightly squished face.
- D: **invented** circular disc + pouch on the lower back (not on the sheet);
  helmet slightly tall.

## Selection: **Candidate B**

Best sheet fidelity where the candidates differ: helmet dome/brow-band closest
to the HELMET panel, single front baldric + correct X-cross back straps with
stud columns, zero invented accessories, lowest-but-one triangle count. The
visually best and technically best candidate are the same (B). All four passed
the hard quality gate (correct hands, no crest, no fusions, complete backs).
