using UnityEngine;

// Autonomous commander for the red army. Deliberately simple for V1: each red
// formation independently picks the nearest living blue formation and attacks
// it, re-targeting whenever its target is destroyed. Approach, archer
// preferred-range behavior, melee contact, disorder, and auto-reform all come
// from the same Formation/Soldier systems the player uses — the AI only
// issues the same orders a player could.
public class EnemyCommander : MonoBehaviour
{
    [Tooltip("Seconds between AI decision passes")]
    public float decisionInterval = 1f;

    private float timer;

    private void Update()
    {
        if (BattleSetup.Instance == null || BattleSetup.Instance.Phase != BattlePhase.Active)
            return;   // no AI before Start is pressed or after the battle ends

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = decisionInterval;

        foreach (var f in BattleSetup.Instance.formations)
        {
            if (f.team != Team.Red || f.soldiers.Count == 0) continue;
            if (!NeedsOrder(f)) continue;
            Formation target = NearestEnemy(f);
            if (target != null) f.IssueAttack(target);
        }
    }

    // Only (re)issue orders when the formation is genuinely idle or its target
    // is gone. Engaged / reforming formations are left to the existing
    // formation systems until they resolve on their own.
    private static bool NeedsOrder(Formation f)
    {
        if (f.State == FormationState.Engaged ||
            f.State == FormationState.Reforming ||
            f.State == FormationState.Withdrawing ||
            f.State == FormationState.BrokenRanks)
            return false;
        return f.attackTarget == null || f.attackTarget.soldiers.Count == 0;
    }

    private static Formation NearestEnemy(Formation f)
    {
        Formation best = null;
        float bestD = float.MaxValue;
        foreach (var e in BattleSetup.Instance.formations)
        {
            if (e.team == f.team || e.soldiers.Count == 0) continue;
            float d = (e.AnchorPos - f.AnchorPos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }
}
