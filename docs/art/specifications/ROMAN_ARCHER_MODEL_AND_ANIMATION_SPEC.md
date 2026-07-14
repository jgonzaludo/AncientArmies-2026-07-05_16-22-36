# Roman Archer — Model and Animation Specification

Date: 2026-07-13. Authority: `docs/art/references/archer_full_reference_sheet.png`
(modular breakdown, assembly, fit, materials) and
`docs/art/references/archer_turnaround.png` (assembled silhouette, polish).
Where they disagree: turnaround wins for silhouette, sheet wins for modular
construction, family style and Unity readability win overall; discrepancies
are documented in the production log.

## Body and scale (matches the Roman family)

- ~2.6 heads tall, ~1.75 m bare head, production target ~1.875 m helmeted.
- Oversized rounded head, compact broad torso, short thick legs, thick arms,
  oversized hands/feet, vertical black oval eyes, minimal mouth, no nose
  detail, smooth rounded forms, toy-like finish. No realism/grit/texture noise.
- Facing −Y in Blender, feet at Z=0, armature import scale 0.01 preserved,
  exports to +Z forward in Unity (Soldier root convention).

## Equipment (required, no additions)

Red tunic + short sleeves, leather belt, pteruges, sandals, brown scale
cuirass, crossed chest harness, shoulder straps, rounded silver helmet with
brass trim, bow in the LEFT hand, brown back quiver with ~6–8 simplified
arrow tops, short gladius/knife at the belt. NO shield, crest, cape, officer
decoration, or fantasy equipment.

## Rig

- Meshy auto-rig, ~24-bone Mixamo-style skeleton, hierarchy/naming compatible
  with the legionary rig (`Hips…Spine02, arms, legs, neck/Head`).
- Expected outcome: **separate body, shared-compatible rig** (recorded in the
  log after inspection).
- Rigid (single-bone) skinning: helmet, quiver body, side blade, arrow props,
  quiver arrow cluster, buckles/fittings, armor sections that would bend.
- Bow: modest visible draw flex via minimal controlled setup (two limb bones
  + grip, small bone set, or a single draw shape key — simplest that works);
  simple string (3-point curve / minimal bones / controlled verts). No physics.

## Materials (the shared eight, no new materials)

skin→`MAT_Roman_Skin`; tunic+sleeves→`MAT_Roman_ClothRed` (faction-swap slot);
scale cuirass, harness, belt, sandals, quiver→`MAT_Roman_Leather`; helmet dome
+ blade→`MAT_Roman_Iron`; helmet trim, buckles, fittings→`MAT_Roman_Brass`;
bow, arrow shafts→`MAT_Roman_Wood`; eyes→`MAT_Roman_Black`;
fletching→`MAT_Roman_ClothNeutral` if a light material is needed.
Per-face UV-centroid classification; never `materials.clear()`; no per-object
duplicates. Faction tinting: cloth slot only, via per-slot MaterialPropertyBlock
(legionary architecture), combined with damage darkening + hit flash.

## Geometry budget

Remesh target 18k–24k tris (default ~22k, the proven legionary value); never
blind-reduce to ~6.5k. Arrow props: `PROP_Archer_Arrow` 60–180 tris (low-sided
cylinder shaft + cone head + two crossed fin planes), `PROP_Archer_Arrow_Hand`
same mesh, `PROP_Archer_QuiverArrowCluster` one combined light mesh, never fired.

## The 13 clips (30 fps, all in place, no root motion, Unity authoritative)

| Clip | Loop | Duration | Purpose |
|---|---|---|---|
| ANIM_Archer_RearRankIdle | yes | 2.0–2.4 s | disciplined idle, bow diagonal at left side |
| ANIM_Archer_FiringReady | yes | 1.5–1.8 s | combat idle between shots; bow relaxed, no arrow nocked |
| ANIM_Archer_FormationWalk | yes | 0.87–1.0 s | only locomotion; light coordinated steps; retreat = faster playback |
| ANIM_Archer_CloseRanksShuffle | yes | 0.8–0.9 s | reform/auto-close slot corrections |
| ANIM_Archer_PivotStep | once | 0.5–0.6 s | explicit formation rotation only |
| ANIM_Archer_Attack_DirectShot | once | 1.3–1.6 s | full cycle: reach quiver → hand arrow on → nock → draw (bow flexes) → hold → release (hand arrow off, projectile activates) → lower → ready. 75–85 % of shots |
| ANIM_Archer_Attack_HighArc | once | 1.4–1.7 s | same cycle, bow +30–45°, slight chin lift; long-range shots, 15–25 % |
| ANIM_Archer_HitLight | once | 0.35–0.45 s | upper-body recoil; cancels draw, hides hand arrow, no accidental release |
| ANIM_Archer_Death_BackOrSide | once | 1.8–2.4 s | backward/side collapse (Meshy death adapted if it fits) |
| ANIM_Archer_Death_ForwardOrKnees | once | 1.3–1.8 s | forward fold / to knees; distinct silhouette |
| ANIM_Archer_KnifeDraw | once | 0.5–0.7 s | blade out, bow across torso as barrier → KnifeGuard |
| ANIM_Archer_KnifeGuard | yes | 1.3–1.7 s | defensive, rear-weighted, disadvantaged stance |
| ANIM_Archer_KnifeStab | once | 0.6–0.75 s | compact abdomen stab, immediate return to guard |

Explicitly excluded: separate retrieval/nock/draw/aim/release clips, runs,
kneeling/partial-draw shots, extra knife moves, sheath clip, directional
hits/deaths, routing clips.

## Animator (`AC_RomanArcher`, one shared controller)

Same generation conventions as `AC_RomanInfantry` (programmatic, Loco-int
pattern). States: the 13 above. Death from AnyState, never exits. Attacks
return to FiringReady (or KnifeGuard in melee mode). Hit returns to current
mode. Deterministic per-soldier phase offsets so volleys don't release on one
frame (visual only — gameplay cooldown untouched).

## Projectile / release integration

Gameplay validates target, permission, damage, cooldown at attack start
(unchanged). Visual controller plays the firing clip; at the release frame
(normalized-time threshold, fires exactly once) the hand arrow hides and the
gameplay projectile spawns via a deferred-spawn hook with the already-computed
authoritative parameters. Fallbacks preserving balance: capsule archers and
any interrupt (hit/death/despawn) spawn immediately. The hand arrow is never
detached into the projectile. `Projectile` keeps damage-at-landing authority;
its sphere visual is upgraded to the shared arrow mesh, oriented along flight.
