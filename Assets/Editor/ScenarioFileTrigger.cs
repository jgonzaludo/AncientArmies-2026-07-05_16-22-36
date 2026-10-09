using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

// Test tooling: drives ScenarioRunner from files, so battles can be scripted
// without any editor bridge. Works while the editor is unfocused.
//   Temp/scenario_request.txt   key=value lines: presets, seed, speed, renderOff, aiOn
//                               (or the single word "refresh" to reimport + recompile)
//   Temp/scenario_ready.txt     rewritten on every script load
//   Temp/scenario_compile.txt   compile errors from the last compilation (empty = clean)
//   Temp/scenario_done.txt      written when a run finishes: ScenarioRunner.Status
[InitializeOnLoad]
public static class ScenarioFileTrigger
{
    private const string Key = "ScenarioFileTrigger.request";
    private static readonly string TempDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
    private static string Req => Path.Combine(TempDir, "scenario_request.txt");
    private static string Done => Path.Combine(TempDir, "scenario_done.txt");
    private static double nextPoll;
    private static bool started;
    private static bool ensured;

    static ScenarioFileTrigger()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlayMode;
        CompilationPipeline.assemblyCompilationFinished += (asm, msgs) =>
        {
            var errs = msgs.Where(m => m.type == CompilerMessageType.Error).Select(m => m.file + ":" + m.line + " " + m.message).ToArray();
            if (errs.Length > 0) File.AppendAllLines(Path.Combine(TempDir, "scenario_compile.txt"), errs);
        };
        CompilationPipeline.compilationStarted += _ => File.WriteAllText(Path.Combine(TempDir, "scenario_compile.txt"), "");
        File.WriteAllText(Path.Combine(TempDir, "scenario_ready.txt"), DateTime.Now.ToString("HH:mm:ss") + " playing=" + EditorApplication.isPlaying);
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;

        if (EditorApplication.isPlaying)
        {
            string pending = SessionState.GetString(Key, "");
            if (pending == "")
            {
                if (File.Exists(Req)) EditorApplication.isPlaying = false;   // leave manual/stuck Play mode, then serve it
                return;
            }
            if (!started)
            {
                if (BattleSetup.Instance == null) return;
                started = true;
                var kv = pending.Split('\n').Select(l => l.Split(new[] { '=' }, 2)).Where(p => p.Length == 2)
                                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
                string Get(string k, string d) => kv.TryGetValue(k, out var v) ? v : d;
                ScenarioRunner.Run(Get("presets", "P0:"), int.Parse(Get("seed", "12345")),
                                   float.Parse(Get("speed", "2"), System.Globalization.CultureInfo.InvariantCulture),
                                   Get("renderOff", "true") == "true", Get("aiOn", "true") == "true", Get("scenario", ""));
                return;
            }
            if (ScenarioRunner.Status.StartsWith("done"))
            {
                File.WriteAllText(Done, ScenarioRunner.Status);
                SessionState.EraseString(Key);
                started = false;
                EditorApplication.isPlaying = false;
            }
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!ensured) { ensured = true; EnsureAsset<CombatConfig>("Assets/AncientArmies/Settings/CombatConfig.asset"); }
        if (!File.Exists(Req)) return;
        string text = File.ReadAllText(Req);
        File.Delete(Req);
        if (text.Trim() == "refresh") { AssetDatabase.Refresh(); return; }
        if (text.TrimStart().StartsWith("task="))
        {
            // editor tasks: "task=Class.Method" runs a public static string Method() and writes
            // its return value (or the exception) to Temp/editor_task_result.txt
            string name = text.Trim().Substring(5).Trim();
            string result;
            try
            {
                int dot = name.LastIndexOf('.');
                var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                                    .FirstOrDefault(t => t.Name == name.Substring(0, dot));
                result = (string)type.GetMethod(name.Substring(dot + 1), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, null);
            }
            catch (Exception e) { result = "EXCEPTION " + (e.InnerException ?? e); }
            File.WriteAllText(Path.Combine(TempDir, "editor_task_result.txt"), result);
            return;
        }
        if (File.Exists(Done)) File.Delete(Done);
        SessionState.SetString(Key, text);
        started = false;
        EditorApplication.isPlaying = true;
    }

    // Creates a settings asset from its class defaults if it does not exist
    // yet (no scene involved, so nothing here needs a scene save).
    private static void EnsureAsset<T>(string path) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        AssetDatabase.SaveAssets();
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode) started = false;
    }
}
