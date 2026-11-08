using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// the battlefield mood: a warm, slightly muted colour grade, soft vignette and bloom, a tilt-shift blur on the top and
// bottom edges of the screen, and a warm haze layer that drifts over the field. presentation only: built at runtime on the main camera,
// so the scene and the pipeline assets stay untouched, and it applies to everything in the world, troops included.
// the hud is a screen-space overlay canvas, so it is never graded or hazed.
public class MoodPresentation : MonoBehaviour
{
    public static bool Enabled = true;
    public static bool EdgeBlur = true;      // the tilt-shift pass, the most expensive part on a phone: switch off here if it costs too much

    private TiltShiftPass tilt;

    private Camera cam;
    private Transform haze;
    private Material hazeMat;
    private Volume volume;

    public static void Ensure()
    {
        if (!Enabled) return;
        var cam = Camera.main;
        if (cam == null || cam.GetComponent<MoodPresentation>() != null) return;
        cam.gameObject.AddComponent<MoodPresentation>();
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        BuildVolume();
        BuildHaze();
        var sh = Resources.Load<Shader>("Mood/TiltShift");
        if (EdgeBlur && sh != null) tilt = new TiltShiftPass(new Material(sh) { name = "MAT_TiltShift" });
        RenderPipelineManager.beginCameraRendering += Queue;
        // soft warm ambient so the shaded side of soldiers and props is brown-green, not grey
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.46f, 0.43f, 0.36f);
    }

    private void BuildVolume()
    {
        var go = new GameObject("MoodVolume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 10f;
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        var ca = p.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.24f); ca.contrast.Override(20f); ca.saturation.Override(-20f);
        ca.colorFilter.Override(new Color(1f, 0.985f, 0.94f));
        var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(3f); wb.tint.Override(-2f);
        var smh = p.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.94f, 0.98f, 0.98f, -0.06f));
        smh.highlights.Override(new Vector4(1.10f, 1.02f, 0.84f, 0.05f));
        var vg = p.Add<Vignette>(true); vg.intensity.Override(0.24f); vg.smoothness.Override(0.55f); vg.color.Override(new Color(0.12f, 0.08f, 0.04f));
        var bl = p.Add<Bloom>(true); bl.threshold.Override(0.85f); bl.intensity.Override(0.35f); bl.scatter.Override(0.6f);
        bl.tint.Override(new Color(1f, 0.85f, 0.65f)); bl.maxIterations.Override(4);
        // the fitted colour match: a 32^3 lookup (ArtSource/Terrain/mood/fit_lut.py) that gives the frame the mockup's
        // exact colour distribution, applied as the last grading step
        var lutBytes = Resources.Load<TextAsset>("Mood/MoodLut");
        if (lutBytes != null)
        {
            // raw rgb bytes into a linear texture: a decoded png would be read as gamma and skew the colours
            var lut = new Texture2D(1024, 32, TextureFormat.RGB24, false, true) { name = "MoodLut", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            if (lutBytes.bytes.Length == 1024 * 32 * 3)
            {
                lut.LoadRawTextureData(lutBytes.bytes); lut.Apply(false, true);
                var cl = p.Add<ColorLookup>(true);
                cl.texture.Override(lut); cl.contribution.Override(1f);
            }
        }
        volume.sharedProfile = p;
    }

    private void OnDestroy() { RenderPipelineManager.beginCameraRendering -= Queue; }

    // the tilt-shift pass is queued onto this camera's renderer every frame it draws (also for scenario captures)
    private void Queue(ScriptableRenderContext ctx, Camera c)
    {
        if (c != cam || tilt == null) return;
        c.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(tilt);
    }

    private float DistanceToGround(float viewportY)
    {
        var ray = cam.ViewportPointToRay(new Vector3(0.5f, viewportY, 0f));
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float d)) return d;
        return 100f;
    }

    // one camera-facing quad just past the near plane: warm haze strongest at the top right, drifting slowly
    private void BuildHaze()
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MoodHaze" };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (n - 1f), v = y / (n - 1f);
                float corner = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(1.05f, 1.1f)) / 0.95f);
                float top = Mathf.Clamp01((v - 0.55f) / 0.45f);
                float n1 = Mathf.PerlinNoise(u * 3.1f, v * 2.3f), n2 = Mathf.PerlinNoise(u * 7.3f + 4f, v * 5.1f + 2f);
                // god rays: soft shafts slanting down-left from the bright corner
                float along = (u - 1.05f) * 0.8f + (v - 1.1f) * 0.6f, across = (u - 1.05f) * 0.6f - (v - 1.1f) * 0.8f;
                float shafts = Mathf.Pow(Mathf.Clamp01(Mathf.PerlinNoise(across * 9f, 0.5f) * 1.3f - 0.35f), 2f) * Mathf.Clamp01(1f + along * 0.9f);
                float a = corner * corner * 0.45f + top * top * 0.03f + shafts * 0.22f;
                a *= 0.75f + 0.35f * n1 + 0.15f * n2;
                px[y * n + x] = new Color32(255, 226, 178, (byte)Mathf.Clamp(a * 255f, 0, 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "MoodHaze";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        hazeMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        hazeMat.renderQueue = 3500;
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = hazeMat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.sortingOrder = 5000;
        haze = q.transform;
        PlaceHaze();
    }

    private void PlaceHaze()
    {
        if (haze == null) return;
        float d = cam.nearClipPlane + 0.5f;
        float h = cam.orthographic ? cam.orthographicSize * 2f : 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        haze.localPosition = new Vector3(0f, 0f, d);
        haze.localRotation = Quaternion.identity;
        haze.localScale = new Vector3(h * cam.aspect * 1.02f, h * 1.02f, 1f);
    }

    private void LateUpdate()
    {
        PlaceHaze();
        if (hazeMat != null) hazeMat.color = new Color(1f, 1f, 1f, 0.85f + 0.15f * Mathf.Sin(Time.time * 0.15f));
    }
}
