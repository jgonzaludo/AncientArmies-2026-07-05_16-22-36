using UnityEngine;

// Battlefield ground dressing: a single near-uniform warm sand albedo
// (TEX_Battlefield_Sand, loaded from Resources) tiled across the
// runtime-created ground plane under one flat-lit material. Sand replaced
// the grass pass because the field is now visible edge-to-edge at max
// zoom-out and the grass tile's contrast read as an obvious repeating
// pattern; the sand is deliberately low-contrast so large tiles disappear
// at distance while the grain still reads up close. No extra geometry,
// no per-frame work.
public static class BattlefieldDecor
{
    public static void Decorate(GameObject ground)
    {
        var r = ground.GetComponent<Renderer>();
        if (r == null) return;

        var tex = Resources.Load<Texture2D>("TEX_Battlefield_Sand");
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = "MAT_Battlefield_Sand";
        mat.SetTexture("_BaseMap", tex != null ? (Texture)tex : FallbackTexture());
        // ~15 m per tile on the 410 x 340 field: few repeats at full zoom-out,
        // grain still visible at command zoom
        mat.SetTextureScale("_BaseMap", new Vector2(27f, 22f));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.1f);   // matte, no specular glare
        r.sharedMaterial = mat;
    }

    // Bright low-contrast stand-in if the texture asset is ever missing —
    // keeps the field readable instead of falling back to dark magenta/grey.
    private static Texture2D FallbackTexture()
    {
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGB24, true);
        var a = new Color(0.76f, 0.68f, 0.52f);
        var b = new Color(0.80f, 0.73f, 0.57f);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                px[y * S + x] = Color.Lerp(a, b, Mathf.PerlinNoise(x * 0.06f, y * 0.06f));
        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply(true);
        return tex;
    }
}
