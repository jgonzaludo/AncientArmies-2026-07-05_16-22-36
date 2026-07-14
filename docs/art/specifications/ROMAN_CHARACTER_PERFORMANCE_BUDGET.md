# Roman Character Performance Budget

> **STATUS: PROVISIONAL.** Every number below is a starting target chosen
> from mobile-crowd experience, not a measured limit. They become real only
> after Unity mobile profiling on target devices (pipeline step 10). Revise
> this file with measured data; do not treat targets as quality goals to
> spend up to.

Sizing scenario: **400–500 simultaneously visible soldiers** on a mid-range
mobile device, fixed orthographic camera, URP, 30 fps floor (60 fps
desirable at default zoom with ~100–200 visible).

Strategy in one line: *one body family, one skeleton, few shared
materials, no per-soldier uniqueness, reusable meshes, low bone count,
simple clips.*

---

## Per-soldier targets (basic legionary, fully equipped)

| Metric | Target | Ceiling | Notes |
|---|---|---|---|
| Total triangles (body + gear) | 4,000–5,000 | 6,500 | 500 × 5k ≈ 2.5 M tris worst case — acceptable for ortho mobile with GPU skinning, pending profiling |
| — base body (+ eyes) | 1,800–2,300 | 2,600 | See base body spec |
| — clothing (tunic/skirt/belt/sandals) | 700–1,000 | 1,300 | Derived from body topology |
| — armor (segmentata + shoulders) | 600–900 | 1,200 | |
| — helmet | 350–500 | 700 | Dome + guards merged |
| — gladius | 80–150 | 250 | |
| — scutum + boss | 250–400 | 600 | Curvature needs the polys |
| Deforming bones | ≤ 20 | 22 | + ≤ 8 non-deform sockets |
| Skin weights (runtime) | 2 bones/vertex | 4 | Author at 4, project Quality clamps to 2 |
| SkinnedMeshRenderers / soldier | 2 | 3 | Merged per material layer at export |
| Rigid MeshRenderers / soldier | ≤ 4 | 6 | Helmet, sword, shield, scabbard |
| Materials / soldier | ≤ 6 | 8 | All from the shared `MAT_Roman_*` set |

## Family-wide targets

| Metric | Target | Notes |
|---|---|---|
| Material assets, entire Roman family | ≤ 8 | `MAT_Roman_*`; zero per-soldier instances (MPB only) |
| Textures | 0 required; ≤ 1 optional shared atlas ≤ 1024², no mips issues (flat colors preferred) | Emblems/insignia only; never per-soldier |
| Skeletons | 1 (`RIG_Roman`, Generic) | Shared by every Roman unit |
| Animation clips (first pass) | 5 (Idle, Walk, AttackSword, Hit, Death) | ~15–30 keyed frames each, in-place |
| Animator controllers | 1 shared | Override controllers for later unit types |

## Scene-level targets (500 soldiers visible)

| Metric | Target | Notes |
|---|---|---|
| Soldier draw calls (SRP batched) | few hundred, not thousands | Shared materials + SRP batcher; verify with Frame Debugger |
| Animator cost | ≤ ~3 ms CPU total | Generic rigs, culling `CullUpdateTransforms`; escalation: distance-based update throttling |
| Skinning | GPU skinning ON | With 2-bone weights and ≤ 20 bones |
| Shadows | Cast ON to start; **first thing profiled** | Fallbacks ready: distance/zoom cutoff or blob discs |
| Per-soldier scripts in visuals | 0 | Gameplay scripts live on the root, unchanged |

## Rules that keep the budget honest

1. No unique meshes, materials, or textures per soldier — ever.
2. Delete faces the camera can never see (under armor, mesh interiors) on
   equipment and export merges — but never on the base body source.
3. Any new unit variant must reuse the base body, skeleton, and material
   set; a variant's *new* geometry budget is its gear only.
4. Budget increases require a profiling capture that shows headroom, noted
   in `../reviews/`.
5. If profiling shows the ceiling is generous (ortho + small on-screen
   size often is), spend first on: shield/helmet silhouette smoothness,
   then shoulder deformation loops — never on face/texture detail.
