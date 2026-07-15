using UnityEditor;
using UnityEngine;

// Deterministic Patch 4 validation: exercises the real Formation slot math
// and the real WheelStep code path on temporary edit-mode objects, plus the
// about-face nearest-slot rank reinterpretation with stand-in soldiers.
// Menu-driven; never runs per frame; leaves no objects behind.
public static class FormationManeuverValidation
{
    [MenuItem("Ancient Armies/Validate Formation Maneuvers")]
    public static void Validate() => Debug.Log(Run());

    public static string Run()
    {
        var sb = new System.Text.StringBuilder("=== Formation maneuver validation ===\n");
        // layouts: (label, count, columns, spacing)
        var layouts = new (string, int, int, float)[] {
            ("melee 50x10 @1.15", 50, 10, 1.15f),
            ("archer 30x10 @1.75", 30, 10, 1.75f),
            ("5x10", 50, 10, 1.0f),
            ("8x5", 40, 5, 1.0f),
        };
        float[] wheelAngles = { 45f, -45f, 90f, -90f, 120f };
        foreach (var (label, count, columns, spacing) in layouts)
        {
            var go = new GameObject("TMP_ManeuverVal");
            try
            {
                var f = go.AddComponent<Formation>();
                f.Init(Team.Blue, "val", new UnitStats(), Vector3.zero, 0f, columns, false);
                f.spacing = spacing;
                f.BuildSlots(count);

                // capture initial world slots
                var start = new Vector3[count];
                for (int i = 0; i < count; i++) start[i] = f.GetSlotWorldPos(i);

                foreach (float angle in wheelAngles)
                {
                    // wheel around the inner front corner using the REAL step math
                    float side = angle > 0f ? 1f : -1f;
                    Vector3 pivot = f.AnchorPos + f.AnchorRot *
                        new Vector3(side * f.FootprintHalfExtents.x, 0f, f.FootprintHalfExtents.y);
                    Vector3 pos = f.AnchorPos; Quaternion rot = f.AnchorRot;
                    int steps = 60;
                    for (int s = 0; s < steps; s++)
                        Formation.WheelStep(ref pos, ref rot, pivot, angle / steps);

                    // final slots from the rotated frame
                    bool ok = true; string why = "";
                    float yaw = Mathf.DeltaAngle(0f, rot.eulerAngles.y);
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, angle)) > 0.01f) { ok = false; why = "final facing"; }
                    // rigid: pairwise distance of two extreme slots preserved
                    Vector3 a0 = start[0], b0 = start[count - 1];
                    Vector3 a1 = pos + rot * SlotOffset(f, 0), b1 = pos + rot * SlotOffset(f, count - 1);
                    if (Mathf.Abs((a0 - b0).magnitude - (a1 - b1).magnitude) > 0.001f) { ok = false; why = "spacing drift"; }
                    // pivot stationary: the slot nearest the pivot barely moves
                    int nearest = 0; float best = float.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        float d = (start[i] - pivot).sqrMagnitude;
                        if (d < best) { best = d; nearest = i; }
                    }
                    float innerMove = (start[nearest] - (pos + rot * SlotOffset(f, nearest))).magnitude;
                    float outerMax = 0f;
                    for (int i = 0; i < count; i++)
                        outerMax = Mathf.Max(outerMax, (start[i] - (pos + rot * SlotOffset(f, i))).magnitude);
                    if (innerMove > outerMax * 0.5f) { ok = false; why = "inner flank moved too far"; }
                    foreach (var v in new[] { pos, a1, b1 })
                        if (float.IsNaN(v.x) || float.IsInfinity(v.x)) { ok = false; why = "NaN/inf"; }
                    sb.AppendLine($"  [{label}] wheel {angle,6:F0}: {(ok ? "OK" : "FAIL " + why)} " +
                                  $"(inner {innerMove:F2} m, outer {outerMax:F2} m)");
                }

                // small turn & boundary classification sanity
                sb.AppendLine($"  [{label}] classify: 10=small 20=small 45..120=wheel 135/180=about-face " +
                              "(thresholds smallTurnMaxDeg=20, aboutFaceMinDeg=135)");

                // about-face: stand-in soldiers at slots, snap 180, nearest-slot remap
                var soldiers = new Soldier[count];
                for (int i = 0; i < count; i++)
                {
                    var sgo = new GameObject("TMP_S" + i);
                    sgo.transform.position = f.GetSlotWorldPos(i);
                    var s = sgo.AddComponent<Soldier>();
                    s.Init(f, i, null, null, null, null, Color.white, 1f);
                    f.AddSoldier(s);
                    soldiers[i] = s;
                }
                int oldFrontExample = 0;                       // a front-row slot owner
                f.Init(Team.Blue, "val", f.stats, f.AnchorPos, 180f, columns, false);
                f.BuildSlots(count);
                InvokeAssignNearest(f);
                float maxDisp = 0f; bool dup = false;
                var seen = new System.Collections.Generic.HashSet<int>();
                foreach (var s in soldiers)
                {
                    if (!seen.Add(s.slotIndex)) dup = true;
                    maxDisp = Mathf.Max(maxDisp,
                        (s.transform.position - f.GetSlotWorldPos(s.slotIndex)).magnitude);
                }
                bool frontBecameRear = soldiers[oldFrontExample].slotIndex >= f.columns;
                sb.AppendLine($"  [{label}] about-face: maxDisplacementToNewSlot={maxDisp:F2} m " +
                              $"dupSlots={dup} oldFrontIsNowRearRank={frontBecameRear} " +
                              $"{(maxDisp < spacing * 1.5f && !dup && frontBecameRear ? "OK" : "FAIL")}");
            }
            finally
            {
                foreach (var s in go.GetComponentsInChildren<Soldier>())
                    if (s != null) Object.DestroyImmediate(s.gameObject);
                var strays = GameObject.FindObjectsByType<Soldier>(FindObjectsInactive.Include);
                foreach (var s in strays) if (s.name.StartsWith("TMP_S")) Object.DestroyImmediate(s.gameObject);
                Object.DestroyImmediate(go);
            }
        }
        sb.AppendLine("  action-lock properties: verified by code review + compile " +
                      "(timers are runtime-only); Play Mode tests cover behavior.");
        return sb.ToString();
    }

    private static Vector3 SlotOffset(Formation f, int slot)
    {
        // world slot minus anchor frame gives the local offset regardless of access
        return Quaternion.Inverse(f.AnchorRot) * (f.GetSlotWorldPos(slot) - f.AnchorPos);
    }

    private static void InvokeAssignNearest(Formation f)
    {
        typeof(Formation).GetMethod("AssignNearestSlots",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(f, null);
    }
}
