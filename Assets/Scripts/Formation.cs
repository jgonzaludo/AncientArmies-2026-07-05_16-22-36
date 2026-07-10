using System.Collections.Generic;
using UnityEngine;

public enum FormationState { Ordered, Attacking, Engaged, BrokenRanks, Withdrawing, Reforming }

// Owns the high-level state machine, the anchor (position + facing) and the slot grid.
// Soldiers stay lightweight agents that blend slot attraction with local combat.
public class Formation : MonoBehaviour
{
    public Team team;
    public string displayName = "Formation";
    public UnitStats stats;
    public bool autoPilot;   // red side: strategically stationary, fights back, auto-tidies

    [Header("Layout")]
    public int columns = 6;
    public float spacing = 1.3f;

    [Header("Movement")]
    public float moveSpeed = 2.6f;
    public float rotateSpeedDeg = 70f;

    [Header("Engagement")]
    public float personalEngageRadius = 3f;    // enemy this close => soldier is "engaged"
    public float acquireRadiusOrdered = 2.2f;  // fight-back reach while holding formation
    public float acquireRadiusEngaged = 4.5f;
    public float acquireRadiusBroken = 28f;
    public float disengageRadius = 9f;         // no enemy within this of any soldier => can reform
    public float brokenLeash = 26f;
    public float meleeChaseStopDist = 3.5f;    // anchor-to-anchor stop distance when charging

    [Header("Rank replacement (ordered melee)")]
    public float promoteInterval = 0.6f;       // how often vacancies are scanned
    public float promoteVacancyDist = 1.6f;    // slot counts as open when its fighter strays this far
    public float promoteCoherenceDist = 2.2f;  // only soldiers still near their own slot advance

    [Header("Auto-close (ordered formations compress over losses)")]
    public float autoCloseInterval = 2f;       // how often structural gaps are scanned
    public int autoCloseDeficit = 2;           // casualties since last rebuild that trigger compaction

    [Header("Edge engagement (local freedom near the fight)")]
    public float edgeEngageRadius = 6f;        // unengaged soldiers this close to an enemy loosen up

    public FormationState State { get; private set; } = FormationState.Ordered;
    public bool CanReform { get; private set; }
    public bool IsSelected { get; private set; }
    public Formation attackTarget;

    public readonly List<Soldier> soldiers = new List<Soldier>();
    public int TotalSpawned { get; private set; }

    public Vector3 AnchorPos { get; private set; }
    public Quaternion AnchorRot { get; private set; } = Quaternion.identity;
    public Vector3 AnchorForward => AnchorRot * Vector3.forward;
    public float BoundingRadius { get; private set; } = 4f;
    public Vector2 FootprintHalfExtents { get; private set; } = new Vector2(4f, 4f);

    // travel direction when the formation has somewhere to go, otherwise its facing
    public Vector3 CurrentHeading
    {
        get
        {
            if (hasDestination)
            {
                Vector3 to = destination - AnchorPos;
                to.y = 0f;
                if (to.sqrMagnitude > 0.04f) return to.normalized;
            }
            return AnchorForward;
        }
    }

    public float TotalHealth
    {
        get { float h = 0f; foreach (var s in soldiers) h += s.Health; return h; }
    }

    public float TotalMaxHealth => soldiers.Count * stats.maxHealth;

    private Vector3[] slotOffsets = new Vector3[0];
    private Vector3 destination;
    private bool hasDestination;
    private float engageTimer;
    private float noContactTime;
    private float reformTimer;
    private float promoteTimer;
    private float autoCloseTimer;
    private int lostSinceSlotRebuild;
    private int engagedCount;
    private bool dirtySinceReform;
    private float autoReformCooldown;

    // ---------------- setup ----------------

    public void Init(Team team, string name, UnitStats stats, Vector3 pos, float yawDeg, int columns, bool autoPilot)
    {
        this.team = team;
        displayName = name;
        this.stats = stats;
        this.columns = Mathf.Max(1, columns);
        this.autoPilot = autoPilot;
        AnchorPos = pos;
        AnchorRot = Quaternion.Euler(0f, yawDeg, 0f);
        transform.position = pos;
        transform.rotation = AnchorRot;
    }

    public void AddSoldier(Soldier s)
    {
        soldiers.Add(s);
        TotalSpawned++;
    }

    public void BuildSlots(int count)
    {
        int rows = Mathf.CeilToInt(count / (float)columns);
        slotOffsets = new Vector3[count];
        float maxSq = 0f, maxX = 0f, maxZ = 0f;
        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int col = i % columns;
            int inThisRow = Mathf.Min(columns, count - row * columns);
            float x = (col - (inThisRow - 1) * 0.5f) * spacing;
            float z = ((rows - 1) * 0.5f - row) * spacing;   // row 0 is the front rank
            slotOffsets[i] = new Vector3(x, 0f, z);
            maxSq = Mathf.Max(maxSq, slotOffsets[i].sqrMagnitude);
            maxX = Mathf.Max(maxX, Mathf.Abs(x));
            maxZ = Mathf.Max(maxZ, Mathf.Abs(z));
        }
        BoundingRadius = Mathf.Sqrt(maxSq) + spacing;
        FootprintHalfExtents = new Vector2(maxX + spacing * 0.5f, maxZ + spacing * 0.5f);
        lostSinceSlotRebuild = 0;
    }

    // smallest XZ distance from a battlefield point to any living soldier
    public float DistanceToNearestSoldier(Vector3 point)
    {
        float best = float.MaxValue;
        foreach (var s in soldiers)
        {
            Vector3 d = s.transform.position - point;
            d.y = 0f;
            best = Mathf.Min(best, d.sqrMagnitude);
        }
        return best == float.MaxValue ? float.MaxValue : Mathf.Sqrt(best);
    }

    // XZ distance from a battlefield point to the formation's oriented
    // rectangular footprint (0 when the point is inside it)
    public float DistanceToFootprint(Vector3 point)
    {
        Vector3 local = Quaternion.Inverse(AnchorRot) * (point - AnchorPos);
        float dx = Mathf.Max(0f, Mathf.Abs(local.x) - FootprintHalfExtents.x);
        float dz = Mathf.Max(0f, Mathf.Abs(local.z) - FootprintHalfExtents.y);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // The formation-level touch target: soldiers or the footprint rectangle,
    // whichever the point is closest to. Combat scatter and the ordered block
    // both stay grabbable this way.
    public float InteractionDistance(Vector3 point)
    {
        return Mathf.Min(DistanceToNearestSoldier(point), DistanceToFootprint(point));
    }

    public Vector3 GetSlotWorldPos(int slot)
    {
        if (slot < 0 || slot >= slotOffsets.Length) return AnchorPos;
        return AnchorPos + AnchorRot * slotOffsets[slot];
    }

    // ---------------- commands ----------------

    public void IssueMove(Vector3 dest)
    {
        dest.y = 0f;
        if (State == FormationState.Engaged || State == FormationState.BrokenRanks)
        {
            // any movement order while in melee is an attempt to disengage
            State = FormationState.Withdrawing;
            attackTarget = null;
        }
        else if (State != FormationState.Withdrawing)
        {
            State = FormationState.Ordered;
            attackTarget = null;
        }
        destination = dest;
        hasDestination = true;
    }

    public void IssueAttack(Formation target)
    {
        if (target == null || target.team == team || target.soldiers.Count == 0) return;
        attackTarget = target;
        if (State != FormationState.BrokenRanks)
            State = FormationState.Attacking;
        hasDestination = true;
    }

    // Rotate = face direction, not a wheel maneuver: the canonical facing snaps
    // to the new direction and every soldier is reassigned the new-frame slot
    // nearest to where it already stands, so each soldier pivots roughly in
    // place instead of the whole footprint orbiting the anchor. (Ordered only.)
    public void IssueFace(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;
        if (State != FormationState.Ordered) return;
        AnchorRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
        AssignNearestSlots();
    }

    public void IssueBreakRanks()
    {
        if (soldiers.Count == 0) return;
        State = FormationState.BrokenRanks;
    }

    public void IssueReform()
    {
        if (!CanReform || soldiers.Count == 0) return;

        Vector3 c = Vector3.zero;
        foreach (var s in soldiers) c += s.transform.position;
        c /= soldiers.Count;
        c.y = 0f;
        AnchorPos = c;

        // best practical smaller rectangle, roughly 2:1 wide
        int n = soldiers.Count;
        columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n * 2f)), 1, n);
        BuildSlots(n);
        AssignNearestSlots();

        attackTarget = null;
        hasDestination = false;
        reformTimer = 0f;
        dirtySinceReform = false;
        autoReformCooldown = 4f;
        State = FormationState.Reforming;
    }

    private void AssignNearestSlots()
    {
        var unassigned = new List<Soldier>(soldiers);
        for (int slot = 0; slot < slotOffsets.Length && unassigned.Count > 0; slot++)
        {
            Vector3 wp = GetSlotWorldPos(slot);
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < unassigned.Count; i++)
            {
                float d = (unassigned[i].transform.position - wp).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            unassigned[best].slotIndex = slot;
            unassigned.RemoveAt(best);
        }
    }

    public void NotifyDeath(Soldier s)
    {
        soldiers.Remove(s);
        dirtySinceReform = true;
        lostSinceSlotRebuild++;
    }

    public void SetSelected(bool sel)
    {
        IsSelected = sel;
        foreach (var s in soldiers) s.SetSelected(sel);
    }

    // ---------------- per-soldier behavior knobs ----------------

    // Local engagement allowance: the closer a soldier stands to the active
    // fight, the more slot freedom and target reach it gets; soldiers far from
    // contact stay strongly constrained. This lets uneven melee edges bend and
    // wrap slightly without the whole formation dissolving into a mob.
    private bool NearCombat(Soldier s) => s.NearestEnemyDist <= edgeEngageRadius;

    public float GetSlotWeight(Soldier s)
    {
        switch (State)
        {
            case FormationState.Ordered: return 1f;
            case FormationState.Attacking: return 0.95f;
            case FormationState.Engaged:
                if (s.IsEngaged) return 0.12f;
                return NearCombat(s) ? 0.35f : 0.75f;
            case FormationState.BrokenRanks: return 0.05f;
            case FormationState.Withdrawing: return 1f;
            case FormationState.Reforming: return 1f;
            default: return 1f;
        }
    }

    public float GetAcquireRadius(Soldier s)
    {
        float r;
        switch (State)
        {
            case FormationState.Ordered:
            case FormationState.Attacking:
                r = acquireRadiusOrdered; break;
            case FormationState.Engaged:
                r = s.IsEngaged || NearCombat(s) ? acquireRadiusEngaged
                                                 : personalEngageRadius; break;
            case FormationState.BrokenRanks:
                r = acquireRadiusBroken; break;
            default:
                return 0f;   // Withdrawing / Reforming: stop seeking engagements
        }
        if (stats.isRanged) r = Mathf.Max(r, stats.rangedRange);
        return r;
    }

    public static string StateLabel(FormationState s)
    {
        return s == FormationState.BrokenRanks ? "Broken Ranks" : s.ToString();
    }

    // ---------------- update loop ----------------

    private void Update()
    {
        if (BattleSetup.Instance == null || BattleSetup.Instance.Phase != BattlePhase.Active)
            return;

        // A destroyed formation is inert: its anchor must never keep chasing a
        // target, or its "Defeated" label follows the survivor around the map.
        if (soldiers.Count == 0)
        {
            attackTarget = null;
            hasDestination = false;
            return;
        }

        UpdateAnchorMovement();
        UpdateEngagement();
        UpdateStateMachine();
        UpdateRankReplacement();
        UpdateAutoClose();
        transform.position = AnchorPos;
        transform.rotation = AnchorRot;
        if (autoReformCooldown > 0f) autoReformCooldown -= Time.deltaTime;
    }

    // Auto-close: baseline competence of a formation trying to stay ordered.
    // Casualties leave permanently empty slots behind; once enough accumulate,
    // rebuild the slot grid for the surviving headcount (same anchor, same
    // facing, same frontage) and let everyone walk to their nearest new slot.
    // Rear soldiers flow forward and lateral holes squeeze shut over a few
    // seconds — no Reform needed for ordinary attrition. Engaged fighters keep
    // fighting (their slot pull is tiny), so combat still deforms the unit;
    // this only stops the grid from preserving empty historical positions.
    private void UpdateAutoClose()
    {
        if (State != FormationState.Ordered && State != FormationState.Attacking &&
            State != FormationState.Engaged) return;
        autoCloseTimer -= Time.deltaTime;
        if (autoCloseTimer > 0f) return;
        autoCloseTimer = autoCloseInterval;

        if (lostSinceSlotRebuild < autoCloseDeficit || soldiers.Count == 0) return;
        if (soldiers.Count >= slotOffsets.Length) return;
        columns = Mathf.Clamp(columns, 1, soldiers.Count);
        BuildSlots(soldiers.Count);
        AssignNearestSlots();
    }

    // During ordered melee, depth must matter: when a front slot's fighter surges
    // into combat or dies, the coherent soldier one row behind is promoted into
    // that slot. Front-to-back cascading compresses each column forward one step
    // per pass, feeding soldiers into the fight progressively while the rest of
    // the formation stays structured. Break Ranks stays a separate, wilder mode.
    private void UpdateRankReplacement()
    {
        if (State != FormationState.Engaged) return;
        promoteTimer -= Time.deltaTime;
        if (promoteTimer > 0f) return;
        promoteTimer = promoteInterval;

        if (slotOffsets.Length == 0 || columns <= 0 || soldiers.Count == 0) return;

        var owner = new Soldier[slotOffsets.Length];
        foreach (var s in soldiers)
            if (s.slotIndex >= 0 && s.slotIndex < owner.Length) owner[s.slotIndex] = s;

        float vac2 = promoteVacancyDist * promoteVacancyDist;
        float coh2 = promoteCoherenceDist * promoteCoherenceDist;

        for (int slot = 0; slot < slotOffsets.Length; slot++)
        {
            var holder = owner[slot];
            bool vacant = holder == null ||
                          (holder.IsEngaged &&
                           (holder.transform.position - GetSlotWorldPos(slot)).sqrMagnitude > vac2);
            if (!vacant) continue;

            int behind = slot + columns;   // same column, one row back
            if (behind >= slotOffsets.Length) continue;
            var candidate = owner[behind];
            if (candidate == null || candidate.IsEngaged) continue;
            if ((candidate.transform.position - GetSlotWorldPos(behind)).sqrMagnitude > coh2) continue;

            // advance the rear soldier; the displaced fighter rejoins at the rear
            // slot once it disengages, which naturally rotates tired ranks back
            candidate.slotIndex = slot;
            if (holder != null) holder.slotIndex = behind;
            owner[slot] = candidate;
            owner[behind] = holder;        // null lets the next row cascade forward
        }
    }

    private void UpdateAnchorMovement()
    {
        bool chasing = (State == FormationState.Attacking ||
                        State == FormationState.BrokenRanks) && attackTarget != null;
        bool canMove = State == FormationState.Ordered ||
                       State == FormationState.Withdrawing || chasing;
        if (!canMove) return;

        if (chasing)
        {
            if (attackTarget.soldiers.Count == 0)
            {
                attackTarget = null;
                if (State == FormationState.Attacking) State = FormationState.Ordered;
                hasDestination = false;
                return;
            }
            destination = attackTarget.AnchorPos;
            hasDestination = true;
        }
        if (!hasDestination) return;

        Vector3 to = destination - AnchorPos;
        to.y = 0f;
        float dist = to.magnitude;
        // Ranged formations hold their preferred firing distance instead of
        // marching into melee range; melee closes to contact.
        float stopDist = chasing
            ? (stats.isRanged ? stats.rangedPreferredRange : meleeChaseStopDist)
            : 0.2f;
        if (dist <= stopDist)
        {
            if (chasing && dist > 0.5f)
            {
                // hold position but wheel to face the target (archer firing line)
                Quaternion face = Quaternion.LookRotation(to / dist, Vector3.up);
                AnchorRot = Quaternion.RotateTowards(AnchorRot, face, rotateSpeedDeg * Time.deltaTime);
            }
            if (!chasing) hasDestination = false;
            return;
        }
        Vector3 dir = to / dist;

        if (State == FormationState.Withdrawing)
        {
            // back away without wheeling the whole grid through the melee
            AnchorPos += dir * Mathf.Min(moveSpeed * 0.85f * Time.deltaTime, dist);
            return;
        }

        Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
        AnchorRot = Quaternion.RotateTowards(AnchorRot, want, rotateSpeedDeg * Time.deltaTime);
        float align = Vector3.Dot(AnchorForward, dir);
        if (align > 0.3f)
            AnchorPos += AnchorForward * Mathf.Min(moveSpeed * align * Time.deltaTime, dist);
    }

    private void UpdateEngagement()
    {
        engageTimer -= Time.deltaTime;
        if (engageTimer > 0f) return;
        engageTimer = 0.25f;

        if (BattleSetup.Instance == null) return;
        var enemies = BattleSetup.Instance.GetSoldiers(team == Team.Blue ? Team.Red : Team.Blue);

        engagedCount = 0;
        bool anyWithinDisengage = false;
        float er2 = personalEngageRadius * personalEngageRadius;
        float dr2 = disengageRadius * disengageRadius;

        foreach (var s in soldiers)
        {
            bool engaged = false;
            float best2 = float.MaxValue;
            Vector3 p = s.transform.position;
            foreach (var e in enemies)
            {
                float d2 = (e.transform.position - p).sqrMagnitude;
                if (d2 < best2) best2 = d2;
                if (d2 < er2) { engaged = true; anyWithinDisengage = true; break; }
                if (d2 < dr2) anyWithinDisengage = true;
            }
            s.IsEngaged = engaged;
            s.NearestEnemyDist = best2 == float.MaxValue ? float.MaxValue : Mathf.Sqrt(best2);
            if (engaged) engagedCount++;
        }

        CanReform = !anyWithinDisengage && soldiers.Count > 0 && State != FormationState.Reforming;
    }

    private void UpdateStateMachine()
    {
        switch (State)
        {
            case FormationState.Ordered:
            case FormationState.Attacking:
                if (engagedCount > 0)
                {
                    State = FormationState.Engaged;
                    hasDestination = false;
                    noContactTime = 0f;
                }
                break;

            case FormationState.Engaged:
                if (engagedCount == 0)
                {
                    noContactTime += Time.deltaTime;
                    if (noContactTime > 1.5f)
                    {
                        if (attackTarget != null && attackTarget.soldiers.Count > 0)
                        {
                            State = FormationState.Attacking;   // re-close with the target
                        }
                        else
                        {
                            State = FormationState.Ordered;     // auto-close compacts the stragglers
                            attackTarget = null;
                            hasDestination = false;
                        }
                    }
                }
                else noContactTime = 0f;
                break;

            case FormationState.Reforming:
                reformTimer += Time.deltaTime;
                bool done = true;
                foreach (var s in soldiers)
                {
                    if ((s.transform.position - GetSlotWorldPos(s.slotIndex)).sqrMagnitude > 0.36f)
                    {
                        done = false;
                        break;
                    }
                }
                if (done || reformTimer > 12f) State = FormationState.Ordered;
                break;
        }

        // red side tidies itself up, but only while genuinely idle — never
        // mid-march toward an AI-issued attack target
        if (autoPilot && CanReform && dirtySinceReform && autoReformCooldown <= 0f &&
            State == FormationState.Ordered && attackTarget == null && soldiers.Count > 0)
        {
            IssueReform();
        }
    }
}
