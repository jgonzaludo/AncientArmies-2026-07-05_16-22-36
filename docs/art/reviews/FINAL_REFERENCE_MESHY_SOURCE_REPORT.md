# Final-Reference Meshy Source Report

Date: 2026-07-12

- **Source reference:** `docs/art/references/full_reference_sheet.png` (single controlling authority)
- **Generation:** Meshy multi-image-to-3D, `ai_model: latest`, `should_texture: true`, `enable_pbr: false`, no rigging/animation/environment. 4 candidates, 120 credits total (balance 2,885).
- **Selected:** Candidate B — see `FINAL_REFERENCE_MESHY_CANDIDATE_REVIEW.md` (score 159/180; correct back straps; no invented props).
- **Raw asset:** `ArtSource/Generated/Meshy/FinalReferenceLegionary/meshy_output/candB_019f5918/Meshy_FinalReference_Legionary_B.glb` (19.6 MB, untouched; A/C/D also preserved)
- **Blender source file:** `ArtSource/Blender/Romans/RomanCharacterSystem_MeshyFinalReference_Source.blend`
  - `SRC_Meshy_FinalReference_Legionary_B` in `05_MESHY_SOURCE`, feet at Z=0, centered X=0, facing −Y, helmeted height **1.875 m** (scale 0.9872 from raw 1.887)
  - v010 repaired modules remain in file, hidden (kept per no-deletion rule; authoritative copies in v010)
- **Raw stats:** 616,016 tris, 1 mesh object, 1 material (2048² baked texture)
- **Matches:** proportions, helmet w/ brow band + cheek guards + neck guard + top fitting (no crest), segmentata + shoulder plates, single front baldric / X-cross back, red tunic, brown pteruges w/ studs, sandals, gladius right hand, curved scutum left hand w/ brass rim + boss + wing-and-arrow emblem, palette.
- **Mismatches (minor):** sword guard slightly chunkier than the SWORD panel; pteruges one row shorter than CLOTHING panel; baked texture ≠ final flat materials (expected — replaced during cleanup).
- **Known topology:** single fused sculpt shell, ~616k tris — needs remesh + modular separation (planned next phase, pending approval). No hidden body under armor (blank body remains a separate build task).
- **Cleanup expectation:** Meshy Remesh API (~5 credits) to ~20k, per-face material classification, module separation per the sheet's exploded panels; the sheet's HELMET/ARMOR/SHIELD/SWORD exploded views now define the target module boundaries.
- **Verdict: PASSES the visual gate — good enough to continue.**

Renders: `renders/final_reference_meshy_source/` (`comparison_sheet_vs_candidateB.png`, `selected_*.png`, `compare_front_ABCD.png`, per-candidate sets).

Confirmations: no modularization, no retopo, no rigging, no animation, no FBX,
no Unity work, no play mode, no commit/push. **Stopped — awaiting approval.**
