using System.Collections;
using UnityEngine;

// Individual simulated soldier. Blends formation-slot attraction with local combat
// behavior; the blend weight and target-acquisition radius come from the formation state.
public class Soldier : MonoBehaviour
{
    public Team team;
    public Formation formation;
    public int slotIndex;
    public SoldierRole role = SoldierRole.Legionary;   // data-reserved century role (Phase 1)

    public bool Alive { get; private set; } = true;
    public bool IsEngaged;                    // maintained by the formation's engagement scan
    public float NearestEnemyDist = float.MaxValue;   // also from the engagement scan
    public float Health { get; private set; }

    public Vector3 Velocity => rb != null && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;

    // Visual hooks (presentation only; gameplay stays authoritative).
    public event System.Action OnAttack;
    public event System.Action OnHurt;
    public event System.Action OnDeath;
    [System.NonSerialized] public bool suppressFallRotation;   // set by animated visuals
    [System.NonSerialized] public bool suppressTint;            // visual controller owns all tinting
    // Animated archers defer the projectile spawn to the firing clip's release
    // frame. The shot is validated, damaged, and cooldown-charged at attack
    // time as always; only the spawn moment moves. Capsule archers (flag off)
    // keep the immediate spawn.
    [System.NonSerialized] public bool deferRangedRelease;

    // Animated legionaries defer the melee damage to each attack clip's
    // authored contact frame (same philosophy as deferRangedRelease): the hit
    // is validated, variant-chosen, and cooldown-charged at attack time; only
    // the damage moment moves. Capsule melee (flag off) keeps instant damage.
    [System.NonSerialized] public bool deferMeleeImpact;

    // Far-zoom impostor LOD: the formation impostor renderer hides this
    // soldier's visual hierarchy behind a merged billboard quad. Simulation
    // is untouched — only presentation goes dormant.
    [System.NonSerialized] public bool Imposted;

    // 0 thrust / 1 over-shield / 2 diagonal slash — chosen by gameplay so the
    // damage timing and the displayed clip can never disagree.
    public int MeleeAttackVariant { get; private set; }

    [Tooltip("Relative weights: thrust / over-shield / diagonal slash")]
    [SerializeField] private Vector3 meleeVariantWeights = new Vector3(0.5f, 0.25f, 0.25f);

    // Seconds from attack commit to the authored contact frame, per variant
    // (thrust f9/30fps, over-shield f12, diagonal f13 — trued to the clips).
    private static readonly float[] MeleeImpactDelay = { 0.30f, 0.40f, 0.43f };

    private Soldier pendingMeleeTarget;
    private float pendingMeleeDamage;
    private float pendingMeleeTimer;
    private int lastVariant = -1, prevVariant = -1;

    private Soldier pendingShotTarget;
    private float pendingShotDamage;

    private UnitStats S => formation.stats;

    private Rigidbody rb;
    private Renderer bodyRenderer;
    private Transform weapon;
    private GameObject selectionDisc;
    private MaterialPropertyBlock mpb;
    private Color baseColor;
    private GameObject[] visualParts;      // "VisualRoot", or the Body/Weapon primitives
    private Renderer[] shadowRenderers;    // child renderers minus the selection disc
    private bool castsShadows = true;
    private Animator visualAnimator;       // pose-evaluated on impostor wake
    private bool animatorCached;

    // Contact-line saturation (Phase 4): melee attackers register on their
    // victim so scoring can spread strikes along the boundary instead of the
    // whole rear rank swarming one enemy.
    [System.NonSerialized] public int meleeAttackerCount;
    private const int MaxMeleeAttackersPerTarget = 3;
    private float volleyJitter01;   // fixed loose offset inside the volley window

    // Volley spread (v1.8.2): ranged attackers register too, with a GRADUATED
    // score penalty instead of a hard cap — 160 archers distribute in
    // proportion to who is already being shot at, instead of every archer
    // resolving the identical nearest-first argmin and massacring one man.
    // A tiny deterministic per-soldier salt breaks residual lockstep between
    // archers that see identical counts and distances. No RNG, no allocs.
    [System.NonSerialized] public int rangedAttackerCount;
    private const float RangedSpreadPenaltyPerAttacker = 0.35f;
    private float targetSalt = 1f;   // 0.97..1.03, fixed per soldier

    // grid query scratch (Phase 6F): shared, main-thread only
    private static readonly Soldier[] sepBuffer = new Soldier[24];
    private static readonly Soldier[] targetBuffer = new Soldier[96];

    private Soldier target;
    private float attackTimer;
    private float retargetTimer;
    private float skill = 1f;                 // fixed per-soldier variation, not hidden dice
    private Coroutine flashRoutine;

    private Vector3 sepVel;                   // cached friendly-separation push
    private int sepTick;                      // staggered so a quarter of soldiers recompute per tick
    private static int sepStagger;

    private bool movingForFacing;             // hysteresis state for movement-facing

    // Committed-action lock: while a soldier is visibly striking, drawing,
    // stabbing, or reacting to a hit, slot correction (and idle drift toward
    // combat targets) is suspended so the action stays planted; it ramps back
    // quickly when the window ends. Gameplay timing drives these windows —
    // never Animator state names. Death stops movement entirely (existing).
    private float actionLockTimer;
    private float lockRecovery = 1f;          // 0 locked -> 1 free, quick ramp

    // Action-priority reads (Patch 4): derived from gameplay state only.
    public bool IsInCommittedCombatAction => actionLockTimer > 0f || pendingShotTarget != null;
    public bool IsImmediatelyThreatened => IsEngaged;
    public bool CanPerformStrongSlotCorrection =>
        Alive && !IsInCommittedCombatAction && !IsEngaged;
    public bool CanPerformWeakSlotCorrection => Alive && !IsInCommittedCombatAction;

    // Centralized committed-action windows (seconds), matched to the visible
    // clip lengths but timed by gameplay.
    private const float MeleeAttackLockSeconds = 0.85f;    // sword strike window
    private const float KnifeLockSeconds = 0.7f;           // archer sidearm stab
    private const float RangedFollowThroughSeconds = 0.35f; // after arrow release
    private const float RangedImmediateLockSeconds = 0.5f;  // capsule-fallback shot
    private const float HitLockSeconds = 0.45f;            // hit-reaction window
    private const float LockRampSeconds = 0.3f;            // correction ramp back in

    private const float Accel = 25f;
    private const float FaceCombatDegPerSec = 480f;   // snapping onto an opponent
    private const float FaceMoveDegPerSec = 360f;     // turning into the march direction
    private const float FaceIdleDegPerSec = 240f;     // settling on formation facing (~pivot clip pace)
    private const float FaceStartSpeed = 0.45f;       // m/s: begin facing movement
    private const float FaceStopSpeed = 0.25f;        // m/s: fall back to hold/idle facing
    // separation tuning now lives on BattleSetup (Phase 4 central exposure)
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public void Init(Formation f, int slot, Rigidbody rb, Renderer body, Transform weapon,
                     GameObject disc, Color color, float skill)
    {
        formation = f;
        team = f.team;
        slotIndex = slot;
        this.rb = rb;
        bodyRenderer = body;
        this.weapon = weapon;
        selectionDisc = disc;
        baseColor = color;
        this.skill = skill;
        Health = f.stats.maxHealth;
        mpb = new MaterialPropertyBlock();
        retargetTimer = Random.value * 0.3f;
        attackTimer = Random.value * 0.5f;
        sepTick = sepStagger++;               // spread separation recomputes across ticks
        targetSalt = 0.97f + ((slot * 31) % 7) * 0.01f;   // deterministic volley tiebreak
        volleyJitter01 = ((slot * 29) % 16) / 16f;        // spread shots across the window
        RefreshTint();
    }

    // ---------------- movement ----------------

    private void FixedUpdate()
    {
        if (!Alive) return;
        // Deployment (Pre) allows full physical movement — formations march to
        // their deployment positions; only combat is gated on Active (Update).
        if (BattleSetup.Instance == null || BattleSetup.Instance.Phase == BattlePhase.Ended)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 pos = transform.position;
        float w = formation.GetSlotWeight(this);
        // charging formations sprint: every desired velocity scales together
        float ms = S.moveSpeed * formation.SpeedMultiplier;

        Vector3 toSlot = formation.GetSlotWorldPos(slotIndex) - pos;
        toSlot.y = 0f;
        Vector3 slotDesire = Arrive(toSlot, ms);

        Vector3 desired;
        if (target != null && target.Alive)
        {
            Vector3 toT = target.transform.position - pos;
            toT.y = 0f;
            float d = toT.magnitude;
            Vector3 combatDesire = Vector3.zero;
            if (S.isRanged && d > S.rangedMinRange)
            {
                if (d > S.rangedRange * 0.9f) combatDesire = toT.normalized * ms;
            }
            else
            {
                if (d > S.strikeRange * 0.8f) combatDesire = toT.normalized * ms;
            }
            desired = Vector3.Lerp(combatDesire, slotDesire, w);
        }
        else
        {
            desired = slotDesire * Mathf.Clamp01(w + 0.35f);
        }

        // Committed-action lock: a striking/drawing/stabbing/hit-reacting
        // soldier stays planted (no slot chasing, no drift toward far
        // targets); when the window ends, correction ramps back over
        // LockRampSeconds instead of snapping. Combat stays authoritative:
        // in-reach fighting has near-zero desired velocity anyway, and the
        // lock timer is set by the same gameplay events that deal damage.
        if (actionLockTimer > 0f)
        {
            actionLockTimer -= Time.fixedDeltaTime;
            lockRecovery = 0f;
        }
        else if (pendingShotTarget != null) lockRecovery = 0f;   // bow drawn
        else lockRecovery = Mathf.Min(1f, lockRecovery + Time.fixedDeltaTime / LockRampSeconds);
        desired *= lockRecovery;

        // never stray past the leash, even with broken ranks; the leash
        // tightens (and its center moves) while pursuing
        Vector3 fromAnchor = pos - formation.AnchorPos;
        fromAnchor.y = 0f;
        if (fromAnchor.magnitude > formation.EffectiveLeash)
            desired = -fromAnchor.normalized * ms;

        // soft same-team separation: recomputed every 4th tick (staggered),
        // cached in between; biases the desired velocity, never overpowers it.
        // Kept partially active while locked so overlaps still resolve gently.
        if ((sepTick++ & 3) == 0) RecomputeSeparation(pos);
        desired += sepVel * Mathf.Max(0.4f, lockRecovery);

        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = Vector3.MoveTowards(vel, desired, Accel * Time.fixedDeltaTime);
    }

    private static Vector3 Arrive(Vector3 offset, float maxSpeed)
    {
        float dist = offset.magnitude;
        if (dist < 0.05f) return Vector3.zero;
        float speed = Mathf.Min(maxSpeed, dist * 3f);
        return offset / dist * speed;
    }

    // Friendly anti-clumping: a capped push away from same-team soldiers closer
    // than a fraction of formation spacing (looser while packed into melee).
    // Enemies are never pushed, so combat contact is untouched. Linear ramp with
    // penetration depth; a deterministic tiebreak keeps coincident soldiers from
    // jittering. Allocation-free brute force over the team registry, affordable
    // because each soldier only recomputes every 4th tick.
    private void RecomputeSeparation(Vector3 pos)
    {
        FormationState st = formation.State;
        bool packed = st == FormationState.BrokenRanks || st == FormationState.Engaged;
        var bs = BattleSetup.Instance;
        float sepDist = formation.spacing *
                        (packed ? bs.separationFractionPacked : bs.separationFractionOrdered);
        float sep2 = sepDist * sepDist;

        // Phase 6F: grid-local neighbors instead of the whole team registry —
        // at 640 friends the full scan was the hottest loop in the game.
        int nearby = BattleGrid.CollectFriends(pos, team, sepDist, sepBuffer);
        Vector3 push = Vector3.zero;
        for (int i = 0; i < nearby; i++)
        {
            Soldier f = sepBuffer[i];
            if (f == this || !f.Alive) continue;
            Vector3 away = pos - f.transform.position;
            away.y = 0f;
            float d2 = away.sqrMagnitude;
            if (d2 >= sep2) continue;
            if (d2 < 0.0001f)
            {
                // nearly coincident: no direction to push along, so break the tie
                // by entity ID — stable across frames, never Random
                push += GetEntityId().CompareTo(f.GetEntityId()) < 0 ? Vector3.right : Vector3.left;
                continue;
            }
            float d = Mathf.Sqrt(d2);
            push += away * ((sepDist - d) / (sepDist * d));   // unit dir * penetration 0..1
        }
        sepVel = Vector3.ClampMagnitude(push * bs.separationMaxPush, bs.separationMaxPush);
    }

    // ---------------- combat ----------------

    private void Update()
    {
        if (!Alive) return;
        if (BattleSetup.Instance == null || BattleSetup.Instance.Phase != BattlePhase.Active)
        {
            // no targeting or attacks before Start / after battle end, but the
            // models still track facing so a pre-battle Rotate command turns
            // the soldiers, not just the ground arrow
            UpdateFacing();
            return;
        }

        // deferred melee hit: land on the authored contact frame
        if (pendingMeleeTarget != null)
        {
            pendingMeleeTimer -= Time.deltaTime;
            if (pendingMeleeTimer <= 0f) LandPendingMeleeHit();
        }

        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0f)
        {
            retargetTimer = 0.3f;
            AcquireTarget();
        }

        attackTimer -= Time.deltaTime;
        if (target != null && target.Alive)
        {
            Vector3 toT = target.transform.position - transform.position;
            toT.y = 0f;
            float d = toT.magnitude;
            bool shoot = S.isRanged && d > S.rangedMinRange;
            float reach = shoot ? S.rangedRange : S.strikeRange;
            // volley discipline (v1.9): arrows loose only inside the
            // formation's volley window, each archer at its own offset —
            // melee and the sidearm are never gated
            bool volleyReady = !shoot ||
                (formation.VolleyOpen &&
                 formation.VolleyPhase >= volleyJitter01 * formation.volleyWindow);
            if (d <= reach && attackTimer <= 0f && volleyReady)
            {
                attackTimer = S.attackCooldown * Random.Range(0.9f, 1.15f);
                // formation-level tactical truth: front 1x, flank 1.5x, rear 2x
                float dirMult = BattleSetup.Instance.GetDirectionalMultiplier(
                    formation, target.formation);
                if (shoot)
                {
                    formation.NotifyRangedShot();   // archer IsFiring window
                    float dmg = S.attackDamage * skill * dirMult;
                    if (deferRangedRelease)
                    {
                        ReleasePendingShot();   // an unreleased previous shot flies now
                        pendingShotTarget = target;
                        pendingShotDamage = dmg;
                        // the pending shot itself locks correction until release
                    }
                    else
                    {
                        Projectile.Spawn(transform.position + Vector3.up * 1.3f, target,
                                         dmg, S.projectileSpeed);
                        actionLockTimer = Mathf.Max(actionLockTimer, RangedImmediateLockSeconds);
                    }
                }
                else
                {
                    // charge impact bonus bakes in at commit time, exactly like
                    // the directional multiplier — a deferred hit that lands
                    // after the window closes still carries the charge's force
                    float dmg = S.attackDamage * skill * dirMult * (S.isRanged ? 0.4f : 1f)
                                * formation.ChargeDamageMultiplier;
                    if (deferMeleeImpact && !S.isRanged)
                    {
                        // damage lands on the clip's contact frame; everything
                        // else (validation, cooldown, lock) charges now
                        MeleeAttackVariant = PickMeleeVariant();
                        LandPendingMeleeHit();   // an unlanded previous hit resolves now
                        pendingMeleeTarget = target;
                        pendingMeleeDamage = dmg;
                        pendingMeleeTimer = MeleeImpactDelay[MeleeAttackVariant];
                        actionLockTimer = Mathf.Max(actionLockTimer, MeleeAttackLockSeconds);
                    }
                    else
                    {
                        target.TakeDamage(dmg);
                        actionLockTimer = Mathf.Max(actionLockTimer,
                            S.isRanged ? KnifeLockSeconds : MeleeAttackLockSeconds);
                    }
                }
                OnAttack?.Invoke();
                if (weapon != null) StartCoroutine(LungeAnim());
            }
        }

        UpdateFacing();
    }

    private void AcquireTarget()
    {
        float radius = formation.GetAcquireRadius(this);
        if (radius <= 0f) { SetTarget(null); return; }

        if (target != null && target.Alive)
        {
            float d = (target.transform.position - transform.position).magnitude;
            // committed melee: never swap opponents while one is at sword's reach
            if (!S.isRanged && d <= S.strikeRange * 1.2f) return;
            if (d < radius * 1.25f) return;   // hysteresis: keep current fight
        }
        SetTarget(null);

        if (BattleSetup.Instance == null) return;
        // Phase 6F: grid-local candidates. The buffer bounds the scan; ring
        // expansion order means truncation drops only the farthest candidates.
        int found = BattleGrid.CollectEnemies(transform.position, team, radius, targetBuffer);
        float max2 = radius * radius;
        float bestScore = float.MaxValue;
        Soldier best = null;
        // ranged units must be able to acquire anything inside their own range,
        // even when it stands beyond the broken-ranks leash around the anchor
        float leash = formation.EffectiveLeash;
        if (S.isRanged) leash = Mathf.Max(leash, S.rangedRange + 2f);
        float leash2 = leash * leash;
        Vector3 p = transform.position;
        Vector3 fwd = transform.forward;
        for (int i = 0; i < found; i++)
        {
            Soldier e = targetBuffer[i];
            if (!e.Alive) continue;
            Vector3 to = e.transform.position - p;
            float d2 = to.sqrMagnitude;
            if (d2 >= max2) continue;
            if ((e.transform.position - formation.AnchorPos).sqrMagnitude > leash2) continue;
            // mild preference for enemies roughly ahead: a soldier should not
            // spin away from the local fight for a marginally closer enemy at
            // its back, but a lone rear threat is still acquired
            float score = Vector3.Dot(fwd, to) < 0f ? d2 * 1.6f : d2;
            // contact-line spread (Phase 4): an enemy already mobbed by the
            // attacker cap scores badly, so the next rank looks for an open
            // position along the boundary instead of piling on
            if (!S.isRanged && e.meleeAttackerCount >= MaxMeleeAttackersPerTarget)
                score *= 3f;
            // volley spread (v1.8.2): graduated penalty per archer already on
            // this victim + per-soldier salt so a century's arrows distribute
            if (S.isRanged)
                score *= (1f + RangedSpreadPenaltyPerAttacker * e.rangedAttackerCount)
                         * targetSalt;
            if (score < bestScore) { bestScore = score; best = e; }
        }
        SetTarget(best);
    }

    // Central target setter: keeps the victim's attacker counts honest —
    // melee reservations bound the contact line (Phase 4), ranged counts
    // drive the graduated volley-spread penalty (v1.8.2).
    private void SetTarget(Soldier t)
    {
        if (target == t) return;
        if (!S.isRanged)
        {
            if (target != null)
                target.meleeAttackerCount = Mathf.Max(0, target.meleeAttackerCount - 1);
            if (t != null) t.meleeAttackerCount++;
        }
        else
        {
            if (target != null)
                target.rangedAttackerCount = Mathf.Max(0, target.rangedAttackerCount - 1);
            if (t != null) t.rangedAttackerCount++;
        }
        target = t;
    }

    // Called by the visual controller at the firing clip's release frame (or
    // immediately on interrupt). Spawns the shot validated at attack time.
    public void ReleasePendingShot()
    {
        if (pendingShotTarget == null) return;
        var t = pendingShotTarget;
        pendingShotTarget = null;
        actionLockTimer = Mathf.Max(actionLockTimer, RangedFollowThroughSeconds);
        if (!Alive || !t.Alive) return;
        Projectile.Spawn(transform.position + Vector3.up * 1.3f, t,
                         pendingShotDamage, S.projectileSpeed);
    }

    public void CancelPendingShot() { pendingShotTarget = null; }

    // Resolve the staged melee hit (contact frame reached, or a new attack is
    // committing before the previous one landed). Damage was computed at
    // commit time; the target just has to still be there to receive it.
    private void LandPendingMeleeHit()
    {
        if (pendingMeleeTarget == null) return;
        var t = pendingMeleeTarget;
        pendingMeleeTarget = null;
        if (!Alive || !t.Alive) return;
        t.TakeDamage(pendingMeleeDamage);
    }

    // Weighted pick over the three attack clips; one re-roll if the choice
    // would make three identical strikes in a row. Visual variety only —
    // damage, cooldown, and reach are identical across variants.
    private int PickMeleeVariant()
    {
        int v = RollMeleeVariant();
        if (v == lastVariant && v == prevVariant) v = RollMeleeVariant();
        prevVariant = lastVariant;
        lastVariant = v;
        return v;
    }

    private int RollMeleeVariant()
    {
        float total = meleeVariantWeights.x + meleeVariantWeights.y + meleeVariantWeights.z;
        if (total <= 0f) return 0;
        float r = Random.value * total;
        if (r < meleeVariantWeights.x) return 0;
        return r < meleeVariantWeights.x + meleeVariantWeights.y ? 1 : 2;
    }

    // Facing resolver, in priority order: pending shot > active combat >
    // meaningful movement > formation facing. The gameplay root is the single
    // dynamically rotated transform (the visual prefab is an identity child),
    // so models, the mild ahead-preference in AcquireTarget, and formation
    // presentation all read the same facing. Directional damage stays
    // formation-level (AnchorForward) and is unaffected. RotateTowards is
    // frame-rate independent and takes the shortest horizontal path; the
    // dead zone below retains the last valid facing instead of guessing.
    private void UpdateFacing()
    {
        Vector3 v = Velocity;
        float sp2 = v.sqrMagnitude;
        // hysteresis: slot-correction noise must not flip between movement
        // facing and idle facing every few frames
        if (movingForFacing) { if (sp2 < FaceStopSpeed * FaceStopSpeed) movingForFacing = false; }
        else if (sp2 > FaceStartSpeed * FaceStartSpeed) movingForFacing = true;

        Vector3 dir;
        float degPerSec;
        if (pendingShotTarget != null && pendingShotTarget.Alive)
        {
            // mid-draw archer: hold on the shot actually being released, even
            // if target acquisition has already moved on — no mid-draw wobble
            dir = pendingShotTarget.transform.position - transform.position;
            degPerSec = FaceCombatDegPerSec;
        }
        else if (target != null && target.Alive && InCombatFacingRange())
        {
            dir = target.transform.position - transform.position;
            degPerSec = FaceCombatDegPerSec;
        }
        else if (movingForFacing)
        {
            dir = v;
            degPerSec = FaceMoveDegPerSec;
        }
        else
        {
            // Broken and idle: keep the last meaningful visual direction —
            // there is no formation facing to settle onto (Phase 2 policy).
            if (formation.State == FormationState.BrokenRanks) return;
            // idle: the formation's canonical facing — this is what makes an
            // explicit Rotate command end with soldiers facing the arrow
            dir = formation.AnchorForward;
            degPerSec = FaceIdleDegPerSec;
        }
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;   // dead zone: keep last facing
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want,
                                                      degPerSec * Time.deltaTime);
    }

    // Melee faces its opponent only while genuinely fighting (in contact or in
    // reach), not a distant acquisition while marching. Ranged holds on a
    // target inside bow range while standing in the firing line; on the move
    // both face their movement instead of twisting toward a far-away target.
    private bool InCombatFacingRange()
    {
        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        float d2 = to.sqrMagnitude;
        if (!S.isRanged)
        {
            float r = S.strikeRange * 1.5f;
            return IsEngaged || d2 <= r * r;
        }
        if (movingForFacing) return false;
        float rr = S.rangedRange * 1.1f;
        return d2 <= rr * rr;
    }

    public void TakeDamage(float dmg)
    {
        if (!Alive) return;
        Health -= dmg;
        if (Health <= 0f)
        {
            Die();
            return;
        }
        actionLockTimer = Mathf.Max(actionLockTimer, HitLockSeconds);
        OnHurt?.Invoke();
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());
    }

    private void Die()
    {
        Alive = false;
        pendingMeleeTarget = null;   // a dead soldier never finishes a swing
        SetTarget(null);             // release the contact-line reservation
        formation.NotifyDeath(this);
        if (BattleSetup.Instance != null) BattleSetup.Instance.Unregister(this);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        rb.isKinematic = true;
        if (selectionDisc != null) selectionDisc.SetActive(false);
        OnDeath?.Invoke();
        StopAllCoroutines();
        StartCoroutine(DeathAnim());
    }

    private IEnumerator DeathAnim()
    {
        SetTint(Color.Lerp(baseColor, Color.black, 0.55f));
        if (!suppressFallRotation)
        {
            Quaternion start = transform.rotation;
            Quaternion fallen = start * Quaternion.Euler(90f, 0f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                transform.rotation = Quaternion.Slerp(start, fallen, t);
                yield return null;
            }
            transform.rotation = fallen;
            yield return new WaitForSeconds(1.2f);
        }
        else
        {
            // animated visual plays its own death clip (~2.4 s); keep overall timing
            yield return new WaitForSeconds(1.55f);
        }
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            transform.position += Vector3.down * (1.6f * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject);
    }

    // ---------------- visuals ----------------

    private IEnumerator HitFlash()
    {
        SetTint(Color.white);
        yield return new WaitForSeconds(0.08f);
        RefreshTint();
        flashRoutine = null;
    }

    private void RefreshTint()
    {
        float hp01 = Mathf.Clamp01(Health / S.maxHealth);
        SetTint(Color.Lerp(Color.Lerp(baseColor, Color.black, 0.6f), baseColor, hp01));
    }

    private void SetTint(Color c)
    {
        // A renderer-level MPB overrides _BaseColor on EVERY material slot of a
        // multi-material renderer; the Roman visual controller tints per slot instead.
        if (suppressTint) return;
        if (bodyRenderer == null) return;
        bodyRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, c);
        bodyRenderer.SetPropertyBlock(mpb);
    }

    public void SetSelected(bool sel)
    {
        // imposted soldiers show selection through the impostor quad tint,
        // never through 80 individual discs the quads would z-fight with
        if (selectionDisc != null) selectionDisc.SetActive(sel && !Imposted);
    }

    // Swap between the full visual hierarchy and the formation impostor quad.
    // Gameplay (movement, targeting, damage, death timing) is untouched; only
    // the skinned mesh / animator / primitive stack goes dormant.
    public void SetImpostor(bool on)
    {
        if (Imposted == on) return;
        Imposted = on;
        // Disabling the animator mid-draw would silently swallow an archer's
        // validated shot — the release frame never arrives. Let it fly first
        // (same rule the archer controller applies in OnDestroy).
        if (on) ReleasePendingShot();
        // A soldier that died while imposted never gets its visuals back; the
        // impostor layer already presented the death.
        if (!on && !Alive) return;
        if (visualParts == null) CacheVisualParts();
        for (int i = 0; i < visualParts.Length; i++)
            if (visualParts[i] != null) visualParts[i].SetActive(!on);
        if (!on)
        {
            // A re-enabled Animator holds an unevaluated bind pose until its
            // next update — one frame of 80 T-poses reads as a flicker at the
            // LOD boundary. Evaluate a real pose on the wake frame.
            if (!animatorCached)
            {
                animatorCached = true;
                visualAnimator = GetComponentInChildren<Animator>(true);
            }
            if (visualAnimator != null) visualAnimator.Update(0f);
        }
        // re-apply selection under the new impostor state so discs hide at
        // LOD-in and restore on LOD-out
        SetSelected(formation != null && formation.IsSelected);
    }

    // The Roman prefab instantiates as a single "VisualRoot" child; the
    // capsule fallback splits into "Body" and "Weapon" primitives. Cached
    // once — the visual hierarchy never changes after spawn.
    private void CacheVisualParts()
    {
        Transform vis = transform.Find("VisualRoot");
        if (vis != null)
        {
            visualParts = new[] { vis.gameObject };
            return;
        }
        Transform body = transform.Find("Body");
        Transform wpn = transform.Find("Weapon");
        int n = (body != null ? 1 : 0) + (wpn != null ? 1 : 0);
        visualParts = new GameObject[n];
        int k = 0;
        if (body != null) visualParts[k++] = body.gameObject;
        if (wpn != null) visualParts[k] = wpn.gameObject;
    }

    // Mid-zoom shadow tier: soldier shadows roughly double the army's drawn
    // geometry, and below the tier threshold they stop reading as shadows.
    // Renderer list cached once, state-guarded so redundant calls are free.
    public void SetShadowCasting(bool on)
    {
        if (castsShadows == on) return;
        castsShadows = on;
        if (shadowRenderers == null) CacheShadowRenderers();
        var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On
                      : UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < shadowRenderers.Length; i++)
            if (shadowRenderers[i] != null) shadowRenderers[i].shadowCastingMode = mode;
    }

    private void CacheShadowRenderers()
    {
        // the selection disc already never casts; everything else toggles
        var all = GetComponentsInChildren<Renderer>(true);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
            if (selectionDisc == null || all[i].gameObject != selectionDisc) n++;
        shadowRenderers = new Renderer[n];
        int k = 0;
        for (int i = 0; i < all.Length; i++)
            if (selectionDisc == null || all[i].gameObject != selectionDisc)
                shadowRenderers[k++] = all[i];
    }

    private IEnumerator LungeAnim()
    {
        Vector3 basePos = weapon.localPosition;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.22f)
        {
            weapon.localPosition = basePos + Vector3.forward * (0.45f * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        weapon.localPosition = basePos;
    }
}
