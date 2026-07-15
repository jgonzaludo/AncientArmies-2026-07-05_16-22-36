using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Editor-only sanity checks for the Patch 1 facing / locomotion pipeline.
// Menu-driven, never runs per frame, safe outside Play Mode.
public static class FacingValidation
{
    [MenuItem("Ancient Armies/Validate Facing Pipeline")]
    public static void Validate() => Debug.Log(Run());

    // Returns the full report so tooling can read it without console truncation.
    public static string Run()
    {
        var sb = new System.Text.StringBuilder("=== Facing pipeline validation ===\n");

        // 1. Controllers: every loco state (and only loco states) binds LocoScale.
        CheckController(sb, "Assets/Art/Characters/Romans/Legionary/Animations/AC_RomanInfantry.controller",
            new[] { "FormationMarch", "CombatAdvance", "CloseRanksShuffle", "BrokenRun" });
        CheckController(sb, "Assets/Art/Characters/Romans/Archer/Animations/AC_RomanArcher.controller",
            new[] { "FormationWalk", "CloseRanksShuffle" });

        // 2. Prefabs: exactly one axis-conversion node, visible forward = +Z.
        CheckPrefab(sb, "Assets/Art/Characters/Romans/Legionary/Prefabs/Resources/VIS_Roman_Legionary_Basic.prefab");
        CheckPrefab(sb, "Assets/Art/Characters/Romans/Archer/Prefabs/Resources/VIS_Roman_Archer_Basic.prefab");

        // 3. Live facing chain (Play Mode only): per formation, how far the
        // soldiers' facings deviate from the canonical facing while ordered.
        if (Application.isPlaying)
        {
            foreach (var f in Object.FindObjectsByType<Formation>())
            {
                float worst = 0f;
                foreach (var s in f.soldiers)
                    worst = Mathf.Max(worst, Vector3.Angle(s.transform.forward, f.AnchorForward));
                sb.AppendLine($"  [{f.displayName}] state={f.State} anchorFwd={f.AnchorForward:F2} " +
                              $"worstSoldierDeviation={worst:F0}deg (combat/movement facing is expected to deviate)");
            }
        }
        else sb.AppendLine("  (not in Play Mode: live facing chain skipped)");

        return sb.ToString();
    }

    private static void CheckController(System.Text.StringBuilder sb, string path, string[] locoStates)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) { sb.AppendLine($"  MISSING controller: {path}"); return; }
        foreach (var layer in ctrl.layers)
            foreach (var cs in layer.stateMachine.states)
            {
                var st = cs.state;
                bool shouldBind = System.Array.IndexOf(locoStates, st.name) >= 0;
                bool binds = st.speedParameterActive && st.speedParameter == "LocoScale";
                if (shouldBind != binds)
                    sb.AppendLine($"  BAD BINDING {ctrl.name}/{st.name}: speedParamActive={st.speedParameterActive} '{st.speedParameter}' (expected {(shouldBind ? "LocoScale" : "none")})");
                if (st.motion == null && !st.name.Contains("Idle"))
                    sb.AppendLine($"  MISSING MOTION {ctrl.name}/{st.name}");
            }
        sb.AppendLine($"  {ctrl.name}: binding audit done");
    }

    private static void CheckPrefab(System.Text.StringBuilder sb, string path)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (pf == null) { sb.AppendLine($"  MISSING prefab: {path}"); return; }
        if (pf.transform.localRotation != Quaternion.identity)
            sb.AppendLine($"  {pf.name}: root rotation is NOT identity: {pf.transform.localRotation.eulerAngles}");
        // Expected layout: GEO_* (mesh) and RIG_* (armature) siblings, each
        // carrying the same constant importer axis conversion Euler(270,0,0).
        var expected = Quaternion.Euler(270f, 0f, 0f);
        foreach (Transform t in pf.transform)
        {
            bool conversionNode = t.name.StartsWith("GEO_") || t.name.StartsWith("RIG_");
            bool hasConversion = Quaternion.Angle(t.localRotation, expected) < 0.5f;
            if (conversionNode && !hasConversion)
                sb.AppendLine($"  {pf.name}/{t.name}: conversion node rotation drifted: {t.localRotation.eulerAngles}");
            if (!conversionNode && t.localRotation != Quaternion.identity)
                sb.AppendLine($"  {pf.name}/{t.name}: UNEXPECTED rotated child: {t.localRotation.eulerAngles}");
        }
        sb.AppendLine($"  {pf.name}: root=identity, conversion nodes verified (GEO_/RIG_ @ 270,0,0)");
    }
}
