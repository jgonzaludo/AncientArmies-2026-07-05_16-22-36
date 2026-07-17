# Roman Modular Equipment Specification

Defines the layered equipment system built on top of the blank base body
(`ROMAN_BASE_BODY_SPEC.md`). Reference authority:
`../references/roman_unit_family_reference.png` (family style and unit
recipes) and `../references/basic_legionary_turnaround.png` (basic
legionary gear layout).

**Core rule: no unit is a separate character.** Every unit is the one base
body plus a stack of interchangeable modules. Swapping a module must never
require rebuilding the character.

---

## 1. Layer stack

```
Base Body            (always present, never modified per unit)
  → Clothing         (tunic, skirt, belt, sandals, …)
  → Armor            (segmentata, mail, officer torso, shoulder plates, …)
  → Helmet           (dome + guards + optional crest modules)
  → Weapons          (gladius, spear, bow, …)
  → Shield           (scutum, round, oval)
  → Accessories      (standards, horn, pelt, cape, decorations, …)
```

Any layer can be absent (e.g. armor removed → tunic soldier). Layers stack
visually: each layer is modeled over the one beneath with a small clearance
(~1–2 cm at world scale) so deformation doesn't cause popping.

## 2. Behavior classifications

Every module gets exactly one primary classification:

* **Deforming** — skinned to the shared rig with smooth multi-bone weights;
  moves and bends with the body.
* **Rigid (skinned)** — skinned to the shared rig but with 100% single-bone
  ("rigid") weights per element; keeps its shape, rides a bone. Used when a
  part must be exported inside a skinned mesh but never bend.
* **Bone-parented** — a plain static mesh parented to an attachment bone /
  socket at runtime. Not skinned at all. Preferred for anything swappable
  in-game (weapons, shields, helmets).
* **Potentially skinned** — starts bone-parented/rigid; may gain simple
  skinning later if motion looks too stiff.
* **Future / optional** — not built in the first production pass.

No cloth simulation anywhere (Blender or Unity, confirmed): flexible parts
use ordinary skeletal skinning only. Armor never bends like cloth.

## 3. Module catalog

### 3.1 Clothing

| Module | Description | Classification |
|---|---|---|
| `CLOTH_Tunic_Red` | Short-sleeved muted-red tunic, torso + sleeve geometry derived from body mesh | **Deforming** (copies body weights) |
| `CLOTH_Tunic_Green` | Auxiliary variant, dark green/teal | **Deforming** (future — auxiliary pass) |
| `CLOTH_Sleeves` | Only if ever split from tunic; default: sleeves are part of the tunic mesh | **Deforming** (avoid as separate module unless needed) |
| `CLOTH_Belt_Military` | Brown belt + rectangular brass buckle | **Rigid (skinned)** to `hips`; deformation not visually necessary |
| `CLOTH_Skirt_Panels` | Red leather/cloth skirt panels (simplified pteruges) around hips | **Deforming** (simple weights: `hips` + small thigh influence; panels modeled as chunky merged slabs, not individual strips) |
| `CLOTH_Sandals` | Thick-strapped brown sandals, replaces/covers body feet | **Deforming** (foot bone weights, near-rigid) |
| `CLOTH_Trousers_Aux` | Below-knee brown trousers (auxiliary) | **Deforming** (future — auxiliary pass) |
| `CLOTH_Scarf` | Small neck scarf | **Rigid (skinned)** to `neck` (future/optional) |
| `CLOTH_Cape_Officer` | Red officer cloak over shoulders/back | **Deforming**, simple weights (`chest`/`spine`), **future** — no secondary motion in V1 |

### 3.2 Armor

| Module | Description | Classification |
|---|---|---|
| `ARMOR_Segmentata` | 4–5 broad horizontal iron torso bands + brass rivets + crossing chest straps, chunky simplified | **Rigid (skinned)**: each band 100% to `chest` or `spine` so the stack flexes only at band boundaries; never bends within a band |
| `ARMOR_ShoulderPlates` | Curved overlapping shoulder plates (pair) | **Rigid (skinned)**: 100% to `upperarm.L/.R` (or clavicle if added) so they ride the arm swing |
| `ARMOR_ChestDecorations` | 5–6 gold phalerae disks + harness (centurion/aquilifer) | **Rigid (skinned)** to `chest`; merged into one mesh (future — officer pass) |
| `ARMOR_Mail` | Simplified mail/scale shirt (signifer, aquilifer, auxiliary); reads via silhouette + material, no individual rings | **Deforming**, tunic-style weights (future — standard-bearer/auxiliary pass) |
| `ARMOR_Cuirass_Officer` | Decorated officer torso (centurion) | **Rigid (skinned)** to `chest`/`spine` (future — officer pass) |
| `ARMOR_Greaves` | Officer shin protection (pair) | **Rigid (skinned)** to `shin.L/.R` (future/optional) |

### 3.3 Helmet

| Module | Description | Classification |
|---|---|---|
| `HELMET_Legionary` | One merged mesh: hemispherical dome, small crown fitting, brow band, hinged-look cheek guards, flared rear neck guard | **Bone-parented** to head socket. Cheek and neck guards are part of the merged mesh — they do not articulate |
| `HELMET_Centurion` | Ornate dome, gold brow reinforcement, gold-trimmed cheek guards, larger neck guard, **crest mount** | **Bone-parented** (future — officer pass) |
| `HELMET_Auxiliary` | Bronze conical dome, crown knob, simple cheek guards, reduced neck guard | **Bone-parented** (future — auxiliary pass) |
| `HELMET_CrestMount` | Small crown fitting able to receive crest modules; on basic legionary it stays empty (no crest, confirmed by reference) | Part of helmet meshes |
| `PROP_Crest_Transverse` | Broad rounded red fan, left–right across the helmet (centurion) | **Bone-parented** (attached to helmet, effectively merged at export) (future) |
| `PROP_Crest_Plume` | Front-to-back crest / plume variants | **Future / optional** |

### 3.4 Weapons

| Module | Description | Classification |
|---|---|---|
| `WEAPON_Gladius` | Broad straight blade w/ soft edges + visible thickness, small brass guard, rounded pommel, ~0.55 m total | **Bone-parented** to right-hand socket |
| `WEAPON_Gladius_Sheathed` | Gladius-in-scabbard combined prop (hip continuity when main weapon is a standard/horn/staff) | **Bone-parented** to hip socket |
| `WEAPON_Spear` | ~2.1 m wooden shaft (≥ 4 cm dia.), leaf-shaped iron point | **Bone-parented** to right-hand socket (future — auxiliary pass) |
| `WEAPON_Pilum` | Javelin variant | **Future / optional** |
| `WEAPON_Bow` | Simple curved bow body; rigid in V1 (no bending limbs); string is thick geometry | **Bone-parented**, left-hand socket. **Potentially skinned** later (2-bone limb flex) if firing reads too stiff (future — archer pass) |
| `PROP_Quiver` | Chunky quiver + 3–4 merged arrow heads | **Bone-parented** to back socket (future — archer pass) |
| `PROP_Cornu` | Near-full-circle brass horn, thick tube, flared bell, shoulder brace | **Bone-parented** to chest-front socket or held via both hand sockets (future — musician pass) |
| `PROP_Staff_Optio` | Tall wooden staff, brass knob | **Bone-parented** to hand socket (future) |

### 3.5 Shields

| Module | Description | Classification |
|---|---|---|
| `SHIELD_Scutum` | Curved rectangle ~1.0 × 0.55 m, thick slab, gold rim, red field | **Bone-parented** to left-hand/forearm socket |
| `SHIELD_Boss` | Central gold hemisphere; merged into each shield mesh (kept a separate object in the Blender source for reuse) | Part of shield meshes |
| `SHIELD_Round` | Small round shield (aquilifer sidearm), gold rim + boss | **Bone-parented**, left forearm socket (future) |
| `SHIELD_Oval` | Tall oval, dark red, brass rim, green vine motif (auxiliary) | **Bone-parented** (future — auxiliary pass) |
| Shield decoration | Wing/bolt motif (rank-and-file), laurel wreath (centurion), vine (auxiliary) | Material/texture detail on the shield face — interchangeable without remodeling (shared atlas or flat decal geometry; decide in material pass) |

### 3.6 Special equipment

| Module | Description | Classification |
|---|---|---|
| `PROP_Standard_Aquila` | Dark pole, gold SPQR plaque, chunky gold eagle w/ spread wings, crosspiece, two red tassels, ground spike | Pole/eagle/plaque **bone-parented** (hand socket); tassels **potentially skinned** later, rigid in V1 (future — standard-bearer pass) |
| `PROP_Standard_Signum` | Pole, open-hand finial, wreath, 3 large brass disks + collars | **Bone-parented** (future) |
| `PROP_Standard_Vexillum` | Pole, spear finial, crossbar, hanging square red banner w/ gold fringe + emblem | Pole **bone-parented**; banner a slightly thickened plane, **rigid in V1** ("hangs flat" per reference), **potentially skinned** (2–3 bones) later (future) |
| `PROP_Pelt_Wolf` | Animal head above forehead (rounded ears, simplified muzzle) + fur draping behind shoulders; large fur clumps, no strands | Head portion **bone-parented** (over helmet); cloak portion **deforming** w/ simple weights (future — signifer pass) |
| `PROP_TabletCase` | Small leather case/pouch at hip (optio) | **Bone-parented** to hip socket (future/optional) |
| Officer decorations | Gold trim, phalerae — see `ARMOR_ChestDecorations` | (future — officer pass) |

## 4. Attachment system

### 4.1 Sockets (non-deforming attachment bones)

The shared armature `RIG_Roman` contains, in addition to ≤ 20 deform bones,
these zero-weight attachment bones. They exist in Blender (so animations
can key them) and become plain transforms in Unity (so code/prefabs can
parent props to them).

| Socket bone | Parent bone | Default use |
|---|---|---|
| `ATTACH_Head` | `head` | Helmets, pelt head |
| `ATTACH_Hand.R` | `hand.R` | Gladius, spear, staff (primary weapon) |
| `ATTACH_Hand.L` | `hand.L` | Scutum/round/oval shield grip; bow |
| `ATTACH_Forearm.L` | `forearm.L` | Strapped shields (alternative mount) |
| `ATTACH_Hip.L` | `hips` | Sheathed gladius/scabbard |
| `ATTACH_Hip.R` | `hips` | Pouch/tablet case |
| `ATTACH_Back` | `chest` | Quiver, slung shield |
| `ATTACH_Chest` | `chest` | Cornu brace point |

**Handedness (confirmed by turnaround): weapon in the right hand, shield in
the left.** No mirrored variants are planned; all units share this.

### 4.2 Attachment rules

* **Bone-parented props** are separate static meshes. In Unity they are
  `MeshRenderer` objects parented under the imported socket transforms in
  the prefab. Swapping a helmet = swapping one child object.
* **Skinned modules** (clothing, armor) are separate skinned meshes bound
  to the *same* armature and driven by the *same* Animator. Weights for
  deforming clothing are transferred from the base body, then cleaned.
* Props are authored at world origin with their own sensible pivot (sword:
  grip center; shield: hand-grip point; helmet: head-center), so parenting
  to a socket with zero local offset seats them correctly.
* Per-unit **export merges are allowed** (e.g. merging tunic + skirt +
  segmentata into one skinned mesh for the legionary FBX) as a performance
  packaging step — but the *source* Blender modules stay separate so the
  kit remains editable. See Unity integration spec.

## 5. Animation-aware behavior matrix

Planned clips: idle, march/walk, sword attack, hit reaction, death, pivot,
bow attack, spear thrust, standard-bearing idle/walk. (No animation is
produced in Step 1.) All locomotion is **in-place** — gameplay code owns
position and rotation.

| Component | During all clips |
|---|---|
| Base body, tunic, skirt, sandals | Deform smoothly (shoulders/hips priority) |
| Segmentata bands, shoulder plates | Ride their bones rigidly; the *stack* articulates at band boundaries, bands never flex |
| Belt, scarf | Rigid on `hips`/`neck` |
| Helmet (+ guards, crest) | Perfectly rigid on head socket |
| Sword, spear, staff | Perfectly rigid in `ATTACH_Hand.R`; attack motion comes from arm bones |
| Shield (+ boss) | Perfectly rigid in `ATTACH_Hand.L`; blocks/moves via arm |
| Bow | Rigid in V1 (bow-attack reads via arm draw pose); optional limb bones later |
| Standards, horn | Rigid on sockets; standard sway comes from hand/arm animation, not the prop |
| Pelt cloak, cape, tassels, banner | V1: near-rigid simple skinning (no secondary motion, no cloth sim); revisit only if clearly dead-looking |
| Eyes | Rigid with head |

Death: single keyframed collapse; no ragdoll. (Gameplay removes dead
soldiers when reforming, per project invariants.)

## 6. Unit recipes (assembly reference)

How the eight reference units decompose into modules — future passes build
only the missing modules, never new bodies:

| Unit | Modules (beyond base body) |
|---|---|
| **Basic legionary** (first asset) | Tunic red, skirt, belt, sandals, segmentata, shoulder plates, legionary helmet (no crest), gladius, scutum, sheathed-scabbard hip prop (understated) |
| Centurion | + centurion helmet, transverse crest, officer cuirass/decorations, cape, greaves; laurel scutum face |
| Optio | Legionary set + optio staff, tablet case |
| Aquilifer | Mail torso, aquila standard, round shield, sheathed gladius |
| Signifer | Mail torso, wolf pelt, signum, sheathed gladius |
| Vexillarius | Legionary armor set + vexillum, sheathed gladius |
| Cornicen | Simplified torso armor + cornu, sheathed gladius |
| Auxiliary spearman | Tunic green, trousers, auxiliary helmet, mail, spear, oval shield, red scarf |

Rank is equipment only — every recipe uses the identical body and skeleton.
