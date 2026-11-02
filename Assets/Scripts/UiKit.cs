using TMPro;
using UnityEngine;
using UnityEngine.UI;

// small builders so the hud code reads as layout, not boilerplate
public static class UiKit
{
    public static Sprite Spr(string name) { return Resources.Load<Sprite>(name); }

    public static RectTransform Rt(GameObject g) { return g.GetComponent<RectTransform>() ?? g.AddComponent<RectTransform>(); }

    // anchors and places a rect: anchor point (ax, ay) on the parent, pivot (px, py), offset and size in canvas px
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Fill(RectTransform rt, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        return rt;
    }

    // an image; a sliced pill sprite is scaled so its round ends match the target height
    public static Image Img(Transform parent, string name, Sprite sp, Color col, float pillHeight = 0f)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var im = g.GetComponent<Image>(); im.sprite = sp; im.color = col; im.raycastTarget = false;
        if (sp != null && sp.border != Vector4.zero) { im.type = Image.Type.Sliced; if (pillHeight > 0f) im.pixelsPerUnitMultiplier = sp.rect.height / pillHeight; }
        return im;
    }

    public static TextMeshProUGUI Tmp(Transform parent, string name, string text, TMP_FontAsset font, float size, Color col,
                                      TextAlignmentOptions align = TextAlignmentOptions.Midline, float tracking = 0f)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        g.transform.SetParent(parent, false);
        var t = g.GetComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = size; t.color = col; t.alignment = align; t.text = text;
        t.characterSpacing = tracking; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
}
