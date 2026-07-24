# Art References — Roman Character Family

This directory holds the authoritative visual references for the Roman
character pipeline. **Do not overwrite, crop, recompress, or edit the
originals.** Derived studies (paint-overs, proportion callouts, palette
samples) must be saved as new files with a `study_` prefix.

## Required files

| Filename | Content | Authoritative for |
|---|---|---|
| `roman_unit_family_reference.png` | Eight-unit Roman lineup on green field (legionary, centurion, optio, aquilifer, signifer, vexillarius, cornicen, auxiliary spearman) | Overall art style, color language, material feel, how unit variants relate to each other |
| `basic_legionary_turnaround.png` | Front / side / back turnaround of the basic legionary on gray background | The basic melee unit: proportions, silhouette, armor layout, helmet, shield, sword, handedness |

> **RESOLVED (Step 2, 2026-07-12):** Both original PNGs are now present in
> this directory at the exact filenames above (`roman_unit_family_reference.png`
> 1448×1086 px, `basic_legionary_turnaround.png` 1774×887 px) and are loaded
> as locked, non-rendering image empties in
> `ArtSource/Blender/Romans/RomanCharacterSystem_v001.blend`.

## Reference priority

When the two references disagree, or a spec document disagrees with a
reference:

1. **`basic_legionary_turnaround.png`** wins for everything about the basic
   legionary: body proportions, silhouette, armor layout, helmet shape,
   shield shape, and which hand holds what (sword right, shield left).
2. **`roman_unit_family_reference.png`** wins for family-wide style: palette,
   material feel, how rank/role is communicated, and variant relatedness.
3. The written specifications in `../specifications/` win only for things the
   images cannot show: exact heights, unit scale, topology, naming, budgets,
   and Unity integration.

## Companion text reference

The unit-by-unit written description of all eight sprites (shared visual
language, per-unit equipment, poses, and modeling priorities) was supplied
alongside the images. The reference images plus `../ROMAN_CHARACTER_STYLE.md`
are now the authority for that description — the former
`ROMAN_MODULAR_EQUIPMENT_SPEC.md` it was folded into has been removed
(modularity abandoned; every unit is an independent full Meshy character).
