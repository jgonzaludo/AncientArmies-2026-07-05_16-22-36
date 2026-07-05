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

    public FormationState State { get; private set; } = FormationState.Ordered;
    public bool CanReform { get; private set; }
    public bool IsSelected { get; private set; }
    public Formation attackTarget;

    public readonly List<Soldier> soldiers = new List<Soldier>();
    public int TotalSpawned { get; private set; }

    public Vector3 AnchorPos { get; private set; }
    public Quaternion AnchorRot { get; private set; } = Quaternion.identity;
    public Vector3 AnchorForward => AnchorRot * Vector3.forward;

    private Vector3[] slotOffsets = new Vector3[0];
    private Vector3 destination;
    private bool hasDestination;
    private float engageTimer;
    private float noContactTime;
    private float reformTimer;
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
        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int col = i % columns;
            int inThisRow = Mathf.Min(columns, count - row * columns);
            float x = (col - (inThisRow - 1) * 0.5f) * spacing;
            float z = ((rows - 1) * 0.5f - row) * spacing;   // row 0 is the front rank
            slotOffsets[i] = new Vector3(x, 0f, z);
        }
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
    }

    public void SetSelected(bool sel)
    {
        IsSelected = sel;
        foreach (var s in soldiers) s.SetSelected(sel);
    }

    // ---------------- per-soldier behavior knobs ----------------

    public float GetSlotWeight(Soldier s)
    {
        switch (State)
        {
            case FormationState.Ordered: return 1f;
            case FormationState.Attacking: return 0.95f;
            case FormationState.Engaged: return s.IsEngaged ? 0.12f : 0.7f;
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
                r = s.IsEngaged ? acquireRadiusEngaged : personalEngageRadius; break;
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
        UpdateAnchorMovement();
        UpdateEngagement();
        UpdateStateMachine();
        transform.position = AnchorPos;
        transform.rotation = AnchorRot;
        if (autoReformCooldown > 0f) autoReformCooldown -= Time.deltaTime;
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
        float stopDist = chasing ? 3.5f : 0.2f;
        if (dist <= stopDist)
        {
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
            Vector3 p = s.transform.position;
            foreach (var e in enemies)
            {
                float d2 = (e.transform.position - p).sqrMagnitude;
                if (d2 < er2) { engaged = true; anyWithinDisengage = true; break; }
                if (d2 < dr2) anyWithinDisengage = true;
            }
            s.IsEngaged = engaged;
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
                            State = FormationState.Ordered;     // ragged: gaps stay until Reform
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

        // red side tidies itself up once combat has moved away
        if (autoPilot && CanReform && dirtySinceReform && autoReformCooldown <= 0f &&
            State != FormationState.Reforming && soldiers.Count > 0)
        {
            IssueReform();
        }
    }
}
