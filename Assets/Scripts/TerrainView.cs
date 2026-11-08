using System.Collections.Generic;
using UnityEngine;

// draws a TerrainMap: a painted ground texture, kaykit trees/rocks/bushes, a bridge and ford stones,
// soft blob shadows and a warm sun. presentation only: no colliders, gameplay never reads this.
public static class TerrainView
{
    private const int Ppc = 5;   // texture pixels per map cell

    private static Color H(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f, 1f);
    private static readonly Color GrassA = new Color(0.46f, 0.60f, 0.20f), GrassB = new Color(0.58f, 0.68f, 0.24f), GrassDry = new Color(0.72f, 0.66f, 0.26f);
    private static readonly Color RoadC = new Color(0.72f, 0.60f, 0.41f), MudC = new Color(0.45f, 0.34f, 0.23f), FordC = new Color(0.58f, 0.76f, 0.77f);
    private static readonly Color DeepC = new Color(0.25f, 0.50f, 0.64f), RockC = new Color(0.60f, 0.58f, 0.52f), ForestC = new Color(0.44f, 0.54f, 0.32f), SandC = new Color(0.79f, 0.70f, 0.49f);

    // props in a deployment zone would sit under the armies, so open ground there stays clear
    // painted light on the ground: cloud shade multiplier, sunlit multiplier, warm add
    public static Vector4 MoodLight = new Vector4(0.6f, 1.24f, 0.05f, 0f);

    private static bool InDeployment(float x, float z) => Mathf.Abs(z) >= 42f && Mathf.Abs(x) <= 138f;

    public static void Build(TerrainMap map, GameObject ground)
    {
        var r = ground.GetComponent<Renderer>();
        if (r != null)
        {
            var splat = Resources.Load<Material>("Terrain/Ground/MAT_TerrainGround");
            Material mat;
            if (splat != null)
            {
                // painted tile textures blended in the shader by weight maps baked from the map
                mat = new Material(splat) { name = "MAT_Battlefield_Terrain" };
                Splat(map, out Texture2D c1, out Texture2D c2);
                mat.SetTexture("_Control", c1); mat.SetTexture("_Control2", c2);
                mat.SetVector("_Field", new Vector4(map.halfX, map.halfZ, 16f, 23f));   // tile repeats in metres: small enough that blades and tufts read up close
                mat.SetVector("_Light", MoodLight);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MAT_Battlefield_Terrain" };
                mat.SetTexture("_BaseMap", GroundTexture(map));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0f); mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_SpecularHighlights", 0f); mat.SetFloat("_EnvironmentReflections", 0f);
            }
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        TerrainGridPlanner.Warm(map);   // the planner's cell data, built now rather than on the first order
        var root = new GameObject("TerrainProps");
        FarHidden.Clear();
        Scatter(map, root.transform);
        BuildCrossings(map, root.transform);
        root.AddComponent<ZoomLod>().hide = FarHidden.ToArray();
        TuneLight();
        MoodPresentation.Ensure();   // grade, haze and depth blur on the main camera
    }

    // ---------------- ground ----------------

    private static Color CellColor(TerrainType t)
    {
        switch (t)
        {
            case TerrainType.Road: return RoadC;
            case TerrainType.Mud: return MudC;
            case TerrainType.Ford: return FordC;
            case TerrainType.Deep: case TerrainType.Bridge: return DeepC;
            case TerrainType.Rock: return RockC;
            case TerrainType.Forest: return ForestC;
            default: return GrassA;
        }
    }

    private static bool IsWater(TerrainType t) => t == TerrainType.Deep || t == TerrainType.Ford || t == TerrainType.Bridge;

    private static readonly Dictionary<TerrainMap, Texture2D> groundCache = new Dictionary<TerrainMap, Texture2D>();

    // a real smoothstep: 0 below e0, 1 above e1, eased in between (unity's Mathf.SmoothStep is a different thing)
    private static float Sstep(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

    private static float Warp(float x, float z, float f, float off) => Mathf.PerlinNoise(x * f + off, z * f + off * 0.37f) * 2f - 1f;

    // land layers that get painted; water is separate. cells that are water show grass underneath, so banks are grassy
    private const int Layers = 5;   // grass, road, mud, rock, forest
    private static int LayerOf(TerrainType t)
    {
        switch (t) { case TerrainType.Road: return 1; case TerrainType.Mud: return 2; case TerrainType.Rock: return 3; case TerrainType.Forest: return 4; default: return 0; }
    }

    // three passes of a 3x3 box blur, close to a gaussian a couple of cells wide: this is what makes zone edges soft and round
    private static float[] Blur(float[] src, int w, int h)
    {
        var a = (float[])src.Clone(); var b = new float[a.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float sum = 0f; int n = 0;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = Mathf.Clamp(x + dx, 0, w - 1), ny = Mathf.Clamp(y + dy, 0, h - 1);
                    sum += a[ny * w + nx]; n++;
                }
                b[y * w + x] = sum / n;
            }
            var t = a; a = b; b = t;
        }
        return a;
    }

    private static float Bilinear(float[] f, int w, int h, float cx, float cz)
    {
        cx = Mathf.Clamp(cx, 0f, w - 1.001f); cz = Mathf.Clamp(cz, 0f, h - 1.001f);
        int x0 = (int)cx, z0 = (int)cz; float fx = cx - x0, fz = cz - z0;
        return Mathf.Lerp(Mathf.Lerp(f[z0 * w + x0], f[z0 * w + x0 + 1], fx), Mathf.Lerp(f[(z0 + 1) * w + x0], f[(z0 + 1) * w + x0 + 1], fx), fz);
    }

    // the colour of a land layer at a world point: patchy two-tone grass, rutted road, blotchy mud, mottled forest floor
    private static Color LayerColor(int layer, float wx, float wz)
    {
        float mott = Mathf.PerlinNoise(wx * 0.11f + 70f, wz * 0.11f + 20f);
        switch (layer)
        {
            case 1: return Color.Lerp(RoadC * 0.93f, RoadC * 1.06f, mott);
            case 2: return Color.Lerp(MudC * 0.85f, MudC * 1.15f, mott);
            case 3: return Color.Lerp(RockC * 0.9f, RockC * 1.05f, mott);
            case 4:   // canopy shade: big soft clumps so a forest reads as one dark mass even from far away
                return Color.Lerp(ForestC * 0.72f, ForestC * 1.08f, Sstep(0.3f, 0.7f, Mathf.PerlinNoise(wx * 0.09f + 5f, wz * 0.09f + 31f))) * (0.92f + 0.16f * mott);
            default:
                float n = Mathf.PerlinNoise(wx * 0.05f + 11f, wz * 0.05f + 5f);
                Color c = Color.Lerp(GrassA, GrassB, Sstep(0.35f, 0.65f, n));
                return Color.Lerp(c, GrassDry, Sstep(0.62f, 0.85f, Mathf.PerlinNoise(wx * 0.02f + 40f, wz * 0.02f + 9f)) * 0.7f);
        }
    }

    private static readonly Dictionary<TerrainMap, Texture2D[]> splatCache = new Dictionary<TerrainMap, Texture2D[]>();

    // bakes the two weight maps the ground shader mixes its painted textures by: soft round-edged zones from blurred,
    // warped cell weights. rgba1 = grass road mud forest, rgba2 = rock, water mask, ford share
    private static void Splat(TerrainMap m, out Texture2D c1, out Texture2D c2)
    {
        if (splatCache.TryGetValue(m, out var hit) && hit[0] != null) { c1 = hit[0]; c2 = hit[1]; return; }
        int cw = m.Width, ch = m.Height;
        var layer = new float[Layers][]; for (int l = 0; l < Layers; l++) layer[l] = new float[cw * ch];
        var water = new float[cw * ch]; var ford = new float[cw * ch];
        for (int z = 0; z < ch; z++) for (int x = 0; x < cw; x++)
        {
            var t = m.AtCell(x, z); int i = z * cw + x;
            layer[IsWater(t) ? 0 : LayerOf(t)][i] = 1f;
            if (IsWater(t)) water[i] = 1f;
            if (t == TerrainType.Ford) ford[i] = 1f;
        }
        for (int l = 0; l < Layers; l++) layer[l] = Blur(layer[l], cw, ch);
        water = Blur(water, cw, ch); ford = Blur(ford, cw, ch);
        int w = cw * Ppc, h = ch * Ppc; float cs = m.CellSizeX;
        var a = new Color32[w * h]; var b = new Color32[w * h]; var wgt = new float[Layers];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float wx = (x + 0.5f) / Ppc * cs - m.halfX, wz = (y + 0.5f) / Ppc * cs - m.halfZ;
                float sx = wx + Warp(wx, wz, 0.045f, 0f) * 3.4f + Warp(wx, wz, 0.22f, 50f) * 0.9f;
                float sz = wz + Warp(wx, wz, 0.045f, 100f) * 3.4f + Warp(wx, wz, 0.22f, 150f) * 0.9f;
                float scx = (sx + m.halfX) / cs - 0.5f, scz = (sz + m.halfZ) / cs - 0.5f;
                for (int l = 0; l < Layers; l++) wgt[l] = Sstep(0.22f, 0.78f, Bilinear(layer[l], cw, ch, scx, scz));
                float qx = wx + Warp(wx, wz, 0.2f, 200f) * 0.5f, qz = wz + Warp(wx, wz, 0.2f, 250f) * 0.5f;
                float qcx = (qx + m.halfX) / cs - 0.5f, qcz = (qz + m.halfZ) / cs - 0.5f;
                float wm = Bilinear(water, cw, ch, qcx, qcz);
                float fordFrac = wm > 0.001f ? Mathf.Clamp01(Bilinear(ford, cw, ch, qcx, qcz) / wm) : 0f;
                int o = y * w + x;   // texture space matches world space: u runs +x, v runs +z
                a[o] = new Color32((byte)(wgt[0] * 255f), (byte)(wgt[1] * 255f), (byte)(wgt[2] * 255f), (byte)(wgt[4] * 255f));
                b[o] = new Color32((byte)(wgt[3] * 255f), (byte)(Mathf.Clamp01(wm) * 255f), (byte)(fordFrac * 255f), 0);
            }
        c1 = Make(a, w, h, "TEX_Terrain_Control1"); c2 = Make(b, w, h, "TEX_Terrain_Control2");
        splatCache[m] = new[] { c1, c2 };
    }

    private static Texture2D Make(Color32[] px, int w, int h, string name)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        t.SetPixels32(px); t.Apply(false, true); return t;
    }

    private static Texture2D GroundTexture(TerrainMap m)
    {
        if (groundCache.TryGetValue(m, out var cached) && cached != null) return cached;
        int cw = m.Width, ch = m.Height;
        // one weight map per land layer, plus water and ford, all blurred at cell resolution
        var layer = new float[Layers][]; for (int l = 0; l < Layers; l++) layer[l] = new float[cw * ch];
        var water = new float[cw * ch]; var ford = new float[cw * ch];
        for (int z = 0; z < ch; z++) for (int x = 0; x < cw; x++)
        {
            var t = m.AtCell(x, z); int i = z * cw + x;
            layer[IsWater(t) ? 0 : LayerOf(t)][i] = 1f;
            if (IsWater(t)) water[i] = 1f;
            if (t == TerrainType.Ford) ford[i] = 1f;
        }
        for (int l = 0; l < Layers; l++) layer[l] = Blur(layer[l], cw, ch);
        water = Blur(water, cw, ch); ford = Blur(ford, cw, ch);

        int w = cw * Ppc, h = ch * Ppc;
        var px = new Color32[w * h];
        float cs = m.CellSizeX;
        var wgt = new float[Layers];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float wx = (x + 0.5f) / Ppc * cs - m.halfX, wz = (y + 0.5f) / Ppc * cs - m.halfZ;
                // domain warp: a big slow wobble plus a small quick one, so soft edges wander like real ground
                float sx = wx + Warp(wx, wz, 0.045f, 0f) * 3.4f + Warp(wx, wz, 0.22f, 50f) * 0.9f;
                float sz = wz + Warp(wx, wz, 0.045f, 100f) * 3.4f + Warp(wx, wz, 0.22f, 150f) * 0.9f;
                float scx = (sx + m.halfX) / cs - 0.5f, scz = (sz + m.halfZ) / cs - 0.5f;
                float tot = 0f;
                for (int l = 0; l < Layers; l++) { wgt[l] = Sstep(0.22f, 0.78f, Bilinear(layer[l], cw, ch, scx, scz)); tot += wgt[l]; }
                Color c = Color.black;
                if (tot < 0.001f) c = LayerColor(0, wx, wz);
                else for (int l = 0; l < Layers; l++) if (wgt[l] > 0f) c += LayerColor(l, wx, wz) * (wgt[l] / tot);
                // water: its own gentle wobble so the banks curve smoothly but still match where units can walk
                float qx = wx + Warp(wx, wz, 0.2f, 200f) * 0.5f, qz = wz + Warp(wx, wz, 0.2f, 250f) * 0.5f;
                float qcx = (qx + m.halfX) / cs - 0.5f, qcz = (qz + m.halfZ) / cs - 0.5f;
                float wm = Bilinear(water, cw, ch, qcx, qcz);
                float inWater = Sstep(0.4f, 0.6f, wm);
                if (inWater > 0f)
                {
                    float fordFrac = wm > 0.001f ? Mathf.Clamp01(Bilinear(ford, cw, ch, qcx, qcz) / wm) : 0f;
                    Color wc = Color.Lerp(DeepC, FordC, Sstep(0.3f, 0.7f, fordFrac));
                    float shallow = 1f - Sstep(0.6f, 0.98f, wm);   // lighter near the edge
                    wc = Color.Lerp(wc, FordC, 0.5f * shallow);
                    c = Color.Lerp(c, wc, inWater);
                }
                if (wm > 0.1f && wm < 0.55f)   // wet sand rim
                    c = Color.Lerp(c, SandC, 0.65f * Mathf.Clamp01(1f - Mathf.Abs(wm - 0.28f) / 0.3f) * (1f - inWater));
                float grain = 1f + 0.035f * (Mathf.PerlinNoise(wx * 0.12f, wz * 0.12f) - 0.5f) * 2f;
                px[(h - 1 - y) * w + (w - 1 - x)] = (Color32)(c * grain);   // the unity plane's uv runs toward -x and -z, so write rotated 180
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "TEX_Terrain_Ground", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
        tex.SetPixels32(px); tex.Apply(true, true);
        groundCache[m] = tex;
        return tex;
    }

    // ---------------- props ----------------

    private static float Rf(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    // painted 2d props from the sprite atlas, placed from the map. none of this affects gameplay
    private static void Scatter(TerrainMap m, Transform root)
    {
        var spr = TerrainSprites.Load();
        if (spr == null) return;   // the atlas is not imported yet: the map still plays, just bare
        var rng = new System.Random(20261107);
        string[] broad = spr.Names("broadleaf_"), pine = spr.Names("pine_"), bush = spr.Names("bush_"), rock = spr.Names("rock_"), dead = spr.Names("dead_");
        var main = new List<TerrainSprites.Placement>(); var lone = new List<TerrainSprites.Placement>();
        System.Action<List<TerrainSprites.Placement>, string[], Vector3, float, bool> add = (list, names, pos, size, byWidth) =>
        {
            if (names.Length > 0) list.Add(new TerrainSprites.Placement { sprite = names[rng.Next(names.Length)], pos = pos, size = size, byWidth = byWidth });
        };
        // bank woods use their own random stream, so adding them never moves any other prop
        var bank = new System.Random(4242);
        System.Action<System.Random, Vector3> BankWoods = (r, pos) =>
        {
            double q = r.NextDouble();
            if (q < 0.15)
            {
                bool pineTree = r.NextDouble() >= 0.7; var names = pineTree ? pine : broad;
                if (names.Length > 0) main.Add(new TerrainSprites.Placement { sprite = names[r.Next(names.Length)], pos = pos, size = pineTree ? 7f + (float)r.NextDouble() * 2f : 6f + (float)r.NextDouble() * 2f, byWidth = false });
            }
            else if (q < 0.28 && bush.Length > 0) main.Add(new TerrainSprites.Placement { sprite = bush[r.Next(bush.Length)], pos = pos, size = 2.2f + (float)r.NextDouble(), byWidth = false });
        };
        for (int z = 0; z < m.Height; z++)
            for (int x = 0; x < m.Width; x++)
            {
                var t = m.AtCell(x, z);
                Vector3 c = m.CellCenter(x, z);
                c.x += Rf(rng, -1f, 1f); c.z += Rf(rng, -1f, 1f);
                switch (t)
                {
                    case TerrainType.Forest:
                        if (rng.NextDouble() < 0.075) { if (rng.NextDouble() < 0.68) add(main, broad, c, Rf(rng, 6.2f, 8.2f), false); else add(main, pine, c, Rf(rng, 7.5f, 9.5f), false); }
                        else if (rng.NextDouble() < 0.04) add(main, bush, c, Rf(rng, 2.2f, 3.2f), false);
                        break;
                    case TerrainType.Rock:
                        if (rng.NextDouble() < 0.10) add(main, rock, c, Rf(rng, 3.4f, 5.8f), true);
                        break;
                    case TerrainType.Mud:
                    {
                        double dq = rng.NextDouble();   // drawn first either way, so the main layout never shifts
                        if (NearWater(m, c, 7f) && !NearCrossing(m, c, 14f) && !InDeployment(c.x, c.z)) BankWoods(bank, c);
                        if (dq < 0.006) add(main, dead, c, Rf(rng, 4.5f, 6.0f), false);
                        break;
                    }
                    case TerrainType.Grass:
                        if (InDeployment(c.x, c.z)) break;
                        double p = rng.NextDouble();
                        // riverbank woods: trees and bushes line the banks (not at crossings), and mirror in the water
                        if (NearWater(m, c, 7f) && !NearCrossing(m, c, 14f)) BankWoods(bank, c);   // and the original lone props still follow
                        if (p < 0.0025) add(lone, broad, c, Rf(rng, 6.0f, 7.6f), false);
                        else if (p < 0.0040) add(lone, bush, c, Rf(rng, 2.2f, 3.0f), false);
                        else if (p < 0.0052) add(lone, rock, c, Rf(rng, 2.6f, 4.0f), true);
                        break;
                }
            }
        spr.Build("Trees", root, main, true, true);
        FarHidden.Add(spr.Build("FieldClutter", root, lone));   // single trees and boulders in the open, hidden when zoomed far out
        Reflections(m, spr, root, main, lone);
    }

    // water reflections: a tree on a north bank is mirrored onto the water south of it. the camera never turns, so the
    // mirror image is the same picture flipped and laid flat, 1/sin(pitch) deep to read at its true height, and cut where
    // the water ends so it never lies on land. darker and fading out with distance from the bank.
    private static void Reflections(TerrainMap m, TerrainSprites spr, Transform root, params List<TerrainSprites.Placement>[] lists)
    {
        bool Wet(Vector3 p) { var t = m.At(p); return t == TerrainType.Deep || t == TerrainType.Ford; }
        var decals = new List<TerrainSprites.Decal>();
        float depthPerMetre = 1f / Mathf.Sin(40f * Mathf.Deg2Rad);
        foreach (var list in lists)
            foreach (var pl in list)
            {
                if (pl.byWidth) continue;   // rocks sit low; only standing trees and bushes reflect
                float start = -1f;
                for (float d = 0.5f; d <= 18f; d += 0.5f) if (Wet(pl.pos - Vector3.forward * d)) { start = d; break; }
                if (start < 0f) continue;
                // the mirror image runs from the tree's base, so the stretch over the dry gap is hidden: show from there on
                float full = pl.size * depthPerMetre;
                if (start >= full - 1f) continue;
                float len = 0f;
                while (start + len < full && Wet(pl.pos - Vector3.forward * (start + len + 0.5f))) len += 0.5f;
                if (len < 1f) continue;
                float z1 = pl.pos.z - start, from = start / full, to = (start + len) / full;
                byte a0 = (byte)(225f * (1f - from * 0.5f)), a1 = (byte)(225f * (1f - to * 0.8f) + 25f);
                decals.Add(new TerrainSprites.Decal
                {
                    sprite = pl.sprite, centerX = pl.pos.x, width = pl.size * spr.Aspect(pl.sprite), z1 = z1, z0 = z1 - len,
                    flipV = true, vFrom = from, vFrac = to, near = new Color32(96, 108, 96, a0), far = new Color32(96, 108, 96, a1)
                });
            }
        if (decals.Count > 0) spr.BuildDecals("WaterReflections", root, decals);
    }

    private static bool Wet(TerrainType t) => t == TerrainType.Deep || t == TerrainType.Ford;

    private static bool NearWater(TerrainMap m, Vector3 c, float r)
    {
        for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; if (Wet(m.At(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r))) return true; }
        return false;
    }

    private static bool NearCrossing(TerrainMap m, Vector3 c, float r)
    {
        for (float dx = -r; dx <= r; dx += 3f) for (float dz = -r; dz <= r; dz += 3f)
        { var t = m.At(c + new Vector3(dx, 0f, dz)); if (t == TerrainType.Ford || t == TerrainType.Bridge || t == TerrainType.Road) return true; }
        return false;
    }

    private static readonly List<GameObject> FarHidden = new List<GameObject>();

    // when the camera pulls far back, small clutter turns into speckle, so it is switched off
    private sealed class ZoomLod : MonoBehaviour
    {
        public GameObject[] hide; private bool shown = true; private float next;
        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.25f;
            var cam = Camera.main; if (cam == null) return;
            bool show = cam.orthographicSize < 70f;
            if (show == shown) return;
            shown = show; foreach (var g in hide) if (g != null) g.SetActive(show);
        }
    }

    // ---------------- bridge and ford ----------------

    private struct Box { public int x0, x1, z0, z1; }

    // connected patches of one type, as cell boxes
    private static List<Box> Patches(TerrainMap m, TerrainType type)
    {
        var seen = new bool[m.Width * m.Height];
        var list = new List<Box>();
        var q = new Queue<Vector2Int>();
        for (int z = 0; z < m.Height; z++)
            for (int x = 0; x < m.Width; x++)
            {
                if (seen[z * m.Width + x] || m.AtCell(x, z) != type) continue;
                var b = new Box { x0 = x, x1 = x, z0 = z, z1 = z };
                seen[z * m.Width + x] = true; q.Enqueue(new Vector2Int(x, z));
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    b.x0 = Mathf.Min(b.x0, c.x); b.x1 = Mathf.Max(b.x1, c.x); b.z0 = Mathf.Min(b.z0, c.y); b.z1 = Mathf.Max(b.z1, c.y);
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = c.x + (d == 0 ? 1 : d == 1 ? -1 : 0), nz = c.y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || nz < 0 || nx >= m.Width || nz >= m.Height || seen[nz * m.Width + nx] || m.AtCell(nx, nz) != type) continue;
                        seen[nz * m.Width + nx] = true; q.Enqueue(new Vector2Int(nx, nz));
                    }
                }
                list.Add(b);
            }
        return list;
    }

    // water on the two x-ends of the box means the river runs east-west, so the way across is north-south (z), and the other way round
    private static bool CrossesAlongZ(TerrainMap m, Box b)
    {
        int waterX = 0, waterZ = 0;
        for (int z = b.z0; z <= b.z1; z++) { if (IsWater(m.AtCell(b.x0 - 1, z))) waterX++; if (IsWater(m.AtCell(b.x1 + 1, z))) waterX++; }
        for (int x = b.x0; x <= b.x1; x++) { if (IsWater(m.AtCell(x, b.z0 - 1))) waterZ++; if (IsWater(m.AtCell(x, b.z1 + 1))) waterZ++; }
        return waterX >= waterZ;
    }

    private static Material Flat(Color c)
    {
        var mt = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mt.SetColor("_BaseColor", c); mt.SetFloat("_Smoothness", 0.05f); mt.SetFloat("_Metallic", 0f);
        mt.SetFloat("_SpecularHighlights", 0f); mt.SetFloat("_EnvironmentReflections", 0f);
        return mt;
    }

    private static GameObject Cube(PrimitiveType pt, Transform parent, Vector3 pos, Vector3 scale, Material mt, float yaw = 0f)
    {
        var g = GameObject.CreatePrimitive(pt);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.position = pos; g.transform.localScale = scale; g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = mt; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return g;
    }

    private static void BuildCrossings(TerrainMap m, Transform root)
    {
        var cross = new GameObject("Crossings").transform; cross.SetParent(root, false);
        var woodA = Flat(new Color(0.55f, 0.37f, 0.20f)); var woodB = Flat(new Color(0.48f, 0.31f, 0.17f)); var post = Flat(new Color(0.38f, 0.25f, 0.14f));
        float cx = m.CellSizeX, cz = m.CellSizeZ;
        foreach (var b in Patches(m, TerrainType.Bridge))
        {
            Vector3 lo = m.CellCenter(b.x0, b.z0) - new Vector3(cx * 0.5f, 0f, cz * 0.5f), hi = m.CellCenter(b.x1, b.z1) + new Vector3(cx * 0.5f, 0f, cz * 0.5f);
            Vector3 ctr = (lo + hi) * 0.5f, size = hi - lo;
            bool alongZ = CrossesAlongZ(m, b);                  // the way across the water
            var art = TerrainSprites.Load();
            if (alongZ && art != null && art.Has("bridge_1"))
            {
                // the painted bridge, laid flat over the bridge cells plus a little to each side and end
                // the picture is a true orthographic view: deck about 88% of the width and 80% of the height, the rest posts. its screen height is
                // ground depth x sin(pitch), so the ground rectangle is the deck's depth over 0.8, centred on the bridge cells
                float gw = (size.x + 1f) / 0.88f, gd = (size.z + 2f) / 0.80f;
                var decal = new List<TerrainSprites.Decal> { new TerrainSprites.Decal { sprite = "bridge_1", centerX = ctr.x, width = gw, z0 = ctr.z - gd * 0.5f, z1 = ctr.z + gd * 0.5f } };
                art.BuildDecals("PaintedBridge", cross, decal);
                continue;
            }
            float len = (alongZ ? size.z : size.x) + 2f, wid = (alongZ ? size.x : size.z) - 1f;
            float yaw = alongZ ? 0f : 90f;
            Vector3 along = alongZ ? Vector3.forward : Vector3.right, side = alongZ ? Vector3.right : Vector3.forward;
            int planks = Mathf.RoundToInt(len / 1.0f);
            for (int i = 0; i < planks; i++)
            {
                float t = (i + 0.5f) / planks - 0.5f;
                Cube(PrimitiveType.Cube, cross, ctr + along * (t * len) + Vector3.up * 0.38f, new Vector3(wid, 0.28f, len / planks - 0.08f).Swizzle(alongZ), i % 2 == 0 ? woodA : woodB, 0f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Cube(PrimitiveType.Cube, cross, ctr + side * (s * (wid * 0.5f - 0.15f)) + Vector3.up * 1.35f, new Vector3(0.28f, 0.22f, len).Swizzle(alongZ, true), post, 0f);
                for (int i = 0; i <= 4; i++)
                {
                    float t = i / 4f - 0.5f;
                    Cube(PrimitiveType.Cube, cross, ctr + along * (t * (len - 0.6f)) + side * (s * (wid * 0.5f - 0.15f)) + Vector3.up * 0.8f, new Vector3(0.34f, 1.7f, 0.34f), post, 0f);
                }
            }
        }
        // ford stepping stones: painted stones from the atlas, standing small and low in the shallows
        var stoneArt = TerrainSprites.Load();
        var stones = new List<TerrainSprites.Placement>();
        var rng = new System.Random(77);
        string[] stoneNames = stoneArt != null ? stoneArt.Names("ford_") : new string[0];
        if (stoneNames.Length > 0)
            foreach (var b in Patches(m, TerrainType.Ford))
            {
                Vector3 lo = m.CellCenter(b.x0, b.z0), hi = m.CellCenter(b.x1, b.z1);
                for (float x = lo.x; x <= hi.x; x += 2.6f)
                    for (float z = lo.z; z <= hi.z; z += 2.4f)
                    {
                        var p = new Vector3(x + Rf(rng, -0.5f, 0.5f), 0f, z + Rf(rng, -0.5f, 0.5f));
                        if (m.At(p) != TerrainType.Ford) continue;
                        stones.Add(new TerrainSprites.Placement { sprite = stoneNames[rng.Next(stoneNames.Length)], pos = p, size = Rf(rng, 1.5f, 2.1f), byWidth = true });
                    }
            }
        if (stones.Count > 0) FarHidden.Add(stoneArt.Build("FordStones", cross, stones, false));
        if (Application.isPlaying) StaticBatchingUtility.Combine(cross.gameObject);
    }

    private static Vector3 Swizzle(this Vector3 v, bool alongZ, bool rail = false)
    {
        // sizes are authored as (across, thickness, along) for planks and (thickness, height, along) for rails
        return alongZ ? v : new Vector3(v.z, v.y, v.x);
    }

    // ---------------- light ----------------

    private static void TuneLight()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null) return;
        sun.color = new Color(1f, 0.88f, 0.68f);   // warm low late-afternoon sun
        sun.intensity = 1.35f;
        // from the upper left of the screen, the same side the props are painted lit from; their cast shadows fall the other way
        sun.transform.rotation = Quaternion.Euler(36f, 135f, 0f);
        sun.shadows = LightShadows.None;           // blob shadows instead, to keep the frame cheap on a phone
    }
}
