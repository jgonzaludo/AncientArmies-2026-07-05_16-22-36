# Modular Repair Report

Date: 2026-07-12
Scope: repair-only pass. No rigging, no animation, no export, no Unity work.
The larger end-to-end task remains **paused**.

## 1. Archives

- Bad-modularization archive: `ArtSource/Blender/Romans/Archive/RomanLegionary_BadModularization_Archive.blend`
  (plus `RomanCharacterSystem_v009_MeshyCleanedUnrigged.blend` retains the same bad state)
- Rejected primitive archive (earlier): `Archive/RomanLegionary_PrimitivePrototype_Rejected.blend`
- Working repaired file: **`RomanCharacterSystem_v010_ModularRepair.blend`**

## 2. Untouched Meshy source

- In-scene: `SRC_Meshy_RomanLegionary_A` (hidden, original texture) + `WORK_Remesh22k` (hidden) in `05_MESHY_SOURCE`
- On disk: `ArtSource/Generated/Meshy/RomanLegionary/meshy_output/` (candidates A/B/C + 22k remesh + task metadata)

## 3. Fragments before repair

14 objects whose contents were arbitrary patch-shells cut from 132 mesh islands
by color/position masks — visually complete assembled, meaningless as swappable
modules (armor chunks, torn cloth patches, disconnected skin fragments).

## 4. Final production objects: 19

| Group | Objects |
|---|---|
| Body | BODY_RomanBase, BODY_RomanBase_Eyes |
| Clothing | CLOTH_Tunic_Basic, CLOTH_Sleeves_Basic, CLOTH_Belt_Basic, CLOTH_Pteruges_Basic, CLOTH_Sandals_Basic |
| Armor | ARMOR_Segmentata_Basic, ARMOR_Shoulder_L_Basic, ARMOR_Shoulder_R_Basic |
| Helmet | HELMET_ImperialBasic_Dome, _CheekGuard_L, _CheekGuard_R, _NeckGuard, _CrestMount |
| Weapon | WEAPON_Gladius_Basic |
| Shield | SHIELD_Scutum_Basic, SHIELD_ScutumBoss_Basic, SHIELD_Emblem_Basic |

Assembled total ≈ 12.1k tris (body 2,368+384; Meshy helmet set 2,328; Meshy boss 285 + emblem 1,293; rebuilt modules 3,794).

## 5. Joined vs rebuilt

- **Kept from Meshy (coherent, high-value):** helmet — split into 5 logical
  parts by region masks (dome 1,611 / cheek L 189 / cheek R 187 / neck 251 /
  crest mount 90 tris); shield boss; shield emblem (wing-and-arrow relief).
- **Rebuilt native (Meshy as shape/position guide):** tunic, sleeves, belt
  (waist band + brass buckle + baldric; **back sheath removed per user
  request**), pteruges (12 strips + brass studs, one object), sandals (one
  object, both feet), segmentata (6 overlapping bands + collar + brass stud
  columns, one object), shoulder guards (3 nested lames each, L/R),
  gladius (one object, blade/guard/grip/pommel via material slots),
  scutum board (curved, brass rim; positioned from emblem-plane PCA so the
  preserved Meshy boss/emblem sit exactly on its face).
- **Discarded fragments:** all 11 torn patch modules (preserved in both archives).

## 6. Blank body completeness

`BODY_RomanBase` is the complete, watertight-shell reusable body (head, neck,
torso, arms, fists, legs, feet, neutral fitted-shorts material region) —
verified standing alone with ALL equipment hidden (`layer_body.png`,
`swap_test_no_equipment.png`). No armor-shaped holes, no floating patches.

## 7. Exploded modular view

`06_EXPLODED_VIEW` collection (linked duplicates, excluded from render by
default) arranged by logical group: body / clothing / armor / helmet / weapon
/ shield — `renders/modular_repair/exploded_view.png`.

## 8. Validation

- Visibility test: body → +clothing → +armor → +helmet → +weapons renders
  (`layer_*.png`) — each layer coherent. ✔
- Swap test: helmet/armor/sword/shield hidden — no holes or debris. ✔
- Object count: 19 named production objects, zero stray fragments. ✔
- Shape test: assembled render matches approved candidate proportions
  (1.75 m bare, ~1.87 m helmeted, ~2.55 heads). ✔
- Source preservation: Meshy raw + both archives intact. ✔

## 9. Remaining topology/visual notes

- Meshy cheek/neck guards show some backface darkness in isolated views
  (thin open shells) — invisible assembled; candidate for a solidify pass
  before rigging.
- Helmet seat lowered 0.05 m; brow now sits just above the eyes; a small
  skin strip remains visible at some angles (matches reference acceptably).
- Sword grip alignment to the fist is approximate until the rig's
  ATTACH_Weapon_R socket exists.
- Rebuilt hard-surface modules are simpler than the Meshy sculpt (deliberate:
  clean topology, shared materials); helmet/emblem/boss keep the Meshy charm.

## 10–14. Confirmations

- No rigging occurred. ✔
- No animation occurred. ✔
- No Unity work occurred (project untouched this pass). ✔
- No commit/push. ✔
- The original full production task remains **paused**, awaiting visual approval.
