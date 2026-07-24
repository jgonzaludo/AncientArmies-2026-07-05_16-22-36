using System.Collections.Generic;
using UnityEngine;

// Phase 7: a Roman-inspired coordinated commander for the 8-century red army
// (6 melee + 2 archers). Two layers replace the flat V1.1 target-scorer:
//
//   Battle phases:  Advance -> Engagement.
//     Advance    — the army marches as a structure to a phase line short of
//                  the middle: 4 melee centuries abreast (the main line),
//                  2 melee held back as reserves, archers tucked behind.
//     Engagement — per-century tasking on a decision cadence.
//
//   Roles (classified once at battle start):
//     MainLine — lane-based target scoring (the V1.1 score plus a
//                lane-alignment bonus so centuries fight what is in front of
//                them instead of crossing the field), with switchMargin
//                hysteresis so orders stay sticky.
//     Archer   — explicit range bands: withdraw behind the main line when
//                threatened, shoot what is in range, otherwise reposition to
//                a supporting spot behind the line. Never ordered into melee
//                and never given targets beyond bow range (so an attack
//                order can never march them past the main line).
//     Reserve  — hold position untasked until a commitment trigger (a
//                main-line century breaking or dropping under strength, a
//                blue formation rounding a flank, or blue reaching the
//                archers), then commit ONE reserve and lock it for a
//                cooldown; a committed reserve behaves like main line.
//
// The commander only speaks the public Formation API the player has
// (IssueMove / IssueAttack / IssueCharge / IssueReform) — the formation state
// machine executes everything, no soldier or transform is ever touched, and no
// hidden information or stat cheats are used.
public class EnemyCommander : MonoBehaviour
{
    private enum CommanderPhase { Advance, Engagement }
    private enum Role { MainLine, Reserve, Archer }

    [Header("Cadence")]
    [Tooltip("Seconds between strategic passes")]
    public float decisionInterval = 1.5f;

    [Header("Advance phase")]
    [Tooltip("Where the main line halts, as a fraction of the half-separation toward the enemy (0 = spawn line, 1 = midfield)")]
    public float phaseLineFraction = 0.18f;
    [Tooltip("How far behind the main line the reserves take post (world units)")]
    public float reserveBackset = 25f;
    [Tooltip("How far behind the main line the archers take post (world units)")]
    public float archerBackset = 15f;
    [Tooltip("Any blue formation this close to any red formation ends the Advance phase")]
    public float engageThreshold = 45f;

    [Header("Main line target scoring (world-unit currency: 1 point = 1 meter closer)")]
    [Tooltip("Score lost per friendly formation already attacking a target")]
    public float saturationPenalty = 18f;
    [Tooltip("How much of the saturation penalty an archer assignment contributes (melee counts fully)")]
    public float archerAssignWeight = 0.5f;
    [Tooltip("Melee-only bonus for targets threatening our archer formations")]
    public float archerThreatBonus = 40f;
    [Tooltip("Bonus when the candidate is currently attacking the scoring formation")]
    public float attackingMeBonus = 15f;
    [Tooltip("Max bonus for finishing a nearly-dead target (scaled by missing health)")]
    public float finishBonus = 10f;
    [Tooltip("Lane discipline: score lost per meter of lateral (x) offset between a century and a candidate target — keeps the line fighting straight ahead")]
    public float laneWeight = 0.6f;
    [Tooltip("A new target must beat the current one's score by this margin before switching")]
    public float switchMargin = 25f;

    [Header("Archers")]
    [Tooltip("A blue formation this close to an archer century forces it to withdraw behind the main line")]
    public float dangerRadius = 14f;
    [Tooltip("How far behind the main line's average z a withdrawing archer century rallies")]
    public float archerWithdrawBackset = 18f;
    [Tooltip("Archers farther than this from their supporting post walk back to it (when idle and not shooting)")]
    public float archerRepostTolerance = 6f;

    [Header("Reserves")]
    [Tooltip("A main-line century below this fraction of its spawned strength triggers a reserve commitment")]
    public float weakStrengthFraction = 0.4f;
    [Tooltip("A blue formation this close to the main line's left/right flank extreme triggers a reserve commitment")]
    public float flankRadius = 30f;
    [Tooltip("Seconds a freshly committed reserve keeps its order before the main-line scorer may re-task it")]
    public float commitCooldown = 12f;

    [Header("Reform")]
    [Tooltip("A broken red century reforms only when no blue formation is within this distance of it")]
    public float reformSafeRadius = 20f;

    private CommanderPhase phase = CommanderPhase.Advance;
    private bool classified;
    private float timer;

    // Role rosters, filled once at battle start and pruned as centuries die.
    // A committed reserve migrates from reserves to mainLine permanently.
    private readonly List<Formation> mainLine = new List<Formation>();
    private readonly List<Formation> reserves = new List<Formation>();
    private readonly List<Formation> archers = new List<Formation>();

    // Per-tick scratch, reused so the decision loop never allocates.
    private readonly List<Formation> reds = new List<Formation>();
    private readonly List<Formation> blues = new List<Formation>();
    private readonly List<Formation> archerThreats = new List<Formation>();
    private readonly Dictionary<Formation, float> commitLockUntil =
        new Dictionary<Formation, float>();

    private void Update()
    {
        if (BattleSetup.Instance == null || BattleSetup.Instance.Phase != BattlePhase.Active)
            return;   // no AI before Start is pressed or after the battle ends

        if (!classified)
        {
            ClassifyArmy();
            IssueAdvanceOrders();
            classified = true;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = decisionInterval;
        Think();
    }

    // ---------------- battle start: roles + march orders ----------------

    // Red archers are whoever carries ranged stats; of the melee, the two
    // closest to the rear/center become the reserve (red faces -Z, so rear is
    // larger z; |x| breaks ties toward the center) and the rest form the line.
    private void ClassifyArmy()
    {
        mainLine.Clear();
        reserves.Clear();
        archers.Clear();
        foreach (var f in BattleSetup.Instance.formations)
        {
            if (f.team != Team.Red || f.soldiers.Count == 0) continue;
            if (f.stats.isRanged) archers.Add(f);
            else mainLine.Add(f);
        }

        // Pull the two most rear/central melee centuries out as reserves.
        int reserveCount = Mathf.Min(2, Mathf.Max(0, mainLine.Count - 1));
        for (int r = 0; r < reserveCount; r++)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < mainLine.Count; i++)
            {
                Vector3 p = mainLine[i].AnchorPos;
                float s = p.z - 0.5f * Mathf.Abs(p.x);   // rear first, center as tiebreak
                if (s > bestScore) { bestScore = s; best = i; }
            }
            reserves.Add(mainLine[best]);
            mainLine.RemoveAt(best);
        }

        // Left-to-right lane order for the line (stable for lane positions).
        mainLine.Sort((a, b) => a.AnchorPos.x.CompareTo(b.AnchorPos.x));
    }

    // Advance as a structure: the main line moves abreast to evenly spread
    // lane positions on the phase line (preserving left-to-right order and
    // roughly its current frontage), reserves and archers to posts behind it.
    // No attack targets are picked from spawn — targeting starts at contact.
    private void IssueAdvanceOrders()
    {
        float lineZ = PhaseLineZ();

        if (mainLine.Count > 0)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var f in mainLine)
            {
                minX = Mathf.Min(minX, f.AnchorPos.x);
                maxX = Mathf.Max(maxX, f.AnchorPos.x);
            }
            float span = maxX - minX;
            for (int i = 0; i < mainLine.Count; i++)
            {
                float t = mainLine.Count > 1 ? i / (float)(mainLine.Count - 1) : 0.5f;
                mainLine[i].IssueMove(ClampToField(
                    new Vector3(minX + span * t, 0f, lineZ)));
            }
        }

        // Reserves and archers keep their own lateral station behind the line.
        foreach (var f in reserves)
            f.IssueMove(ClampToField(new Vector3(f.AnchorPos.x, 0f, lineZ + reserveBackset)));
        foreach (var f in archers)
            f.IssueMove(ClampToField(new Vector3(f.AnchorPos.x, 0f, lineZ + archerBackset)));
    }

    // Red spawns at +z facing -z, so its phase line sits at positive z, a
    // configurable fraction of the way from spawn toward midfield.
    private float PhaseLineZ()
    {
        return phaseLineFraction * BattleSetup.Instance.armySeparation * 0.5f;
    }

    private Vector3 ClampToField(Vector3 p)
    {
        var bs = BattleSetup.Instance;
        float m = bs.fieldEdgeMargin;
        p.x = Mathf.Clamp(p.x, -bs.fieldHalfX + m, bs.fieldHalfX - m);
        p.z = Mathf.Clamp(p.z, -bs.fieldHalfZ + m, bs.fieldHalfZ - m);
        return p;
    }

    // ---------------- strategic pass ----------------

    private void Think()
    {
        RefreshRosters();
        if (blues.Count == 0) return;

        // Broken red centuries pull themselves together whenever it is safe,
        // regardless of battle phase — reforming is defensive housekeeping.
        TryReformBroken();

        if (phase == CommanderPhase.Advance)
        {
            if (ShouldEnterEngagement()) phase = CommanderPhase.Engagement;
            else return;   // keep marching; no targeting from spawn
        }

        FindArcherThreats();
        RunMainLine();
        RunArchers();
        RunReserves();
    }

    // Rebuild the live red/blue lists and prune dead centuries out of the
    // role rosters. Reverse loops keep this allocation-free.
    private void RefreshRosters()
    {
        reds.Clear();
        blues.Clear();
        foreach (var f in BattleSetup.Instance.formations)
        {
            if (f.soldiers.Count == 0) continue;
            if (f.team == Team.Red) reds.Add(f);
            else blues.Add(f);
        }
        for (int i = mainLine.Count - 1; i >= 0; i--)
            if (mainLine[i].soldiers.Count == 0) mainLine.RemoveAt(i);
        for (int i = reserves.Count - 1; i >= 0; i--)
            if (reserves[i].soldiers.Count == 0) reserves.RemoveAt(i);
        for (int i = archers.Count - 1; i >= 0; i--)
            if (archers[i].soldiers.Count == 0) archers.RemoveAt(i);
    }

    // Advance ends when the fight finds us or the line is in position:
    // any red century engaged, the main line's average z at the phase line,
    // or any blue formation inside engageThreshold of any red formation.
    private bool ShouldEnterEngagement()
    {
        foreach (var f in reds)
            if (f.State == FormationState.Engaged) return true;

        if (mainLine.Count > 0 && MainLineAverageZ() <= PhaseLineZ() + 1f)
            return true;

        float t2 = engageThreshold * engageThreshold;
        foreach (var b in blues)
            foreach (var r in reds)
            {
                Vector3 d = b.AnchorPos - r.AnchorPos;
                d.y = 0f;
                if (d.sqrMagnitude < t2) return true;
            }
        return false;
    }

    private float MainLineAverageZ()
    {
        if (mainLine.Count == 0) return PhaseLineZ();
        float z = 0f;
        foreach (var f in mainLine) z += f.AnchorPos.z;
        return z / mainLine.Count;
    }

    // ---------------- main line ----------------

    // Lane-based scored targeting with hysteresis (the V1.1 scorer plus lane
    // alignment). Engaged / broken / reforming / withdrawing centuries belong
    // to the formation state machine; a reserve inside its commit cooldown
    // keeps the order it was committed with.
    private void RunMainLine()
    {
        foreach (var f in mainLine)
        {
            if (f.State != FormationState.Ordered && f.State != FormationState.Attacking)
                continue;
            if (commitLockUntil.TryGetValue(f, out float lockUntil) && Time.time < lockUntil)
                continue;

            Formation current = (f.attackTarget != null && f.attackTarget.soldiers.Count > 0)
                ? f.attackTarget : null;

            Formation best = null;
            float bestScore = float.MinValue;
            foreach (var t in blues)
            {
                float s = Score(f, t);
                if (s > bestScore) { bestScore = s; best = t; }
            }
            if (best == null) continue;

            if (current == null)
            {
                // idle, or its target was destroyed: take the best assignment.
                // The order sets f.attackTarget immediately, so centuries
                // decided later this tick already see this as saturation.
                IssueAttackOrCharge(f, best);
            }
            else if (best != current && bestScore > Score(f, current) + switchMargin)
            {
                // only a clearly better plan (e.g. an archer emergency)
                // interrupts a valid existing order — no thrashing
                IssueAttackOrCharge(f, best);
            }
        }
    }

    // Discipline is a resource: a century charges only a target that is
    // already beaten — badly under strength or broken — and inside charge
    // reach. Everything else gets the controlled attack.
    private void IssueAttackOrCharge(Formation f, Formation t)
    {
        bool weak = t.soldiers.Count < t.TotalSpawned * weakStrengthFraction ||
                    t.State == FormationState.BrokenRanks;
        if (weak && f.GetChargeBlock() == Formation.ChargeBlock.None)
        {
            Vector3 d = t.AnchorPos - f.AnchorPos;
            d.y = 0f;
            if (d.sqrMagnitude <= f.chargeRange * f.chargeRange)
            {
                f.IssueCharge(t);
                return;
            }
        }
        f.IssueAttack(t);
    }

    // Formation-level target score; bigger is better. All factors share one
    // currency (world units) so the tunables above read as "worth N meters".
    private float Score(Formation f, Formation t)
    {
        Vector3 d = t.AnchorPos - f.AnchorPos;
        d.y = 0f;
        float dist = d.magnitude;
        float score = -dist;                       // closer targets are better

        // lane alignment: fight what is in front of you, not across the field
        score -= laneWeight * Mathf.Abs(d.x);

        // saturation: the rest of the army's existing pressure on this target
        float pressure = 0f;
        foreach (var ally in reds)
        {
            if (ally == f || ally.attackTarget != t) continue;
            pressure += ally.stats.isRanged ? archerAssignWeight : 1f;
        }
        score -= pressure * saturationPenalty;

        if (t.attackTarget == f) score += attackingMeBonus;

        float hp = t.TotalHealth / Mathf.Max(1f, t.TotalMaxHealth);
        score += finishBonus * (1f - hp);          // modest finishing preference

        // strongly prefer intercepting whatever is on our archers
        if (archerThreats.Contains(t)) score += archerThreatBonus;

        return score;
    }

    // Blue formations that are near our archer centuries or actively
    // attacking them. Recomputed once per strategic pass.
    private void FindArcherThreats()
    {
        archerThreats.Clear();
        float r2 = dangerRadius * dangerRadius;
        foreach (var red in archers)
        {
            foreach (var b in blues)
            {
                if (archerThreats.Contains(b)) continue;
                if (b.attackTarget == red ||
                    (b.AnchorPos - red.AnchorPos).sqrMagnitude < r2)
                    archerThreats.Add(b);
            }
        }
    }

    // ---------------- archers ----------------

    // Explicit range bands, checked in priority order:
    //   1. danger  — a blue formation inside dangerRadius: withdraw to a spot
    //                behind the main line (own x, average line z + backset).
    //                Archers never receive melee attack orders.
    //   2. shoot   — a blue target inside rangedRange: IssueAttack; the
    //                formation's own movement already holds preferred range.
    //                Because the target is already in bow range, the order
    //                can never march the century past the main line.
    //   3. support — nothing shootable: walk to the supporting post behind
    //                the line rather than chasing targets across the field.
    private void RunArchers()
    {
        float lineZ = MainLineAverageZ();
        foreach (var f in archers)
        {
            // Broken / reforming archers are the state machine's problem;
            // withdrawal orders are still legal from Engaged (they become a
            // Withdrawing move), which is exactly what a swarmed archer wants.
            if (f.State != FormationState.Ordered &&
                f.State != FormationState.Attacking &&
                f.State != FormationState.Engaged)
                continue;

            Formation nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var b in blues)
            {
                Vector3 d = b.AnchorPos - f.AnchorPos;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < nearestDist) { nearestDist = dist; nearest = b; }
            }
            if (nearest == null) continue;

            if (nearestDist < dangerRadius)
            {
                // withdraw behind the friendly line, keeping this century's lane
                f.IssueMove(ClampToField(
                    new Vector3(f.AnchorPos.x, 0f, lineZ + archerWithdrawBackset)));
                continue;
            }

            if (f.State == FormationState.Engaged)
                continue;   // in contact but not in immediate danger band: let it fight

            if (nearestDist <= f.stats.rangedRange)
            {
                if (f.attackTarget != nearest || f.State != FormationState.Attacking)
                    f.IssueAttack(nearest);
                continue;
            }

            // Nothing in range: hold/regain the supporting post behind the
            // line. Don't fidget while mid-volley or already near the post.
            if (f.IsFiring) continue;
            Vector3 post = ClampToField(
                new Vector3(f.AnchorPos.x, 0f, lineZ + archerBackset));
            Vector3 toPost = post - f.AnchorPos;
            toPost.y = 0f;
            if (toPost.magnitude > archerRepostTolerance)
                f.IssueMove(post);
        }
    }

    // ---------------- reserves ----------------

    // Reserves hold their post untasked until something goes wrong, then ONE
    // reserve (the closer of the two) is committed at the problem and locked
    // for commitCooldown so the scorer can't immediately re-plan it away.
    // Once committed, a reserve is main line for the rest of the battle.
    private void RunReserves()
    {
        if (reserves.Count == 0) return;

        Formation threat = null;      // blue formation to counterattack
        Vector3 crisisPoint = default;
        bool hasCrisis = false;

        // Trigger 1: a main-line century breaking or under strength — commit
        // at its attacker if it has one, otherwise just move to shore it up.
        foreach (var f in mainLine)
        {
            bool broken = f.State == FormationState.BrokenRanks;
            bool weak = f.TotalSpawned > 0 &&
                        f.soldiers.Count < f.TotalSpawned * weakStrengthFraction;
            if (!broken && !weak) continue;
            hasCrisis = true;
            crisisPoint = f.AnchorPos;
            threat = FindNearestBlue(f.AnchorPos, float.MaxValue);
            break;
        }

        // Trigger 2: a blue formation rounding the line's flank extremes.
        if (!hasCrisis && mainLine.Count > 0)
        {
            Formation left = mainLine[0], right = mainLine[0];
            foreach (var f in mainLine)
            {
                if (f.AnchorPos.x < left.AnchorPos.x) left = f;
                if (f.AnchorPos.x > right.AnchorPos.x) right = f;
            }
            float r2 = flankRadius * flankRadius;
            foreach (var b in blues)
            {
                bool outsideLine = b.AnchorPos.x < left.AnchorPos.x ||
                                   b.AnchorPos.x > right.AnchorPos.x;
                if (!outsideLine) continue;
                Formation flank = b.AnchorPos.x < left.AnchorPos.x ? left : right;
                Vector3 d = b.AnchorPos - flank.AnchorPos;
                d.y = 0f;
                if (d.sqrMagnitude < r2)
                {
                    hasCrisis = true;
                    threat = b;
                    crisisPoint = b.AnchorPos;
                    break;
                }
            }
        }

        // Trigger 3: a blue formation reaching the archers.
        if (!hasCrisis)
        {
            foreach (var a in archers)
            {
                Formation b = FindNearestBlue(a.AnchorPos, dangerRadius);
                if (b == null) continue;
                hasCrisis = true;
                threat = b;
                crisisPoint = b.AnchorPos;
                break;
            }
        }

        if (!hasCrisis) return;

        // Commit the reserve closest to the crisis, lock it, and promote it.
        int bestIdx = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < reserves.Count; i++)
        {
            Vector3 d = crisisPoint - reserves[i].AnchorPos;
            d.y = 0f;
            float d2 = d.sqrMagnitude;
            if (d2 < bestD) { bestD = d2; bestIdx = i; }
        }
        Formation reserve = reserves[bestIdx];
        reserves.RemoveAt(bestIdx);
        mainLine.Add(reserve);
        commitLockUntil[reserve] = Time.time + commitCooldown;

        if (threat != null) reserve.IssueAttack(threat);
        else reserve.IssueMove(ClampToField(crisisPoint));
    }

    // Nearest living blue formation to a point, optionally within maxDist.
    private Formation FindNearestBlue(Vector3 point, float maxDist)
    {
        Formation best = null;
        float best2 = maxDist * maxDist;
        foreach (var b in blues)
        {
            Vector3 d = b.AnchorPos - point;
            d.y = 0f;
            float d2 = d.sqrMagnitude;
            if (d2 < best2) { best2 = d2; best = b; }
        }
        return best;
    }

    // ---------------- reform ----------------

    // A broken red century reforms itself once the fight has moved on: no
    // blue formation within reformSafeRadius and the formation reports it can.
    private void TryReformBroken()
    {
        foreach (var f in reds)
        {
            if (f.State != FormationState.BrokenRanks || !f.CanReform) continue;
            if (FindNearestBlue(f.AnchorPos, reformSafeRadius) != null) continue;
            f.IssueReform();
        }
    }
}
