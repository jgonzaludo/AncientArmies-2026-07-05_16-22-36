using UnityEngine;

// Battlefield ground dressing: a single light warm yellow-green seamless
// grass albedo (TEX_Battlefield_Grass_Light, loaded from Resources) tiled
// across the runtime-created ground plane under one flat-lit material.
// Replaces the earlier dark mottle + 3D tuft pass, which read muddy and
// speckled at gameplay zoom. No extra geometry, no per-frame work.
public static class BattlefieldDecor
{
    public static void Decorate(GameObject ground)
    {
        var r = ground.GetComponent<Renderer>();
        if (r == null) return;

        var tex = Resources.Load<Texture2D>("TEX_Battlefield_Grass_Light");
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = "MAT_Battlefield_Grass_Light";
        mat.SetTexture("_BaseMap", tex != null ? (Texture)tex : FallbackTexture());
        // ~5.5 m per tile on the 120 x 80 plane: fine detail without visible repetition
        mat.SetTextureScale("_BaseMap", new Vector2(22f, 15f));
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
        var a = new Color(0.56f, 0.63f, 0.37f);
        var b = new Color(0.65f, 0.70f, 0.43f);
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
