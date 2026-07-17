# Roman Archer — Meshy Candidate Review

Date: 2026-07-13. Four candidates from identical inputs (4 geometry crops +
style prompt), compared in a normalized Blender scene
(`ArtSource/Blender/Romans/Archer/RomanArcher_v000_CandidateCompare.blend`):
equal height (1.875 m), same cameras (front/side/back/¾/iso), same lights.
Raw outputs preserved under `ArtSource/Generated/Meshy/RomanArcher/meshy_output/`.

Raw stats: A 588k tris · B 593k · C 662k · D 621k (all single fused meshes).

## Scores (1–10)

| Criterion | A | B | C | D |
|---|---|---|---|---|
| Overall silhouette | 8 | 9 | 7 | 8 |
| Head size/shape | 8 | 9 | 7 | 8 |
| Face simplicity (family eyes) | 7 | 9 | 7 | 8 |
| Torso width | 8 | 9 | 8 | 8 |
| Limb thickness / leg length | 8 | 8 | 7 | 8 |
| Hands / feet | 8 | 8 | 8 | 8 |
| Helmet shape | 8 | 9 | 8 | 8 |
| Scale armor + harness | 8 | 8 | 8 | 9 |
| Tunic silhouette | 8 | 8 | 8 | 8 |
| Bow shape | 8 | 9 | 8 | 8 |
| Quiver shape | 7 | 8 | 7 | 9 |
| Side-sword | 8 | 8 | 8 | 8 |
| Back view | 7 | 8 | 8 | 9 |
| Reference match | 8 | 9 | 7 | 9 |
| Cleanup difficulty (inverted) | 8 | 9 | 8 | 6 |
| Rigging suitability | 8 | 9 | 8 | 6 |
| Isometric readability | 8 | 9 | 8 | 8 |
| **Total (17 criteria)** | **133** | **146** | **130** | **136** |

## Notes

- **A**: solid all-round; frowning face; dark undifferentiated arrow cluster.
- **B**: best family-match face (large vertical oval eyes, like the
  legionary), cleanest chunky forms, double-brass-band helmet with rivets,
  white-fletched arrows, and — decisive for the pipeline — the bow is held
  clearly separated from the body (safest remesh + auto-rig).
- **C**: heaviest mesh, slightly squat, smaller eyes; slimmer quiver.
- **D**: best quiver (big strapped cylinder, matches the turnaround) and
  strong back view, but the bowstring passes through/along the forearm —
  high risk of the remesh fusing string to arm and of skinning artifacts.

## Selection

**Candidate B** (task `019f5a1a-55f8-7054-93a7-124d74e26a37`), on merit —
scored independently; the coincidence with the legionary's Candidate B is
noted. Runner-up D's quiver is the reference ideal; if B's quiver reads weak
after remesh, revisiting D's quiver region is the recorded fallback.
