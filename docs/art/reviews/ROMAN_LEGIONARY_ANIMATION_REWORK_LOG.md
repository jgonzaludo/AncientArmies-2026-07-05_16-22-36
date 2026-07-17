# Roman Legionary Animation Rework — Production Log

Date started: 2026-07-13. Task: faction-color fix + disciplined formation
animation set (rear/front rank distinction, compact attacks, march,
combat advance, hit, two deaths, pivot, close-ranks shuffle) with
gameplay-authoritative integration. No balance / AI / spacing changes.

## Pre-existing git state (recorded before any edits)

Branch: `v1-prototype`. Modified (pre-existing, NOT from this task):
`.gitignore`, `Assets/Scenes/Battle.unity`, `Assets/Scripts/Soldier.cs`,
`Assets/Scripts/SoldierFactory.cs`. Untracked (pre-existing): `.agents/`,
`ArtSource/`, `Assets/Art.meta`, `Assets/Art/`,
`Assets/Scripts/RomanLegionaryVisualController.cs(+.meta)`, `docs/art/`,
`skills-lock.json`. None of these are reverted or reset by this task.

## Pre-change state (inspected 2026-07-13)

### Animation files

- Blender source: `ArtSource/Blender/Romans/RomanCharacterSystem_v011_Animated.blend`
  (rigged, skinned, material-classified, 7 Meshy-derived actions).
- FBX: `Assets/Art/Characters/Romans/Legionary/Models/RomanLegionary_Basic.fbx`
  (2.5 MB, 7 takes, −Z forward / Y up / FBX_SCALE_UNITS, Generic rig).
- Imported clips (from `.meta`): ANIM_Roman_Idle (41f loop),
  ANIM_Roman_March (26f loop), ANIM_Roman_SwordAttack (73f once),
  ANIM_Roman_Hit (40f once), ANIM_Roman_Death (72f once),
  ANIM_Roman_Pivot (50f loop, unwired), ANIM_Roman_Run (16f loop, spare).

### Animator setup

- `Assets/Art/Characters/Romans/Legionary/Animations/AC_RomanInfantry.controller`
- Parameters: `Speed` (float), `Attack` / `Hit` / `Die` (triggers).
- States: Idle (1x), March (0.85x), Attack (SwordAttack @2x), Hit (@2.5x),
  Death (1x, no exit). Idle↔March on Speed 0.4/0.2 thresholds;
  Attack/Hit return to Idle; Die from AnyState.
- No rank/role awareness of any kind; every soldier plays the same wide
  Meshy Thrust_Slash attack (the "shield-flailing" clip this task replaces).

### Material state

- 8 shared `MAT_Roman_*.mat` URP Lit assets under
  `Assets/Art/Characters/Romans/Shared/Materials/`, colors per
  `ROMAN_MATERIAL_PALETTE.md` — **correct on disk** (verified
  `MAT_Roman_ClothRed` `_BaseColor` = 0.60/0.11/0.09 etc.).
- FBX `externalObjects` remap: all 8 names → shared assets (correct).
- Prefab `VIS_Roman_Legionary_Basic` SkinnedMeshRenderer has 6 slots:
  0 = Iron, 1 = ClothRed, 2 = Black, 3 = Leather, 4 = Brass, 5 = Skin
  (by GUID; all pointing at the shared assets — correct).

### Code integration

- `SoldierFactory.Create`: melee → instantiate visual prefab, sets
  `bodyR` = the SkinnedMeshRenderer and `color = Color.white`.
- `Soldier.SetTint(c)`: `GetPropertyBlock/SetPropertyBlock` on that
  renderer **without a material index** — renderer-level MPB.
- `RomanLegionaryVisualController`: Speed float + Attack/Hit/Die triggers,
  CullUpdateTransforms, suppressFallRotation.

## ROOT CAUSE — all-silver soldiers (confirmed before fixing)

The materials, import remap, and prefab slots are all correct. The silver
look is created at runtime: `Soldier.Init` → `RefreshTint()` →
`SetTint(white)` applies a **renderer-level** MaterialPropertyBlock with
`_BaseColor = white` to the fused SkinnedMeshRenderer. A renderer-level
MPB overrides `_BaseColor` on **every material slot**, so all six palette
colors are replaced by white. Metallic/smoothness are not in the MPB and
stay per-slot, so Iron (metallic 0.85) and Brass (0.90) read as bright
silver-white metal and skin/cloth/leather read as flat white — at gameplay
distance the whole soldier reads "metallic silver". Damage darkening then
greys the whole figure uniformly, never restoring the palette.

(The `color = Color.white` choice in `SoldierFactory` was intended as a
"neutral" tint, but with a renderer-level MPB there is no neutral value —
any value stomps all slots. Materials were NOT intentionally removed;
nothing is wrong in Blender or the FBX.)

## Changes made

### Code (all compile-verified)

- `Soldier.cs`: added `suppressTint` flag; `SetTint` no-ops when set. No other
  gameplay change.
- `Formation.cs`: added read-only `HasMoveDestination` property and
  `OnFacingSnapped` event (invoked at the end of `IssueFace`). No behavior change.
- `SoldierFactory.cs`: sets `suppressTint = true` on soldiers that received the
  Roman visual (before `Init`, so the renderer-wide white MPB is never applied).
- `RomanLegionaryVisualController.cs`: rewritten. Per-slot MPB tinting
  (faction cloth swap on the `MAT_Roman_ClothRed` slot only — blue team gets
  (0.10, 0.21, 0.55); red team keeps the palette red), combined
  damage-darkening × hit-flash × faction color, event-driven (no per-frame
  material work, renderer + slot colors cached at Start). Animation role
  driver: `Loco` int + `LocoScale`, attack/death variant selection, pivot hook,
  hit-during-attack suppression via state tag. Details in
  `ROMAN_LEGIONARY_ANIMATION_SPEC.md`.
- The old "Romans are never team-recolored" decision (integration spec §5) is
  superseded by this task's explicit red/blue faction requirement, since both
  teams currently share the legionary model.

### Blender

- New file `RomanCharacterSystem_v012_AnimationRework.blend`; v011 untouched on
  disk as rollback. Legacy actions (March, SwordAttack, Hit, Pivot) archived in
  v012 but excluded from export. Old death kept, renamed
  `ANIM_Roman_Death_BackOrSide`.
- Authored: RearRankIdle, FrontRankGuard, CombatAdvance, CloseRanksShuffle,
  Attack_Thrust, Attack_OverShield, HitLight, Death_ShieldSide, PivotStep;
  FormationMarch is a damped fcurve revision of the Meshy walk. All in place,
  30 fps, quaternion keys, loops close (first = last key), key poses verified
  numerically against the pose bank after a keying-pipeline bug was found and
  fixed (see traps below).
- Review cameras `CAM_AnimReview_*` added to the scene; 30 captures written to
  `docs/art/reviews/renders/legionary_animation_rework/`.
- **Traps hit (recorded for the future):** (1) the armature was left with
  `pose_position='REST'` in the open session — actions evaluated but the mesh
  never deformed; (2) Blender 5 slotted actions: assigning
  `animation_data.action` without `action_slot` evaluates nothing; (3) once an
  `action_slot` is assigned, `view_layer.update()` re-evaluates the action at
  the current frame and stomps manually staged poses — keys must be inserted
  with the slot unassigned (assign it after all keys are in).

### FBX / Unity

- `RomanLegionary_Basic.fbx` re-exported (13 takes; −Z forward, Y up,
  FBX_SCALE_UNITS, no leaf bones, all actions baked). Previous FBX backed up at
  `ArtSource/Blender/Romans/Archive/RomanLegionary_Basic_v011_backup.fbx`.
- Import: all 13 clips configured (loop flags per spec; root rotation + XZ
  locked to pose, Y original). Material name-remap to shared `MAT_Roman_*.mat`
  preserved automatically (externalObjects untouched).
- `AC_RomanInfantry.controller` rebuilt in place (same GUID — prefab reference
  intact): 13 states, 8 parameters, generated transition mesh; Death from
  AnyState with no exit. Validated: 0 states missing motion, prefab loads,
  6/6 material slots resolved, visual controller present.

## Unresolved issues

- Runtime behavior not play-tested (per instructions). Acceptance gate:
  `ROMAN_LEGIONARY_ANIMATION_MANUAL_TESTS.md`.
- Over-shield strike reads as a compact diagonal cut, not a tall chop; review
  in battle.
- Minor foot sliding possible in CombatAdvance/Shuffle when formation speed
  varies (upper-body stability prioritized).
- Rear-rank mechanics: the existing acquire radii already restrict unengaged
  rear soldiers to ~3 m fight-back range, so no mechanical change was needed
  and none was made; attack animations fire only on real `OnAttack` events.
- Backward-move facing flip (soldiers turn around to walk "backward") is a
  pre-existing design issue, documented, not changed.
- `graphify-out/` knowledge graph not refreshed after these changes
  (`/graphify . --update` pending).

## Compilation result

- Standalone Roslyn compile of Assembly-CSharp sources: **exit 0, no errors**.
- In-editor: `Assembly-CSharp.dll` rebuilt after all script edits (mtime newer
  than every changed file); Editor.log shows **zero `error CS` after the last
  assembly reload** (the 12 historical FormationTapRadius errors are V1.2-era
  mid-edit transients from before this task). Unity Console: 0 errors,
  1 benign MCP websocket notice. No Play Mode entered. No commit/push made.
