# Roman Asset Naming Standard

Naming rules for everything in the character pipeline, Blender through
Unity. Names are contracts: import automation, prefab wiring, and future
sessions all key off them. **PascalCase after the prefix, no spaces, ASCII
only.** Blender's `.L`/`.R` suffix convention is used for bones and paired
objects (it enables symmetry tools); Unity-side assets use `_L`/`_R`.

---

## 1. Prefixes

| Prefix | Meaning | Examples |
|---|---|---|
| `BODY_` | Base body meshes | `BODY_RomanBase` |
| `CLOTH_` | Clothing layer meshes | `CLOTH_Tunic_Red` |
| `ARMOR_` | Armor layer meshes | `ARMOR_Segmentata` |
| `HELMET_` | Helmet meshes | `HELMET_Legionary` |
| `WEAPON_` | Hand-held weapons | `WEAPON_Gladius` |
| `SHIELD_` | Shields | `SHIELD_Scutum` |
| `PROP_` | Standards, instruments, accessories | `PROP_Standard_Aquila` |
| `RIG_` | Armatures | `RIG_Roman` |
| `MAT_` | Materials | `MAT_Roman_Iron` |
| `ANIM_` | Actions / animation clips | `ANIM_Roman_Walk` |
| `ATTACH_` | Non-deform socket bones | `ATTACH_Hand.R` |
| `EXPORT_` | Blender export collections | `EXPORT_Roman_LegionaryBasic` |

## 2. Blender

### Collections

```
ROMAN/                          top-level collection, one per faction
  ROMAN_Base/                   base body + eyes
  ROMAN_Rig/                    RIG_Roman armature
  ROMAN_Clothing/               all CLOTH_ modules
  ROMAN_Armor/                  all ARMOR_ modules
  ROMAN_Helmets/                all HELMET_ + crest modules
  ROMAN_Weapons/                all WEAPON_ modules
  ROMAN_Shields/                all SHIELD_ modules
  ROMAN_Props/                  all PROP_ modules
  ROMAN_Export/                 EXPORT_* collections (temporary merged copies)
    EXPORT_Roman_BaseBody/
    EXPORT_Roman_LegionaryBasic/
    EXPORT_Roman_Props/
```

### Objects and meshes

* Object and its mesh datablock share the same name
  (object `BODY_RomanBase` → mesh `BODY_RomanBase`).
* Variants use a trailing descriptor: `CLOTH_Tunic_Red`, `CLOTH_Tunic_Green`,
  `HELMET_Legionary`, `HELMET_Centurion`.
* Paired objects: `ARMOR_ShoulderPlate.L` / `ARMOR_ShoulderPlate.R`
  (or one merged `ARMOR_ShoulderPlates` object when never split).

### Armature and bones

* One armature: `RIG_Roman` (object and data both).
* Deform bones, lowercase: `root`, `hips`, `spine`, `chest`, `neck`,
  `head`, `upperarm.L`, `forearm.L`, `hand.L`, `thigh.L`, `shin.L`,
  `foot.L` (+ `.R` mirrors).
* Socket bones: `ATTACH_Head`, `ATTACH_Hand.R`, `ATTACH_Hand.L`,
  `ATTACH_Forearm.L`, `ATTACH_Hip.L`, `ATTACH_Hip.R`, `ATTACH_Back`,
  `ATTACH_Chest` — zero deform weight, "Deform" flag off.
* Control/helper bones (IK targets, poles): `CTRL_` prefix, never exported.

### Materials

`MAT_Roman_<Surface>`: `MAT_Roman_Skin`, `MAT_Roman_ClothRed`,
`MAT_Roman_ClothNeutral`, `MAT_Roman_Iron`, `MAT_Roman_Brass`,
`MAT_Roman_Leather`, `MAT_Roman_Wood`, `MAT_Roman_Black`.
Same names are reused for the Unity material assets so the import remap is
mechanical.

### Actions

`ANIM_Roman_<Clip>`: `ANIM_Roman_Idle`, `ANIM_Roman_Walk`,
`ANIM_Roman_AttackSword`, `ANIM_Roman_Hit`, `ANIM_Roman_Death`,
`ANIM_Roman_Pivot`, `ANIM_Roman_AttackBow`, `ANIM_Roman_ThrustSpear`,
`ANIM_Roman_StandardIdle`, `ANIM_Roman_StandardWalk`.

### Files

```
art/blender/roman_characters.blend      single family source file
art/blender/roman_characters_v###.blend backups/milestones (optional)
```

## 3. Unity

### FBX files (under `Assets/Art/Roman/Models/`)

| File | Content |
|---|---|
| `Roman_BaseBody.fbx` | Rig + blank base body (review/reference asset) |
| `Roman_LegionaryBasic.fbx` | Rig + merged legionary skinned meshes |
| `Roman_Props.fbx` | All rigid props (weapons, shields, helmets) as static meshes |
| `Roman_Animations.fbx` | Rig + all ANIM_ clips, no meshes (added at animation stage) |

### Unity assets

| Kind | Pattern | Examples |
|---|---|---|
| Materials | `MAT_Roman_<Surface>.mat` | `MAT_Roman_Iron.mat` |
| Animator controller | `AC_Roman_Soldier.controller` | shared by all Roman units |
| Visual prefab | `VIS_Roman_<Unit>.prefab` | `VIS_Roman_LegionaryBasic.prefab` |
| Unit prefab variants | `VIS_Roman_<Unit>_<Variant>.prefab` | `VIS_Roman_Legionary_Centurion.prefab` (prefab variant of the basic legionary visual) |
| Folders | `Assets/Art/Roman/{Models,Materials,Animations,Prefabs,Textures}` | |

`VIS_` marks these as visual-only prefabs that get parented under the
gameplay soldier root — they are not gameplay prefabs (see
`ROMAN_UNITY_INTEGRATION_SPEC.md`).

## 4. Worked example — base body and basic legionary

Blender source:

```
ROMAN/ROMAN_Rig/RIG_Roman
ROMAN/ROMAN_Base/BODY_RomanBase, BODY_RomanBase_Eyes
ROMAN/ROMAN_Clothing/CLOTH_Tunic_Red, CLOTH_Skirt_Panels,
                     CLOTH_Belt_Military, CLOTH_Sandals
ROMAN/ROMAN_Armor/ARMOR_Segmentata, ARMOR_ShoulderPlates
ROMAN/ROMAN_Helmets/HELMET_Legionary
ROMAN/ROMAN_Weapons/WEAPON_Gladius, WEAPON_Gladius_Sheathed
ROMAN/ROMAN_Shields/SHIELD_Scutum   (contains SHIELD_Boss object)
Actions: ANIM_Roman_Idle, ANIM_Roman_Walk, ...
Materials: MAT_Roman_Skin, MAT_Roman_ClothRed, MAT_Roman_Iron,
           MAT_Roman_Brass, MAT_Roman_Leather, MAT_Roman_Wood,
           MAT_Roman_Black, MAT_Roman_ClothNeutral
```

Unity result:

```
Assets/Art/Roman/Models/Roman_LegionaryBasic.fbx
Assets/Art/Roman/Models/Roman_Props.fbx
Assets/Art/Roman/Materials/MAT_Roman_*.mat
Assets/Art/Roman/Prefabs/VIS_Roman_LegionaryBasic.prefab
Assets/Art/Roman/Animations/AC_Roman_Soldier.controller
```
