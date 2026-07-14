# Roman Material Palette

Shared material palette for the Roman faction. **This document is
authoritative for both Blender and Unity** — the Blender source file
(`RomanCharacterSystem_v001.blend`) and the Unity material assets
(`MAT_Roman_*.mat`) must both match the values below. Names follow
`docs/art/specifications/ROMAN_ASSET_NAMING_STANDARD.md`
(`MAT_Roman_<Surface>`, identical on both sides so the import remap is
mechanical).

## Philosophy

* **Flat colors only.** Every material is a single solid base color on a
  Principled BSDF (Blender) / URP Lit (Unity). Readability at gameplay
  camera distance comes from silhouette and value contrast, not surface
  detail.
* **No textures, no normal maps, no procedural noise.** Nothing to bake,
  nothing to UV beyond trivial unwraps, nothing that can drift between
  Blender and Unity.
* **Shared materials only.** Every mesh slot references one of the eight
  materials below. No per-soldier material instances — hit-flash and
  damage-darkening are done at runtime via `MaterialPropertyBlock`
  (`_BaseColor`) in `Soldier.cs`, which requires the shared materials to
  stay untouched.
* Metallic/roughness are the only other authored parameters. Unity
  Smoothness = 1 − Blender Roughness.

## Palette

| Material | Base Color (linear RGB) | Metallic | Roughness (Blender) | Smoothness (Unity) | Used by |
|---|---|---|---|---|---|
| `MAT_Roman_Skin` | (0.95, 0.72, 0.47) | 0 | 0.60 | 0.40 | body, hands, face |
| `MAT_Roman_ClothRed` | (0.60, 0.11, 0.09) | 0 | 0.80 | 0.20 | tunic, sleeves, shield face |
| `MAT_Roman_ClothNeutral` | (0.82, 0.77, 0.68) | 0 | 0.85 | 0.15 | fitted-shorts underlayer region |
| `MAT_Roman_Iron` | (0.75, 0.77, 0.80) | 0.85 | 0.35 | 0.65 | segmentata, helmet, gladius blade |
| `MAT_Roman_Brass` | (0.85, 0.65, 0.22) | 0.90 | 0.30 | 0.70 | fasteners, shield boss/rim/emblem, sword guard+pommel |
| `MAT_Roman_Leather` | (0.45, 0.28, 0.15) | 0 | 0.75 | 0.25 | belt, pteruges, sandals, sword grip |
| `MAT_Roman_Wood` | (0.52, 0.36, 0.22) | 0 | 0.80 | 0.20 | (reserved: spear shafts, standard poles) |
| `MAT_Roman_Black` | (0.04, 0.04, 0.045) | 0 | 0.50 | 0.50 | eyes |

## Notes

* Colors were sampled/approximated from
  `roman_unit_family_reference.png` (the Step 1 concept reference).
* Romans are **never team-recolored** — this palette is fixed; faction
  distinction is handled on the enemy side.


> **Revision 2026-07-12 (final-reference pass):** `MAT_Roman_Skin` base color
> warmed to (0.95, 0.72, 0.47) and `MAT_Roman_ClothRed` deepened to
> (0.60, 0.11, 0.09) to match `full_reference_sheet.png`. Blender and Unity
> assets updated in the same pass.
