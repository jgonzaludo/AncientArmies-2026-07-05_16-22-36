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
            bool kkScenario = scenario.StartsWith("kk");   // "kk<zoom>[c]" zoom 0 near 1 mid 2 far, trailing c = capsules; AI frontal, rendering on
            SoldierFactory.ForceCapsules = kkScenario && scenario.EndsWith("c");
            bool aiScenario = scenario.StartsWith("ai") || kkScenario;   // "ai1" frontal, "ai2" flank one, "ai3" double flank
            if (aiScenario) bs.GetComponent<EnemyCommander>().plan = kkScenario ? EnemyCommander.AiPlan.Frontal : (EnemyCommander.AiPlan)int.Parse(scenario.Substring(2));
            else if (!aiOn || scenario != "") bs.GetComponent<EnemyCommander>().enabled = false;
            var rec = bs.GetComponent<BattleRecorder>();
            if (rec != null) rec.label = p.name;
            SimSpeed.Set(speed, renderOff);
            bs.StartBattle();
            if (scenario == "flank") yield return FlankScenario(bs);
            else if (scenario == "lines") yield return LinesScenario(bs);
            else if (scenario == "charge") yield return ChargeScenario(bs);
            else if (scenario == "pass") yield return PassScenario(bs);
            else if (scenario == "lock") yield return LockScenario(bs);
            else if (scenario == "b2f") yield return BackToFrontScenario(bs, false);
            else if (scenario == "edge") yield return EdgeScenario(bs);
            else if (scenario == "shots") yield return ShotsScenario(bs, new[] { 8f, 16f });
            else if (scenario == "animsheet") yield return AnimSheetScenario(bs, false);
            else if (scenario == "shieldfit") yield return ShieldFitScenario(bs);
            else if (scenario == "turntable") yield return TurntableScenario(bs);
            else if (scenario == "hudshot") yield return HudShotScenario(bs);
            else if (scenario == "animsheetarcher") yield return AnimSheetScenario(bs, true);
            else if (scenario == "shotsclose") yield return ShotsScenario(bs, new[] { 5.5f });
            else if (scenario == "b2fred") yield return BackToFrontScenario(bs, true);
            else if (scenario == "drift") yield return DriftScenario(bs);
            else if (kkScenario) yield return KayKitScenario(bs, int.Parse(scenario.Substring(2, 1)));
            else
            {
                float aiEnd = bs.BattleTime + 240f;   // AI runs can stall; cap them
                while (bs != null && bs.Phase != BattlePhase.Ended && (!aiScenario || bs.BattleTime < aiEnd)) yield return null;
                if (aiScenario && bs != null && bs.Phase != BattlePhase.Ended) BattleRecorder.NoteEnd(bs, "SCENARIO END, ai plan " + bs.GetComponent<EnemyCommander>().ChosenPlan);
            }
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

    // Charge test: Blue Century 1 marches to ~14 m from Red Century 1, charges,
    // then the run watches 90 s of game time. The recorder's state events give
    // BrokenRanks -> Reforming -> Ordered timings.
    // Ordering: melee marches through friendly archers. Records archer
    // displacement (m, avg / max), soldier-pair overlaps (< 0.9 m centre gap, friendly) and
    // arrival time. Enemy AI is off; nothing fights.
    private IEnumerator PassScenario(BattleSetup bs)
    {
        var mel = Find(bs, "Blue Century 1"); var arch = Find(bs, "Blue Archers 1");
        var start = new Dictionary<Soldier, Vector3>();
        foreach (var a in arch.soldiers) start[a] = a.transform.position;
        Vector3 dir = arch.AnchorPos - mel.AnchorPos; dir.y = 0f; dir.Normalize();
        Vector3 dest = arch.AnchorPos + dir * 12f;
        BattleRecorder.Note("pass: from " + mel.AnchorPos + " via archers " + arch.AnchorPos + " to " + dest);
        mel.IssueMove(dest);
        BattleRecorder.Note("pass: destination after fix " + mel.DestinationDebug);
        for (int i = 0; i < 90; i++) yield return null;
        { var rp = new List<Vector3>(); mel.GetRemainingRoute(rp); var sbr = new System.Text.StringBuilder();
          foreach (var q in rp) sbr.Append(q.ToString("0")).Append(' ');
          BattleRecorder.Note("pass route (" + rp.Count + "): " + sbr + " footprint=" + mel.FootprintHalfExtents + " archersFoot=" + arch.FootprintHalfExtents); }
        float t0 = bs.BattleTime; float maxDisp = 0f; int overlapFrames = 0; int frames = 0;
        while (bs.Phase == BattlePhase.Active && bs.BattleTime - t0 < 90f && mel.HasMoveDestination)
        {
            frames++;
            foreach (var a in arch.soldiers)
            {
                maxDisp = Mathf.Max(maxDisp, Vector3.Distance(a.transform.position, start[a]));
                foreach (var m in mel.soldiers)
                    if ((a.transform.position - m.transform.position).sqrMagnitude < 0.81f) { overlapFrames++; break; }
            }
            yield return null;
        }
        float maxSpeedSettle = 0f; var trace = new System.Text.StringBuilder();
        for (int i = 0; i < 480; i++)
        {
            yield return null;
            foreach (var m in mel.soldiers) maxSpeedSettle = Mathf.Max(maxSpeedSettle, m.Velocity.magnitude);
            foreach (var a in arch.soldiers) maxSpeedSettle = Mathf.Max(maxSpeedSettle, a.Velocity.magnitude);
            if (i % 60 == 0)
            {
                int ov = 0;
                foreach (var a in arch.soldiers) foreach (var m in mel.soldiers)
                    if ((a.transform.position - m.transform.position).sqrMagnitude < 0.81f) { ov++; break; }
                trace.Append(ov).Append(' ');
            }
        }
        BattleRecorder.Note("pass settle: overlapsPerSecond=[" + trace + "] maxSoldierSpeed=" + maxSpeedSettle.ToString("0.0"));
        float sum = 0f; foreach (var a in arch.soldiers) sum += Vector3.Distance(a.transform.position, start[a]);
        int overlapNow = 0;
        foreach (var a in arch.soldiers) foreach (var m in mel.soldiers)
            if ((a.transform.position - m.transform.position).sqrMagnitude < 0.81f) { overlapNow++; break; }
        BattleRecorder.Note("pass RESULT arrival=" + (bs.BattleTime - t0).ToString("0.0") + "s archerMoveMax=" + maxDisp.ToString("0.0") +
            "m archerMoveAvgFinal=" + (sum / Mathf.Max(1, arch.soldiers.Count)).ToString("0.0") + "m overlapArcherFrames=" + overlapFrames +
            " overlappingArchersAtEnd=" + overlapNow);
        BattleRecorder.NoteEnd(bs, "SCENARIO END, pass");
    }

    // Drift: placed destination vs where the block really ends up. Open-ground
    // moves and moves that must bend around friends; after arrival + 6 s we log
    // anchor error, soldier-centroid error and soldier-to-slot distance.
    private IEnumerator DriftScenario(BattleSetup bs)
    {
        var orders = new List<(string name, Vector3 dest)> {
            ("Blue Century 2", new Vector3(-8f, 0f, -60f)),     // straight ahead, open ground
            ("Blue Century 3", new Vector3(30f, 0f, -70f)),     // diagonal
            ("Blue Century 5", new Vector3(-40f, 0f, -150f)),   // sideways, behind the line
            ("Blue Archers 2", new Vector3(10f, 0f, -75f)),     // through the front line gap area
            ("Blue Century 6", new Vector3(14f, 0f, -118f)) };  // short hop onto crowded ground
        foreach (var o in orders) Find(bs, o.name).IssueMove(o.dest);
        float t0 = bs.BattleTime; float nextTrace = 0f; var arch2 = Find(bs, "Blue Archers 2"); var tr = new System.Text.StringBuilder();
        while (bs.Phase == BattlePhase.Active && bs.BattleTime - t0 < 120f)
        {
            if (bs.BattleTime - t0 >= nextTrace) { nextTrace += 3f; tr.Append("[").Append((bs.BattleTime - t0).ToString("0")).Append("s ").Append(arch2.AnchorPos.ToString("0")).Append(" ").Append(arch2.State).Append(" dest=").Append(arch2.DestinationDebug.ToString("0")).Append(arch2.HasMoveDestination ? " moving" : " stopped").Append("] "); }
            bool any = false; foreach (var o in orders) if (Find(bs, o.name).HasMoveDestination) any = true;
            if (!any) break; yield return null;
        }
        for (int round = 0; round < 3; round++)
        {
            for (int i = 0; i < 360; i++) yield return null;
            var line = new System.Text.StringBuilder("drift settle#" + (round + 1) + ": ");
            foreach (var o in orders)
            {
                var f = Find(bs, o.name); float mx = 0f; int far = 0;
                foreach (var sd in f.soldiers) { float d = Vector3.Distance(sd.transform.position, f.GetSlotWorldPos(sd.slotIndex)); mx = Mathf.Max(mx, d); if (d > 2.5f) far++; }
                line.Append(o.name.Replace("Century ", "C").Replace("Archers ", "A")).Append(" max=").Append(mx.ToString("0.0")).Append(" far=").Append(far).Append(" state=").Append(f.State).Append(" | ");
            }
            BattleRecorder.Note(line.ToString());
        }
        { var a2 = Find(bs, "Blue Archers 2"); var sbx = new System.Text.StringBuilder("drift stuck: ");
          foreach (var sd in a2.soldiers)
          {
              Vector3 slotP = a2.GetSlotWorldPos(sd.slotIndex); float d = Vector3.Distance(sd.transform.position, slotP); if (d < 2.5f) continue;
              string near = ""; float nd = float.MaxValue; Soldier ns = null;
              foreach (var o2 in bs.GetSoldiers(Team.Blue)) { if (o2 == sd) continue; float dd = Vector3.Distance(o2.transform.position, sd.transform.position); if (dd < nd) { nd = dd; ns = o2; } }
              near = ns != null ? ns.formation.displayName + " d=" + nd.ToString("0.0") + " nearVel=" + ns.Velocity.magnitude.ToString("0.0") : "-";
              sbx.Append("slot=").Append(sd.slotIndex).Append(" pos=").Append(sd.transform.position.ToString("0")).Append(" slotPos=").Append(slotP.ToString("0")).Append(" dist=").Append(d.ToString("0.0")).Append(" vel=").Append(sd.Velocity.magnitude.ToString("0.00")).Append(" recalling=").Append(sd.IsRecalling).Append(" engaged=").Append(sd.IsEngaged).Append(" lockedAction=").Append(sd.IsInCommittedCombatAction).Append(" nearest=").Append(near).Append(" | ");
          }
          BattleRecorder.Note(sbx.ToString()); }
        foreach (var o in orders)
        {
            var f = Find(bs, o.name); Vector3 c = Vector3.zero; float sumS = 0f, maxS = 0f;
            foreach (var sd in f.soldiers) { c += sd.transform.position; float d = Vector3.Distance(sd.transform.position, f.GetSlotWorldPos(sd.slotIndex)); sumS += d; maxS = Mathf.Max(maxS, d); }
            c /= Mathf.Max(1, f.soldiers.Count);
            BattleRecorder.Note("drift " + o.name + " ordered=" + o.dest.ToString("0") + " fixed=" + f.DestinationDebug.ToString("0") + " anchor=" + f.AnchorPos.ToString("0") +
                " anchorErr=" + Vector3.Distance(o.dest, f.AnchorPos).ToString("0.0") + "m centroidErr=" + Vector3.Distance(o.dest, c).ToString("0.0") + "m soldierToSlot avg=" + (sumS / Mathf.Max(1, f.soldiers.Count)).ToString("0.00") + " max=" + maxS.ToString("0.0"));
        }
        BattleRecorder.Note("drift archers trace: " + tr);
        BattleRecorder.NoteEnd(bs, "SCENARIO END, drift");
    }

    // KayKit visuals: a normal AI battle with rendering on at a fixed zoom. Logs frame-time
    // stats (editor numbers: not a phone), editor draw/set-pass/triangle counts, and which clips played.
    private IEnumerator KayKitScenario(BattleSetup bs, int zoomIdx)
    {
        var camRig = Camera.main != null ? Camera.main.GetComponent<BattleCamera>() : null;
        float[] ortho = { 8f, 18f, 36f };
        if (camRig != null) camRig.ZoomBy(ortho[zoomIdx] / Camera.main.orthographicSize);
        KayKitSoldierVisual.PlayCounts.Clear(); KayKitSoldierVisual.FaceFrames = KayKitSoldierVisual.RootBackFrames = KayKitSoldierVisual.ModelBackFrames = 0;
        for (int i = 0; i < 90; i++) yield return null;   // let the zoom settle
#if UNITY_EDITOR
        if (zoomIdx == 1)
        {
            // does forceMeshLod really change what is drawn? compare triangle counts per forced LOD
            var smrs = new List<SkinnedMeshRenderer>(); foreach (var f3 in bs.formations) foreach (var sd in f3.soldiers) { var r3 = sd.GetComponentInChildren<SkinnedMeshRenderer>(true); if (r3 != null) smrs.Add(r3); }
            var sbl = new System.Text.StringBuilder("kk lodtest: ");
            foreach (short lodTry in new short[] { 0, 1, 2, 3, -1 })
            {
                foreach (var r3 in smrs) r3.forceMeshLod = lodTry;
                for (int i = 0; i < 6; i++) yield return null;
                sbl.Append("LOD" + lodTry + " tris=" + UnityEditor.UnityStats.triangles + " ");
            }
            foreach (var r3 in smrs) r3.enabled = false;
            for (int i = 0; i < 6; i++) yield return null;
            sbl.Append("SOLDIERS_OFF tris=" + UnityEditor.UnityStats.triangles + " batches=" + UnityEditor.UnityStats.drawCalls + " ");
            foreach (var r3 in smrs) { r3.enabled = true; r3.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            for (int i = 0; i < 6; i++) yield return null;
            sbl.Append("SHADOWS_OFF tris=" + UnityEditor.UnityStats.triangles + " batches=" + UnityEditor.UnityStats.drawCalls + " ");
            foreach (var r3 in smrs) r3.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            BattleRecorder.Note(sbl.ToString());
            KayKitCrowdLod.ResetVersionForTest();
        }
#endif
        // per-marker CPU profile over 240 frames (editor numbers): what is the frame actually spent on?
        {
            var recs = new List<(string name, Unity.Profiling.ProfilerRecorder rec, double sum)>();
            var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>(); Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
            var cats = new HashSet<string> { "Scripts", "Physics", "Animation", "Render", "Internal", "Gui", "Lighting", "Memory" };
            foreach (var h in handles)
            {
                var d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h);
                if (d.UnitType.ToString() != "TimeNanoseconds" || !cats.Contains(d.Category.Name)) continue;
                recs.Add((d.Category.Name + "/" + d.Name, Unity.Profiling.ProfilerRecorder.StartNew(d.Category, d.Name), 0));
            }
            var sums = new double[recs.Count];
            for (int f = 0; f < 240; f++) { yield return null; for (int i = 0; i < recs.Count; i++) if (recs[i].rec.Valid) sums[i] += recs[i].rec.LastValue; }
            var top = new List<(double ms, string n)>();
            for (int i = 0; i < recs.Count; i++) { top.Add((sums[i] / 240 / 1e6, recs[i].name)); recs[i].rec.Dispose(); }
            top.Sort((a, b) => b.ms.CompareTo(a.ms));
            var sbp = new System.Text.StringBuilder("kk profile (avg ms/frame, markers overlap): ");
            for (int i = 0; i < Mathf.Min(70, top.Count); i++) sbp.Append(top[i].n).Append('=').Append(top[i].ms.ToString("0.00")).Append("; ");
            BattleRecorder.Note(sbp.ToString());
        }
        {   // are the live animators really in the states the visual asked for?
            var names = new[] { "Idle_A", "Melee_Blocking", "Walking_A", "Running_A", "Melee_1H_Attack_Stab", "Melee_1H_Attack_Chop", "Melee_1H_Attack_Slice_Diagonal", "Melee_Block_Hit", "Hit_A", "Death_A", "Death_A_Pose", "Cheering", "Ranged_Bow_Aiming_Idle", "Ranged_Bow_Draw", "Ranged_Bow_Release" };
            for (int pass = 0; pass < 3; pass++)
            {
                float until2 = 40f + pass * 25f; while (bs.Phase == BattlePhase.Active && bs.BattleTime < until2) yield return null;   // let the armies march and meet
                var hist = new Dictionary<string, int>(); int noCtl = 0, disabled = 0, total = 0, moving = 0;
                foreach (var f4 in bs.formations) foreach (var sd in f4.soldiers)
                {
                    var an = sd.GetComponentInChildren<Animator>(true); if (an == null) continue; total++;
                    if (an.runtimeAnimatorController == null) { noCtl++; continue; }
                    if (!an.isActiveAndEnabled) { disabled++; continue; }
                    string st = "?"; var info = an.GetCurrentAnimatorStateInfo(0);
                    foreach (var n in names) if (info.IsName(n)) { st = n; break; }
                    hist[st] = hist.TryGetValue(st, out int c) ? c + 1 : 1;
                    if (sd.Velocity.sqrMagnitude > 1f) moving++;
                }
                var sbh = new System.Text.StringBuilder("anim states live (pass " + pass + "): total=" + total + " noController=" + noCtl + " animatorOff=" + disabled + " moving=" + moving + " | ");
                foreach (var kv in hist) sbh.Append(kv.Key + "=" + kv.Value + " ");
                BattleRecorder.Note(sbh.ToString());
            }
        }
        bool shotTaken = false; var dts = new List<float>(); float t0 = bs.BattleTime; double drawSum = 0, passSum = 0, triSum = 0; int statN = 0;
        while (bs.Phase == BattlePhase.Active && bs.BattleTime - t0 < 100f)
        {
            if (!shotTaken && bs.BattleTime - t0 > 45f)
            {
                shotTaken = true;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "kk_game_zoom" + zoomIdx + (SoldierFactory.ForceCapsules ? "c" : "") + ".png"));
            }
            if (bs.BattleTime - t0 > 25f)   // measure once the lines are engaging
            {
                dts.Add(Time.unscaledDeltaTime);
#if UNITY_EDITOR
                drawSum += UnityEditor.UnityStats.drawCalls; passSum += UnityEditor.UnityStats.setPassCalls; triSum += UnityEditor.UnityStats.triangles; statN++;
#endif
            }
            yield return null;
        }
        if (bs.Phase == BattlePhase.Ended)
        {
            for (int i = 0; i < 240; i++) yield return null;   // let the victory cheer start
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "end_screen.png"));
            for (int i = 0; i < 5; i++) yield return null;
        }
        {
            var sbf = new System.Text.StringBuilder(bs.ResultText + " | ");
            foreach (var kv in KayKitSoldierVisual.PlayCounts) sbf.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            sbf.Append("| facing: moving frames=" + KayKitSoldierVisual.FaceFrames + " rootBackwards=" + KayKitSoldierVisual.RootBackFrames + " (" + (100f * KayKitSoldierVisual.RootBackFrames / Mathf.Max(1, KayKitSoldierVisual.FaceFrames)).ToString("0.0") + "%) modelBackwards=" + KayKitSoldierVisual.ModelBackFrames + " (" + (100f * KayKitSoldierVisual.ModelBackFrames / Mathf.Max(1, KayKitSoldierVisual.FaceFrames)).ToString("0.0") + "%)");
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "kk_clips.txt"), sbf.ToString());
        }
        dts.Sort();
        float avg = 0f; foreach (var d in dts) avg += d; avg /= Mathf.Max(1, dts.Count);
        float p95 = dts.Count > 0 ? dts[Mathf.Min(dts.Count - 1, (int)(dts.Count * 0.95f))] : 0f;
        var sbc = new System.Text.StringBuilder(); foreach (var kv in KayKitSoldierVisual.PlayCounts) sbc.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
        { int imp = 0, tot = 0; foreach (var f2 in bs.formations) foreach (var sd in f2.soldiers) { tot++; if (sd.Imposted) imp++; }
          var mg = FormationBannerManager.Instance;
          BattleRecorder.Note("kk lod: soldierPx=" + (mg != null ? mg.SoldierScreenPixels.ToString("0.0") : "n/a") + " meshLod=" + KayKitCrowdLod.MeshLod + " animStride=" + KayKitCrowdLod.AnimStride + " imposted=" + imp + "/" + tot); }
        BattleRecorder.Note("kk RESULT zoom=" + zoomIdx + " ortho=" + ortho[zoomIdx] + " capsules=" + SoldierFactory.ForceCapsules + " frames=" + dts.Count +
            " avgMs=" + (avg * 1000f).ToString("0.0") + " p95Ms=" + (p95 * 1000f).ToString("0.0") + " fpsAvg=" + (1f / Mathf.Max(0.0001f, avg)).ToString("0") +
            " batches=" + (statN > 0 ? (drawSum / statN).ToString("0") : "n/a") + " setPass=" + (statN > 0 ? (passSum / statN).ToString("0") : "n/a") + " tris=" + (statN > 0 ? (triSum / statN).ToString("0") : "n/a"));
        BattleRecorder.Note("kk clips: " + sbc);
        BattleRecorder.NoteEnd(bs, "SCENARIO END, kk");
    }

    // The live HUD: a selected century with a move order in flight (command panel, info chip, path line, tide bar, clock) mid-battle, plus the deployment screen.
    private IEnumerator HudShotScenario(BattleSetup bs)
    {
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "Temp");
        var pc = bs.GetComponent<PlayerCommander>();
        var c1 = Find(bs, "Blue Century 1"); var c3 = Find(bs, "Blue Century 3");
        for (int i = 0; i < 60; i++) yield return null;
        pc.SelectOnly(c1);
        c1.IssueMove(new Vector3(c1.AnchorPos.x + 30f, 0f, c1.AnchorPos.z + 60f));
        c3.IssueMove(new Vector3(c3.AnchorPos.x - 10f, 0f, c3.AnchorPos.z + 40f));
        for (int i = 0; i < 240; i++) yield return null;
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "hud_active.png"));
        for (int i = 0; i < 10; i++) yield return null;
        BattleRecorder.NoteEnd(bs, "SCENARIO END — hudshot");
    }

    // One blue and one red soldier (melee, plus an archer) from the front, side, back and the game camera's angle, in play mode with the real lighting.
    private IEnumerator TurntableScenario(BattleSetup bs)
    {
        var cfg = Resources.Load<KayKitVisualConfig>("KayKitVisualConfig");
        Vector3 origin = new Vector3(4000f, 0f, 0f);
        var camGo = new GameObject("TurnCam"); var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 1.45f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.72f, 0.69f, 0.58f);
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 40f; cam.enabled = false;
        int T = 380; var rt = new RenderTexture(T, T, 24);
        // views: front, left-3/4, side, back, game camera (40 deg pitch, from behind)
        var views = new[] { (0f, 8f), (-45f, 8f), (-90f, 8f), (180f, 8f), (180f, 40f) };
        var rowsDef = new[] { ("VIS_KayKit_Melee", cfg.blueMaterials[2], false), ("VIS_KayKit_Melee", cfg.redMaterials[1], false), ("VIS_KayKit_Archer", cfg.blueMaterials[0], true), ("VIS_KayKit_Archer", cfg.redMaterials[3], true) };
        var sheet = new Texture2D(views.Length * T, rowsDef.Length * T, TextureFormat.RGB24, false);
        for (int r = 0; r < rowsDef.Length; r++)
        {
            var go = Object.Instantiate(Resources.Load<GameObject>(rowsDef[r].Item1), origin, Quaternion.identity);
            foreach (var rr in go.GetComponentsInChildren<Renderer>(true)) rr.sharedMaterial = (rowsDef[r].Item3 && rr is MeshRenderer) ? cfg.bowMaterial : rowsDef[r].Item2;
            go.GetComponentInChildren<SkinnedMeshRenderer>().updateWhenOffscreen = true;
            var anim = go.GetComponent<Animator>(); anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Play(Animator.StringToHash(rowsDef[r].Item3 ? "Ranged_Bow_Aiming_Idle" : "Melee_Blocking"), 0, 0.3f); anim.speed = 0f;
            yield return null; yield return null;
            for (int v = 0; v < views.Length; v++)
            {
                var focus = origin + new Vector3(0f, 1.1f, 0f);
                cam.transform.position = focus + Quaternion.Euler(views[v].Item2, views[v].Item1, 0f) * new Vector3(0f, 0f, 8f); cam.transform.LookAt(focus);
                yield return new WaitForEndOfFrame();
                cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                var prev = RenderTexture.active; RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, T, T), v * T, (rowsDef.Length - 1 - r) * T); RenderTexture.active = prev;
            }
            Object.Destroy(go);
        }
        sheet.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "turntable.png"), sheet.EncodeToPNG());
        BattleRecorder.NoteEnd(bs, "SCENARIO END, turntable");
    }

    // Shield fit: bake the real skinned mesh in every pose and count left-arm vertices (the first 584 of the merged mesh) that sit
    // inside the shield's box; then search hand-slot-local offsets for the smallest shift that clears them.
    private IEnumerator ShieldFitScenario(BattleSetup bs)
    {
        var prefab = Resources.Load<GameObject>("VIS_KayKit_Melee");
        var go = Object.Instantiate(prefab, new Vector3(4000f, 0f, 0f), Quaternion.identity);
        var anim = go.GetComponent<Animator>(); anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(); smr.updateWhenOffscreen = true;
        Transform slot = null; foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "handslot.l") slot = t;
        var shieldMesh = smr.sharedMesh;
        // shield box in slot-local space: take the vertices of the merged mesh that were skinned to the slot's bone (bone weight 1 on handslot.l's bone)
        int slotBone = -1; for (int i = 0; i < smr.bones.Length; i++) if (smr.bones[i] == slot) slotBone = i;
        var weights = shieldMesh.boneWeights; var rest = shieldMesh.vertices; var bind = shieldMesh.bindposes;
        Bounds box = default; bool have = false;
        for (int i = 584; i < rest.Length; i++)
            if (weights[i].boneIndex0 == slotBone && weights[i].weight0 > 0.99f)
            { Vector3 local = bind[slotBone].MultiplyPoint3x4(rest[i]); if (!have) { box = new Bounds(local, Vector3.zero); have = true; } else box.Encapsulate(local); }
        string[] states = { "Idle_A", "Melee_Blocking", "Walking_A", "Running_A", "Melee_1H_Attack_Stab", "Melee_1H_Attack_Chop", "Melee_1H_Attack_Slice_Diagonal", "Melee_Block_Hit", "Hit_A", "Cheering" };
        var poses = new List<Vector3[]>();   // left-arm vertices in slot-local space, per pose
        var baked = new Mesh();
        foreach (var st in states)
            foreach (float nt in new[] { 0.15f, 0.35f, 0.55f, 0.75f })
            {
                anim.Play(Animator.StringToHash(st), 0, nt); anim.speed = 0f;
                yield return null; yield return null;
                smr.BakeMesh(baked); var v = baked.vertices; var l2w = smr.transform.localToWorldMatrix; var w2s = slot.worldToLocalMatrix;
                var arm = new Vector3[584]; for (int i = 0; i < 584; i++) arm[i] = w2s.MultiplyPoint3x4(l2w.MultiplyPoint3x4(v[i]));
                poses.Add(arm); anim.speed = 1f;
            }
        var sb = new System.Text.StringBuilder("shieldfit: slotBone=" + slotBone + " shieldBox center=" + box.center.ToString("0.000") + " size=" + box.size.ToString("0.000") + " poses=" + poses.Count + " | ");
        System.Func<Vector3, int> count = off =>
        {
            var b = new Bounds(box.center + off, box.size * 0.96f); int n = 0;
            foreach (var arm in poses) foreach (var p in arm) if (b.Contains(p)) n++;
            return n;
        };
        int baseCount = count(Vector3.zero); sb.Append("armVertsInsideShield(offset 0)=" + baseCount + " of " + (poses.Count * 584) + " | ");
        var best = new List<(float mag, Vector3 off, int n)>();
        for (float x = -0.24f; x <= 0.241f; x += 0.04f) for (float y = -0.24f; y <= 0.241f; y += 0.04f) for (float z = -0.24f; z <= 0.241f; z += 0.04f)
        { var o = new Vector3(x, y, z); best.Add((o.magnitude, o, count(o))); }
        best.Sort((a, c) => a.mag.CompareTo(c.mag));
        foreach (float frac in new[] { 0.5f, 0.2f, 0.05f, 0f })
        {
            foreach (var c in best) if (c.n <= baseCount * frac) { sb.Append("smallest offset with <=" + (frac * 100) + "% of the clipping: " + c.off.ToString("0.00") + " (|" + c.mag.ToString("0.00") + "| m, " + c.n + " verts) | "); break; }
        }
        BattleRecorder.Note(sb.ToString());
        BattleRecorder.NoteEnd(bs, "SCENARIO END, shieldfit");
    }

    // One soldier, every animation state, in PLAY mode with the real Animator and the scene's real lighting.
    private IEnumerator AnimSheetScenario(BattleSetup bs, bool archer)
    {
        var cfg = Resources.Load<KayKitVisualConfig>("KayKitVisualConfig");
        var prefab = Resources.Load<GameObject>(archer ? "VIS_KayKit_Archer" : "VIS_KayKit_Melee");
        string[] states = archer
            ? new[] { "Ranged_Bow_Aiming_Idle", "Idle_A", "Walking_A", "Running_A", "Ranged_Bow_Draw", "Ranged_Bow_Release", "Hit_A", "Death_A", "Death_A_Pose", "Cheering" }
            : new[] { "Idle_A", "Melee_Blocking", "Walking_A", "Running_A", "Melee_1H_Attack_Stab", "Melee_1H_Attack_Chop", "Melee_1H_Attack_Slice_Diagonal", "Melee_Block_Hit", "Hit_A", "Death_A", "Death_A_Pose", "Cheering" };
        Vector3 origin = new Vector3(4000f, 0f, 0f);
        var go = Object.Instantiate(prefab, origin, Quaternion.identity);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterial = (archer && r is MeshRenderer) ? cfg.bowMaterial : cfg.blueMaterials[0];
        var anim = go.GetComponent<Animator>(); anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; anim.applyRootMotion = false;
        go.GetComponentInChildren<SkinnedMeshRenderer>().updateWhenOffscreen = true;
        var camGo = new GameObject("SheetCam"); var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 1.7f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.72f, 0.69f, 0.58f);
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 40f; cam.enabled = false;
        var focus = origin + new Vector3(0f, 1.1f, 0f);
        cam.transform.position = focus + Quaternion.Euler(12f, 40f, 0f) * new Vector3(0f, 0f, 8f); cam.transform.LookAt(focus);   // front-left, shield side to camera
        // the soldier faces +z; the camera sits in front and to its left
        cam.transform.position = focus + Quaternion.Euler(12f, -40f, 0f) * new Vector3(0f, 0f, 8f); cam.transform.LookAt(focus);
        int cols = 6, rows = Mathf.CeilToInt(states.Length * 2 / (float)cols), T = 360;
        var rt = new RenderTexture(T, T, 24); var sheet = new Texture2D(cols * T, rows * T, TextureFormat.RGB24, false);
        for (int i = 0; i < states.Length; i++)
            for (int k = 0; k < 2; k++)
            {
                anim.Play(Animator.StringToHash(states[i]), 0, k == 0 ? 0.25f : 0.6f);
                anim.speed = 0f;            // freeze on the sampled frame
                yield return null; yield return null; yield return new WaitForEndOfFrame();
                cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                var prev = RenderTexture.active; RenderTexture.active = rt;
                int idx = i * 2 + k; sheet.ReadPixels(new Rect(0, 0, T, T), (idx % cols) * T, (rows - 1 - idx / cols) * T); RenderTexture.active = prev;
                anim.speed = 1f;
            }
        sheet.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Temp", archer ? "play_sheet_archer.png" : "play_sheet.png"), sheet.EncodeToPNG());
        BattleRecorder.Note("animsheet states: " + string.Join(", ", states));
        BattleRecorder.NoteEnd(bs, "SCENARIO END, animsheet");
    }

    // Style check: real in-game captures of both armies at a close and a normal zoom, no fighting.
    private IEnumerator ShotsScenario(BattleSetup bs, float[] ortho)
    {
        var cam = Camera.main; var rig = cam != null ? cam.GetComponent<BattleCamera>() : null;
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "Temp");
        { // what is the live soldier really drawing with?
            var sbd = new System.Text.StringBuilder("shots diag: ");
            foreach (var team in new[] { Team.Blue, Team.Red })
            {
                Soldier first = null; foreach (var sd in bs.GetSoldiers(team)) { first = sd; break; }
                if (first == null) continue;
                var smr = first.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var mt = smr.sharedMaterial; var tx = mt != null ? mt.GetTexture("_BaseMap") : null;
                var counts = new Dictionary<string, int>();
                foreach (var uv in smr.sharedMesh.uv) { string k = Mathf.Clamp((int)(uv.x * 8f), 0, 7) + "," + Mathf.Clamp((int)((1f - uv.y) * 4f), 0, 3); counts[k] = counts.TryGetValue(k, out int c) ? c + 1 : 1; }
                sbd.Append(team + ": mesh=" + smr.sharedMesh.name + " mat=" + (mt != null ? mt.name : "null") + " tex=" + (tx != null ? tx.name : "null") + " tiles(5,3)=" + (counts.TryGetValue("5,3", out int n53) ? n53 : 0) + " (3,0)=" + (counts.TryGetValue("3,0", out int n30) ? n30 : 0) + " | ");
            }
            BattleRecorder.Note(sbd.ToString());
        }
        foreach (float o in ortho)
        {
            if (rig != null) rig.ZoomBy(o / cam.orthographicSize);
            for (int i = 0; i < 150; i++) yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "shot_blue_" + o + ".png"));
            for (int i = 0; i < 10; i++) yield return null;
            if (rig != null) rig.PanBy(new Vector3(0f, 0f, 200f));
            for (int i = 0; i < 200; i++) yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "shot_red_" + o + ".png"));
            for (int i = 0; i < 10; i++) yield return null;
            if (rig != null) rig.PanBy(new Vector3(0f, 0f, -200f));
            for (int i = 0; i < 200; i++) yield return null;
        }
        BattleRecorder.NoteEnd(bs, "SCENARIO END, shots");
    }

    // Edge contact: Blue's left edge clips the right edge of Red C1 (offset 9 m), then Blue is ordered to attack it.
    // Logs, every 3 s after first contact, the share of Blue's men in contact and the angle between Blue's facing
    // and the direction to the enemy block, with and without the swing-in rule.
    private IEnumerator EdgeScenario(BattleSetup bs)
    {
        var blue = Find(bs, "Blue Century 1"); var red = Find(bs, "Red Century 1");
        blue.IssueMove(new Vector3(red.AnchorPos.x + 9f, 0f, red.AnchorPos.z - 22f));
        while (bs.Phase == BattlePhase.Active && blue.HasMoveDestination) yield return null;
        blue.IssueAttack(red);
        float t0 = -1f; var sb = new System.Text.StringBuilder("edge: ");
        float next = 0f; float until = bs.BattleTime + 140f;
        while (bs.Phase == BattlePhase.Active && bs.BattleTime < until)
        {
            int eng = 0; foreach (var sd in blue.soldiers) if (sd.IsEngaged) eng++;
            if (t0 < 0f && eng > 0) { t0 = bs.BattleTime; next = 0f; }
            if (t0 >= 0f && bs.BattleTime - t0 >= next)
            {
                Vector3 to = red.AnchorPos - blue.AnchorPos; to.y = 0f;
                sb.Append("+" + next.ToString("0") + "s inContact=" + (100f * eng / Mathf.Max(1, blue.soldiers.Count)).ToString("0") + "% facingErr=" + Vector3.Angle(blue.AnchorForward, to).ToString("0") + "deg | ");
                next += 3f; if (next > 30f) break;
            }
            yield return null;
        }
        BattleRecorder.Note(sb.ToString());
        BattleRecorder.NoteEnd(bs, "SCENARIO END, edge");
    }

    // Back to front: both rear Blue centuries are ordered through the archers and
    // the front line to a spot ahead of it. Records how far every OTHER friendly
    // soldier is dragged from where it stood (max / average / count > 3 m).
    private IEnumerator BackToFrontScenario(BattleSetup bs, bool red)
    {
        string tm = red ? "Red" : "Blue"; Team team = red ? Team.Red : Team.Blue; float zs = red ? -1f : 1f;   // red mirrors z
        var movers = new List<Formation> { Find(bs, tm + " Century 5"), Find(bs, tm + " Century 6") };
        var start = new Dictionary<Soldier, Vector3>();
        foreach (var f in bs.formations) if (f.team == team && !movers.Contains(f)) foreach (var s2 in f.soldiers) start[s2] = s2.transform.position;
        movers[0].IssueMove(new Vector3(-10f, 0f, -60f * zs)); movers[1].IssueMove(new Vector3(10f, 0f, -60f * zs));
        for (int i = 0; i < 90; i++) yield return null;
        { var rp = new List<Vector3>(); movers[0].GetRemainingRoute(rp); var sbr = new System.Text.StringBuilder();
          foreach (var q in rp) sbr.Append(q.ToString("0")).Append(' ');
          { float maxTurn = 0f; int kinks = 0; Vector3 prev = movers[0].AnchorPos; Vector3 pd = Vector3.zero;
            foreach (var q in rp) { Vector3 dd = q - prev; dd.y = 0f; if (dd.magnitude > 0.3f) { dd.Normalize(); if (pd != Vector3.zero) { float a = Vector3.Angle(pd, dd); maxTurn = Mathf.Max(maxTurn, a); if (a > 25f) kinks++; } pd = dd; prev = q; } }
            BattleRecorder.Note("b2f pathShape: points=" + rp.Count + " maxTurnPerSegment=" + maxTurn.ToString("0") + "deg kinks>25deg=" + kinks); }
          BattleRecorder.Note("b2f route C5 (" + rp.Count + "): " + sbr + " hasRoute=" + movers[0].HasRoute + " pos=" + movers[0].AnchorPos); }
        float t0 = bs.BattleTime; float peak = 0f;
        var lastRoute = new List<Vector3>(); var curRoute = new List<Vector3>(); int routeFlips = 0, routeSamples = 0; float nextSample = 0f;
        while (bs.Phase == BattlePhase.Active && bs.BattleTime - t0 < 80f && (movers[0].HasMoveDestination || movers[1].HasMoveDestination))
        {
            foreach (var kv in start) if (kv.Key != null) peak = Mathf.Max(peak, Vector3.Distance(kv.Key.transform.position, kv.Value));
            if (bs.BattleTime - t0 >= nextSample)
            {
                nextSample += 0.5f; routeSamples++;
                movers[0].GetRemainingRoute(curRoute);
                bool changed = curRoute.Count > lastRoute.Count;   // shrinking = a waypoint was reached; growing/different = re-plan
                if (!changed && curRoute.Count > 0 && lastRoute.Count > 0)
                {
                    Vector3 a = curRoute[curRoute.Count - 1 - (curRoute.Count - 1)]; // first remaining waypoint
                    Vector3 b = lastRoute[lastRoute.Count - curRoute.Count];          // same waypoint index counted from the end
                    changed = (a - b).sqrMagnitude > 4f;
                }
                if (changed && lastRoute.Count > 0) routeFlips++;
                lastRoute.Clear(); lastRoute.AddRange(curRoute);
            }
            yield return null;
        }
        BattleRecorder.Note("b2f routeFlips=" + routeFlips + " of " + routeSamples + " samples (C5)");
        for (int i = 0; i < 240; i++) yield return null;
        float sum = 0f, max = 0f; int n = 0, far = 0;
        var worst = new Dictionary<string, float>();
        foreach (var kv in start)
        {
            if (kv.Key == null) continue;
            float d = Vector3.Distance(kv.Key.transform.position, kv.Value);
            sum += d; n++; max = Mathf.Max(max, d); if (d > 3f) far++;
            string nm = kv.Key.formation.displayName; worst[nm] = Mathf.Max(worst.TryGetValue(nm, out float w) ? w : 0f, d);
        }
        { var sbd = new System.Text.StringBuilder();
          foreach (var kv in start)
          {
              if (kv.Key == null) continue;
              float d = Vector3.Distance(kv.Key.transform.position, kv.Value); if (d < 20f) continue;
              float nearM = float.MaxValue; foreach (var mv in movers) foreach (var ms in mv.soldiers) nearM = Mathf.Min(nearM, Vector3.Distance(ms.transform.position, kv.Key.transform.position));
              sbd.Append(kv.Key.formation.displayName).Append(" slot=").Append(kv.Key.slotIndex).Append(" start=").Append(kv.Value.ToString("0")).Append(" end=").Append(kv.Key.transform.position.ToString("0")).Append(" nearestMover=").Append(nearM.ToString("0.0")).Append(" state=").Append(kv.Key.formation.State).Append(" | ");
          }
          BattleRecorder.Note("b2f dragged: " + sbd); }
        var sb = new System.Text.StringBuilder(); foreach (var kv in worst) sb.Append(kv.Key.Replace("Century ", "C").Replace("Archers ", "A")).Append('=').Append(kv.Value.ToString("0")).Append(' ');
        BattleRecorder.Note("b2f RESULT arrival=" + (bs.BattleTime - t0).ToString("0.0") + "s peakDrag=" + peak.ToString("0.0") + "m finalMax=" + max.ToString("0.0") + "m finalAvg=" + (sum / Mathf.Max(1, n)).ToString("0.00") + "m soldiersMoved>3m=" + far + "/" + n + " | perFormationMax " + sb);
        BattleRecorder.NoteEnd(bs, "SCENARIO END, b2f");
    }

    // Target lock: Blue attacks the far Red century while a neighbouring Red
    // century's corner lies on the way. Records who Blue ended up fighting and
    // when it first engaged each.
    private IEnumerator LockScenario(BattleSetup bs)
    {
        var blue = Find(bs, "Blue Century 1"); var near = Find(bs, "Red Century 2"); var far = Find(bs, "Red Century 3");
        near.IssueMove(new Vector3(4f, 0f, 30f));   // bystander parked so only a corner of its block overlaps Blue's line to the far target
        blue.IssueAttack(far);
        foreach (var f in bs.formations) BattleRecorder.Note("lock layout " + f.displayName + " " + f.AnchorPos);
        float t0 = bs.BattleTime; float engagedAt = -1f; string engagedWith = ""; int engagedOnTarget = 0;
        var firstBy = new Dictionary<string, float>();
        while (bs.Phase == BattlePhase.Active && bs.BattleTime - t0 < 150f)
        {
            float t = bs.BattleTime - t0;
            foreach (var e in bs.formations)
                if (e.team == Team.Red && blue.ContactsWith(e) > 0 && !firstBy.ContainsKey(e.displayName)) firstBy[e.displayName] = t;
            if (blue.State == FormationState.Engaged && engagedAt < 0f)
            {
                engagedAt = t;
                var near2 = new Dictionary<string, int>();
                foreach (var e in bs.GetSoldiers(Team.Red))
                    foreach (var m in blue.soldiers)
                        if ((e.transform.position - m.transform.position).sqrMagnitude < 4f) { near2[e.formation.displayName] = near2.TryGetValue(e.formation.displayName, out int c) ? c + 1 : 1; break; }
                foreach (var kv2 in near2) engagedWith += kv2.Key + "(" + kv2.Value + ") ";
                engagedOnTarget = near2.TryGetValue(far.displayName, out int ft) ? ft : 0;
            }
            yield return null;
        }
        var sbf = new System.Text.StringBuilder();
        foreach (var kv in firstBy) sbf.Append(kv.Key).Append('@').Append(kv.Value.ToString("0.0")).Append("s ");
        BattleRecorder.Note("lock firstContacts: " + sbf + "| engagedAt=" + engagedAt.ToString("0.0") + " with " + engagedWith + "soldiersOnTarget=" + engagedOnTarget);
        BattleRecorder.Note("lock RESULT finalState=" + blue.State + " target=" + (blue.attackTarget != null ? blue.attackTarget.displayName : "-"));
        BattleRecorder.NoteEnd(bs, "SCENARIO END, lock");
    }

    private IEnumerator ChargeScenario(BattleSetup bs)
    {
        var blue = Find(bs, "Blue Century 1"); var red = Find(bs, "Red Century 1");
        blue.IssueMove(new Vector3(red.AnchorPos.x, 0f, red.AnchorPos.z - 14f));   // close enough that the 5 s rush actually reaches contact
        while (bs.Phase == BattlePhase.Active && (blue.HasMoveDestination || blue.State != FormationState.Ordered)) yield return null;
        blue.IssueCharge(red);
        BattleRecorder.Note("charge issued state=" + blue.State + " target=" + (blue.attackTarget != null ? blue.attackTarget.displayName : "-"));
        float until = bs.BattleTime + 90f;
        while (bs.Phase == BattlePhase.Active && bs.BattleTime < until) yield return null;
        yield return null;
        if (bs.Phase != BattlePhase.Ended) BattleRecorder.NoteEnd(bs, "SCENARIO END, charge, 90 s");
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
        BattleRecorder.Note("colliderRadius=" + bs.combatConfig.soldierColliderRadius);
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
                { var idle = Find(bs, "Blue Century 6"); float sp = 0f; foreach (var s in idle.soldiers) sp += s.Velocity.magnitude;
                  BattleRecorder.Note("idleSpeed@" + marks[next] + "s BlueC6 avg=" + (idle.soldiers.Count > 0 ? (sp / idle.soldiers.Count).ToString("0.000") : "-") + " m/s"); }
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
        if (bs.Phase != BattlePhase.Ended) BattleRecorder.NoteEnd(bs, "SCENARIO END, lines, 180 s after contact");
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
            BattleRecorder.NoteEnd(bs, blue.IsRouting ? "SCENARIO END, Blue Century 4 routed" : "SCENARIO END, no rout within 150 s of the flank order");
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
