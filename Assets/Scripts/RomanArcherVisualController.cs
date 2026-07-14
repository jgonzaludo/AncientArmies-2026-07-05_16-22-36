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

    private Soldier soldier;
    private Formation formation;
    private Animator animator;
    private SkinnedMeshRenderer smr;
    private MaterialPropertyBlock mpb;
    private Color[] slotBase;
    private GameObject handArrow;          // PROP_Archer_Arrow_Hand child, may be null
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
        smr = GetComponentInChildren<SkinnedMeshRenderer>();
        mpb = new MaterialPropertyBlock();
        var t = transform.Find("PROP_Archer_Arrow_Hand");
        if (t == null)
        {
            foreach (var tr in GetComponentsInChildren<Transform>(true))
                if (tr.name == "PROP_Archer_Arrow_Hand") { t = tr; break; }
        }
        handArrow = t != null ? t.gameObject : null;
        if (handArrow != null) handArrow.SetActive(false);
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

    private void Update()
    {
        if (dead || soldier == null || !soldier.Alive) return;

        float speed = soldier.Velocity.magnitude;
        if (speed > 0.35f) isMoving = true;
        else if (speed < 0.2f) isMoving = false;

        float refSpeed = formation != null ? Mathf.Max(0.5f, formation.stats.moveSpeed) : 2.6f;
        animator.SetFloat(LocoScaleId, Mathf.Clamp(speed / refSpeed, 0.6f, 1.4f));
        animator.SetInteger(LocoId, ComputeLoco());

        if (shotPendingVisual) UpdateReleaseFrame();
    }

    private int ComputeLoco()
    {
        if (formation == null) return isMoving ? LocoWalk : LocoRearIdle;
        FormationState st = formation.State;
        var stats = formation.stats;

        // Melee fallback mode: an enemy is inside sidearm range.
        bool meleeThreat = soldier.NearestEnemyDist <= stats.rangedMinRange + 0.5f;
        if (meleeThreat && !isMoving) return LocoKnifeGuard;

        if (isMoving)
        {
            bool shuffle = st == FormationState.Reforming ||
                           (st == FormationState.Ordered && !formation.HasMoveDestination);
            return shuffle ? LocoShuffle : LocoWalk;
        }

        // Stationary with a live target in bow range → firing-ready stance.
        bool combat = st == FormationState.Engaged || st == FormationState.Attacking;
        if (combat && soldier.NearestEnemyDist <= stats.rangedRange * 1.1f)
            return LocoFiringReady;
        return LocoRearIdle;
    }

    // ---------------- firing cycle ----------------

    private void OnAttack()
    {
        if (dead || !soldier.Alive) return;
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
        dead = true;
        shotPendingVisual = false;
        if (handArrow != null) handArrow.SetActive(false);
        soldier.CancelPendingShot();           // the dying archer's arrow is lost
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        ApplyDeathTint();
        animator.SetInteger(DeathVariantId, Random.value < 0.5f ? 0 : 1);
        animator.SetTrigger(DieId);
    }

    private void OnPivot()
    {
        if (dead || soldier == null || !soldier.Alive) return;
        if (formation.State != FormationState.Ordered || isMoving) return;
        animator.SetTrigger(PivotId);
    }

    // ---------------- per-slot tinting (legionary architecture) ----------------

    private void InitPalette()
    {
        if (smr == null) return;
        var mats = smr.sharedMaterials;
        slotBase = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) { slotBase[i] = Color.white; continue; }
            Color c = mats[i].HasProperty(BaseColorId)
                ? mats[i].GetColor(BaseColorId) : Color.white;
            if (mats[i].name.StartsWith("MAT_Roman_ClothRed") && soldier.team == Team.Blue)
                c = BlueFactionCloth;
            slotBase[i] = c;
        }
        smr.SetPropertyBlock(null);
    }

    private void ApplyPalette(float hp01, float flash01)
    {
        if (smr == null || slotBase == null) return;
        for (int i = 0; i < slotBase.Length; i++)
        {
            Color c = Color.Lerp(Color.Lerp(slotBase[i], Color.black, 0.6f), slotBase[i], hp01);
            if (flash01 > 0f) c = Color.Lerp(c, Color.white, flash01);
            mpb.SetColor(BaseColorId, c);
            smr.SetPropertyBlock(mpb, i);
        }
    }

    private void ApplyDeathTint()
    {
        if (smr == null || slotBase == null) return;
        for (int i = 0; i < slotBase.Length; i++)
        {
            mpb.SetColor(BaseColorId, Color.Lerp(slotBase[i], Color.black, 0.55f));
            smr.SetPropertyBlock(mpb, i);
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

