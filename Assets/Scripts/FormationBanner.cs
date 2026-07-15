using UnityEngine;

// One floating banner per formation, replacing the old text label: a faction-
// colored pennant with a white sword (melee) or bow-and-arrow (archer) icon.
// The banner marks where the formation *is*: it follows the anchor while the
// unit holds its slots, and the dominant living soldier cluster while engaged
// or broken (Formation.DominantGroupCenter — the same point Reform rallies
// on). Presentation only: no colliders anywhere, so it can never steal taps.
public class FormationBanner : MonoBehaviour
{
    private const float HoverHeight = 5.2f;      // world height above the troops
    private const float Smoothing = 3f;          // 1-exp(-k dt) position easing
    private const float Width = 2.6f;            // pennant width in world units

    private static readonly Color BlueBanner = new Color(0.16f, 0.38f, 0.92f);
    private static readonly Color RedBanner = new Color(0.85f, 0.2f, 0.16f);

    private static Mesh pennantMesh;
    private static Texture2D swordTex, bowTex;
    private static Material blueMat, redMat, swordIconMat, bowIconMat;

    private Formation f;
    private Transform banner;
    private Transform cam;
    private Vector3 pos;
    private bool hasPos;

    private void Start()
    {
        f = GetComponent<Formation>();
        EnsureShared();

        var root = new GameObject("Banner");
        banner = root.transform;

        var back = new GameObject("Pennant");
        back.transform.SetParent(banner, false);
        var mf = back.AddComponent<MeshFilter>();
        mf.sharedMesh = PennantMesh();
        var mr = back.AddComponent<MeshRenderer>();
        mr.sharedMaterial = f.team == Team.Blue ? blueMat : redMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(icon.GetComponent<Collider>());
        icon.name = "Icon";
        icon.transform.SetParent(banner, false);
        // pennant spans y [0, -1.4] locally; center the icon on its body
        icon.transform.localPosition = new Vector3(0f, -0.62f, -0.01f);
        icon.transform.localScale = new Vector3(Width * 0.62f, Width * 0.62f, 1f);
        var ir = icon.GetComponent<MeshRenderer>();
        ir.sharedMaterial = f.stats.isRanged ? bowIconMat : swordIconMat;
        ir.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void LateUpdate()
    {
        if (f == null || banner == null) return;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;

        // Destroyed formations have no banner — never a marker over a corpse
        // pile, never a banner trailing the surviving attacker.
        bool show = f.soldiers.Count > 0;
        if (banner.gameObject.activeSelf != show) banner.gameObject.SetActive(show);
        if (!show) return;

        // Structured states follow the anchor; combat deformation and broken
        // ranks follow the dominant living cluster (interval-updated by the
        // formation; smoothing below hides the steps).
        bool scattered = f.State == FormationState.Engaged ||
                         f.State == FormationState.BrokenRanks;
        Vector3 target = scattered && f.DominantGroupCount > 0
            ? f.DominantGroupCenter : f.AnchorPos;
        target.y = HoverHeight;

        if (!hasPos) { pos = target; hasPos = true; }
        else pos = Vector3.Lerp(pos, target, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));
        banner.position = pos;
        if (cam != null) banner.rotation = cam.rotation;   // billboard
    }

    private void OnDestroy()
    {
        if (banner != null) Destroy(banner.gameObject);
    }

    // ---------------- shared meshes / textures / materials ----------------

    // Swallow-tail pennant hanging below its pivot, facing -Z (toward an
    // isometric camera after billboarding). Two-sided via duplicated winding.
    private static Mesh PennantMesh()
    {
        if (pennantMesh != null) return pennantMesh;
        float w = Width * 0.5f, h = 1.4f, notch = 0.34f;
        var verts = new[]
        {
            new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f),
            new Vector3(w, -h, 0f), new Vector3(-w, -h, 0f),
            new Vector3(0f, -h + notch, 0f),   // swallow-tail notch
        };
        var tris = new[] { 0, 1, 2, 0, 2, 4, 0, 4, 3,      // front (toward -Z)
                           2, 1, 0, 4, 2, 0, 3, 4, 0 };    // back
        pennantMesh = new Mesh { name = "FormationPennant", vertices = verts, triangles = tris };
        pennantMesh.RecalculateNormals();
        pennantMesh.RecalculateBounds();
        return pennantMesh;
    }

    private static void EnsureShared()
    {
        if (blueMat != null) return;
        blueMat = BattleVisuals.TransparentUnlit(new Color(BlueBanner.r, BlueBanner.g, BlueBanner.b, 0.92f));
        redMat = BattleVisuals.TransparentUnlit(new Color(RedBanner.r, RedBanner.g, RedBanner.b, 0.92f));
        swordTex = DrawIcon(false);
        bowTex = DrawIcon(true);
        swordIconMat = IconMaterial(swordTex);
        bowIconMat = IconMaterial(bowTex);
    }

    private static Material IconMaterial(Texture2D tex)
    {
        var m = BattleVisuals.TransparentUnlit(Color.white);
        m.SetTexture("_BaseMap", tex);
        return m;
    }

    // White icon silhouette on transparent, drawn per pixel once and cached:
    // a gladius (blade + guard + grip + pommel) or a bow-and-arrow (arc,
    // string, arrow with head). Simple distance tests, readable at 96px.
    private static Texture2D DrawIcon(bool bow)
    {
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // normalized coords, (0,0) center, +y up
                float nx = (x - S * 0.5f) / (S * 0.5f);
                float ny = (y - S * 0.5f) / (S * 0.5f);
                bool on = bow ? BowPixel(nx, ny) : SwordPixel(nx, ny);
                tex.SetPixel(x, y, on ? Color.white : clear);
            }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    private static bool SwordPixel(float x, float y)
    {
        // blade: tapering vertical strip, tip at top
        if (y > -0.28f && y < 0.82f)
        {
            float halfW = 0.10f * Mathf.Clamp01((0.82f - y) / 0.55f + 0.25f);
            if (Mathf.Abs(x) < Mathf.Min(halfW, 0.10f)) return true;
        }
        // crossguard
        if (Mathf.Abs(y + 0.32f) < 0.05f && Mathf.Abs(x) < 0.34f) return true;
        // grip
        if (y > -0.66f && y < -0.36f && Mathf.Abs(x) < 0.06f) return true;
        // pommel
        if ((x * x + (y + 0.74f) * (y + 0.74f)) < 0.09f * 0.09f) return true;
        return false;
    }

    private static bool BowPixel(float x, float y)
    {
        // bow arc: ring segment opening to the left (string side)
        float r = Mathf.Sqrt(x * x + y * y);
        if (r > 0.62f && r < 0.76f && x > -0.15f) return true;
        // string: vertical chord joining the arc tips
        float tipX = -0.13f;
        if (Mathf.Abs(x - tipX) < 0.035f && Mathf.Abs(y) < 0.68f) return true;
        // arrow shaft: horizontal through the middle
        if (Mathf.Abs(y) < 0.045f && x > -0.55f && x < 0.55f) return true;
        // arrowhead at the right
        if (x > 0.52f && x < 0.78f && Mathf.Abs(y) < (0.78f - x) * 0.55f) return true;
        // fletching at the left
        if (x > -0.62f && x < -0.44f && Mathf.Abs(y) < (x + 0.66f) * 0.65f && Mathf.Abs(y) > 0.03f)
            return true;
        return false;
    }
}
