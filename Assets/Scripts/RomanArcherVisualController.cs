using System.Collections;
using UnityEngine;

// Presentation-only adapter for the Roman archer visual, following the
// RomanLegionaryVisualController pattern: per-slot MPB tinting (faction cloth
// swap, damage darkening, hit flash combined) plus gameplay-state → Animator
// translation. The archer adds the firing cycle: gameplay validates and
// charges the shot at attack time (Soldier stores it as a pending shot); this
// controller shows the hand arrow during the nock, and at the clip's release
// frame hides it and calls Soldier.ReleasePendingShot() so the projectile
// leaves the bow on the visible release. Interrupts release immediately
// (hit) or cancel (death) so gameplay balance is preserved. It never selects
// targets, moves the soldier, or applies damage.
public class RomanArcherVisualController : MonoBehaviour
{
    // Locomotion roles (Animator "Loco" int).
    private const int LocoRearIdle = 0;
    private const int LocoWalk = 1;
    private const int LocoFiringReady = 2;
    private const int LocoShuffle = 3;
    private const int LocoKnifeGuard = 4;

    private static readonly int LocoId = Animator.StringToHash("Loco");
    private static readonly int LocoScaleId = Animator.StringToHash("LocoScale");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    private static readonly int AttackVariantId = Animator.StringToHash("AttackVariant");
    private static readonly int HitId = Animator.StringToHash("Hit");
    private static readonly int DieId = Animator.StringToHash("Die");
    private static readonly int DeathVariantId = Animator.StringToHash("DeathVariant");
    private static readonly int PivotId = Animator.StringToHash("Pivot");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private static readonly Color BlueFactionCloth = new Color(0.10f, 0.21f, 0.55f);

    [Tooltip("Normalized time of the firing clips at which the arrow visually releases")]
    [SerializeField] private float releaseNormalizedTime = 0.66f;   // string release frame of both firing clips
    [Tooltip("Normalized time at which the hand arrow becomes visible (nock)")]
    [SerializeField] private float nockNormalizedTime = 0.2f;
    [Tooltip("Shots at more than this fraction of rangedRange use the high-arc clip")]
    [SerializeField] private float highArcRangeFraction = 0.6f;
    [Tooltip("Normalized time of the KnifeDraw clip at which the dagger visually leaves the sheath and appears in the hand")]
    [SerializeField] private float drawHandoffNormalizedTime = 0.45f;

    // Locomotion calibration (see RomanLegionaryVisualController): reference
    // speeds are the measured stance-foot ground speeds of each clip at 1x
    // playback; playback = actualSpeed / reference, clamped. Only Walk and
    // Shuffle bind LocoScale, so draws / releases / knife work never speed up.
    [SerializeField] private float walkReferenceSpeed = 0.66f;     // ANIM_Archer_FormationWalk
    [SerializeField] private float shuffleReferenceSpeed = 0.35f;  // ANIM_Archer_CloseRanksShuffle
    [SerializeField] private float minLocoPlayback = 0.6f;
    [SerializeField] private float maxLocoPlayback = 2.2f;

    private const float ShuffleMaxSpeed = 1.2f;   // m/s: faster corrections walk instead

    private Soldier soldier;
    private Formation formation;
    private Animator animator;
    private SkinnedMeshRenderer[] smrs;   // body + stowed dagger share the palette
    private MaterialPropertyBlock mpb;
    private Color[][] slotBase;           // per renderer, per material slot
    private GameObject handArrow;          // PROP_Archer_Arrow_Hand child, may be null
    private EquipmentVisualSlot dagger;    // sheathed belt dagger <-> hand pugio, may be null
    private Coroutine flashRoutine;
    private bool isMoving;
    private bool dead;
    private bool shotPendingVisual;        // waiting for the release frame
    private bool handArrowShown;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        mpb = new MaterialPropertyBlock();
        var t = transform.Find("PROP_Archer_Arrow_Hand");
        if (t == null)
        {
            foreach (var tr in GetComponentsInChildren<Transform>(true))
                if (tr.name == "PROP_Archer_Arrow_Hand") { t = tr; break; }
        }
        handArrow = t != null ? t.gameObject : null;
        if (handArrow != null) handArrow.SetActive(false);
        dagger = GetComponent<EquipmentVisualSlot>();
    }

    private void Start()
    {
        soldier = GetComponentInParent<Soldier>();
        if (soldier == null) { enabled = false; return; }
        formation = soldier.formation;
        soldier.OnAttack += OnAttack;
        soldier.OnHurt += OnHurt;
        soldier.OnDeath += OnDeath;
        if (formation != null) formation.OnFacingSnapped += OnPivot;
        soldier.suppressFallRotation = true;
        soldier.suppressTint = true;
        soldier.deferRangedRelease = true;   // projectile spawns on the release frame

        InitPalette();
        ApplyPalette(1f, 0f);

        animator.SetInteger(LocoId, LocoRearIdle);
        animator.Play("RearRankIdle", 0, Random.value);
        animator.speed = Random.Range(0.97f, 1.03f);
    }

    private void OnDestroy()
    {
        if (soldier != null)
        {
            soldier.OnAttack -= OnAttack;
            soldier.OnHurt -= OnHurt;
            soldier.OnDeath -= OnDeath;
            soldier.ReleasePendingShot();   // never swallow a validated shot
        }
        if (formation != null) formation.OnFacingSnapped -= OnPivot;
    }

    // Fires on LOD-in when the impostor layer re-activates VisualRoot (on the
    // first activation soldier is still null — Start owns setup). Damage taken
    // while imposted must be visible immediately, a hit flash interrupted by
    // the swap must not stick at white, and a draw interrupted by the swap
    // already released its shot — clear the stale firing-cycle visual state
    // before it re-shows the hand arrow.
    private void OnEnable()
    {
        if (soldier == null || dead) return;
        shotPendingVisual = false;
        handArrowShown = false;
        if (handArrow != null) handArrow.SetActive(false);
        ApplyPalette(Health01(), 0f);
    }

    private void Update()
    {
        if (dead || soldier == null || !soldier.Alive) return;

        float speed = soldier.Velocity.magnitude;
        if (speed > 0.35f) isMoving = true;
        else if (speed < 0.2f) isMoving = false;

        int loco = ComputeLoco(speed);
        animator.SetInteger(LocoId, loco);
        animator.SetFloat(LocoScaleId, LocoPlayback(loco, speed));

        if (shotPendingVisual) UpdateReleaseFrame();
        UpdateDaggerState(loco);
    }

    // Dagger visual state resolver. Gameplay decides melee mode (via the loco
    // role and the sidearm attack); this only times the sheath <-> hand
    // handoff so the two dagger representations are never both visible.
    // Self-correcting on interruptions: a hit or a target change mid-draw
    // resolves to Active while melee persists and back to Stowed the moment
    // the archer returns to bow work. Death freezes the state (dead guard in
    // Update). There is no return clip yet, so re-sheathing is an instant
    // handoff — documented in the patch notes.
    private void UpdateDaggerState(int loco)
    {
        if (dagger == null) return;
        var info = animator.GetCurrentAnimatorStateInfo(0);
        bool meleeNow = loco == LocoKnifeGuard || info.IsTag("Melee");
        if (meleeNow)
        {
            if (dagger.State == EquipmentVisualState.Stowed)
                dagger.SetState(EquipmentVisualState.Drawing);
            if (dagger.State == EquipmentVisualState.Drawing)
            {
                // handoff mid-draw, or immediately on arrival in a fighting
                // state (direct stab / guard without a completed draw)
                bool handoff =
                    (info.IsName("KnifeDraw") && info.normalizedTime >= drawHandoffNormalizedTime) ||
                    info.IsName("KnifeGuard") || info.IsName("KnifeStab");
                if (handoff) dagger.SetState(EquipmentVisualState.Active);
            }
        }
        else if (dagger.State != EquipmentVisualState.Stowed)
        {
            dagger.SetState(EquipmentVisualState.Stowed);
        }
    }

    // Stride matching against the clip's own measured reference speed.
    private float LocoPlayback(int loco, float speed)
    {
        float reference;
        switch (loco)
        {
            case LocoWalk: reference = walkReferenceSpeed; break;
            case LocoShuffle: reference = shuffleReferenceSpeed; break;
            default: return 1f;   // stationary loops don't bind LocoScale
        }
        return Mathf.Clamp(speed / Mathf.Max(0.05f, reference), minLocoPlayback, maxLocoPlayback);
    }

    private int ComputeLoco(float speed)
    {
        if (formation == null) return isMoving ? LocoWalk : LocoRearIdle;
        FormationState st = formation.State;
        var stats = formation.stats;

        // Melee fallback mode: an enemy is inside sidearm range.
        bool meleeThreat = soldier.NearestEnemyDist <= stats.rangedMinRange + 0.5f;
        if (meleeThreat && !isMoving) return LocoKnifeGuard;

        if (isMoving)
        {
            // Slow corrections shuffle; fast ones (reform rushes, auto-close)
            // use the full walk so feet keep up with the ground.
            bool shuffle = speed < ShuffleMaxSpeed &&
                           (st == FormationState.Reforming ||
                            (st == FormationState.Ordered && !formation.HasMoveDestination));
            return shuffle ? LocoShuffle : LocoWalk;
        }

        // Stationary with an enemy in bow range → firing-ready stance. Matches
        // gameplay: archers can shoot back in any state except Withdrawing /
        // Reforming (acquire radius 0 there), including Ordered and Broken.
        bool canShoot = st != FormationState.Withdrawing && st != FormationState.Reforming;
        if (canShoot && soldier.NearestEnemyDist <= stats.rangedRange * 1.1f)
            return LocoFiringReady;
        return LocoRearIdle;
    }

    // ---------------- firing cycle ----------------

    private void OnAttack()
    {
        if (dead || !soldier.Alive) return;
        if (!isActiveAndEnabled)
        {
            // Imposted: no release frame will ever come, and an unreleased
            // pending shot would freeze the archer (the pending lock never
            // clears) — the validated shot flies immediately instead,
            // matching the capsule fallback's timing.
            soldier.ReleasePendingShot();
            return;
        }
        var stats = formation != null ? formation.stats : null;
        bool melee = stats != null && soldier.NearestEnemyDist <= stats.rangedMinRange + 0.5f;
        if (melee)
        {
            // sidearm strike (gameplay already applied the damage)
            shotPendingVisual = false;
            if (handArrow != null) handArrow.SetActive(false);
            soldier.ReleasePendingShot();          // no bow shot rides along
            animator.SetInteger(AttackVariantId, 2);
            animator.SetTrigger(AttackId);
            return;
        }
        // Bow shot: variant by distance (high arc for long shots — the shared
        // projectile already flies a ballistic arc scaled by distance).
        bool highArc = stats != null &&
                       soldier.NearestEnemyDist > stats.rangedRange * highArcRangeFraction;
        animator.SetInteger(AttackVariantId, highArc ? 1 : 0);
        animator.SetTrigger(AttackId);
        shotPendingVisual = true;
        handArrowShown = false;
    }

    private void UpdateReleaseFrame()
    {
        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsTag("Shot"))
        {
            // Attack state never entered or already left (interrupt): fire now.
            if (info.IsTag("Melee") || !animator.IsInTransition(0)) return;
            return;
        }
        float nt = info.normalizedTime;
        if (!handArrowShown && nt >= nockNormalizedTime)
        {
            handArrowShown = true;
            if (handArrow != null) handArrow.SetActive(true);
        }
        if (nt >= releaseNormalizedTime)
        {
            FinishRelease();
        }
    }

    private void FinishRelease()
    {
        shotPendingVisual = false;
        handArrowShown = false;
        if (handArrow != null) handArrow.SetActive(false);
        soldier.ReleasePendingShot();
    }

    // ---------------- reactions ----------------

    private void OnHurt()
    {
        if (dead || !soldier.Alive) return;
        // dormant while imposted: the impostor layer flashes the quad instead
        // (no draw can be in progress — imposted attacks release immediately)
        if (!isActiveAndEnabled) return;
        // A hit cancels the visible draw; the already-validated shot flies
        // immediately so gameplay balance is unchanged.
        if (shotPendingVisual) FinishRelease();
        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsTag("Shot") && !info.IsTag("Melee")) animator.SetTrigger(HitId);

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());
    }

    private void OnDeath()
    {
        // Bookkeeping must run even while imposted: the dead flag gates every
        // other handler, the pending-shot cancel is a gameplay rule (the dying
        // archer's arrow is lost), and SetPropertyBlock works on inactive
        // renderers so the corpse is correctly darkened if it ever LODs in.
        dead = true;
        shotPendingVisual = false;
        if (handArrow != null) handArrow.SetActive(false);
        soldier.CancelPendingShot();
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        ApplyDeathTint();
        // only the animator part is display work the impostor layer replaces
        if (!isActiveAndEnabled) return;
        animator.SetInteger(DeathVariantId, Random.value < 0.5f ? 0 : 1);
        animator.SetTrigger(DieId);
    }

    private void OnPivot()
    {
        if (dead || soldier == null || !soldier.Alive) return;
        if (!isActiveAndEnabled) return;   // dormant while imposted
        if (formation.State != FormationState.Ordered || isMoving) return;
        animator.SetTrigger(PivotId);
    }

    // ---------------- per-slot tinting (legionary architecture) ----------------

    private void InitPalette()
    {
        if (smrs == null) return;
        slotBase = new Color[smrs.Length][];
        for (int r = 0; r < smrs.Length; r++)
        {
            var mats = smrs[r].sharedMaterials;
            slotBase[r] = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) { slotBase[r][i] = Color.white; continue; }
                Color c = mats[i].HasProperty(BaseColorId)
                    ? mats[i].GetColor(BaseColorId) : Color.white;
                if (mats[i].name.StartsWith("MAT_Roman_ClothRed") && soldier.team == Team.Blue)
                    c = BlueFactionCloth;
                slotBase[r][i] = c;
            }
            smrs[r].SetPropertyBlock(null);
        }
    }

    private void ApplyPalette(float hp01, float flash01)
    {
        if (smrs == null || slotBase == null) return;
        for (int r = 0; r < smrs.Length; r++)
            for (int i = 0; i < slotBase[r].Length; i++)
            {
                Color c = Color.Lerp(Color.Lerp(slotBase[r][i], Color.black, 0.6f), slotBase[r][i], hp01);
                if (flash01 > 0f) c = Color.Lerp(c, Color.white, flash01);
                mpb.SetColor(BaseColorId, c);
                smrs[r].SetPropertyBlock(mpb, i);
            }
    }

    private void ApplyDeathTint()
    {
        if (smrs == null || slotBase == null) return;
        for (int r = 0; r < smrs.Length; r++)
            for (int i = 0; i < slotBase[r].Length; i++)
            {
                mpb.SetColor(BaseColorId, Color.Lerp(slotBase[r][i], Color.black, 0.55f));
                smrs[r].SetPropertyBlock(mpb, i);
            }
    }

    private float Health01()
    {
        return formation != null
            ? Mathf.Clamp01(soldier.Health / formation.stats.maxHealth) : 1f;
    }

    private IEnumerator HitFlash()
    {
        ApplyPalette(Health01(), 1f);
        yield return new WaitForSeconds(0.08f);
        if (!dead) ApplyPalette(Health01(), 0f);
        flashRoutine = null;
    }
}

