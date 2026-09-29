using UnityEngine;
using UnityEngine.SceneManagement;

// Small grade number above each officer of the SELECTED or INSPECTED
// formation only, so the battlefield stays clean (Chunk D). Presentation
// only: reads OfficerRanks and selection state, never writes anything.
// Self-installing on scene load; nothing else references it.
public class OfficerGradeMarkers : MonoBehaviour
{
    private const float MarkerHeight = 2.4f;   // above a soldier's head

    private PlayerCommander commander;
    private GUIStyle style;
    private int styledForHeight;
    private Texture2D pillTex;   // dark pill behind each number so it reads on any ground

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    private static void Attach()
    {
        var bs = BattleSetup.Instance;
        if (bs != null && bs.GetComponent<OfficerGradeMarkers>() == null)
            bs.gameObject.AddComponent<OfficerGradeMarkers>();
    }

    private void OnGUI()
    {
        var bs = BattleSetup.Instance;
        if (bs == null || bs.officerRanks == null || bs.Phase == BattlePhase.Ended) return;
        if (commander == null) { commander = bs.GetComponent<PlayerCommander>(); if (commander == null) return; }
        Camera cam = Camera.main;
        if (cam == null) return;
        EnsureStyle();

        if (bs.moraleConfig != null && bs.moraleConfig.forceGradeMarkers)
        {
            foreach (var f in bs.formations) DrawFormation(cam, bs.officerRanks, f);
            return;
        }
        foreach (var f in commander.Selection) DrawFormation(cam, bs.officerRanks, f);
        if (commander.InspectedEnemy != null) DrawFormation(cam, bs.officerRanks, commander.InspectedEnemy);
    }

    private void DrawFormation(Camera cam, OfficerRanks ranks, Formation f)
    {
        if (f == null || f.soldiers.Count == 0) return;
        foreach (var s in f.soldiers)
        {
            int grade = ranks.Grade(s.role);
            if (grade <= 0) continue;
            Vector3 sp = cam.WorldToScreenPoint(s.transform.position + Vector3.up * MarkerHeight);
            if (sp.z < 0f) return;
            string text = grade.ToString();
            Vector2 size = style.CalcSize(new GUIContent(text));
            float w = Mathf.Max(size.x, size.y);   // square pill, never narrower than tall
            var r = new Rect(sp.x - w * 0.5f, Screen.height - sp.y - size.y * 0.5f, w, size.y);
            GUI.Label(r, text, style);
        }
    }

    private void EnsureStyle()
    {
        if (style != null && styledForHeight == Screen.height) return;
        styledForHeight = Screen.height;
        if (pillTex == null)
        {
            pillTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pillTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            pillTex.Apply();
        }
        int fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.03f));   // ~3 % of screen height
        int pad = Mathf.Max(3, fontSize / 5);
        style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(pad, pad, pad / 2, pad / 2)
        };
        style.normal.background = pillTex;
        style.normal.textColor = Color.white;
    }
}
