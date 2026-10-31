using UnityEngine;
using UnityEngine.SceneManagement;

// Small state + morale label under each formation (below the block, clear of
// the banner). Morale is tinted green / amber / red. Toggled live by
// MoraleConfig.showDebugMorale. Officer grades live in OfficerGradeMarkers.
//
// Self-installing on every scene load, so nothing else references it: delete
// this file and the overlay is gone. Reads simulation state, never writes it.
public class MoraleDebugOverlay : MonoBehaviour
{
    private const float LabelGap = 1.5f;        // metres behind the block's rear edge

    private GUIStyle formationStyle;
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
        if (SimSpeed.RenderOff || (!Debug.isDebugBuild && !Application.isEditor) || (BattleSetup.Instance != null && BattleSetup.Instance.Phase == BattlePhase.Ended)) return;   // debug readouts never ship in a release build   // scripted headless runs disable cameras; drawing GUI then errors
        var bs = BattleSetup.Instance;
        if (bs == null || bs.moraleConfig == null || !bs.moraleConfig.showDebugMorale) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        EnsureStyles();

        foreach (var f in bs.formations)
        {
            if (f == null || f.soldiers.Count == 0) continue;

            float m = f.Morale != null ? f.Morale.Value : 100f;
            string morale = f.Morale != null ? m.ToString("0") : "--";
            formationStyle.normal.textColor = m >= 60f ? new Color(0.65f, 1f, 0.65f)
                                            : m >= 30f ? new Color(1f, 0.85f, 0.35f)
                                            : new Color(1f, 0.45f, 0.4f);
            // Vector3.back is screen-down for this camera, so the label sits under
            // the block whichever way the formation faces
            Draw(cam, f.AnchorPos + Vector3.back * (f.FootprintHalfExtents.y + LabelGap),
                 $"{Formation.StateLabel(f.State)}  {morale}", formationStyle);
        }
    }

    private static Texture2D plate; private static bool plateTried;

    private static void Draw(Camera cam, Vector3 world, string text, GUIStyle style)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f) return;   // behind the camera
        Vector2 size = style.CalcSize(new GUIContent(text));
        var r = new Rect(sp.x - size.x * 0.5f, Screen.height - sp.y - size.y * 0.5f, size.x, size.y);
        if (plate == null && !plateTried) { plateTried = true; var sprite = Resources.Load<Sprite>("UI_Frame_Plate"); plate = sprite != null ? sprite.texture : null; }
        if (plate != null)
        {
            // the gold-bevel plate behind the label: caps are half the plate's height, the middle stretches
            var pr = new Rect(r.x - 18f, r.y - 6f, r.width + 36f, r.height + 12f);
            GUI.DrawTexture(pr, plate, ScaleMode.StretchToFill, true, 0f, Color.white, new Vector4(pr.height * 0.5f, pr.height * 0.3f, pr.height * 0.5f, pr.height * 0.3f), Vector4.zero);
        }
        GUI.Label(r, text, style);
    }

    // Font sizes follow screen height so labels stay readable on a phone.
    private void EnsureStyles()
    {
        if (formationStyle != null && styledForHeight == Screen.height) return;
        styledForHeight = Screen.height;
        formationStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(11, Screen.height / 62),
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        formationStyle.normal.textColor = Color.white;
    }
}
