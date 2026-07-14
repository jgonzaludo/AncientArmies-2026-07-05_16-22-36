# Roman Archer — Overnight Production Log

Started: 2026-07-13 ~02:15. End-to-end archer production (Meshy → Blender →
Unity) + spacing increase + closer camera zoom + legionary attack validation +
compile + manual test docs. No commits, no pushes, no Play Mode.

## Preflight (02:10)

- Repo: `/Users/josephgonzaludo/AncientArmies/AncientArmies`, branch `v1-prototype`.
- Pre-existing modified (from the completed legionary animation rework, NOT
  reverted): `.gitignore`, `Assets/Scenes/Battle.unity`, `Soldier.cs`,
  `Formation.cs`, `SoldierFactory.cs`. Pre-existing untracked: `.agents/`,
  `ArtSource/`, `Assets/Art(.meta)`, `RomanLegionaryVisualController.cs(.meta)`,
  `docs/art/`, `skills-lock.json`.
- References confirmed on disk: `docs/art/references/archer_full_reference_sheet.png`
  (2.1 MB), `archer_turnaround.png` (1.7 MB). Originals untouched.
- Meshy key in `.env` ✓, venv `~/.venvs/meshy` ✓, Blender MCP up (v012 blend
  open) ✓, Unity MCP responsive ✓, compile clean (Roslyn exit 0 + clean
  Editor.log after last reload) ✓.

## Assumptions (recorded per instructions)

1. The task brief was truncated after the legionary over-shield strike section
   (~Section 30). Conventions for the remaining sections are carried from the
   completed legionary production: thrust rules per the shared-stance +
   shield-hard-limit sections, FBX/A nimator/report conventions per the
   legionary end-to-end + animation rework reports, and a final consolidated
   manual-test document.
2. The existing `Projectile` already flies a ballistic arc (parabolic, height
   `clamp(dist*0.22, 0.6, 3.5)`), so `ANIM_Archer_Attack_HighArc` is
   connectable for long shots without new ballistics: HighArc = visual variant
   for long-range shots, DirectShot otherwise; same gameplay projectile.
3. Release-frame integration follows the brief's sanctioned path: gameplay
   validates the shot at attack start (authoritative), the visual controller
   fires the projectile at the clip's release frame via a small deferred-spawn
   hook on `Soldier`; capsule archers and interrupted/dead archers fall back
   to immediate/instant release so balance is preserved for all cases.
4. No projectile pool exists; per brief, none is built. The primitive-sphere
   arrow is replaced by one shared low-poly arrow mesh + shared material
   (no per-shot materials — same cost profile as the current primitive path).
5. Archer body: generated as its own mesh (Meshy multi-image), targeting the
   same 24-bone Mixamo-style hierarchy via Meshy auto-rig =
   "separate body, shared-compatible rig" (decision documented after rig
   inspection).

## Parallel tracks dispatched (02:20)

- **Track A (subagent):** Meshy crops + contact sheet + 4 candidates →
  `ArtSource/Generated/Meshy/RomanArcher/`.
- **Track B (subagent):** spacing 0.95→1.05 / 1.7→1.85 (C# + Battle.unity via
  editor MCP, both fields explicitly saved) + camera min-zoom reduction +
  `FORMATION_SPACING_ADJUSTMENT_REPORT.md` + `CAMERA_CLOSE_ZOOM_REPORT.md`.
- **Track C (subagent):** numeric validation of
  `ANIM_Roman_Attack_Thrust` / `ANIM_Roman_Attack_OverShield` against the
  shared-stance + shield-hard-limit spec; minimal patches only on measured
  failures; saves as v013 only if patched; no FBX export (main session owns
  export serialization).

(Sections below appended as phases complete.)

## Spacing & camera (gameplay-tuning agent)

- Formation spacing +~10%: `meleeSpacing` 0.95 → 1.05 (+10.5%), `archerSpacing`
  1.7 → 1.85 (+8.8%). Changed in both `BattleSetup.cs` defaults and the
  serialized `Battle.unity` values (MCP execute_code → SetDirty → SaveScene;
  verified on disk, lines 331-332). All consumers (BuildSlots, footprint/touch
  targets, auto-close, reform, rotation) derive from `Formation.spacing` at
  rebuild time — no other tuning touched.
- Closer zoom: `BattleCamera.zoomMin` 8 → 5.5 (−31%), `zoomMax` 36 and all
  smoothing unchanged. BattleCamera is runtime-added (not scene-serialized),
  so the C# default is authoritative. Static clipping check: ortho zoom never
  moves the camera; near plane 0.3 vs ~48+ units to nearest geometry — safe.
- Compile gate: static Roslyn compile of all 15 Assembly-CSharp sources — PASS
  (exit 0).
- Details: `FORMATION_SPACING_ADJUSTMENT_REPORT.md`, `CAMERA_CLOSE_ZOOM_REPORT.md`.

## Legionary attack validation

2026-07-13 — Blender-side validation of ANIM_Roman_Attack_Thrust and
ANIM_Roman_Attack_OverShield in RIG_RomanLegionary against the attack spec.
All measurements numeric (world-space pose bone matrices, pose_position=POSE,
slotted-action evaluation). LeftHand used as shield proxy, RightHand as sword
proxy. Forward = -Y, up = +Z.

### Measurements (after patch)

| Rule | Thrust | OverShield | Verdict |
|---|---|---|---|
| Guard first-vs-last bone delta | 0.000° / 0.00 mm | 0.000° / 0.00 mm | PASS both |
| Root motion (max Hips drift) | 0.0 mm | 0.0 mm | PASS both |
| Shield horiz displacement (≤ ~6 cm) | 5.3 cm (was 15.0 cm) | 6.6 cm | PASS / marginal PASS |
| Shield rotation (≤ ~10-15°) | 4.6° (was 13.0°) | 7.8° | PASS both |
| Shield motion < sword motion | 0.05 m vs 0.38 m | 0.09 m vs 0.65 m | PASS both |
| Sword behind head (> head-Y + 5 cm) | prep peaks +19.3 cm at waist height (guard baseline is +11.7 cm) | never exceeds guard baseline; peak hand z 1.00 m, below head (1.16 m) | PASS by intent (see note) |
| Torso rotation (thrust ≤ ~12°) | 4.6° (was 13.0°) | 7.8° | PASS both |
| Duration | 20 f = 0.67 s (own 0.55-0.8 s budget) | 26 f = 0.87 s (was 22 f), impact key at f13 | PASS both |
| OverShield descent angle at impact | n/a | 48.4° (f11→f13); 38-60° through strike window | PASS (35-55° spec) |
| No full overhead chop | n/a | hand never above head or behind head plane during swing | PASS |

Note on behind-head rule: the shared guard pose itself chambers the sword hand
+11.7 cm past the head-Y plane (at waist height, z≈0.68 vs head z≈1.16), so
the strict "head-Y + 5 cm" proxy fails even the mandated start/end pose. Judged
by intent (no overhead/behind-head wind-up): both clips pass — the thrust prep
is a waist-level pull-back 7.6 cm beyond guard; the OverShield wind stays
forward of the head plane and below head height throughout. No patch made to
the shared guard pose.

### Patches applied (v012 → v013)

1. Thrust shield sway fix: the 13° torso twist carried the shield 15 cm
   sideways. Blended Spine/Spine01/Spine02 quaternion keys at frames 4/9/13
   toward their frame-0 values (k=0.35, chosen by numeric sweep over
   k=0.40/0.35/0.30/0.25). Result: shield sway 5.3 cm, shield rot 4.6°, torso
   4.6°; sword impact still f9 with 38 cm travel and -0.23 m forward extent.
   Guard keys (f0/f20) untouched. Applied as direct fcurve value edits (no
   pose-stage keying, so no depsgraph-stomp risk).
2. OverShield retime 22 f → 26 f (0.73 s → 0.87 s, spec 24-28 f): scaled
   keyframe X and handles by 26/22 and snapped keys 0/6/11/16/22 →
   0/7/13/19/26 (strike lands on f13, spec impact ~13-14). Manual action
   frame range updated to 0-26.

Both clips fully re-verified numerically after patching (table above is
post-patch).

### Artifacts

- Saved as `ArtSource/Blender/Romans/RomanCharacterSystem_v013_AttackPolish.blend`
  (v012 left untouched; no FBX export — main session owns export).
- Review captures (EEVEE, 512 px, CAM_AnimReview_Front / CAM_AnimReview_Right):
  `docs/art/reviews/renders/legionary_animation_rework/validation_{thrust,overshield}_f*_{guard,prep,wind,contact,strike,recover}_{front,side}.png`
  (16 images).

## Legionary patch integration (main session, ~02:45)

- v013 patches (thrust shield-sway fix, over-shield retime 22→26 f) exported to
  `RomanLegionary_Basic.fbx` (13 takes; prior FBX backed up as
  `Archive/RomanLegionary_Basic_v012_backup.fbx`). Legacy actions removed
  in-memory only; v013 on disk keeps all 17.
- Unity reimported; clip ranges updated (OverShield 0–26); AC_RomanInfantry
  resolves all 13 motions (0 missing).

## Archer integration code (main session, ~02:40)

- `Soldier.cs`: `deferRangedRelease` flag + pending-shot storage;
  `ReleasePendingShot()` / `CancelPendingShot()`. Shot validated + charged at
  attack time exactly as before; only the spawn moment defers. Un-flagged
  (capsule) archers unchanged.
- `RomanArcherVisualController.cs`: legionary-pattern tinting + archer Loco
  roles (RearIdle/Walk/FiringReady/Shuffle/KnifeGuard), shot variant by range
  (high-arc past 60% of rangedRange — existing Projectile already flies a
  ballistic arc), hand-arrow show/hide at nock/release normalized times,
  release-exactly-once with immediate release on hit-interrupt and cancel on
  death.
- `SoldierFactory.cs`: ranged soldiers now load `VIS_Roman_Archer_Basic`
  (Resources) with the capsule fallback intact.
- Static Roslyn compile including all new code: exit 0.

## Archer asset pipeline (main session, ~06:20–07:30)

- Candidates A–D generated (~30 cr each), compared in a normalized Blender
  scene; **Candidate B selected** (146/170; best face family-match + safest
  bow separation). Review: `ROMAN_ARCHER_MESHY_CANDIDATE_REVIEW.md`.
- Remesh 22,485 tris (task in `remesh_candB_22k/`), auto-rig 24 bones —
  **identical bone set to the legionary** → "separate body, shared-compatible
  rig". Height exactly 1.875 m, feet at Z=0, faces −Y, armature scale 0.01
  preserved. Walking/running/death library clips fetched (5+5+3 cr).
- **Handedness discrepancy (documented):** both reference images put the bow
  in the character's RIGHT hand (sword at left hip, quiver over left
  shoulder); the written spec said left hand. Per reference-authority rules
  the images win — animations authored for bow-right / draw-left.
- Blender v001–v005 chain created. glTF import in the MCP exec context needed
  `disable_bone_shape=True` + a window override (importer bugs recorded).
- Per-face UV-centroid classification onto the 8 shared materials
  (face counts logged; ClothNeutral confined to fletching); rigid skinning:
  helmet 1829 / quiver 5592 / belt blade 477 / bow 432 verts.
- Props: `PROP_Archer_Arrow` (15 polys, wood/iron/neutral, own FBX),
  `PROP_Archer_Arrow_Hand` + `PROP_Archer_Knife_Hand` bone-parented to the
  LeftHand (draw hand), orientations baked at their display poses. The belt
  knife is fused/rigid to the hips, so knife combat shows a separate hand
  blade while the scabbard stays — documented approach.
- **Bow flex: not implemented** (time-boxed decision; the draw pose carries
  the read at game distance; string survived remesh but procedural string
  deformation was judged high-risk). Recorded as a limitation / follow-up.
- 13 clips authored/derived per spec; all key poses verified numerically
  (draw pose exact at f22, loops close at 0.00°, high-arc +30°, deaths end at
  head z 0.18 m / 0.50 m, walk in place). 30 captures in
  `docs/art/reviews/renders/archer_animation/`.
- FBX exports: `RomanArcher_Basic.fbx` (13 takes, 3.1 MB, same axis/scale
  conventions as the legionary) + `PROP_Archer_Arrow.fbx` (17 KB). Legacy
  Meshy actions excluded from export, archived in v005.
- `Projectile.cs` upgraded: loads the shared arrow prefab from Resources
  (sphere fallback preserved), orients the mesh along the flight path.
  Static Roslyn compile with all changes: exit 0.

## Unity integration completed (~09:10)

- Editor MCP session recovered (root cause of the outage: killing the plugin's
  own freshly-launched server wedged its relaunch state machine; a manual
  reconnect + domain reload restored it — recorded in memory for next time).
- `AC_RomanArcher.controller`: 13 states / 8 params; Loco-int pattern with
  KnifeDraw as the melee-mode entry transition; Shot/Melee state tags for the
  release and hit-suppression logic; Death from AnyState, no exits.
- `VIS_Roman_Archer_Basic.prefab` (Resources): Animator (Generic avatar —
  required `avatarSetup=CreateFromThisModel`, the one import default that
  differed from the legionary) + `RomanArcherVisualController`.
- `PROP_Archer_Arrow.prefab` (Resources) for the upgraded projectile visual.
- Validation: prefab loads via Resources, 8/8 material slots resolve to shared
  `MAT_Roman_*`, hand-arrow + knife props present, 0 states missing motion in
  BOTH controllers, editor recompiled all scripts (DLL 09:03), Console clean
  (single transient directory error from a fixed attempt).
- Battlefield decor added on request: `BattlefieldDecor.cs` — procedural
  mottled ground texture + ~700 sprite-tuft grass quads (0.25–0.45 m,
  deterministic, ~3 draw calls, no shadows/colliders), hooked into
  `BattleSetup.EnsureEnvironment`. Static compile exit 0.

## Final status

COMPLETE pending manual Play Mode acceptance
(`ROMAN_ARCHER_MANUAL_TESTS.md`). No Play Mode entered, no commits, no
pushes, no branch changes. `graphify-out/` refresh still pending.
