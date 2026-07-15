using System.Collections;
using UnityEngine;

// Presentation-only adapter for the Roman legionary visual. Two jobs:
//
// 1. Tinting. The fused skinned mesh has six shared MAT_Roman_* slots; a
//    renderer-level MaterialPropertyBlock would stomp _BaseColor on all of
//    them (the historical "all-silver" bug). This controller instead applies
//    per-slot MPBs: every slot keeps its palette color, only the faction
//    cloth slot (tunic + shield face) is swapped to the team color, and
//    damage darkening / hit flash / death darkening are combined into the
//    final per-slot color so the systems never erase each other.
//
// 2. Animation roles. Gameplay decides everything (targets, damage, movement,
//    formation state); this script only translates that state into Animator
//    parameters: a locomotion role int (rear-rank idle, march, front guard,
//    combat advance, close-ranks shuffle, broken idle/run), one-shot triggers
//    for attacks / hit / death / pivot, and a playback-scale float so stride
//    roughly matches the gameplay move speed. It never moves the soldier.
public class RomanLegionaryVisualController : MonoBehaviour
{
    // Locomotion roles (Animator "Loco" int). Gameplay-derived, animator-displayed.
    private const int LocoRearIdle = 0;
    private const int LocoMarch = 1;
    private const int LocoGuard = 2;
    private const int LocoAdvance = 3;
    private const int LocoShuffle = 4;
    private const int LocoBrokenIdle = 5;
    private const int LocoBrokenRun = 6;

    private static readonly int LocoId = Animator.StringToHash("Loco");
    private static readonly int LocoScaleId = Animator.StringToHash("LocoScale");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    private static readonly int AttackVariantId = Animator.StringToHash("AttackVariant");
    private static readonly int HitId = Animator.StringToHash("Hit");
    private static readonly int DieId = Animator.StringToHash("Die");
    private static readonly int DeathVariantId = Animator.StringToHash("DeathVariant");
    private static readonly int PivotId = Animator.StringToHash("Pivot");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // Faction cloth (tunic + painted shield face). Red team keeps the authentic
    // MAT_Roman_ClothRed palette color; blue team substitutes this muted blue
    // (same value range as the brick red so both read as cloth, not neon).
    private static readonly Color BlueFactionCloth = new Color(0.10f, 0.21f, 0.55f);

    [Tooltip("Front-rank soldiers raise shields when the nearest enemy is inside this range while the formation is attacking or engaged (visual only)")]
    [SerializeField] private float guardRange = 12f;

    // Locomotion calibration: reference speeds are the measured stance-foot
    // ground speeds of each clip at 1x playback (sampled in-editor from the
    // imported FBX), so playback = actualSpeed / reference keeps the feet
    // honest instead of skating. Clamps stop flutter at full gameplay speed
    // (the authored strides are compact; Patch 3 owns re-authoring them) and
    // slow-motion churn near zero. Only loco states bind LocoScale, so
    // attacks / hits / deaths / pivot never speed up.
    [SerializeField] private float marchReferenceSpeed = 0.76f;    // ANIM_Roman_FormationMarch
    [SerializeField] private float advanceReferenceSpeed = 0.55f;  // ANIM_Roman_CombatAdvance
    [SerializeField] private float shuffleReferenceSpeed = 0.43f;  // ANIM_Roman_CloseRanksShuffle
    [SerializeField] private float runReferenceSpeed = 2.54f;      // ANIM_Roman_Run
    [SerializeField] private float minLocoPlayback = 0.6f;
    [SerializeField] private float maxLocoPlayback = 2.4f;         // march / advance
    [SerializeField] private float maxShufflePlayback = 2.2f;
    [SerializeField] private float maxRunPlayback = 1.6f;

    private const float ShuffleMaxSpeed = 1.2f;   // m/s: faster corrections march instead

    private Soldier soldier;
    private Formation formation;
    private Animator animator;
    private SkinnedMeshRenderer smr;
    private MaterialPropertyBlock mpb;
    private Color[] slotBase;          // palette per slot, faction slot substituted
    private Coroutine flashRoutine;
    private bool isMoving;             // hysteresis so idle/move doesn't flicker
    private bool dead;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        smr = GetComponentInChildren<SkinnedMeshRenderer>();
        mpb = new MaterialPropertyBlock();
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
        soldier.suppressFallRotation = true;   // animator owns the death pose
        soldier.suppressTint = true;           // this controller owns all tinting

        InitPalette();
        ApplyPalette(1f, 0f);

        // Subtle per-soldier variation: idle phase offset + tiny playback-speed
        // spread. Visual only; gameplay timing is untouched.
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
        }
        if (formation != null) formation.OnFacingSnapped -= OnPivot;
    }

    // ---------------- animation-role driving ----------------

    private void Update()
    {
        if (dead || soldier == null || !soldier.Alive) return;

        float speed = soldier.Velocity.magnitude;
        if (speed > 0.35f) isMoving = true;
        else if (speed < 0.2f) isMoving = false;

        int loco = ComputeLoco(speed);
        animator.SetInteger(LocoId, loco);
        animator.SetFloat(LocoScaleId, LocoPlayback(loco, speed));
    }

    // Stride matching: playback = actual ground speed over the clip's own
    // measured reference speed, clamped per category (visual playback only).
    private float LocoPlayback(int loco, float speed)
    {
        float reference, max;
        switch (loco)
        {
            case LocoMarch: reference = marchReferenceSpeed; max = maxLocoPlayback; break;
            case LocoAdvance: reference = advanceReferenceSpeed; max = maxLocoPlayback; break;
            case LocoShuffle: reference = shuffleReferenceSpeed; max = maxShufflePlayback; break;
            case LocoBrokenRun: reference = runReferenceSpeed; max = maxRunPlayback; break;
            default: return 1f;   // stationary loops don't bind LocoScale
        }
        return Mathf.Clamp(speed / Mathf.Max(0.05f, reference), minLocoPlayback, max);
    }

    private int ComputeLoco(float speed)
    {
        if (formation == null) return isMoving ? LocoMarch : LocoRearIdle;
        FormationState st = formation.State;

        // Broken ranks: freer individual behavior — open combat stance and a
        // run, no formation rank restrictions (documented fallback clips).
        if (st == FormationState.BrokenRanks)
            return isMoving ? LocoBrokenRun : LocoBrokenIdle;

        // Guard applies to soldiers actually exposed to combat: in melee
        // contact, near the fight (edge-engagement data), or holding the
        // current front rank of an attacking/engaged formation with the enemy
        // approaching. Rear ranks stay controlled.
        bool combatState = st == FormationState.Engaged || st == FormationState.Attacking;
        bool frontRank = soldier.slotIndex < formation.columns;
        bool guard = soldier.IsEngaged
                     || soldier.NearestEnemyDist <= formation.edgeEngageRadius
                     || (frontRank && combatState && soldier.NearestEnemyDist <= guardRange);
        if (guard) return isMoving ? LocoAdvance : LocoGuard;

        if (isMoving)
        {
            // Genuinely small, slow slot corrections read as a controlled
            // shuffle. Fast corrections — reform rushes and auto-close
            // compaction included — use the full march cycle: the shuffle
            // clip can only cover ~0.95 m/s of ground at max playback, so
            // rushing in it is what read as sliding.
            bool shuffle = speed < ShuffleMaxSpeed &&
                           (st == FormationState.Reforming ||
                            (st == FormationState.Ordered && !formation.HasMoveDestination));
            return shuffle ? LocoShuffle : LocoMarch;
        }
        return LocoRearIdle;
    }

    private void OnAttack()
    {
        if (dead || !soldier.Alive) return;
        // Thrust is the workhorse (~60%), over-shield the accent (~40%).
        // Lightweight selection, no allocation; never affects gameplay damage.
        animator.SetInteger(AttackVariantId, Random.value < 0.6f ? 0 : 1);
        animator.SetTrigger(AttackId);
    }

    private void OnHurt()
    {
        if (dead || !soldier.Alive) return;
        // Skip the body reaction while mid-attack (committed strike), but the
        // flash below always shows the hit landed.
        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsTag("Attack")) animator.SetTrigger(HitId);

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());
    }

    private void OnDeath()
    {
        dead = true;
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

    // ---------------- per-slot tinting ----------------

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
            // Only the designated faction slot changes by team; skin, iron,
            // brass, leather, wood, and eyes keep their palette colors.
            if (mats[i].name.StartsWith("MAT_Roman_ClothRed") && soldier.team == Team.Blue)
                c = BlueFactionCloth;
            slotBase[i] = c;
        }
        // Clear any renderer-level block left by earlier tint paths so the
        // per-slot blocks below are the only override.
        smr.SetPropertyBlock(null);
    }

    // Final visible color per slot = palette (faction-substituted) darkened by
    // missing health, then blended toward white by the transient hit flash.
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
