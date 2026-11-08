using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// state + strength pills under each formation and a gold ring around the selected ones, drawn on the ui canvas from
// world positions (so they scale with the rest of the hud and ship in release builds). a small declutter pass nudges a
// pill down when it would sit on another pill or on a banner. presentation only, it never writes simulation state.
public class UnitLabels : MonoBehaviour
{
    private class Entry
    {
        public RectTransform pill, ring; public Image pillBg, ringImg; public TextMeshProUGUI state, count; public Image icon, divider;
        public Vector2 size;
    }

    private RectTransform layer;
    private readonly Dictionary<Formation, Entry> map = new Dictionary<Formation, Entry>();
    private readonly List<Formation> order = new List<Formation>();
    private readonly List<Rect> blocked = new List<Rect>();
    private readonly List<Formation> dead = new List<Formation>();
    private Camera cam;

    private const float LabelGap = 1.4f;   // metres behind the block's rear edge

    public void Init(Transform canvas)
    {
        transform.SetParent(canvas, false);
        layer = gameObject.AddComponent<RectTransform>();
        UiKit.Fill(layer);
    }

    private Entry Make(Formation f)
    {
        var e = new Entry();
        e.ringImg = UiKit.Img(layer, "Ring", UiKit.Spr("UI_Ring"), Color.white);
        e.ring = e.ringImg.rectTransform;
        e.pillBg = UiKit.Img(layer, "Label", UiKit.Spr("UI_Plate"), Color.white, UiTheme.P(32f));
        e.pill = e.pillBg.rectTransform;
        e.pill.anchorMin = e.pill.anchorMax = e.pill.pivot = new Vector2(0.5f, 0.5f);
        e.state = UiKit.Tmp(e.pill, "State", "", UiTheme.Medium, UiTheme.P(20f), UiTheme.InkMuted, TextAlignmentOptions.MidlineLeft);
        e.divider = UiKit.Img(e.pill, "Divider", null, new Color(1f, 1f, 1f, 0.22f));
        e.icon = UiKit.Img(e.pill, "Soldier", UiKit.Spr("UI_Icon_Soldier"), UiTheme.InkMuted);
        e.count = UiKit.Tmp(e.pill, "Count", "", UiTheme.Bold, UiTheme.P(22f), UiTheme.Ink, TextAlignmentOptions.MidlineLeft);
        return e;
    }

    private static void At(RectTransform rt, float x, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(w, h);
    }

    private void LateUpdate()
    {
        var bs = BattleSetup.Instance;
        bool show = bs != null && bs.Phase != BattlePhase.Ended && !SimSpeed.RenderOff;
        layer.gameObject.SetActive(show);
        if (!show) return;
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        float toCanvas = 1080f / Screen.height;
        Vector2 half = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 ToCanvas(Vector3 sp) { return ((Vector2)sp - half) * toCanvas; }

        order.Clear(); blocked.Clear();
        foreach (var f in bs.formations)
        {
            if (f == null || f.soldiers.Count == 0) { if (f != null && map.TryGetValue(f, out var gone)) { gone.pill.gameObject.SetActive(false); gone.ring.gameObject.SetActive(false); } continue; }
            if (!map.ContainsKey(f)) map[f] = Make(f);
            order.Add(f);
            // the formation's own banner, so no other label sits on it
            Vector3 bp = cam.WorldToScreenPoint(f.AnchorPos + Vector3.forward * f.FootprintHalfExtents.y * 0.5f + Vector3.up * 2f);   // banners sit at the screen-top part of their block
            if (bp.z > 0f) { Vector2 c = ToCanvas(bp); float bw = UiTheme.P(50f), bh = UiTheme.P(86f); blocked.Add(new Rect(c.x - bw * 0.5f, c.y - bh * 0.35f, bw, bh)); }
        }
        // selected formations place first, the rest bottom of the screen up
        order.Sort((a, b) =>
        {
            int sa = a.IsSelected ? 0 : 1, sb = b.IsSelected ? 0 : 1;
            if (sa != sb) return sa.CompareTo(sb);
            return b.AnchorPos.z.CompareTo(a.AnchorPos.z);
        });

        var pills = new List<Rect>();
        foreach (var f in order)
        {
            var e = map[f];
            // the enemy's state is hidden: their pill shows only the head count
            bool showState = f.team == Team.Blue;
            e.state.gameObject.SetActive(showState); e.divider.gameObject.SetActive(showState);
            e.state.text = showState ? Formation.StateLabel(f.State) : "";
            e.count.text = UiTheme.Tab(f.soldiers.Count);
            float h = UiTheme.P(32f), pad = UiTheme.P(14f), gap = UiTheme.P(8f), ic = UiTheme.P(15f);
            float sw = showState ? e.state.preferredWidth : 0f, cw = e.count.preferredWidth;
            float w = pad + (showState ? sw + gap + 1.5f + gap : 0f) + ic + UiTheme.P(4f) + cw + pad;
            e.size = new Vector2(w, h);
            e.pill.sizeDelta = e.size;
            float x = pad;
            if (showState)
            {
                At(e.state.rectTransform, x, sw + 2f, h); x += sw + gap;
                At(e.divider.rectTransform, x, 1.5f, h * 0.5f); x += 1.5f + gap;
            }
            At(e.icon.rectTransform, x, ic, ic); x += ic + UiTheme.P(4f);
            At(e.count.rectTransform, x, cw + 2f, h);

            Vector3 sp = cam.WorldToScreenPoint(f.AnchorPos + Vector3.back * (f.FootprintHalfExtents.y + LabelGap));
            if (sp.z < 0f) { e.pill.gameObject.SetActive(false); }
            else
            {
                Vector2 c0 = ToCanvas(sp); c0.y -= h * 0.15f;   // the pill straddles the block's rear edge, so it takes little room below
                Vector2 c = c0; bool free = false;
                for (int i = 0; i < 4 && !free; i++)
                {
                    var r = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
                    bool hit = false;
                    foreach (var o in pills) if (o.Overlaps(r)) { hit = true; c.y = Mathf.Min(c.y, o.yMin - h * 0.5f - 3f); }
                    foreach (var o in blocked) if (o.Overlaps(r) && i < 3) { hit = true; c.y = Mathf.Min(c.y, o.yMin - h * 0.5f - 3f); }
                    if (!hit) free = true;
                }
                if (!free || c0.y - c.y > h * 1.5f) c = c0;   // a nudge never drags a label far from its own formation   // no clean spot within a few nudges: stay put rather than cascade down the screen
                var rect = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
                e.pill.gameObject.SetActive(true);   // labels never hide, the best spot found wins and the selected one is placed first
                e.pill.anchoredPosition = c; pills.Add(rect);
            }

            // selection ring around the formation's footprint
            bool sel = false;   // the gold selection ring is switched off, the banner alone marks the selection
            e.ring.gameObject.SetActive(sel);
            if (sel)
            {
                Vector3 r3 = f.AnchorRot * Vector3.right * f.FootprintHalfExtents.x, f3 = f.AnchorRot * Vector3.forward * f.FootprintHalfExtents.y;
                Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 w3 = f.AnchorPos + ((k & 1) == 0 ? r3 : -r3) + ((k & 2) == 0 ? f3 : -f3);
                    Vector3 p = cam.WorldToScreenPoint(w3); if (p.z < 0f) continue;
                    Vector2 q = ToCanvas(p); mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q);
                }
                float padR = UiTheme.P(14f);
                Vector2 size = new Vector2(Mathf.Max(100f, mx.x - mn.x + padR * 2f), Mathf.Max(100f, mx.y - mn.y + padR * 2f));
                e.ring.anchorMin = e.ring.anchorMax = e.ring.pivot = new Vector2(0.5f, 0.5f);
                e.ring.anchoredPosition = (mn + mx) * 0.5f; e.ring.sizeDelta = size;
            }
        }
    }
}
