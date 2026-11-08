using UnityEngine;

// admin actions for testing, one static call each. the admin panel in BattleHUD (editor and development builds
// only) is the only caller, so none of this can reach a release build.
public static class AdminCommands
{
    public static void Win() { if (BattleSetup.Instance != null) BattleSetup.Instance.AdminEnd("BLUE WINS, COLLAPSED"); }
    public static void Lose() { if (BattleSetup.Instance != null) BattleSetup.Instance.AdminEnd("RED WINS, COLLAPSED"); }
    public static void Draw() { if (BattleSetup.Instance != null) BattleSetup.Instance.AdminEnd("DRAW, TIME"); }

    // routs up to `count` live formations of a side (the real rout path, so the collapse rule fires on its own)
    public static void Rout(Team team, int count)
    {
        var bs = BattleSetup.Instance; if (bs == null) return;
        foreach (var f in bs.formations)
        {
            if (count <= 0) break;
            if (f.team != team || f.soldiers.Count == 0 || f.IsRouting) continue;
            f.AdminRout(); count--;
        }
    }

    // kills every soldier of a side at once
    public static void Wipe(Team team)
    {
        var bs = BattleSetup.Instance; if (bs == null) return;
        foreach (var s in new System.Collections.Generic.List<Soldier>(bs.GetSoldiers(team))) if (s != null) s.TakeDamage(99999f);
    }

    public static void Speed(float x) { if (x <= 1f) SimSpeed.Reset(); else SimSpeed.Set(x, false); }
}
