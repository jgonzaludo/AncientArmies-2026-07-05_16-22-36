using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Test tooling: records a battle's events in SIMULATION time
// (BattleSetup.BattleTime) and writes one compact JSON file at battle end —
// first contact per formation pair, every state change with morale / shock /
// alive / casualty %, every officer death, and the end with its reason, plus
// a summary. Self-installs on scene load. Editor and development builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
public class BattleRecorder : MonoBehaviour
{
    public static BattleRecorder Instance { get; private set; }
    public static string LastFilePath { get; private set; } = "";

    private struct Ev { public float t; public string kind; public string json; }
    private readonly List<Ev> events = new List<Ev>();
    private readonly Dictionary<Formation, FormationState> lastState = new Dictionary<Formation, FormationState>();
    private readonly HashSet<string> contacts = new HashSet<string>();
    private readonly List<float> routTimes = new List<float>();
    private readonly List<string> routOrder = new List<string>();
    private float firstContact = -1f;
    public float FirstContactTime => firstContact;   // -1 until any formations touch
    private readonly int[] hits = new int[3];
    private int movingSamples, reversalSamples;   // 0.5 s samples of men moving > 0.8 m/s; those that reversed direction since the last sample
    // time-to-kill record: victim "unit/role" + cause -> count, total/min/max seconds from first damage to death, total hits taken
    private readonly Dictionary<string, (int n, double sumT, float minT, float maxT, double sumH)> kills = new Dictionary<string, (int, double, float, float, double)>();         // front / flank / rear hits landed this battle
    private bool written;
    public string label = "";   // set by ScenarioRunner; empty for manual play

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }
    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Attach();
    private static void Attach()
    {
        var bs = BattleSetup.Instance;
        if (bs != null && bs.GetComponent<BattleRecorder>() == null)
            bs.gameObject.AddComponent<BattleRecorder>();
    }

    private BattleSetup bs;
    private void Awake() { Instance = this; bs = GetComponent<BattleSetup>(); }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private static string Q(string s) => "\"" + s.Replace("\"", "'") + "\"";
    private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private string Snapshot(Formation f)
    {
        var m = f.Morale;
        float loss = f.TotalSpawned > 0 ? 100f * (f.TotalSpawned - f.soldiers.Count - f.EscapedCount) / f.TotalSpawned : 0f;
        return "\"f\":" + Q(f.displayName) + ",\"team\":" + Q(f.team.ToString()) +
               ",\"morale\":" + (m != null ? F(m.Value) : "null") + ",\"shock\":" + (m != null ? F(m.ShockDebt) : "null") +
               ",\"alive\":" + f.soldiers.Count + ",\"casualtyPct\":" + F(loss);
    }

    private void Add(float t, string kind, string body)
    {
        events.Add(new Ev { t = t, kind = kind, json = "{\"t\":" + F(t) + ",\"e\":" + Q(kind) + "," + body + "}" });
    }

    // ---- hooks called by the simulation (cheap no-ops when no recorder) ----

    public static void NoteContact(Formation a, Formation b)
    {
        var r = Instance; if (r == null || r.bs == null) return;
        string key = string.CompareOrdinal(a.displayName, b.displayName) < 0 ? a.displayName + "|" + b.displayName : b.displayName + "|" + a.displayName;
        if (!r.contacts.Add(key)) return;
        float t = r.bs.BattleTime;
        if (r.firstContact < 0f) r.firstContact = t;
        r.Add(t, "contact", "\"a\":" + Q(a.displayName) + ",\"b\":" + Q(b.displayName));
    }

    public static void NoteDeath(Formation f, Soldier s)
    {
        var r = Instance; if (r == null || r.bs == null) return;
        var ranks = r.bs.officerRanks;
        if (ranks == null || !ranks.IsOfficer(s.role)) return;
        r.Add(r.bs.BattleTime, "officerDeath", "\"role\":" + Q(s.role.ToString()) + "," + r.Snapshot(f));
    }

    private int targetSwitches, targetDrops;
    public static void NoteTargetSwitch() { var r = Instance; if (r != null) r.targetSwitches++; }
    public static void NoteTargetDrop() { var r = Instance; if (r != null) r.targetDrops++; }

    public static void NoteMovingSample(bool reversed)
    {
        var r = Instance; if (r == null) return;
        r.movingSamples++; if (reversed) r.reversalSamples++;
    }

    public static void NoteKill(string victim, bool arrow, float ttk, int hitsTaken)
    {
        var r = Instance; if (r == null) return;
        string k = victim + "|" + (arrow ? "arrow" : "sword");
        r.kills.TryGetValue(k, out var v);
        r.kills[k] = (v.n + 1, v.sumT + ttk, v.n == 0 ? ttk : Mathf.Min(v.minT, ttk), Mathf.Max(v.maxT, ttk), v.sumH + hitsTaken);
    }

    private string KillsJson()
    {
        var sb = new StringBuilder(",\"kills\":{");
        bool first = true;
        foreach (var kv in kills)
        {
            if (!first) sb.Append(','); first = false;
            sb.Append(Q(kv.Key)).Append(":{\"n\":").Append(kv.Value.n).Append(",\"avgTtk\":").Append(F((float)(kv.Value.sumT / kv.Value.n)))
              .Append(",\"minTtk\":").Append(F(kv.Value.minT)).Append(",\"maxTtk\":").Append(F(kv.Value.maxT))
              .Append(",\"avgHits\":").Append(F((float)(kv.Value.sumH / kv.Value.n))).Append('}');
        }
        return sb.Append('}').ToString();
    }

    public static void NoteHit(DamageZone zone)
    {
        var r = Instance; if (r != null) r.hits[(int)zone]++;
    }

    public static void Note(string text)
    {
        var r = Instance; if (r == null || r.bs == null) return;
        r.Add(r.bs.BattleTime, "note", "\"text\":" + Q(text));
    }

    public static void NoteEnd(BattleSetup setup, string result)
    {
        var r = setup != null ? setup.GetComponent<BattleRecorder>() : null;
        if (r == null) return;
        r.bs = setup;
        r.Add(setup.BattleTime, "end", "\"result\":" + Q(result));
        r.Write(result);
    }

    private void Update()
    {
        if (bs == null || bs.Phase != BattlePhase.Active) return;
        float t = bs.BattleTime;
        foreach (var f in bs.formations)
        {
            if (!lastState.TryGetValue(f, out var prev)) { lastState[f] = f.State; continue; }
            if (prev == f.State) continue;
            lastState[f] = f.State;
            string extra = "";
            if (f.State == FormationState.Routing && f.Morale != null)
            {
                var sbc = new StringBuilder(",\"primaryCause\":" + Q(f.Morale.PrimaryCause.ToString()) + ",\"zones\":" + f.Morale.EngagedZones + ",\"causes\":{");
                bool first = true;
                foreach (ShockSource src in System.Enum.GetValues(typeof(ShockSource)))
                {
                    float v = f.Morale.SourceTotal(src);
                    if (v <= 0.005f) continue;
                    if (!first) sbc.Append(','); first = false;
                    sbc.Append(Q(src.ToString())).Append(':').Append(F(v));
                }
                extra = sbc.Append('}').ToString();
            }
            Add(t, "state", "\"from\":" + Q(prev.ToString()) + ",\"to\":" + Q(f.State.ToString()) + "," + Snapshot(f) + extra);
            if (f.State == FormationState.Routing) { routTimes.Add(t); routOrder.Add(f.displayName); }
        }
    }

    private void Write(string result)
    {
        if (written) return;
        written = true;
        float end = bs.BattleTime;
        float firstRout = routTimes.Count > 0 ? routTimes[0] : -1f;
        int maxWindow = 0;
        for (int i = 0; i < routTimes.Count; i++)
        {
            int n = 0;
            for (int j = i; j < routTimes.Count && routTimes[j] - routTimes[i] <= 5f; j++) n++;
            maxWindow = Mathf.Max(maxWindow, n);
        }
        var sb = new StringBuilder();
        sb.Append("{\"label\":").Append(Q(label)).Append(",\"seed\":").Append(SimSeed.Current)
          .Append(",\"speed\":").Append(F(SimSpeed.Multiplier)).Append(",\"renderOff\":").Append(SimSpeed.RenderOff ? "true" : "false")
          .Append(",\"summary\":{\"result\":").Append(Q(result)).Append(",\"endTime\":").Append(F(end))
          .Append(",\"firstContact\":").Append(F(firstContact))
          .Append(",\"contactToFirstRout\":").Append(firstRout < 0f || firstContact < 0f ? "null" : F(firstRout - firstContact))
          .Append(",\"contactToEnd\":").Append(firstContact < 0f ? "null" : F(end - firstContact))
          .Append(",\"maxRoutsIn5s\":").Append(maxWindow)
          .Append(",\"hits\":{\"front\":").Append(hits[0]).Append(",\"flank\":").Append(hits[1]).Append(",\"rear\":").Append(hits[2]).Append('}')
          .Append(KillsJson()).Append(",\"targetSwitches\":").Append(targetSwitches).Append(",\"targetDrops\":").Append(targetDrops).Append(",\"reversals\":{\"movingSamples\":").Append(movingSamples).Append(",\"reversed\":").Append(reversalSamples).Append(",\"pct\":").Append(F(movingSamples > 0 ? 100f * reversalSamples / movingSamples : 0f)).Append("}").Append(",\"routOrder\":[");
        for (int i = 0; i < routOrder.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Q(routOrder[i])); }
        sb.Append("],\"casualtyPct\":{");
        bool firstTeam = true;
        foreach (Team team in new[] { Team.Blue, Team.Red })
        {
            int spawned = 0, alive = 0, esc = 0;
            foreach (var f in bs.formations) if (f.team == team) { spawned += f.TotalSpawned; alive += f.soldiers.Count; esc += f.EscapedCount; }
            if (!firstTeam) sb.Append(','); firstTeam = false;
            sb.Append(Q(team.ToString())).Append(':').Append(spawned > 0 ? F(100f * (spawned - alive - esc) / spawned) : "0");
        }
        sb.Append("},\"final\":[");
        for (int i = 0; i < bs.formations.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var f = bs.formations[i];
            sb.Append("{").Append(Snapshot(f)).Append(",\"state\":").Append(Q(f.State.ToString())).Append(",\"escaped\":").Append(f.EscapedCount).Append('}');
        }
        sb.Append("]},\"events\":[");
        for (int i = 0; i < events.Count; i++) { if (i > 0) sb.Append(','); sb.Append(events[i].json); }
        sb.Append("]}");

        string dir = Path.Combine(Application.isEditor ? Path.Combine(Application.dataPath, "..", "Temp") : Application.persistentDataPath, "BattleLogs");
        Directory.CreateDirectory(dir);
        string name = (label != "" ? label : "battle") + "_s" + SimSeed.Current + "_" + System.DateTime.Now.ToString("HHmmss") + ".json";
        LastFilePath = Path.GetFullPath(Path.Combine(dir, name));
        File.WriteAllText(LastFilePath, sb.ToString());
        Debug.Log("[BattleRecorder] wrote " + LastFilePath);
    }
}
#else
public class BattleRecorder : MonoBehaviour
{
    public static void NoteContact(Formation a, Formation b) { }
    public static void NoteDeath(Formation f, Soldier s) { }
    public static void Note(string text) { }
    public static void NoteHit(DamageZone zone) { }
    public static void NoteKill(string victim, bool arrow, float ttk, int hitsTaken) { }
    public static void NoteMovingSample(bool reversed) { }
    public static void NoteTargetSwitch() { }
    public static void NoteTargetDrop() { }
    public static void NoteEnd(BattleSetup setup, string result) { }
}
#endif
