using UnityEngine;

// Battlefield ground dressing: a single flat sand colour on the
// runtime-created ground plane. No texture, no tiling, no repeating-pattern
// artefacts at max zoom-out — the field reads as clean neutral ground so
// formations and orders are the only things competing for attention.
public static class BattlefieldDecor
{
    // Warm mid sand. Matte, so it never glares under the directional light.
    private static readonly Color Sand = new Color(0.78f, 0.71f, 0.55f);

    public static void Decorate(GameObject ground)
    {
        var r = ground.GetComponent<Renderer>();
        if (r == null) return;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = "MAT_Battlefield_Sand";
        mat.SetColor("_BaseColor", Sand);
        mat.SetFloat("_Smoothness", 0.1f);   // matte, no specular glare
        r.sharedMaterial = mat;
    }
}
