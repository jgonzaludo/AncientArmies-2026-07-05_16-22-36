using UnityEngine;
using TMPro;

// one place for every ui number: spacing, colors, type. the draft was laid out for an 860 px tall view, so every
// draft pixel goes through P() to land on the 1080 px reference canvas.
public static class UiTheme
{
    public const float K = 1080f / 860f;
    public static float P(float draftPx) { return draftPx * K; }

    // spacing (draft px, scaled)
    public static readonly float Margin = P(24f);
    public static readonly float GapIn = P(12f);
    public static readonly float BtnH = P(52f);        // top row buttons and pills
    public static readonly float BtnHBig = P(68f);     // charge and rotate
    public const float HitH = 120f;                    // 44pt target on a phone, done with raycast padding

    // palette
    static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    public static readonly Color Ink = H("#F2E8D0");
    public static readonly Color InkMuted = H("#B9AA8A");
    public static readonly Color Gold = H("#C79A4A");
    public static readonly Color GoldHi = H("#E6C77E");
    public static readonly Color BannerBlue = H("#2C4F8A");
    public static readonly Color BannerBlueHi = H("#4A7BC4");
    public static readonly Color BannerRed = H("#9E2A22");
    public static readonly Color BannerRedHi = H("#C4463A");
    public static readonly Color PanelNavy = H("#1E1712");
    public static readonly Color TrackDark = H("#120D09");
    public static readonly Color HealthGreen = H("#5FC96E");
    public static readonly Color HealthAmber = H("#E2B040");
    public static readonly Color HealthRed = H("#C4463A");
    public static readonly Color EnemyTint = H("#E8877B");

    // fonts: marcellus (trajan-style roman capitals) for labels and body, cinzel bold for titles, unit names and numbers
    static TMP_FontAsset med, semi, bold, cinzel;
    public static TMP_FontAsset Medium { get { return med ? med : med = Resources.Load<TMP_FontAsset>("Marcellus-Regular SDF"); } }
    public static TMP_FontAsset SemiBold { get { return semi ? semi : semi = Resources.Load<TMP_FontAsset>("Marcellus-Regular SDF"); } }
    public static TMP_FontAsset Bold { get { return bold ? bold : bold = Resources.Load<TMP_FontAsset>("Cinzel-Bold SDF"); } }
    public static TMP_FontAsset Cinzel { get { return cinzel ? cinzel : cinzel = Resources.Load<TMP_FontAsset>("Cinzel-Bold SDF"); } }

    // tabular digits: wrap changing numbers so they do not jitter
    public static string Tab(object n) { return "<mspace=0.62em>" + n + "</mspace>"; }

    private static Texture2D plateTex; private static bool plateTried;

    // draws a pill (round caps, stretched middle) with the ui plate art, for the imgui debug markers
    public static void DrawPill(Rect pr, Color tint)
    {
        if (!plateTried) { plateTried = true; var sp = Resources.Load<Sprite>("UI_Plate"); plateTex = sp != null ? sp.texture : null; }
        if (plateTex == null) return;
        var old = GUI.color; GUI.color = tint;
        float cap = pr.height * 0.5f, u = 44f / plateTex.width;
        GUI.DrawTextureWithTexCoords(new Rect(pr.x + cap * 0.5f, pr.y, pr.width - cap, pr.height), plateTex, new Rect(u, 0f, 1f - u * 2f, 1f));
        GUI.DrawTextureWithTexCoords(new Rect(pr.x, pr.y, cap, pr.height), plateTex, new Rect(0f, 0f, u, 1f));
        GUI.DrawTextureWithTexCoords(new Rect(pr.xMax - cap, pr.y, cap, pr.height), plateTex, new Rect(1f - u, 0f, u, 1f));
        GUI.color = old;
    }
}
