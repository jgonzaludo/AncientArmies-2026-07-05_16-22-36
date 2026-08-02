using UnityEditor;
using UnityEngine;

// Editor-only sanity check for the live facing chain: how far each formation's
// soldiers deviate from its canonical AnchorForward. Menu-driven, never runs
// per frame, safe outside Play Mode.
//
// The former controller/prefab rig audits were dropped with the Roman art —
// placeholder capsules carry no Animator, rig, or axis-conversion nodes.
public static class FacingValidation
{
    [MenuItem("Ancient Armies/Validate Facing Pipeline")]
    public static void Validate() => Debug.Log(Run());

    // Returns the full report so tooling can read it without console truncation.
    public static string Run()
    {
        var sb = new System.Text.StringBuilder("=== Facing pipeline validation ===\n");

        if (!Application.isPlaying)
        {
            sb.AppendLine("  (not in Play Mode: live facing chain skipped)");
            return sb.ToString();
        }

        foreach (var f in Object.FindObjectsByType<Formation>())
        {
            float worst = 0f;
            foreach (var s in f.soldiers)
                worst = Mathf.Max(worst, Vector3.Angle(s.transform.forward, f.AnchorForward));
            sb.AppendLine($"  [{f.displayName}] state={f.State} anchorFwd={f.AnchorForward:F2} " +
                          $"worstSoldierDeviation={worst:F0}deg (combat/movement facing is expected to deviate)");
        }

        return sb.ToString();
    }
}
