using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Editor-only sanity checks for the dominant-group clustering that drives
// banner placement and the reform rally anchor. Exercises the exact shipped
// code path (Formation.ComputeDominantGroup is static, like WheelStep).
public static class ClusterValidation
{
    private const float Spacing = 1.15f;              // melee spacing
    private const float Link = Spacing * 2.5f;        // default clusterLinkFactor
    private const float Switch = 1.3f;                // default clusterSwitchFactor

    [MenuItem("Ancient Armies/Validate Dominant Group")]
    public static void Validate() => Debug.Log(Run());

    public static string Run()
    {
        var sb = new System.Text.StringBuilder("=== Dominant group validation ===\n");

        // Two blobs far apart: 40 at x=0, 10 at x=30.
        var pos = new List<Vector3>();
        Blob(pos, Vector3.zero, 40);
        Blob(pos, new Vector3(30f, 0f, 0f), 10);
        Vector3 c = Formation.ComputeDominantGroup(pos, Link, Vector3.zero, false, Switch, out int n);
        Check(sb, "40/10 split → banner with the 40", n == 40 && c.x < 5f,
              $"count={n} center={c:F1}");

        // Hysteresis: incumbent 24 keeps ownership vs rival 26 (< 1.3x).
        pos.Clear();
        Blob(pos, Vector3.zero, 24);
        Blob(pos, new Vector3(30f, 0f, 0f), 26);
        c = Formation.ComputeDominantGroup(pos, Link, Vector3.zero, true, Switch, out n);
        Check(sb, "26 vs incumbent 24 → incumbent holds", n == 24 && c.x < 5f,
              $"count={n} center={c:F1}");

        // 25/25 with incumbent: stays put.
        pos.Clear();
        Blob(pos, Vector3.zero, 25);
        Blob(pos, new Vector3(30f, 0f, 0f), 25);
        c = Formation.ComputeDominantGroup(pos, Link, Vector3.zero, true, Switch, out n);
        Check(sb, "25/25 → incumbent holds", n == 25 && c.x < 5f, $"count={n} center={c:F1}");

        // Rival clearly bigger than incumbent×1.3: ownership transfers.
        pos.Clear();
        Blob(pos, Vector3.zero, 15);
        Blob(pos, new Vector3(30f, 0f, 0f), 35);
        c = Formation.ComputeDominantGroup(pos, Link, Vector3.zero, true, Switch, out n);
        Check(sb, "35 vs incumbent 15 → transfers", n == 35 && c.x > 25f,
              $"count={n} center={c:F1}");

        // No previous center: plain largest cluster wins.
        c = Formation.ComputeDominantGroup(pos, Link, Vector3.zero, false, Switch, out n);
        Check(sb, "35/15 cold start → largest wins", n == 35 && c.x > 25f,
              $"count={n} center={c:F1}");

        // Single soldier.
        pos.Clear();
        pos.Add(new Vector3(7f, 0f, -3f));
        c = Formation.ComputeDominantGroup(pos, Link, Vector3.zero, true, Switch, out n);
        Check(sb, "single survivor", n == 1 && (c - pos[0]).magnitude < 0.01f,
              $"count={n} center={c:F1}");

        // Empty list: previous center returned, count 0.
        pos.Clear();
        c = Formation.ComputeDominantGroup(pos, Link, new Vector3(1f, 0f, 2f), true, Switch, out n);
        Check(sb, "empty formation", n == 0 && c == new Vector3(1f, 0f, 2f), $"count={n}");

        // One intact 50-block is a single cluster centered on the block.
        pos.Clear();
        Blob(pos, new Vector3(-4f, 0f, 9f), 50);
        c = Formation.ComputeDominantGroup(pos, Link, Vector3.zero, false, Switch, out n);
        Check(sb, "intact block → one cluster", n == 50 && (c - new Vector3(-4f, 0f, 9f)).magnitude < 1f,
              $"count={n} center={c:F1}");

        return sb.ToString();
    }

    // Deterministic grid blob whose centroid is exactly `center` (partial last
    // rows would otherwise bias the mean), spaced tighter than the link distance.
    private static void Blob(List<Vector3> pos, Vector3 center, int count)
    {
        int start = pos.Count;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        Vector3 mean = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            var p = new Vector3((i % cols) * Spacing, 0f, (i / cols) * Spacing);
            pos.Add(p);
            mean += p;
        }
        mean /= count;
        for (int i = start; i < pos.Count; i++) pos[i] += center - mean;
    }

    private static void Check(System.Text.StringBuilder sb, string name, bool ok, string detail)
    {
        sb.AppendLine($"  {(ok ? "PASS" : "FAIL")} {name} ({detail})");
    }
}
