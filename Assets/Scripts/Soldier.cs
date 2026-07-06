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
    public float Health { get; private set; }

    public Vector3 Velocity => rb != null && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;

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

    private const float Accel = 25f;
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

    // ---------------- combat ----------------

    private void Update()
    {
        if (!Alive) return;
        if (BattleSetup.Instance == null || BattleSetup.Instance.Phase != BattlePhase.Active)
            return;   // no targeting or attacks before Start / after battle end

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
                if (shoot)
                    Projectile.Spawn(transform.position + Vector3.up * 1.3f, target,
                                     S.attackDamage * skill, S.projectileSpeed);
                else
                    target.TakeDamage(S.attackDamage * skill * (S.isRanged ? 0.4f : 1f));
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
            if (d < radius * 1.25f) return;   // hysteresis: keep current fight
        }
        target = null;

        if (BattleSetup.Instance == null) return;
        var enemies = BattleSetup.Instance.GetSoldiers(team == Team.Blue ? Team.Red : Team.Blue);
        float best = radius * radius;
        float leash2 = formation.brokenLeash * formation.brokenLeash;
        Vector3 p = transform.position;
        foreach (var e in enemies)
        {
            if (!e.Alive) continue;
            float d2 = (e.transform.position - p).sqrMagnitude;
            if (d2 >= best) continue;
            if ((e.transform.position - formation.AnchorPos).sqrMagnitude > leash2) continue;
            best = d2;
            target = e;
        }
    }

    private void UpdateFacing()
    {
        Vector3 dir;
        if (target != null && target.Alive)
            dir = target.transform.position - transform.position;
        else
        {
            Vector3 v = Velocity;
            dir = v.sqrMagnitude > 0.2f ? v : formation.AnchorForward;
        }
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 420f * Time.deltaTime);
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
        StopAllCoroutines();
        StartCoroutine(DeathAnim());
    }

    private IEnumerator DeathAnim()
    {
        SetTint(Color.Lerp(baseColor, Color.black, 0.55f));
        Quaternion start = transform.rotation;
        Quaternion fallen = start * Quaternion.Euler(90f, 0f, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            transform.rotation = Quaternion.Slerp(start, fallen, t);
            yield return null;
        }
        transform.rotation = fallen;
        yield return new WaitForSeconds(1.2f);
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
