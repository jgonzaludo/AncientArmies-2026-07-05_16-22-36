using UnityEngine;
using UnityEngine.Rendering;

// Shared code-built visuals for the mobile control language: the formation facing
// arrow, command drag line, ground markers, and confirmation pulses.
public static class BattleVisuals
{
    private static Mesh arrowMesh;
    private static Material arrowMat, previewArrowMat, moveLineMat, attackLineMat,
                            moveMarkerMat, enemyRingMat;

    // Flat arrow on the XZ plane, pivot at the base, pointing +Z. ~3m long.
    public static Mesh ArrowMesh
    {
        get
        {
            if (arrowMesh != null) return arrowMesh;
            arrowMesh = new Mesh { name = "FormationArrow" };
            const float shaftW = 0.38f, shaftL = 1.7f, headW = 1.05f, headL = 1.3f;
            arrowMesh.vertices = new[]
            {
                new Vector3(-shaftW, 0f, 0f),          // 0 shaft base L
                new Vector3(shaftW, 0f, 0f),           // 1 shaft base R
                new Vector3(shaftW, 0f, shaftL),       // 2 shaft top R
                new Vector3(-shaftW, 0f, shaftL),      // 3 shaft top L
                new Vector3(-headW, 0f, shaftL),       // 4 head L
                new Vector3(headW, 0f, shaftL),        // 5 head R
                new Vector3(0f, 0f, shaftL + headL),   // 6 tip
            };
            arrowMesh.triangles = new[] { 0, 3, 2, 0, 2, 1, 4, 6, 5 };
            arrowMesh.RecalculateNormals();
            arrowMesh.RecalculateBounds();
            return arrowMesh;
        }
    }

    public static Material TransparentUnlit(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetFloat("_Surface", 1f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", c);
        return m;
    }

    private static void EnsureMaterials()
    {
        if (arrowMat != null) return;
        arrowMat = TransparentUnlit(new Color(1f, 1f, 1f, 0.85f));
        previewArrowMat = TransparentUnlit(new Color(1f, 0.9f, 0.3f, 0.9f));
        moveLineMat = TransparentUnlit(new Color(1f, 1f, 1f, 0.7f));
        attackLineMat = TransparentUnlit(new Color(1f, 0.3f, 0.25f, 0.85f));
        moveMarkerMat = TransparentUnlit(new Color(1f, 1f, 1f, 0.6f));
        enemyRingMat = TransparentUnlit(new Color(1f, 0.25f, 0.2f, 0.55f));
    }

    public static Transform CreateArrow(string name, bool preview = false)
    {
        EnsureMaterials();
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = ArrowMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = preview ? previewArrowMat : arrowMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
    }

    public static LineRenderer CreateCommandLine()
    {
        EnsureMaterials();
        var go = new GameObject("CommandLine");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.widthMultiplier = 0.18f;
        lr.material = moveLineMat;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.numCapVertices = 2;
        return lr;
    }

    public static void SetLineAttackStyle(LineRenderer lr, bool attack)
    {
        EnsureMaterials();
        lr.material = attack ? attackLineMat : moveLineMat;
    }

    // Arrow color state (v1.8.2): white = settled current facing, yellow =
    // an edit in progress or a facing the formation is still turning toward.
    // Shared materials — swapping costs nothing.
    public static void SetArrowPreviewStyle(Transform arrow, bool preview)
    {
        EnsureMaterials();
        var mr = arrow.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = preview ? previewArrowMat : arrowMat;
    }

    public static GameObject CreateGroundDisc(string name, float diameter, bool enemyStyle)
    {
        EnsureMaterials();
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<Collider>());
        disc.name = name;
        disc.transform.localScale = new Vector3(diameter, 0.02f, diameter);
        var mr = disc.GetComponent<Renderer>();
        mr.sharedMaterial = enemyStyle ? enemyRingMat : moveMarkerMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        return disc;
    }

    // brief expanding confirmation pulse at a battlefield point
    public static void SpawnPulse(Vector3 pos, bool enemyStyle)
    {
        var disc = CreateGroundDisc("Pulse", 1f, enemyStyle);
        disc.transform.position = new Vector3(pos.x, 0.07f, pos.z);
        disc.AddComponent<PulseMarker>();
    }
}

public class PulseMarker : MonoBehaviour
{
    private float t;

    private void Update()
    {
        t += Time.deltaTime / 0.45f;
        if (t >= 1f) { Destroy(gameObject); return; }
        float d = Mathf.Lerp(1f, 3.4f, t);
        transform.localScale = new Vector3(d, 0.02f, d);
    }
}
