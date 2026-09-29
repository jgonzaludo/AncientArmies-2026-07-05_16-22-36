#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// Test tooling: run full no-input battles back to back with overrides, a seed
// and a speed, one recorder file each. Call from Play mode (CLI eval):
//   ScenarioRunner.Run("P0:; P1:shockMultiplier=1.5,contagionShock=10", 12345, 20f, true);
// then poll ScenarioRunner.Status until it starts with "done". Overrides are
// any MoraleConfig or CombatConfig field by name. Everything overridden is
// restored afterwards (the asset instances are edited in memory only).
// scenario: "" full battle | "flank" | "lines".
public class ScenarioRunner : MonoBehaviour
{
    public static string Status { get; private set; } = "idle";
    public static List<string> ResultFiles { get; } = new List<string>();

    private struct Preset { public string name; public Dictionary<string, string> overrides; }
    private static ScenarioRunner instance;

    public static string Run(string presets, int seed, float speed, bool renderOff = true, bool aiOn = true, string scenario = "")
    {
        if (!Application.isPlaying) return "ERROR: enter Play mode first";
        if (instance != null) Destroy(instance.gameObject);
        var go = new GameObject("ScenarioRunner");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ScenarioRunner>();
        instance.StartCoroutine(instance.RunAll(Parse(presets), seed, speed, renderOff, aiOn, scenario));
        return "started " + presets;
    }

    private static List<Preset> Parse(string s)
    {
        var list = new List<Preset>();
        foreach (var chunk in s.Split(';'))
        {
            if (chunk.Trim() == "") continue;
            int colon = chunk.IndexOf(':');
            var p = new Preset { name = (colon < 0 ? chunk : chunk.Substring(0, colon)).Trim(), overrides = new Dictionary<string, string>() };
            string body = colon < 0 ? "" : chunk.Substring(colon + 1);
            foreach (var kv in body.Split(','))
            {
                int eq = kv.IndexOf('=');
                if (eq > 0) p.overrides[kv.Substring(0, eq).Trim()] = kv.Substring(eq + 1).Trim();
            }
            list.Add(p);
        }
        return list;
    }

    private IEnumerator RunAll(List<Preset> presets, int seed, float speed, bool renderOff, bool aiOn, string scenario)
    {
        ResultFiles.Clear();
        var cfg = BattleSetup.Instance != null ? BattleSetup.Instance.moraleConfig : null;
        if (cfg == null) { Status = "done ERROR: no MoraleConfig"; yield break; }
        // snapshot every config field so all overrides can be restored
        var saved = new Dictionary<FieldInfo, object>();
        foreach (var fi in typeof(MoraleConfig).GetFields(BindingFlags.Public | BindingFlags.Instance)) saved[fi] = fi.GetValue(cfg);
        CombatConfig combat = LoadCombatAsset() ?? BattleSetup.Instance.combatConfig;
        var savedCombat = new Dictionary<FieldInfo, object>();
        foreach (var fi in typeof(CombatConfig).GetFields(BindingFlags.Public | BindingFlags.Instance)) savedCombat[fi] = fi.GetValue(combat);

        foreach (var p in presets)
        {
            Status = "running " + p.name;
            foreach (var kv in saved) kv.Key.SetValue(cfg, kv.Value);   // start from baseline
            foreach (var kv in savedCombat) kv.Key.SetValue(combat, kv.Value);
            foreach (var kv in p.overrides)
            {
                object target = cfg;
                var fi = typeof(MoraleConfig).GetField(kv.Key);
                if (fi == null) { fi = typeof(CombatConfig).GetField(kv.Key); target = combat; }
                if (fi == null) { Status = "done ERROR: unknown override " + kv.Key; foreach (var c in savedCombat) c.Key.SetValue(combat, c.Value); RestoreAndFinish(saved, cfg); yield break; }
                fi.SetValue(target, fi.FieldType == typeof(bool) ? (object)(kv.Value == "true" || kv.Value == "1")
                              : fi.FieldType == typeof(int) ? (object)int.Parse(kv.Value)
                              : (object)float.Parse(kv.Value, CultureInfo.InvariantCulture));
            }
            SimSeed.Set(seed);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            yield return null; yield return null;   // Awake has spawned with the seed
            var bs = BattleSetup.Instance;
            bs.combatConfig = combat;   // runtime assignment: the scene reference may not be set yet
            if (!aiOn || scenario != "") bs.GetComponent<EnemyCommander>().enabled = false;
            var rec = bs.GetComponent<BattleRecorder>();
            if (rec != null) rec.label = p.name;
            SimSpeed.Set(speed, renderOff);
            bs.StartBattle();
            if (scenario == "flank") yield return FlankScenario(bs);
            else if (scenario == "lines") yield return LinesScenario(bs);
            else while (bs != null && bs.Phase != BattlePhase.Ended) yield return null;
            yield return null;
            ResultFiles.Add(BattleRecorder.LastFilePath);
        }
        foreach (var kv in savedCombat) kv.Key.SetValue(combat, kv.Value);
        RestoreAndFinish(saved, cfg);
    }

    private static CombatConfig LoadCombatAsset()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<CombatConfig>("Assets/AncientArmies/Settings/CombatConfig.asset");
#else
        return null;
#endif
    }

    private static string OfficersAlive(Formation f)
    {
        var sb = new System.Text.StringBuilder();
        foreach (SoldierRole role in new[] { SoldierRole.Centurion, SoldierRole.Signifer, SoldierRole.Optio, SoldierRole.Tesserarius, SoldierRole.Cornicen })
        {
            bool alive = false;
            foreach (var s in f.soldiers) if (s.role == role) { alive = true; break; }
            sb.Append(role.ToString().Substring(0, 3)).Append(alive ? "+" : "-").Append(' ');
        }
        return sb.ToString().Trim();
    }

    // How deep enemy soldiers stand inside each engaged Blue block: average
    // distance (m) behind the block's front edge, over enemies inside the
    // footprint laterally and past the front edge.
    private static string Interleave(BattleSetup bs)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var f in bs.formations)
        {
            if (f.team != Team.Blue || f.soldiers.Count == 0 || f.Morale == null || !f.Morale.InMelee) continue;
            Vector3 fwd = f.AnchorForward, right = f.AnchorRot * Vector3.right;
            float halfW = f.FootprintHalfExtents.x, halfD = f.FootprintHalfExtents.y;
            float sum = 0f, max = 0f; int n = 0;
            foreach (var e in bs.GetSoldiers(Team.Red))
            {
                Vector3 d = e.transform.position - f.AnchorPos; d.y = 0f;
                if (Mathf.Abs(Vector3.Dot(d, right)) > halfW) continue;
                float depth = halfD - Vector3.Dot(d, fwd);   // 0 at the front edge, positive inside
                if (depth <= 0f || depth > 2f * halfD) continue;
                sum += depth; max = Mathf.Max(max, depth); n++;
            }
            sb.Append(f.displayName.Replace("Century ", "C")).Append(": n=").Append(n).Append(" avg=").Append(n > 0 ? (sum / n).ToString("0.0") : "-").Append(" max=").Append(max.ToString("0.0")).Append("m ");
        }
        return sb.ToString();
    }

    // Adjacent lines: each Red front century attacks the Blue century opposite
    // it, Red archers close to bow range, nobody flanks. Snapshots 60 / 120 /
    // 180 s after first contact, then ends.
    private IEnumerator LinesScenario(BattleSetup bs)
    {
        for (int i = 1; i <= 4; i++) Find(bs, "Red Century " + i).IssueAttack(Find(bs, "Blue Century " + i));
        Find(bs, "Red Archers 1").IssueAttack(Find(bs, "Blue Century 2"));
        Find(bs, "Red Archers 2").IssueAttack(Find(bs, "Blue Century 3"));
        var rec = bs.GetComponent<BattleRecorder>();
        var everTwoZones = new HashSet<Formation>();
        float[] marks = { 60f, 120f, 180f };
        int next = 0;
        while (bs.Phase == BattlePhase.Active && next < marks.Length)
        {
            foreach (var f in bs.formations) if (f.Morale != null && f.Morale.EngagedZones >= 2) everTwoZones.Add(f);
            float fc = rec != null ? rec.FirstContactTime : -1f;
            if (fc >= 0f && bs.BattleTime - fc >= marks[next])
            {
                var sb = new System.Text.StringBuilder("casualties@" + marks[next] + "s ");
                foreach (var f in bs.formations)
                    sb.Append(f.displayName.Replace("Century ", "C").Replace("Archers ", "A")).Append('=')
                      .Append((100f * (f.TotalSpawned - f.soldiers.Count - f.EscapedCount) / f.TotalSpawned).ToString("0.0")).Append("% ");
                BattleRecorder.Note(sb.ToString());
                BattleRecorder.Note("interleave@" + marks[next] + "s " + Interleave(bs));
                next++;
            }
            yield return null;
        }
        var end = new System.Text.StringBuilder("twoZonesEver=" + everTwoZones.Count + " | ");
        foreach (var f in bs.formations)
            end.Append(f.displayName.Replace("Century ", "C").Replace("Archers ", "A")).Append(": surround=")
               .Append(f.Morale != null ? f.Morale.SourceTotal(ShockSource.Surround).ToString("0.0") : "-")
               .Append(" morale=").Append(f.Morale != null ? f.Morale.Value.ToString("0") : "-")
               .Append(" officers[").Append(OfficersAlive(f)).Append("] | ");
        BattleRecorder.Note(end.ToString());
        if (bs.Phase != BattlePhase.Ended) BattleRecorder.NoteEnd(bs, "SCENARIO END — lines, 180 s after contact");
    }

    private static Formation Find(BattleSetup bs, string name)
    {
        foreach (var f in bs.formations) if (f.displayName == name) return f;
        return null;
    }

    // Flank test: Red Century 4 pins Blue Century 4 frontally; once Blue is
    // engaged, a fresh Red Century 3 (already marched out to Blue's open east
    // side) attacks its flank. Ends when Blue Century 4 routs, or 150 s of
    // simulation after the flank order.
    private IEnumerator FlankScenario(BattleSetup bs)
    {
        var blue = Find(bs, "Blue Century 4"); var pin = Find(bs, "Red Century 4"); var flanker = Find(bs, "Red Century 3");
        pin.IssueAttack(blue);
        flanker.IssueMove(new Vector3(blue.AnchorPos.x + 45f, 0f, blue.AnchorPos.z));
        while (bs.Phase == BattlePhase.Active &&
               !(blue.State == FormationState.Engaged && !flanker.HasMoveDestination && flanker.State == FormationState.Ordered))
        {
            if (blue.IsRouting || pin.IsRouting || flanker.IsRouting) break;
            yield return null;
        }
        BattleRecorder.Note("flankOrder zone=" + bs.GetDirectionalZone(flanker, blue) +
                            " blueLossPct=" + (blue.Morale != null ? (blue.Morale.LossFraction * 100f).ToString("0.0") : "-") +
                            " blueMorale=" + (blue.Morale != null ? blue.Morale.Value.ToString("0.0") : "-") +
                            " pin=" + pin.State + " flanker=" + flanker.State);
        flanker.IssueAttack(blue);
        float until = bs.BattleTime + 150f;
        while (bs.Phase == BattlePhase.Active && !blue.IsRouting && bs.BattleTime < until) yield return null;
        yield return null;
        BattleRecorder.Note("atEnd blueOfficers[" + OfficersAlive(blue) + "] blueCasualtyPct=" +
                            (100f * (blue.TotalSpawned - blue.soldiers.Count - blue.EscapedCount) / blue.TotalSpawned).ToString("0.0"));
        if (bs.Phase != BattlePhase.Ended)
            BattleRecorder.NoteEnd(bs, blue.IsRouting ? "SCENARIO END — Blue Century 4 routed" : "SCENARIO END — no rout within 150 s of the flank order");
    }

    private void RestoreAndFinish(Dictionary<FieldInfo, object> saved, MoraleConfig cfg)
    {
        foreach (var kv in saved) kv.Key.SetValue(cfg, kv.Value);
        SimSeed.Clear();
        SimSpeed.Reset();
        Status = "done " + string.Join("|", ResultFiles);
        instance = null;
        Destroy(gameObject);
    }
}
#endif
