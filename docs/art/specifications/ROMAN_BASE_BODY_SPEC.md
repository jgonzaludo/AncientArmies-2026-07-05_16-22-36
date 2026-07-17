# Roman Base Body Specification

Defines the single blank, reusable, unarmored base character that underlies
every Roman unit. Art direction context: `../ROMAN_CHARACTER_STYLE.md`.
Reference authority: `../references/basic_legionary_turnaround.png`
(silhouette/proportions) and `../references/roman_unit_family_reference.png`
(family style).

> **This body contains no armor and no unit-specific equipment.**
> No helmet, no armor, no weapons, no shield, no tunic, no belt, no
> accessories. It is the neutral foundation that later becomes the
> legionary, archer, spearman, centurion, aquilifer, signifer, cornicen,
> veteran, and — where practical — future factions.

---

## 1. Scale and units

| Property | Value |
|---|---|
| Unit convention | `1 Blender unit = 1 m` (scene units: Metric, 1.0 scale) |
| Target height range | 1.70–1.80 m to top of bare head |
| **Default height (confirmed)** | **1.75 m** to top of bare head |
| Helmeted height (later, with gear) | ≈ 1.85–1.90 m |
| Origin | World origin `(0, 0, 0)` at ground level, centered between the feet |
| Forward axis | **To be confirmed during Blender setup.** Recommended: character faces Blender **−Y** (the Blender convention), with the FBX export configured so the character faces **+Z in Unity**. Whatever is chosen must be recorded here and never changed. |
| Up axis | +Z in Blender, +Y in Unity (handled by FBX export settings) |

## 2. Proportions (confirmed: match turnaround, ~2.6 heads)

Head height unit ≈ `1.75 / 2.6 ≈ 0.67 m`. Values below are modeling targets
read off the turnaround; ±5% is acceptable, silhouette match is the real
acceptance test.

| Region | Target | Notes |
|---|---|---|
| Total height | 1.75 m | Ground to top of bare skull |
| Head (chin to crown) | ~0.65–0.68 m | Rounded dome skull, wide soft jaw, almost no neck visible |
| Neck | ~0.02–0.04 m | Short thick cylinder, mostly hidden by head/torso overlap |
| Torso (shoulders to hips) | ~0.70 m | Broad barrel; slight taper to hips |
| Shoulder width | ~0.78–0.85 m | Widest body point (before shoulder armor) |
| Hip width | ~0.55 m | |
| Legs (hips to sole) | ~0.36–0.40 m | Short, thick, slightly spread |
| Arm length (shoulder to wrist) | ~0.42 m | Reaches to about hip level |
| Upper arm / forearm diameter | ~0.14–0.16 m | Simple soft cylinders, minimal muscle definition |
| Hands | ~0.16 m long | Oversized; simplified — thumb + merged fingers ("mitten with knuckle hints") or 4 chunky fingers max |
| Feet | ~0.26 m long, ~0.12 m wide | Big, rounded, flat-soled |

## 3. Neutral modeling pose: relaxed A-pose

The base body is modeled in a **relaxed A-pose**:

* Arms lowered to ~30–40° from the torso (about 55° down from horizontal).
* Elbows very slightly bent, palms facing inward toward the thighs.
* Legs straight, feet shoulder-width apart, toes pointing forward.
* Head level, facing forward.
* Fingers relaxed (slightly curled if fingers are modeled).

**Why A-pose (not T-pose):** this body's arms are short and thick and its
in-game poses are almost entirely arms-down (sword lowered, shield at the
side, hands on standard poles). An A-pose sits near the middle of that
actual animation range, which means less skinning distortion in the poses
players see most, and it keeps the shoulder region relaxed so the later
rigid shoulder plates and segmentata collar don't have to survive a 90°
arm drop from T-pose. With chunky proportions, T-pose also creates armpit
and shoulder topology that only ever gets used in the one pose the game
never shows.

## 4. Facial simplicity

* Two vertical black oval eyes — **separate flat decal geometry** floating
  ~2–3 mm off the face surface, skinned to the head bone. This keeps the
  skin material textureless and lets eye position be tweaked freely.
* No nose geometry (at most a hint of a smooth bump). Little or no mouth;
  a tiny neutral mouth mark is optional and, if used, is material/decal
  detail, not sculpted.
* No ears (helmets and cheek guards cover them on every current unit).
* No hair. The bare head is a clean smooth dome ("bare-head treatment"):
  acceptable because every currently planned unit wears a helmet or pelt.
  A simple cap-style hair module can be added later as an accessory if
  bare-headed units ever appear.

## 5. Underlayer treatment

The blank body is **non-explicit** by construction: smooth, simplified
anatomy with no sculpted detail. To make the blank body presentable on its
own, it wears **fitted shorts** (mid-thigh) modeled as part of the body mesh
and separated only by a material slot (neutral undyed cloth color). No
separate underwear mesh, no sculpted anatomy, no belly button, no nipples.

## 6. Body object breakdown

| Object | Content | Skinning |
|---|---|---|
| `BODY_RomanBase` | One welded mesh: head, neck, torso, arms, hands, legs, feet, fitted-shorts region | Fully skinned, max 4 influences authored (runtime may clamp to 2) |
| `BODY_RomanBase_Eyes` | Two flat black ovals | Skinned 100% to head bone |

That is the entire blank asset: two objects, two material slots on the body
(`MAT_Roman_Skin`, `MAT_Roman_ClothNeutral`) and one on the eyes
(`MAT_Roman_Black`).

Hidden-surface rule: the base body keeps full geometry (it must survive
having any clothing/armor combination removed). Occluded-face deletion
happens on *equipment* meshes and on merged per-unit export copies, never
on the base body source.

## 7. Deformation expectations

Everything on the base body deforms with the skeleton. Deformation quality
priorities, in order:

1. **Shoulders** — arms-down to arms-forward (sword swing, standard grip).
2. **Hips/thighs** — walk cycle.
3. **Neck/head** — subtle; head mostly rides rigidly on the head bone.
4. **Elbows/knees** — simple hinge bends; chunky limbs hide most error.
5. **Wrists/ankles** — near-rigid; hands and feet behave as blocks.

No corrective shapes, no twist bones, no muscle systems in V1. If a
shoulder pinch is unavoidable, hide it under the (separate) shoulder-plate
module rather than adding rig complexity.

## 8. Topology requirements for future rigging

* Quad-dominant; triangles allowed in flat or hidden areas, no n-gons in
  the deforming cage.
* Edge loops at: shoulders (2–3 loops), elbows (3 loops), wrists, hips
  (2–3 loops), knees (3 loops), ankles, neck base.
* Even, moderate density — no dense sculpt-style pole clusters; poles kept
  away from bend regions.
* Symmetrical across the character's left/right axis (model one half,
  mirror; keep the center loop exact).
* All transforms applied (scale = 1.0, rotation = 0) before rigging.
* Clothing meshes will be derived by duplicating and shrink-fattening body
  regions, so the body's loop flow doubles as the clothing loop flow — keep
  it clean.

## 9. Initial triangle budget

| Object | Target | Ceiling |
|---|---|---|
| `BODY_RomanBase` | ~1,800–2,200 tris | 2,500 tris |
| `BODY_RomanBase_Eyes` | ~24–60 tris | 100 tris |

(Provisional; governed by `ROMAN_CHARACTER_PERFORMANCE_BUDGET.md`.)

## 10. Shared skeleton compatibility

The base body is skinned to the **one shared Roman armature**
(`RIG_Roman`), which every unit variant, clothing module, and armor module
also uses. Requirements:

* Deform bones: ≤ 20 (see performance budget). Core chain: `hips`,
  `spine`, `chest`, `neck`, `head`, plus `upperarm/forearm/hand` and
  `thigh/shin/foot` pairs (Blender `.L`/`.R` suffixes).
* Non-deforming attachment bones for equipment sockets (hand weapon, hand
  shield, head, back, hip scabbard) — defined in
  `ROMAN_MODULAR_EQUIPMENT_SPEC.md`.
* Unity import as **Generic** rig (one shared skeleton, no retargeting
  need, lower cost than Humanoid). Confirm at integration.
* One body + one skeleton for **all** Roman units (confirmed decision:
  rank/role never changes the body). Future factions reuse the skeleton
  where practical.
