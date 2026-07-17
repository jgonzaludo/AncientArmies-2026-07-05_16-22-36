# Patch 2 — Equipment State and Rigid Attachments

**Branch:** `v1-prototype` (started from `89b0e36 v1.3.4`)
**Date:** 2026-07-15
**Pre-existing untouched files:** untracked `.agents/`, `skills-lock.json`.
**Play Mode:** never entered; all verification is static/editor-side.

## Pre-change inspection record

- **Archer visual hierarchy (actual):**

```text
VIS_Roman_Archer_Basic            ← Animator + RomanArcherVisualController + EquipmentVisualSlot (new)
├── GEO_RomanArcher               ← fused SkinnedMeshRenderer (8 shared MAT_Roman_* slots)
├── GEO_RomanArcher_DaggerStowed  ← NEW: separated sheathed-dagger SMR (6 slots, Hips-rigid)
├── RIG_RomanArcher               ← armature (24 bones)
│   └── … LeftHand
│       ├── PROP_Archer_Arrow_Hand   (default inactive)
│       └── PROP_Archer_Knife_Hand   (default inactive; mesh rebuilt as pugio)
```

- **Root cause, flat-rectangle dagger:** `PROP_Archer_Knife_Hand` was a literal
  untapered 2.8 × 41 × 9 cm box (72 verts, zero taper — measured constant width
  profile end to end) **and it was permanently active in the prefab** — visible
  in every state, hanging from the left hand. No controller code referenced it.
- **Root cause, duplicated dagger:** the sheathed dagger (brass hilt + iron
  blade at the left hip) is baked into the fused body mesh — it could never be
  hidden, so any hand dagger duplicated it.
- **Root cause, shield deformation:** the legionary scutum (4300 verts across
  fragmented islands) was auto-weight skinned: 2476 verts carried multiple
  >5% influences across LeftHand / LeftArm / LeftForeArm and even
  **LeftToeBase** (25 dominant verts) — arm bends literally bent the shield.
- Bow (432 verts), quiver (5592), archer helmet (1829) were already
  rigid-skinned in the v005 cleanup; verified unchanged.
- Dagger hand: this model's approved reference holds the **bow in the right
  hand**, so the free/draw hand — and therefore the dagger — is the **left**
  hand (the generic "right-hand socket" guidance is mirrored by the approved
  concept; both hand props were already bone-parented to LeftHand with a
  tuned constant local transform, which was kept — no runtime corrections).

## Changes

### Blender (new versioned sources; originals preserved)

- `ArtSource/Blender/Romans/Archer/RomanArcher_v006_EquipmentStates.blend`
  (from v005, untouched):
  - Sheathed dagger separated out of the fused mesh into
    `GEO_RomanArcher_DaggerStowed` (743 verts, Hips-rigid 1.0) — selection
    validated visually through five iterations of delete-and-render tests
    (island analysis + two tight per-face volumes for remesh-fused parts).
  - Reveal-voids problem solved with a leather-painted **convex-hull backing**
    of the dagger volume (shrunk 12%, tucked 1.2 cm inward, 108 faces,
    Hips-rigid): with the dagger hidden it reads as the empty scabbard mount;
    with it shown it is fully covered (verified by renders both ways).
  - `PROP_Archer_Knife_Hand` mesh rebuilt in place as a stylized pugio
    (98 verts: brass pommel + guard, leather grip, leaf-shaped iron blade
    with midrib), same object name / local frame / pivot / axis as the old
    slab so the existing prop transform and clips still fit. Old slab is
    preserved inside the archived FBX backup.
  - No action, pose, or body edits. All 16 actions retained in the .blend.
- `ArtSource/Blender/Romans/RomanCharacterSystem_v014_RigidEquipment.blend`
  (from v013, untouched):
  - Shield: all 4300 scutum verts re-skinned **100% LeftHand** (Option 2,
    single-bone rigid skinning; LeftHand was already the dominant bone so
    motion is preserved). **Numerically verified rigid**: max pairwise drift
    between 4 extreme shield markers across 5 sampled frames of all 13
    export actions = 0.0001 cm (float noise). Impact-frame render checked.
  - Audit fixes (same auto-weight root cause): 1518 helmet-metal verts
    (Head-dominant but blended) normalized to 100% Head; 28 gladius-blade
    verts to 100% RightHand. Torso segmentata left articulated by design.
- Exports: same conventions as all prior exports (`-Z Forward`, `Y Up`,
  `FBX_SCALE_UNITS`, no leaf bones, all 13 intended actions baked as takes,
  legacy Meshy actions stripped in-memory only). Pose reset to rest before
  export. Previous FBX files archived:
  `ArtSource/Blender/Romans/Archive/RomanArcher_Basic_pre_v006_backup.fbx`,
  `RomanLegionary_Basic_pre_v014_backup.fbx` (ArtSource is local-only by
  repo policy — gitignored — so backups and .blend sources are not tracked).

### Unity

- **`EquipmentVisualSlot` (new, reusable):** visual-only component with
  `Stowed / Drawing / Active / Returning / Hidden` states over a serialized
  stowed object + active object pair. Drawing shows the stowed object,
  Returning shows the active one — the caller advances the state at the
  visual handoff, so two full weapons are never visible together. No
  instantiation, no hierarchy searches, no per-frame cost (SetState is
  change-gated); future units (centurions, signifers, spearmen…) reuse it
  per equipment piece.
- **Archer controller:** dagger resolver in `Update` — melee mode (loco role
  KnifeGuard or a "Melee"-tagged state) drives `Stowed → Drawing → Active`
  with the handoff at KnifeDraw normalized time 0.45 (serialized), or
  immediately on arriving in KnifeGuard/KnifeStab (covers the direct-stab
  path that skips the draw). Leaving melee resolves to `Stowed` — there is
  no return clip yet, so re-sheathing is an instant handoff (documented
  limitation; Phase 3 does not add clips either, so this stays until a
  return animation is authored). Interruptions self-correct: hits don't
  touch the state, death freezes it (Update's dead guard), target loss ends
  melee mode → Stowed. Faction/damage/death tinting extended to all skinned
  renderers so the stowed dagger tints with the body.
- **Archer prefab:** body SMR re-synced to the new import (23,362 verts),
  `GEO_RomanArcher_DaggerStowed` SMR added and bone-bound by name (743
  verts, 6 shared materials), both hand props default-inactive,
  `EquipmentVisualSlot` wired (stowed = sheathed SMR, active = pugio prop,
  default Stowed). All serialized references verified post-save.
- **Legionary prefab:** body SMR re-synced to the new import (22,345 verts,
  bone order revalidated). No other changes.
- No gameplay files touched. No new materials (all slots resolve to
  `Shared/Materials` assets — verified). Capsule fallback path untouched.

## Verification

- Static Roslyn compile (full Assembly-CSharp incl. new component): exit 0.
- Editor recompile + import: clean; Console 0 errors / 0 warnings.
- FBX imports: archer 13 clips + 4 meshes; legionary 13 clips + 1 mesh.
- Prefab audit: all SMR bones non-null, 0 material issues, slot wired,
  props inactive, defaults serialized (validated via editor scripting).
- Shield rigidity: numeric (above). Dagger states: render-verified in
  Blender both ways; runtime behavior needs Play Mode (manual tests doc).

## Review renders

`docs/art/reviews/renders/patch_2_equipment/`:
`before_sheathed_dagger_and_slab.png`, `after_stowed_state.png`,
`after_drawn_state_sheath_hidden.png`, `after_hand_pugio.png`,
`legionary_rigid_shield_impact_frame.png`.

## Known limitations

- Re-sheathing is an instant visual handoff (no return clip exists).
- The pugio still moves with the current KnifeGuard/KnifeStab poses —
  pose quality is Phase 3 scope, untouched here.
- The leather scabbard-backing is only visible mid-melee at close zoom;
  it intentionally reads as an empty scabbard mount.
