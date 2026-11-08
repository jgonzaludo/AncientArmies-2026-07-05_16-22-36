using System.Collections.Generic;
using UnityEngine;

// the painted 2d props (trees, rocks, bushes, dead trees) packed in one atlas. the camera never turns, so each prop
// is a flat quad standing square to it (no foreshortening) plus a sheared copy of its own silhouette on the ground
// as a cast shadow. quads are merged into a few chunk meshes: one material, a handful of draw calls.
// presentation only: no colliders, nothing in gameplay reads this.
public sealed class TerrainSprites
{
    public struct Placement { public string sprite; public Vector3 pos; public float size; public bool byWidth; }
    // a sprite laid flat on the ground over a rectangle (a bridge): it must be flat, not standing, so soldiers on it show
    public struct Decal { public string sprite; public float centerX, z0, z1, width; }

    [System.Serializable] private class Entry { public string name; public int x, y, w, h; }
    [System.Serializable] private class Data { public int width, height; public Entry[] sprites; }

    // BattleSetup puts the camera at this pitch and never rotates it
    private const float CameraPitch = 40f;
    private const float ChunkSize = 60f;

    private readonly Dictionary<string, Entry> byName = new Dictionary<string, Entry>();
    private Data data;
    private Material sprites, shadows;

    public static TerrainSprites Load()
    {
        var atlas = Resources.Load<Texture2D>("Terrain/Sprites/terrain_atlas");
        var json = Resources.Load<TextAsset>("Terrain/Sprites/terrain_atlas_data");
        var mat = Resources.Load<Material>("Terrain/Sprites/MAT_TerrainSprites");
        if (atlas == null || json == null || mat == null) return null;
        var t = new TerrainSprites { data = JsonUtility.FromJson<Data>(json.text) };
        foreach (var e in t.data.sprites) t.byName[e.name] = e;
        t.sprites = mat;
        t.shadows = new Material(mat) { name = "MAT_TerrainShadows" };
        return t;
    }

    public bool Has(string name) => byName.ContainsKey(name);

    // flat quads on the ground, one mesh. the picture is already drawn as seen from the game camera, so stretching it
    // over a ground rectangle that is 1/sin(pitch) deeper makes it read the same from that camera
    public GameObject BuildDecals(string name, Transform parent, List<Decal> list)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color32>(); var tri = new List<int>();
        foreach (var d in list)
        {
            if (!byName.TryGetValue(d.sprite, out var e)) continue;
            float u0 = (float)e.x / data.width, u1 = (float)(e.x + e.w) / data.width;
            float v0 = 1f - (float)(e.y + e.h) / data.height, v1 = 1f - (float)e.y / data.height;
            int i = v.Count; float x0 = d.centerX - d.width * 0.5f, x1 = d.centerX + d.width * 0.5f; const float y = 0.06f;
            v.Add(new Vector3(x0, y, d.z0)); v.Add(new Vector3(x1, y, d.z0)); v.Add(new Vector3(x1, y, d.z1)); v.Add(new Vector3(x0, y, d.z1));
            uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(u0, v1));
            for (int k = 0; k < 4; k++) col.Add(new Color32(255, 255, 255, 255));
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2);
        }
        if (v.Count == 0) return go;
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = sprites; mr.sortingOrder = -500;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        return go;
    }

    public string[] Names(string prefix)
    {
        var l = new List<string>();
        foreach (var k in byName.Keys) if (k.StartsWith(prefix)) l.Add(k);
        l.Sort();
        return l.ToArray();
    }

    // one chunk mesh per ~60 m square. quads sorted far to near so the soft edges blend in the right order
    public GameObject Build(string name, Transform parent, List<Placement> list, bool castShadows = true, bool fadeTrees = false)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false);
        Quaternion cam = Quaternion.Euler(CameraPitch, 0f, 0f);
        Vector3 right = cam * Vector3.right, up = cam * Vector3.up;
        var chunks = new Dictionary<Vector2Int, List<Placement>>();
        var fade = fadeTrees ? new List<FadeChunk>() : null;
        foreach (var p in list)
        {
            if (!byName.ContainsKey(p.sprite)) continue;
            var key = new Vector2Int(Mathf.FloorToInt(p.pos.x / ChunkSize), Mathf.FloorToInt(p.pos.z / ChunkSize));
            if (!chunks.TryGetValue(key, out var l)) chunks[key] = l = new List<Placement>();
            l.Add(p);
        }
        foreach (var kv in chunks)
        {
            kv.Value.Sort((a, b) => b.pos.z.CompareTo(a.pos.z));   // far (large z) first
            if (castShadows) Mesh(root.transform, name + "_shadow_" + kv.Key.x + "_" + kv.Key.y, kv.Value, right, up, true, shadows, -1000);
            var fc = Mesh(root.transform, name + "_" + kv.Key.x + "_" + kv.Key.y, kv.Value, right, up, false, sprites, -kv.Key.y, fadeTrees);
            if (fc != null) fade.Add(fc);
        }
        if (fade != null && fade.Count > 0) root.AddComponent<TreeFade>().chunks = fade;
        return root;
    }

    private FadeChunk Mesh(Transform parent, string name, List<Placement> list, Vector3 right, Vector3 up, bool shadow, Material mat, int order, bool fadeTrees = false)
    {
        var v = new List<Vector3>(list.Count * 4); var uv = new List<Vector2>(list.Count * 4); var col = new List<Color32>(list.Count * 4); var tri = new List<int>(list.Count * 6);
        Color32 white = new Color32(255, 255, 255, 255), dark = new Color32(14, 22, 8, 96);
        foreach (var p in list)
        {
            var e = byName[p.sprite];
            float aspect = (float)e.w / e.h;
            float h = p.byWidth ? p.size / aspect : p.size, w = h * aspect;
            float u0 = (float)e.x / data.width, u1 = (float)(e.x + e.w) / data.width;
            float v0 = 1f - (float)(e.y + e.h) / data.height, v1 = 1f - (float)e.y / data.height;
            int i = v.Count;
            if (!shadow)
            {
                Vector3 bl = p.pos - right * (w * 0.5f);
                v.Add(bl); v.Add(bl + right * w); v.Add(bl + right * w + up * h); v.Add(bl + up * h);
            }
            else
            {
                // the silhouette laid on the ground, leaning away from a sun that is up and to the left
                Vector3 s = new Vector3(0.85f, 0f, -0.5f).normalized * (h * 0.62f);
                Vector3 bl = p.pos + Vector3.up * 0.05f - Vector3.right * (w * 0.5f);
                v.Add(bl); v.Add(bl + Vector3.right * w); v.Add(bl + Vector3.right * w + s); v.Add(bl + s);
            }
            uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(u0, v1));
            for (int k = 0; k < 4; k++) col.Add(shadow ? dark : white);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2);
        }
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        if (!fadeTrees || shadow) return null;
        var fc = new FadeChunk { mesh = mesh, cols = col.ToArray(), bases = new Vector3[list.Count], fadable = new bool[list.Count] };
        Vector3 sum = Vector3.zero;
        for (int k = 0; k < list.Count; k++)
        {
            fc.bases[k] = list[k].pos; sum += list[k].pos;
            string n = list[k].sprite; fc.fadable[k] = n.StartsWith("broadleaf_") || n.StartsWith("pine_") || n.StartsWith("dead_");
        }
        fc.center = sum / Mathf.Max(1, list.Count);
        foreach (var b in fc.bases) fc.radius = Mathf.Max(fc.radius, Vector3.Distance(b, fc.center));
        return fc;
    }

    private sealed class FadeChunk { public Mesh mesh; public Color32[] cols; public Vector3[] bases; public bool[] fadable; public Vector3 center; public float radius; public bool faded; }

    // how many trees are currently thinned out (a test reads this)
    public static int FadedTrees;

    // trees close to one of the player's formations turn see-through, so a unit inside a forest stays visible and its
    // banner stays readable. only the vertex colour of the chunk meshes changes, 5 times a second, near units only
    private sealed class TreeFade : MonoBehaviour
    {
        public List<FadeChunk> chunks; private float next; private readonly List<Vector3> blue = new List<Vector3>(); private readonly List<float> reach = new List<float>();
        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.2f;
            var bs = BattleSetup.Instance; if (bs == null) return;
            blue.Clear(); reach.Clear();
            foreach (var f in bs.formations)
                if (f.team == Team.Blue && f.soldiers.Count > 0) { blue.Add(f.AnchorPos); reach.Add(Mathf.Max(f.FootprintHalfExtents.x, f.FootprintHalfExtents.y) + 11f); }
            int total = 0;
            foreach (var c in chunks)
            {
                bool near = false;
                for (int i = 0; i < blue.Count && !near; i++) near = Vector3.Distance(c.center, blue[i]) < c.radius + reach[i] + 4f;
                if (!near && !c.faded) continue;
                bool any = false;
                for (int k = 0; k < c.bases.Length; k++)
                {
                    if (!c.fadable[k]) continue;
                    float a = 1f;
                    for (int i = 0; i < blue.Count; i++)
                    {
                        float d = Vector2.Distance(new Vector2(c.bases[k].x, c.bases[k].z), new Vector2(blue[i].x, blue[i].z));
                        a = Mathf.Min(a, Mathf.Lerp(0.28f, 1f, Mathf.Clamp01((d - reach[i] * 0.7f) / (reach[i] * 0.5f))));
                    }
                    byte al = (byte)(a * 255f);
                    if (a < 0.99f) { total++; any = true; }
                    for (int q = 0; q < 4; q++) { var col = c.cols[k * 4 + q]; col.a = al; c.cols[k * 4 + q] = col; }
                }
                c.faded = any;
                c.mesh.SetColors(c.cols);
            }
            FadedTrees = total;
        }
    }
}
