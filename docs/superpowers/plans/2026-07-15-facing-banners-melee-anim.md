# Facing Authority + Formation Banners + Legionary Melee Rework — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `Formation.AnchorRot` the single displayed facing authority (arrow + rotate UI follow it), replace floating name text with faction banners that track the dominant soldier cluster (and anchor Reform on it), and re-author the legionary's three melee attack clips as disciplined Roman shield-and-sword strikes with contact-frame damage timing.

**Architecture:** No new systems parallel to existing ones. Facing already lives in `Formation.AnchorRot` — Part 1 only makes the arrow and Rotate button read it faithfully. Clustering lives in `Formation` (it owns the soldier list; static core for editor validation, like `WheelStep`). The banner is a presentation component added by `BattleSetup.CreateFormation`, replacing `FormationLabel`. Melee variant choice + impact timing are gameplay-owned in `Soldier` (mirroring the archer `deferRangedRelease` pattern); `RomanLegionaryVisualController` only displays the chosen variant. Clips are re-authored in the Blender source (`RomanCharacterSystem_v015_AnimationQuality.blend` → save as v016) and re-exported over `RomanLegionary_Basic.fbx`.

**Tech Stack:** Unity 6 URP (runtime-built UI/visuals, no prefab UI), Blender via MCP (scripted keying per `blender-anim-authoring-traps` memory), Unity MCP for editor/animator/import work, static Roslyn compile fallback per `unity-compile-verification` memory.

## Global Constraints

* Do NOT touch the archer animation set, `AC_RomanArcher.controller`, or archer FBX.
* Do NOT change rear-rank idle, march, shuffle, hit, death, pivot clips unless a transition into the new guard breaks (Patch 3 audit says attacks already book-end on FrontRankGuard f0 — preserve that convention).
* Exactly three melee attack variants, default weights 50/25/25 (thrust/over-shield/diagonal), weights configurable.
* Attack durations: thrust 0.60–0.75 s, over-shield 0.65–0.80 s, diagonal 0.70–0.85 s. One-shots, no loop, no root motion, no scale keys.
* Damage fires at the authored contact frame (per-variant delay), never before/after the visible strike. Cooldown (1.4 s) and damage values unchanged.
* Banner clustering: no per-frame all-pairs work, no LINQ, no per-update allocations; interval-driven (~0.4 s) with smoothed visual motion; hysteresis so a challenger must be meaningfully larger (default 1.3×) to steal dominance.
* One banner per formation; never blocks raycasts/taps; hidden when the formation is destroyed or before it spawns; correct after Restart (scene reload recreates everything).
* Blender traps (memory `blender-anim-authoring-traps`): `pose_position='POSE'`; author with `action_slot=None`, keyframe immediately after posing (no depsgraph update between), assign `slots[0]` after all keys; verify numerically, not just renders.
* Scene gotcha (memory `unity-compile-verification`): `Battle.unity` serializes existing `BattleSetup`/`Formation` fields — NEW serialized fields fall back to C# defaults (fine); never edit scene YAML on disk while the editor has it open.
* Player control stays formation-level; no scope beyond this patch.

---

### Task 1: Facing authority — Formation API + Rotate gating + arrow rework

**Files:**
- Modify: `Assets/Scripts/Formation.cs` (add `IsAutoFacing`, `RotateBlock`, `GetRotateBlock()`)
- Modify: `Assets/Scripts/PlayerCommander.cs` (`CanEnterRotateMode`, mid-rotate cancel)
- Modify: `Assets/Scripts/FormationArrow.cs` (read `AnchorForward`, hide while broken)
- Modify: `Assets/Editor/FacingValidation.cs` (report the new authority fields in Play Mode)

**Interfaces:**
- Produces: `Formation.IsAutoFacing : bool`, `enum RotateBlock { None, AutoFacing, Broken, Busy, Destroyed }`, `Formation.GetRotateBlock() : RotateBlock`. Task 7 (HUD text) and manual tests consume these.

- [ ] **Step 1: Add the authority API to Formation.cs** (after the `IsSelected` property block):

```csharp
// Facing authority (this patch): AnchorRot is the single authoritative facing —
// directional damage, slots, soldier idle facing, and the ground arrow all read
// it. IsAutoFacing marks the states where combat, not the player, steers it.
public bool IsAutoFacing =>
    soldiers.Count > 0 &&
    ((State == FormationState.Attacking && attackTarget != null) ||
     State == FormationState.Engaged);

public enum RotateBlock { None, AutoFacing, Broken, Busy, Destroyed }

// Why the Rotate command is currently unavailable (None = available).
public RotateBlock GetRotateBlock()
{
    if (soldiers.Count == 0) return RotateBlock.Destroyed;
    if (State == FormationState.BrokenRanks) return RotateBlock.Broken;
    if (IsAutoFacing) return RotateBlock.AutoFacing;
    if (State != FormationState.Ordered) return RotateBlock.Busy;
    return RotateBlock.None;
}
```

- [ ] **Step 2: Gate rotate mode on the authority API in PlayerCommander.cs**:

```csharp
public bool CanEnterRotateMode =>
    selection.Count == 1 &&
    selection[0].GetRotateBlock() == Formation.RotateBlock.None;
```

And in `PruneSelection()` replace the last line with a full authority check so acquiring a target / engaging / breaking mid-rotate cancels the preview:

```csharp
if (RotateMode &&
    (selection.Count != 1 ||
     selection[0].GetRotateBlock() != Formation.RotateBlock.None))
    ExitRotateMode();
```

- [ ] **Step 3: Rework FormationArrow.cs** — authoritative direction, hidden while broken:

```csharp
private void LateUpdate()
{
    if (f == null || arrow == null) return;
    // The arrow is the formation's authoritative facing readout: it always
    // points along AnchorForward (what directional damage and slots use).
    // Broken ranks have no meaningful formation facing — no arrow at all.
    bool show = f.IsSelected && f.soldiers.Count > 0 &&
                f.State != FormationState.BrokenRanks;
    if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
    if (!show) return;

    Vector3 heading = f.AnchorForward;
    arrow.position = f.AnchorPos + heading * (f.BoundingRadius * 0.55f + 1.0f)
                     + Vector3.up * 0.1f;
    arrow.rotation = Quaternion.LookRotation(heading, Vector3.up);
}
```

(AnchorRot is already rotated smoothly by `RotateTowards` in chase/engaged paths, so the arrow inherits the same smoothed turn — no separate smoothing.)

- [ ] **Step 4: Compile** (static Roslyn fallback if the editor naps). Expected: exit 0.
- [ ] **Step 5: Commit** `feat: authoritative facing — arrow reads AnchorForward, rotate gated by RotateBlock`

### Task 2: Dominant-group clustering in Formation + Reform rally anchor

**Files:**
- Modify: `Assets/Scripts/Formation.cs`
- Create: `Assets/Editor/ClusterValidation.cs`

**Interfaces:**
- Produces: `Formation.DominantGroupCenter : Vector3`, `Formation.DominantGroupCount : int` (interval-refreshed), `static Formation.ComputeDominantGroup(List<Vector3> positions, float linkDist, Vector3 prevCenter, bool hasPrev, float switchFactor, out int count) : Vector3` (pure, validation-friendly like `WheelStep`). Task 3's banner and `IssueReform` consume these.

- [ ] **Step 1: Add tuning fields + cached state to Formation.cs**:

```csharp
[Header("Dominant group (banner + reform rally)")]
[Tooltip("Soldiers closer than this multiple of formation spacing are the same local group")]
public float clusterLinkFactor = 2.5f;
[Tooltip("Seconds between dominant-group recomputes (banner interpolates in between)")]
public float clusterInterval = 0.4f;
[Tooltip("A rival group must outnumber the current dominant group by this factor to take the banner")]
public float clusterSwitchFactor = 1.3f;

public Vector3 DominantGroupCenter { get; private set; }
public int DominantGroupCount { get; private set; }

private float clusterTimer;
private bool hasDominantCenter;
private static readonly List<Vector3> clusterScratch = new List<Vector3>(64);
private static int[] clusterParent = new int[64];
private static int[] clusterSize = new int[64];
```

- [ ] **Step 2: Implement the static union-find clustering core** (proximity connected components — ≤50 soldiers/formation, ~1250 sqr-distance checks per recompute per formation, every 0.4 s; no allocations beyond the grow-only static arrays):

```csharp
// Connected-components over the proximity graph (union-find). Returns the
// centroid of the winning cluster. Hysteresis: the cluster nearest prevCenter
// (the incumbent) keeps the banner unless a rival is switchFactor times larger.
// Static + list-driven so editor validation can exercise splits directly.
public static Vector3 ComputeDominantGroup(List<Vector3> positions, float linkDist,
                                           Vector3 prevCenter, bool hasPrev,
                                           float switchFactor, out int count)
{
    int n = positions.Count;
    count = 0;
    if (n == 0) return prevCenter;
    if (clusterParent.Length < n)
    {
        clusterParent = new int[Mathf.NextPowerOfTwo(n)];
        clusterSize = new int[Mathf.NextPowerOfTwo(n)];
    }
    for (int i = 0; i < n; i++) clusterParent[i] = i;
    float link2 = linkDist * linkDist;
    for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        {
            Vector3 d = positions[i] - positions[j];
            d.y = 0f;
            if (d.sqrMagnitude <= link2) Union(i, j);
        }
    for (int i = 0; i < n; i++) clusterSize[i] = 0;
    for (int i = 0; i < n; i++) clusterSize[Find(i)]++;

    // incumbent root: cluster of the member nearest prevCenter, if close enough
    int incumbentRoot = -1;
    if (hasPrev)
    {
        float best = linkDist * linkDist * 4f;   // within 2 links of the old center
        for (int i = 0; i < n; i++)
        {
            Vector3 d = positions[i] - prevCenter;
            d.y = 0f;
            float d2 = d.sqrMagnitude;
            if (d2 < best) { best = d2; incumbentRoot = Find(i); }
        }
    }
    int largestRoot = 0;
    for (int i = 0; i < n; i++)
        if (clusterSize[Find(i)] > clusterSize[Find(largestRoot)]) largestRoot = i;
    largestRoot = Find(largestRoot);

    int winner = largestRoot;
    if (incumbentRoot >= 0 && incumbentRoot != largestRoot &&
        clusterSize[largestRoot] < clusterSize[incumbentRoot] * switchFactor)
        winner = incumbentRoot;

    Vector3 c = Vector3.zero;
    for (int i = 0; i < n; i++)
        if (Find(i) == winner) { c += positions[i]; count++; }
    c /= Mathf.Max(1, count);
    c.y = 0f;
    return c;

    int Find(int x)
    {
        while (clusterParent[x] != x)
        { clusterParent[x] = clusterParent[clusterParent[x]]; x = clusterParent[x]; }
        return x;
    }
    void Union(int a, int b)
    {
        a = Find(a); b = Find(b);
        if (a != b) clusterParent[b] = a;
    }
}
```

- [ ] **Step 3: Interval driver + on-demand refresh in Formation.cs** (call `UpdateDominantGroup()` from `Update()` after `UpdateEngagement()`):

```csharp
private void UpdateDominantGroup(bool force = false)
{
    clusterTimer -= Time.deltaTime;
    if (!force && clusterTimer > 0f) return;
    clusterTimer = clusterInterval;
    clusterScratch.Clear();
    foreach (var s in soldiers) clusterScratch.Add(s.transform.position);
    DominantGroupCenter = ComputeDominantGroup(
        clusterScratch, spacing * clusterLinkFactor,
        DominantGroupCenter, hasDominantCenter, clusterSwitchFactor, out int c);
    DominantGroupCount = c;
    hasDominantCenter = c > 0;
}
```

- [ ] **Step 4: Reform rallies on the dominant group.** In `IssueReform()`, replace the mean-of-all-soldiers block with a forced fresh dominant-group computation (the anchor is computed once here and never re-derived mid-reform — stable rally point):

```csharp
UpdateDominantGroup(force: true);
AnchorPos = DominantGroupCenter;
```

- [ ] **Step 5: Editor validation** `Assets/Editor/ClusterValidation.cs` covering: 40/10 split (banner with the 40), 26/24 (hysteresis holds incumbent), 25/25, 35/15, incumbent shrinking below rival×1.3 (switches), single soldier, empty list. Menu item `Ancient Armies/Validate Dominant Group`, same shape as `FacingValidation`.

- [ ] **Step 6: Compile + run validation via MCP** — expect all scenario PASS lines.
- [ ] **Step 7: Commit** `feat: dominant-group clustering owns banner tracking and reform rally`

### Task 3: Formation banners replace text labels

**Files:**
- Create: `Assets/Scripts/FormationBanner.cs`
- Delete: `Assets/Scripts/FormationLabel.cs` (+ .meta)
- Modify: `Assets/Scripts/BattleSetup.cs` (`CreateFormation` adds `FormationBanner` instead of `FormationLabel`)

**Interfaces:**
- Consumes: `Formation.DominantGroupCenter/Count` (Task 2), `Formation.State`, `f.team`, `f.stats.isRanged`, `AnchorPos`.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write FormationBanner.cs.** One banner per formation: billboarded pennant quad (faction color) + white icon quad (procedural sword / bow-and-arrow texture, static-cached per type), no colliders, ~5 m up. Target = `AnchorPos` while structured (Ordered/Attacking/Withdrawing/Reforming), `DominantGroupCenter` while `Engaged`/`BrokenRanks`; exponential smoothing (`1 - exp(-3 dt)`); instant hide at `soldiers.Count == 0`. Icon textures drawn per-pixel (blade+guard+hilt silhouette; arc+string+arrow silhouette) — full code in implementation.
- [ ] **Step 2: Swap component in `CreateFormation`** (`go.AddComponent<FormationBanner>();`), delete `FormationLabel.cs`.
- [ ] **Step 3: Compile.**
- [ ] **Step 4: Play Mode check via MCP**: banners visible per formation, colored/iconed correctly, no console errors, taps still select (no raycast interference), banner gone after a formation wipes, no duplicates after Restart.
- [ ] **Step 5: Commit** `feat: floating faction banners replace formation text labels`

### Task 4: Melee variant selection + contact-frame damage (gameplay side)

**Files:**
- Modify: `Assets/Scripts/Soldier.cs`
- Modify: `Assets/Scripts/RomanLegionaryVisualController.cs`

**Interfaces:**
- Produces: `Soldier.deferMeleeImpact : bool` (visual controller sets true), `Soldier.MeleeAttackVariant : int` (0 thrust / 1 over-shield / 2 diagonal; visual controller reads in `OnAttack`). Impact delays start at authored contact fractions and are corrected in Task 6 from the final clips.

- [ ] **Step 1: Soldier.cs — deferred melee impact.** Add fields:

```csharp
[System.NonSerialized] public bool deferMeleeImpact;   // set by animated melee visuals
public int MeleeAttackVariant { get; private set; }
[Tooltip("Relative weights: thrust / over-shield / diagonal slash")]
[SerializeField] private Vector3 meleeVariantWeights = new Vector3(0.5f, 0.25f, 0.25f);
// seconds from attack commit to the authored contact frame, per variant
private static readonly float[] MeleeImpactDelay = { 0.30f, 0.40f, 0.43f };
private Soldier pendingMeleeTarget;
private float pendingMeleeDamage;
private float pendingMeleeTimer;
private int lastVariant = -1, prevVariant = -1;
```

Replace the instant-melee branch in `Update()`; commit picks the variant, charges cooldown and lock as today, damage lands `MeleeImpactDelay[variant]` later (target/damage/direction multiplier captured at commit, exactly like the archer's pending shot). Tick the timer at the top of `Update()`; clear pending on death. `PickMeleeVariant()` = weighted roll, one re-roll if it would be the third identical in a row.

- [ ] **Step 2: RomanLegionaryVisualController.cs** — in `Start()`: `soldier.deferMeleeImpact = true;`. In `OnAttack()` replace the 60/40 random with:

```csharp
animator.SetInteger(AttackVariantId, soldier.MeleeAttackVariant);
animator.SetTrigger(AttackId);
```

- [ ] **Step 3: Compile + quick Play Mode sanity** (capsule fallback scenes unaffected: `deferMeleeImpact` stays false there → instant damage as before).
- [ ] **Step 4: Commit** `feat: gameplay-owned melee attack variants with contact-frame damage`

### Task 5: Re-author the three legionary attack clips in Blender

**Files:**
- Create: `ArtSource/Blender/Romans/RomanCharacterSystem_v016_MeleeAttackRework.blend` (from v015)
- Modify (replace): FBX export → `Assets/Art/Characters/Romans/Legionary/Models/RomanLegionary_Basic.fbx` (archive previous as `RomanLegionary_Basic_pre_v016_backup.fbx` outside Resources)

**Interfaces:**
- Produces: actions `ANIM_Roman_Attack_Thrust` (re-authored, ~20 f @30 fps), `ANIM_Roman_Attack_OverShield` (re-authored, ~22 f), `ANIM_Roman_Attack_DiagonalSlash` (new, ~24 f); all begin/end exactly on the FrontRankGuard f0 pose; no root motion, no scale keys; contact frames ≈ thrust f9, over-shield f12, slash f13.

- [ ] **Step 1:** Open v015 via Blender MCP, save-as v016, set `pose_position='POSE'`, capture pose bank (GUARD from FrontRankGuard f0, plus per-clip working poses).
- [ ] **Step 2:** Author each clip with the verified pattern (action → `action_slot=None` → pose+`keyframe_insert` per key with no depsgraph update between → assign `slots[0]` last). Key sheets: thrust f0 guard / f4 shield-lane + chamber / f9 straight extension (impact) / f14 retract / f19 guard; over-shield f0 / f6 sword raised above shield's upper-right / f12 downward strike (impact) / f17 recover / f21 guard; slash f0 / f4 lane opens / f8 high-right prep / f13 down-left cross (impact) / f19 recover / f23 guard. Prohibited-motion list from the spec is the review rubric.
- [ ] **Step 3:** Verify numerically: first/last-frame bone deviation vs FrontRankGuard f0 ≈ 0°, hips horizontal excursion ≈ 0, no scale fcurves; render guard/anticipation/impact/recovery stills (front + three-quarter, isometric-like elevation) into `docs/art/reviews/renders/patch_6_melee_attacks/` and inspect against the spec.
- [ ] **Step 4:** Export FBX with the same conventions as previous exports (bake anims, no leaf bones — match v014/v015 exporter settings recorded in the blend), overwrite `RomanLegionary_Basic.fbx`, archive prior FBX.
- [ ] **Step 5:** Commit blend + FBX + renders.

### Task 6: Unity import, animator third variant, impact-delay truing

**Files:**
- Modify: `Assets/Art/Characters/Romans/Legionary/Models/RomanLegionary_Basic.fbx.meta` (clip list gains `ANIM_Roman_Attack_DiagonalSlash`, loopTime 0) — via ModelImporter through Unity MCP
- Modify: `Assets/Art/Characters/Romans/Legionary/Animations/AC_RomanInfantry.controller` — add `AttackDiagonalSlash` state (tag `Attack`, no LocoScale binding), transitions mirroring `AttackThrust` with `AttackVariant == 2`
- Modify: `Assets/Scripts/Soldier.cs` (`MeleeImpactDelay` corrected to authored contact frames)

- [ ] **Step 1:** Import FBX; add the new clip to `clipAnimations` (copy settings from the existing attack clips: loopTime false, resample curves as before); verify 14 legionary clips, avatar `CreateFromThisModel`, prefab mesh references intact.
- [ ] **Step 2:** Extend the animator via editor scripting (UnityEditor.Animations through MCP `execute_code`): new state `AttackDiagonalSlash`, motion = new clip, replicate every inbound transition of `AttackThrust` with condition `AttackVariant Equals 2`, replicate its exit-time outbound transitions, same tag. Run `Ancient Armies/Validate Facing Pipeline` — expect no BAD BINDING / MISSING MOTION.
- [ ] **Step 3:** True up `MeleeImpactDelay` from the final clip lengths/contact frames (frame/30 ÷ clip seconds → seconds from commit).
- [ ] **Step 4:** Compile + commit.

### Task 7: HUD rotate-state polish, Play Mode test pass, docs

**Files:**
- Modify: `Assets/Scripts/BattleHUD.cs` (rotate button reflects `RotateBlock`; greyed styling already exists via `SetButtonEnabled`)
- Create: `docs/patches/PATCH_6_FACING_BANNERS_MELEE_ATTACKS.md` (+ manual test doc)
- Modify: `CLAUDE.md` current-state blurb only if asked — otherwise patch doc records scope.

- [ ] **Step 1:** `UpdateRotateButton()` — keep CANCEL/ROTATE label; disabled state driven by `commander.CanEnterRotateMode || commander.RotateMode` (now authority-aware from Task 1). No extra UI text needed — button greys for AutoFacing/Broken/Busy/Destroyed alike; states exist explicitly on `Formation.GetRotateBlock()`.
- [ ] **Step 2: Play Mode test sweep via MCP** (SimulateTap/SimulateCommandDrag hooks + console + screenshots): the spec's facing tests 1–15, banner split tests (drive splits by breaking ranks and issuing moves in TestSkirmish/Battle), animation checks at both zooms and multiple world facings. Record pass/fail per item.
- [ ] **Step 3:** Write patch doc + manual test suite; refresh graphify (`/graphify . --update`); final commit.

## Self-Review

- Spec coverage: Part 1 → Tasks 1, 7; Part 2 → Tasks 2, 3; Part 3 → Tasks 4, 5, 6; Part 4 expectations embedded per task; Parts 5–6 → Task 7 sweep. Rotate-button "explicit states" satisfied by `RotateBlock` enum + existing disabled styling.
- Known judgment calls: arrow stays selection-gated (existing behavior; spec silent); banner tracks anchor while structured, cluster while Engaged/Broken; melee variant + timing owned by gameplay (project convention "gameplay decides, visuals display").
- Types cross-checked: `RotateBlock` referenced identically in Tasks 1/7; `DominantGroupCenter/Count` in Tasks 2/3; `MeleeAttackVariant`/`deferMeleeImpact` in Tasks 4/6.
