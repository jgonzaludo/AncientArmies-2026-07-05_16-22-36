using System.Collections.Generic;
using UnityEngine;

// V1.1: one lightweight commander coordinating the red army, replacing V1's
// independent nearest-target bots. Still issues only the ordinary
// formation-level commands the player has (IssueAttack); the existing
// formation state machine executes everything. Decisions are formation-level,
// scored, and sticky:
//
//   score = -distance
//           - saturationPenalty * (friendly formations already on the target)
//           + archerThreatBonus   (melee only, target threatens our archers)
//           + attackingMeBonus    (target is attacking the scoring formation)
//           + finishBonus * missing-health fraction
//           + inRangeBonus        (archers only, target already shootable)
//
// A formation with a valid order keeps it unless a rival target beats it by
// switchMargin, so the army doesn't thrash between similar options. Engaged /
// reforming / broken formations are never re-tasked mid-fight.
public class EnemyCommander : MonoBehaviour
{
    [Header("Cadence")]
    [Tooltip("Seconds between strategic passes")]
    public float decisionInterval = 1f;

    [Header("Target scoring (world-unit currency: 1 point = 1 meter closer)")]
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
    [Tooltip("Archer-only bonus for targets already inside max shooting range")]
    public float inRangeBonus = 12f;

    [Header("Threat and persistence")]
    [Tooltip("Blue formations within this distance of red archers count as archer threats")]
    public float archerProtectRadius = 18f;
    [Tooltip("A new target must beat the current one's score by this margin before switching")]
    public float switchMargin = 25f;

    private float timer;
    private readonly List<Formation> reds = new List<Formation>();
    private readonly List<Formation> blues = new List<Formation>();
    private readonly List<Formation> archerThreats = new List<Formation>();

    private void Update()
    {
        if (BattleSetup.Instance == null || BattleSetup.Instance.Phase != BattlePhase.Active)
            return;   // no AI before Start is pressed or after the battle ends

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = decisionInterval;
        Think();
    }

    private void Think()
    {
        reds.Clear();
        blues.Clear();
        foreach (var f in BattleSetup.Instance.formations)
        {
            if (f.soldiers.Count == 0) continue;
            if (f.team == Team.Red) reds.Add(f);
            else blues.Add(f);
        }
        if (blues.Count == 0) return;

        FindArcherThreats();

        foreach (var f in reds)
        {
            // Engaged / broken / reforming / withdrawing formations belong to
            // the formation state machine; re-tasking mid-melee just churns.
            if (f.State != FormationState.Ordered && f.State != FormationState.Attacking)
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
                // IssueAttack sets f.attackTarget immediately, so formations
                // decided later this tick already see this as saturation.
                f.IssueAttack(best);
            }
            else if (best != current && bestScore > Score(f, current) + switchMargin)
            {
                // only a clearly better plan (e.g. an archer emergency)
                // interrupts a valid existing order — no thrashing
                f.IssueAttack(best);
            }
        }
    }

    // Blue formations that are near our archer formations or actively
    // attacking them. Recomputed once per strategic pass.
    private void FindArcherThreats()
    {
        archerThreats.Clear();
        float r2 = archerProtectRadius * archerProtectRadius;
        foreach (var red in reds)
        {
            if (!red.stats.isRanged) continue;
            foreach (var b in blues)
            {
                if (archerThreats.Contains(b)) continue;
                if (b.attackTarget == red ||
                    (b.AnchorPos - red.AnchorPos).sqrMagnitude < r2)
                    archerThreats.Add(b);
            }
        }
    }

    // Formation-level target score; bigger is better. All factors share one
    // currency (world units) so the tunables above read as "worth N meters".
    private float Score(Formation f, Formation t)
    {
        Vector3 d = t.AnchorPos - f.AnchorPos;
        d.y = 0f;
        float dist = d.magnitude;
        float score = -dist;                       // closer targets are better

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

        if (f.stats.isRanged)
        {
            // archers: prefer what they can shoot without walking anywhere
            if (dist <= f.stats.rangedRange) score += inRangeBonus;
        }
        else
        {
            // melee: strongly prefer intercepting whatever is on our archers
            if (archerThreats.Contains(t)) score += archerThreatBonus;
        }
        return score;
    }
}
