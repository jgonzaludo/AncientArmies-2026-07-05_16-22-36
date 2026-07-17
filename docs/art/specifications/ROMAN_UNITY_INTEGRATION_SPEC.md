# Roman Unity Integration Specification

How the Roman character art integrates with the existing game
(V1.2 codebase). Written against the current implementation:
`Assets/Scripts/SoldierFactory.cs`, `Soldier.cs`, `Formation.cs`.

**Prime directive: gameplay stays authoritative.** The visual asset is a
cosmetic child of the gameplay soldier. It must not silently redefine
spacing, radii, physics, or any simulation behavior.

---

## 1. Authority boundaries

Current gameplay systems remain authoritative for:

* Position and movement (Rigidbody-driven; `FreezePositionY`, no gravity)
* Formation movement and spacing (`Formation.spacing = 1.3 m`)
* Target selection and combat collision (CapsuleCollider: height 1.8 m,
  radius 0.35 m, center y = 1.0)
* Health, death state, and removal of dead soldiers
* Team membership (`Team.Blue` / `Team.Red`)
* Selection indicators (existing `SelectionDisc`)

The visual model therefore: no colliders, no rigidbodies, no root motion,
no scripts that move transforms it doesn't own. If the model looks too
big/small relative to spacing, the *art* is revisited — never the spacing.

## 2. Scale and orientation

| Property | Value |
|---|---|
| Unity scale | 1 unit = 1 m; FBX must import at scale factor 1.0 with no compensating root scaling |
| Character height | 1.75 m bare head / ~1.85–1.90 m helmeted — consistent with the existing 1.8 m gameplay capsule |
| Forward | Character faces **+Z** of the soldier root (the gameplay code already treats root +Z as facing; weapon offset is +Z). FBX export settings must produce this with zero rotation offsets on the visual root — verify during Blender setup |
| Up | +Y |
| Visual footprint | Soldier + held shield should stay within ~1.1–1.2 m diameter so ranks at 1.3 m spacing read dense (V1.2 intent) without constant interpenetration; incidental overlap during combat disorder is acceptable |

## 3. Prefab hierarchy

Visual-only prefab (`VIS_Roman_LegionaryBasic.prefab`):

```
VIS_Roman_LegionaryBasic          (root: identity transform, Animator, no collider)
├── RIG_Roman                     (imported skeleton root)
│   └── ... bones ...
│       ├── ATTACH_Head/          HELMET_Legionary (MeshRenderer)
│       ├── ATTACH_Hand.R/        WEAPON_Gladius (MeshRenderer)
│       ├── ATTACH_Hand.L/        SHIELD_Scutum (MeshRenderer)
│       └── ATTACH_Hip.L/         WEAPON_Gladius_Sheathed (MeshRenderer)
├── SKIN_Body                     (SkinnedMeshRenderer: body + eyes + cloth merged)
└── SKIN_Armor                    (SkinnedMeshRenderer: segmentata + shoulder plates)
```

At runtime it is instantiated as a child of the existing gameplay root:

```
LegionII_S3                       (gameplay root — unchanged: CapsuleCollider,
├── VIS_Roman_LegionaryBasic      Rigidbody, Soldier)
└── SelectionDisc                 (existing indicator, unchanged)
```

The primitive `Body` capsule and `Weapon` cube in `SoldierFactory` are
replaced by the visual prefab at integration time (pipeline step 10). The
factory keeps building the collider/rigidbody/disc exactly as today.

## 4. Collider separation

* The visual meshes carry **no colliders**; the gameplay CapsuleCollider on
  the root remains the only collision/selection volume.
* V1.2's forgiving touch targets and combat radii are collider properties —
  the art must never be required to match them exactly.
* Skinned mesh bounds: use Unity's computed bounds; if culling pops are
  seen on attack animations, widen `localBounds` on the two skinned meshes
  rather than adding update-when-offscreen cost.

## 5. Team identity and tinting (confirmed decisions)

* **Roman materials are never recolored for team identity.** Romans are
  permanently red/silver/brown/gold.
* Enemy armies will be **separate faction model families** (future scope —
  not part of this pipeline pass).
* Until an enemy family exists, team identification uses **UI indicators**
  (the existing selection disc pattern: e.g. an always-on faint team-color
  ground ring), not material recoloring. Recolor/filter approaches may be
  revisited later.
* `Soldier.cs` currently tints the body renderer via MaterialPropertyBlock
  (`_BaseColor`): white hit-flash, damage darkening, death darkening. This
  mechanism is kept but re-pointed: cache all visual `Renderer`s of the
  instantiated prefab and apply the same MPB flash/darkening to all of
  them. MPB tinting works with shared URP Lit materials and does not break
  SRP batching compatibility of other instances. The permanent *team* tint
  path is simply not applied to Roman renderers.

## 6. Shared material strategy

* One material asset set for the entire Roman family (~8 `MAT_Roman_*`
  URP Lit materials), assigned in the FBX importer via name-based remap.
* **No per-soldier material instances at runtime.** All per-soldier visual
  state (flash, darkening) goes through MaterialPropertyBlocks.
* No textures in V1 except (optionally, decided in the material pass) one
  shared emblem atlas ≤ 1024². No per-unit or per-soldier textures.
* All soldier materials must be SRP-batcher compatible (standard URP Lit
  is). Existing runtime-created materials in `SoldierFactory.Lit()` are
  replaced by the shared assets for soldiers.

## 7. Animation controller expectations

* One shared `AC_Roman_Soldier.controller` for all Roman units.
* Rig import: **Generic** (one shared skeleton; no Humanoid retargeting
  need, lower CPU cost). All unit FBXs reference the same avatar.
* States (mapped to existing `Soldier` states): Idle, Walk, AttackSword,
  Hit, Death — plus later AttackBow, ThrustSpear, StandardIdle/Walk via
  the same controller (parameter-selected) or an override controller.
* **All clips in-place.** Root motion off. Gameplay moves the root; Walk
  plays whenever the rigidbody moves, with speed-matched playback rate if
  foot-sliding is visible.
* Animator culling: `CullUpdateTransforms` (offscreen soldiers stop
  skinning but keep state). Pivot-in-place (V1.2) is gameplay rotation; a
  dedicated pivot clip is optional polish.

## 8. FBX workflow

* Source of truth: `art/blender/roman_characters.blend` (one file, whole
  family). FBX files are disposable build artifacts.
* Export via `EXPORT_*` collections (see naming standard):
  * `Roman_LegionaryBasic.fbx` — rig + the unit's skinned meshes,
    **merged per material layer** (body+cloth → `SKIN_Body`,
    armor → `SKIN_Armor`) to minimize skinned mesh count.
  * `Roman_Props.fbx` — all rigid props as static meshes at origin.
  * `Roman_Animations.fbx` — rig + actions only (animation stage).
* Import settings: scale 1.0, no cameras/lights, mesh compression off
  initially, blend shapes off, Read/Write off, Generic rig.
* Re-export must be idempotent: same names → Unity re-links materials,
  prefabs, and animations without manual fixes.

## 9. Modular equipment → prefab variants

* `VIS_Roman_LegionaryBasic.prefab` is the base visual prefab.
* Unit variants are **prefab variants** of it: swap/add the prop meshes
  under the sockets (helmet, weapon, shield, standard) and swap skinned
  armor meshes where needed. No code involvement for cosmetic recipes.
* `SoldierFactory` gains a mapping from unit stats (melee/archer today) to
  a visual prefab reference — the only code touchpoint besides renderer
  caching for flash/darkening.

## 10. Hundreds-of-soldiers considerations

Design assumptions for 400–500 visible soldiers (validated during the
integration/profiling stage — see performance budget):

* ≤ 2 SkinnedMeshRenderers + ≤ 4 rigid MeshRenderers per soldier.
* Rigid props on bones are plain MeshRenderers: SRP-batched by shared
  material; skinned meshes SRP-batch per material as well.
* GPU skinning on (Project Settings); Generic rig, ≤ 20 deform bones,
  skin weights clamped to **2 bones** project-wide for crowd scale
  (authored at 4, Quality setting decides).
* No per-soldier Update() in visual prefabs; Animator + gameplay scripts
  only.
* Shadows: start with cast ON / receive ON and profile; the prepared
  fallbacks are (a) shadow-casting off beyond a zoom threshold, or (b) the
  existing disc pattern as blob shadow. Decision deferred to profiling.
* If profiling demands more, the escalation path (not built now) is:
  LOD1 merged-mesh soldiers → animation-rate throttling by distance →
  GPU-instanced crowd rendering. None of this changes the art contract
  above, which is why the budget spec keeps meshes and bones minimal.
