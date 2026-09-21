using UnityEngine;
using UnityEngine.SceneManagement;

// DEBUG ONLY (Chunk A) — on-device morale tuning. Draws each formation's
// morale and state near its banner, and a TEMPORARY grade number over each
// officer's head (TemporaryOfficerRanks). Toggled live by
// MoraleConfig.showDebugMorale.
//
// Self-installing on every scene load, so nothing else references it: delete
// this file and the overlay is gone. Reads simulation state, never writes it.
public class MoraleDebugOverlay : MonoBehaviour
{
    private const float LabelHeight = 4.5f;     // above the anchor, near the banner
    private const float GradeHeight = 2.4f;     // above a soldier's head

    private GUIStyle formationStyle;
    private GUIStyle gradeStyle;
    private int styledForHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();   // the first scene has already loaded by now
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    private static void Attach()
    {
        var bs = BattleSetup.Instance;
        if (bs != null && bs.GetComponent<MoraleDebugOverlay>() == null)
            bs.gameObject.AddComponent<MoraleDebugOverlay>();
    }

    private void OnGUI()
    {
        var bs = BattleSetup.Instance;
        if (bs == null || bs.moraleConfig == null || !bs.moraleConfig.showDebugMorale) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        EnsureStyles();

        foreach (var f in bs.formations)
        {
            if (f == null || f.soldiers.Count == 0) continue;

            string morale = f.Morale != null ? f.Morale.Value.ToString("0") : "--";
            Draw(cam, f.AnchorPos + Vector3.up * LabelHeight,
                 $"{morale}  {Formation.StateLabel(f.State)}", formationStyle);

            foreach (var s in f.soldiers)
            {
                int grade = TemporaryOfficerRanks.DebugGrade(s.role);
                if (grade == 0) continue;   // line soldiers show nothing
                Draw(cam, s.transform.position + Vector3.up * GradeHeight,
                     grade.ToString(), gradeStyle);
            }
        }
    }

    private static void Draw(Camera cam, Vector3 world, string text, GUIStyle style)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f) return;   // behind the camera
        Vector2 size = style.CalcSize(new GUIContent(text));
        var r = new Rect(sp.x - size.x * 0.5f, Screen.height - sp.y - size.y * 0.5f, size.x, size.y);
        GUI.Label(r, text, style);
    }

    // Font sizes follow screen height so labels stay readable on a phone.
    private void EnsureStyles()
    {
        if (formationStyle != null && styledForHeight == Screen.height) return;
        styledForHeight = Screen.height;
        formationStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.Max(12, Screen.height / 48),
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        formationStyle.normal.textColor = Color.white;
        gradeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(10, Screen.height / 60),
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        gradeStyle.normal.textColor = Color.yellow;
    }
}
