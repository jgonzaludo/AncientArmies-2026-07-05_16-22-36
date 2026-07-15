using System.Collections;
using UnityEngine;

// Individual simulated soldier. Blends formation-slot attraction with local combat
// behavior; the blend weight and target-acquisition radius come from the formation state.
public class Soldier : MonoBehaviour
{
    public Team team;
    public Formation formation;
    public int slotIndex;

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

    private Soldier pendingShotTarget;
    private float pendingShotDamage;

    private UnitStats S => formation.stats;

    private Rigidbody rb;
    private Renderer bodyRenderer;
    private Transform weapon;
    private GameObject selectionDisc;
    private MaterialPropertyBlock mpb;
    private Color baseColor;

    private Soldier target;
    private float attackTimer;
    private float retargetTimer;
    private float skill = 1f;                 // fixed per-soldier variation, not hidden dice
    private Coroutine flashRoutine;

    private Vector3 sepVel;                   // cached friendly-separation push
    private int sepTick;                      // staggered so a quarter of soldiers recompute per tick
    private static int sepStagger;

    private bool movingForFacing;             // hysteresis state for movement-facing

    private const float Accel = 25f;
    private const float FaceCombatDegPerSec = 480f;   // snapping onto an opponent
    private const float FaceMoveDegPerSec = 360f;     // turning into the march direction
    private const float FaceIdleDegPerSec = 240f;     // settling on formation facing (~pivot clip pace)
    private const float FaceStartSpeed = 0.45f;       // m/s: begin facing movement
    private const float FaceStopSpeed = 0.25f;        // m/s: fall back to hold/idle facing
    private const float SepMaxPush = 1.2f;            // m/s cap: separation biases, never flings
    private const float SepFractionOrdered = 0.85f;   // of formation spacing
    private const float SepFractionPacked = 0.72f;    // melee packs tighter, but stays readable
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
        RefreshTint();
    }

    // ---------------- movement ----------------

    private void FixedUpdate()
    {
        if (!Alive) return;
        if (BattleSetup.Instance == null || BattleSetup.Instance.Phase != BattlePhase.Active)
        {
            rb.linearVelocity = Vector3.zero;   // hold position until the battle starts
            return;
        }

        Vector3 pos = transform.position;
        float w = formation.GetSlotWeight(this);

        Vector3 toSlot = formation.GetSlotWorldPos(slotIndex) - pos;
        toSlot.y = 0f;
        Vector3 slotDesire = Arrive(toSlot, S.moveSpeed);

        Vector3 desired;
        if (target != null && target.Alive)
        {
            Vector3 toT = target.transform.position - pos;
            toT.y = 0f;
            float d = toT.magnitude;
            Vector3 combatDesire = Vector3.zero;
            if (S.isRanged && d > S.rangedMinRange)
            {
                if (d > S.rangedRange * 0.9f) combatDesire = toT.normalized * S.moveSpeed;
            }
            else
            {
                if (d > S.strikeRange * 0.8f) combatDesire = toT.normalized * S.moveSpeed;
            }
            desired = Vector3.Lerp(combatDesire, slotDesire, w);
        }
        else
        {
            desired = slotDesire * Mathf.Clamp01(w + 0.35f);
        }

        // never stray past the leash, even with broken ranks
        Vector3 fromAnchor = pos - formation.AnchorPos;
        fromAnchor.y = 0f;
        if (fromAnchor.magnitude > formation.brokenLeash)
            desired = -fromAnchor.normalized * S.moveSpeed;

        // soft same-team separation: recomputed every 4th tick (staggered),
        // cached in between; biases the desired velocity, never overpowers it
        if ((sepTick++ & 3) == 0) RecomputeSeparation(pos);
        desired += sepVel;

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
        float sepDist = formation.spacing * (packed ? SepFractionPacked : SepFractionOrdered);
        float sep2 = sepDist * sepDist;

        var friends = BattleSetup.Instance.GetSoldiers(team);
        Vector3 push = Vector3.zero;
        for (int i = 0; i < friends.Count; i++)
        {
            Soldier f = friends[i];
            if (f == this || !f.Alive) continue;   // registry holds only the living, but be safe
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
        sepVel = Vector3.ClampMagnitude(push * SepMaxPush, SepMaxPush);
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
            if (d <= reach && attackTimer <= 0f)
            {
                attackTimer = S.attackCooldown * Random.Range(0.9f, 1.15f);
                // formation-level tactical truth: front 1x, flank 1.5x, rear 2x
                float dirMult = BattleSetup.Instance.GetDirectionalMultiplier(
                    formation, target.formation);
                if (shoot)
                {
                    float dmg = S.attackDamage * skill * dirMult;
                    if (deferRangedRelease)
                    {
                        ReleasePendingShot();   // an unreleased previous shot flies now
                        pendingShotTarget = target;
                        pendingShotDamage = dmg;
                    }
                    else
                    {
                        Projectile.Spawn(transform.position + Vector3.up * 1.3f, target,
                                         dmg, S.projectileSpeed);
                    }
                }
                else
                    target.TakeDamage(S.attackDamage * skill * dirMult * (S.isRanged ? 0.4f : 1f));
                OnAttack?.Invoke();
                if (weapon != null) StartCoroutine(LungeAnim());
            }
        }

        UpdateFacing();
    }

    private void AcquireTarget()
    {
        float radius = formation.GetAcquireRadius(this);
        if (radius <= 0f) { target = null; return; }

        if (target != null && target.Alive)
        {
            float d = (target.transform.position - transform.position).magnitude;
            // committed melee: never swap opponents while one is at sword's reach
            if (!S.isRanged && d <= S.strikeRange * 1.2f) return;
            if (d < radius * 1.25f) return;   // hysteresis: keep current fight
        }
        target = null;

        if (BattleSetup.Instance == null) return;
        var enemies = BattleSetup.Instance.GetSoldiers(team == Team.Blue ? Team.Red : Team.Blue);
        float max2 = radius * radius;
        float bestScore = float.MaxValue;
        // ranged units must be able to acquire anything inside their own range,
        // even when it stands beyond the broken-ranks leash around the anchor
        float leash = formation.brokenLeash;
        if (S.isRanged) leash = Mathf.Max(leash, S.rangedRange + 2f);
        float leash2 = leash * leash;
        Vector3 p = transform.position;
        Vector3 fwd = transform.forward;
        foreach (var e in enemies)
        {
            if (!e.Alive) continue;
            Vector3 to = e.transform.position - p;
            float d2 = to.sqrMagnitude;
            if (d2 >= max2) continue;
            if ((e.transform.position - formation.AnchorPos).sqrMagnitude > leash2) continue;
            // mild preference for enemies roughly ahead: a soldier should not
            // spin away from the local fight for a marginally closer enemy at
            // its back, but a lone rear threat is still acquired
            float score = Vector3.Dot(fwd, to) < 0f ? d2 * 1.6f : d2;
            if (score < bestScore) { bestScore = score; target = e; }
        }
    }

    // Called by the visual controller at the firing clip's release frame (or
    // immediately on interrupt). Spawns the shot validated at attack time.
    public void ReleasePendingShot()
    {
        if (pendingShotTarget == null) return;
        var t = pendingShotTarget;
        pendingShotTarget = null;
        if (!Alive || !t.Alive) return;
        Projectile.Spawn(transform.position + Vector3.up * 1.3f, t,
                         pendingShotDamage, S.projectileSpeed);
    }

    public void CancelPendingShot() { pendingShotTarget = null; }

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
        OnHurt?.Invoke();
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());
    }

    private void Die()
    {
        Alive = false;
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
        if (selectionDisc != null) selectionDisc.SetActive(sel);
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
