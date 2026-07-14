# Roman Character Style — Art Direction

Permanent art-direction document for the Ancient Armies Roman character
family. Every character asset (model, material, animation) is measured
against this document and the two references in `references/`.

Companion specifications:

* `specifications/ROMAN_BASE_BODY_SPEC.md`
* `specifications/ROMAN_MODULAR_EQUIPMENT_SPEC.md`
* `specifications/ROMAN_ASSET_NAMING_STANDARD.md`
* `specifications/ROMAN_UNITY_INTEGRATION_SPEC.md`
* `specifications/ROMAN_CHARACTER_PERFORMANCE_BUDGET.md`

---

## 1. Visual identity

Chunky, rounded, toy-like polished mobile-game soldiers: readable at a
glance from a fixed isometric camera, friendly-looking without being
unserious, and visually consistent across the entire Roman unit family.

A player glancing at a formation of 50 of these should instantly read:
*which faction, which unit type, which way they are facing, and whether they
are fighting.* Every art decision serves that sentence.

The characters are **miniatures come to life** — like premium painted board
game pieces — not cartoons of children and not realistic soldiers.

## 2. Shape language

* **Rounded everything.** Domes, barrels, capsules, soft boxes. No sharp
  unbeveled edges anywhere on the character; even sword blades get a soft
  edge and visible thickness.
* **Chunky over accurate.** Equipment is oversized and thick: helmets read
  as solid metal domes, shields are thick slabs with rims, sword blades are
  broad. If a real proportion fights readability, readability wins.
* **Big simple masses, few of them.** Head, torso, shield — three big shapes
  dominate the silhouette. Small shapes exist only where they identify
  something (cheek guards, shield boss, belt buckle).
* **Silhouette-first.** Every unit variant must be identifiable from its
  outline alone at gameplay zoom: legionary = tall rectangle at the side;
  centurion = transverse crest fan; aquilifer = eagle on a pole; cornicen =
  circle around the torso; auxiliary = oval shield + spear.

## 3. Real-world scale philosophy

> **Overall character scale should remain broadly human-sized even though
> proportions and equipment are stylized.**

* `1 Blender unit = 1 meter`, `1 Unity unit = 1 meter`. Always.
* Bare-head body height: **1.75 m** (confirmed; range 1.70–1.80 m).
* Helmet dome adds visible height: helmeted total ≈ 1.85–1.90 m.
* The character must never feel like a child-sized figure in a correctly
  scaled Unity world. Scale is real; *proportions* are stylized.
* The existing gameplay capsule (1.8 m tall, 0.35 m radius) already assumes
  this scale — the art conforms to the world, not the reverse.

## 4. Stylized proportion philosophy

The turnaround reference is authoritative and is matched exactly (confirmed
decision): **~2.6 heads tall** at 1.75 m.

* Oversized rounded head (~38% of body height including skull dome).
* Broad barrel torso; shoulders wider than hips.
* Short thick cylindrical arms; oversized simplified hands.
* Short thick legs; big simplified feet in chunky sandals.
* All Roman units share this one body. **Rank is shown through equipment,
  never body size or body shape.**

Exact measurements: see `specifications/ROMAN_BASE_BODY_SPEC.md`.

## 5. Color palette direction

The Roman faction palette is **permanently** muted red, dull silver iron,
warm leather brown, and restrained brass/gold (confirmed decision — no
team recoloring of Roman materials; see Unity integration spec).

Approximate anchors, to be sampled precisely from the reference images
during the material pass:

| Role | Approx. value | Used on |
|---|---|---|
| Roman red (cloth) | `#B03A2E`, shadow `#8E2B22` | Tunic, skirt panels, crest, cloak, banners |
| Roman red (shield field) | `#A32C22` | Scutum/round/oval shield faces |
| Iron silver | `#B9BCBE`, shadow `#8E9194` | Helmet, segmentata bands, blades, mail |
| Brass / gold | `#D9A33B`, shadow `#B07E27` | Shield rim + boss, rivets, buckles, eagle, disks, horn |
| Leather brown | `#8A5A33`, dark `#6E4526` | Belts, straps, sandals, scabbard |
| Wood | `#7B5137` | Pole shafts, spear shaft, sword grip |
| Skin | `#F0AF74` | Face, arms, legs |
| Detail black | `#1A1512` | Eyes, small trim |
| Auxiliary green | `#5C6B3C` | Auxiliary tunic only (variant accent) |

Rules:

* Muted and warm, never saturated primaries. The red is brick/crimson, not
  fire-engine; the gold is antique brass, not yellow.
* Small palette, big areas: each unit is dominated by 3–4 palette colors.
* Gold/brass is the *rank currency* — the more senior the unit, the more
  brass accents (centurion, aquilifer), but always restrained.
* Auxiliary units may introduce muted green/teal cloth but keep red trim to
  stay visually Roman.

## 6. Material appearance

* Flat colors with soft shading; **restrained highlights, not realistic
  metal reflections**. Metal reads as metal through value grouping and a
  soft top highlight, not mirror-like specular or high metalness sparkle.
* Controlled roughness: cloth fully matte; leather near-matte; iron soft
  satin; brass slightly glossier with a gentle warm highlight.
* No noisy textures, no photo textures, no grunge overlays, no dirt.
* Target: a small set of shared flat-color materials (~6–7 for the whole
  family). Texture maps only if genuinely needed (shield emblems), and then
  from one shared atlas. See performance budget.

## 7. Facial design

* Two **vertical black oval eyes**. That is essentially the whole face.
* Flat or gently shaded single skin color. No pores, blush, or paint detail.
* No nose geometry or at most the faintest hint. Little or no mouth (a tiny
  neutral mouth mark is acceptable, per the family reference).
* No expressions, no per-soldier face variation, no realistic anatomy.
* The face must survive being 20 pixels tall in-game and still read "person".

## 8. Readability requirements

At default gameplay zoom (roughly 30–80 soldiers on screen), each soldier
must communicate, in order of priority:

1. **Faction** — palette + silhouette family.
2. **Unit type** — one dominant silhouette cue (shield shape, crest,
   standard, horn, spear).
3. **Facing** — helmet neck guard vs. face, shield side, weapon side.
4. **State** — fighting/moving/dead (animation + existing gameplay
   indicators; dead soldiers are removed by gameplay rules).

Consequences:

* Unit-identifying gear must be large and positioned high or wide
  (crests, standards, shields) — never a small waist-level detail.
* Front and back must differ obviously (face + shield vs. neck guard).
* Nothing important may rely on color differences smaller than the palette
  steps above, or on details thinner than ~3 cm at world scale.

## 9. Isometric-camera considerations

The game camera is a fixed orthographic isometric-style camera (angle and
projection are set in `Battle.unity` and never change; only pan/zoom move).

* The camera looks **down** at the soldiers: the top of the helmet, the top
  of the shoulders, and the top face of the shield rim are the most-seen
  surfaces. They must be clean, rounded, and well-shaded — invest polygons
  there.
* The underside of anything is nearly invisible: chins, undersides of
  skirts, sole of feet — spend nothing there.
* Feet and legs are frequently occluded by neighboring soldiers at 1.3 m
  formation spacing — keep them simple, keep identity cues above the waist.
* Tall thin props (standards, spears) must be thick enough (≥ 4–5 cm shaft
  diameter at world scale) to avoid shimmering to nothing at distance.
* Poses and gear should be readable from a three-quarter high angle, per
  the family reference sheet.

## 10. What becomes geometry vs. material vs. nothing

### Detail classification table

| Feature | Classification |
|---|---|
| Head silhouette | **Must be geometry** |
| Helmet dome, brow band | **Must be geometry** |
| Cheek guards | **Must be geometry** |
| Helmet neck guard | **Must be geometry** |
| Crest (centurion) and crest mount | **Must be geometry** |
| Segmentata band silhouette (major steps) | **Must be geometry** |
| Shoulder plates | **Must be geometry** |
| Shield body, curvature, and rim | **Must be geometry** |
| Shield boss | **Must be geometry** |
| Sword (blade, guard, pommel) | **Must be geometry** |
| Spear, poles, eagle, signum disks, horn | **Must be geometry** |
| Sandal mass + major straps | **Must be geometry** |
| Tunic, skirt panels (major shapes) | **Must be geometry** |
| Belt + buckle mass | **Must be geometry** |
| Eyes | **Geometry decals** (flat black ovals; avoids textures) |
| Shield emblem (wings, wreath, bolts) | Material/texture detail (shared atlas or flat decal geometry — decide in material pass) |
| Small rivets on armor/helmet | Material/texture detail (or a few merged low-poly hemispheres on hero-visible spots) |
| Color borders / trim lines | Material detail (material-slot splits, not texture, where possible) |
| Subtle plate separation lines | Material/shading detail |
| Minor leather markings | Material detail |
| Simple gradients / soft AO | Material/shading detail |
| Skin pores, freckles | **Omit** |
| Realistic eyes, eyelids | **Omit** |
| Fingernails, knuckle detail | **Omit** |
| Fabric weave, stitching | **Omit** |
| Scratches, dents, dirt, weathering | **Omit** |
| Microscopic rivets, hinge mechanics | **Omit** |
| Hidden armor details (under other layers) | **Omit** — delete occluded faces |
| Mouth interior, teeth, tongue | **Omit** |
| Hair (under helmet units) | **Omit** (bare-head treatment: see base body spec) |

## 11. Visual anti-goals

Explicitly rejected:

* Photorealism or gritty/grimdark realism.
* Thin, realistic weapons; fragile silhouettes that shimmer at distance.
* Tiny armor details and mechanical accuracy (hinge straps, lacing).
* Noisy, high-frequency, or photographic textures.
* Overly exaggerated *child* proportions beyond the reference (the reference
  ratio is the floor and the ceiling — do not push cuter).
* Realistic facial anatomy; anime faces and proportions.
* Generic flat-shaded low-poly with sharp unsoftened edges ("asset-store
  low-poly" look). Our low-poly is rounded and polished.
* Rank shown by body size differences.
* Per-soldier visual uniqueness (unique faces, unique textures, decals).

## 12. Reference priority

See `references/README.md`. Summary: turnaround wins for the basic
legionary; family sheet wins for family-wide style; written specs win only
for what images cannot show (exact scale, topology, naming, budgets,
integration).

## 13. First production asset and build order

The first finished unit is the **basic Roman melee legionary with sword and
rectangular scutum**. No other variants are built during the first
production pass. Mandatory order:

1. Blank base body
2. Basic clothing (tunic, skirt, belt, sandals)
3. Armor (segmentata + shoulder plates)
4. Helmet
5. Sword (gladius)
6. Shield (scutum + boss)
7. Materials
8. Rig
9. Animation
10. Unity integration

Each numbered stage should end in a reviewable checkpoint (screenshot or
turntable into `reviews/`).
