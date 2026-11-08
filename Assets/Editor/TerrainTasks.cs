using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// editor tasks for terrain, run through the scenario file trigger ("task=TerrainTasks.RunDataTests")
public static class TerrainTasks
{
    static readonly StringBuilder log = new StringBuilder();
    static int fails;

    static void Check(bool ok, string what) { if (!ok) { fails++; log.AppendLine("FAIL " + what); } }
    static string Done(string name) { string r = (fails == 0 ? "PASS " : "FAIL ") + name + "\n" + log; log.Clear(); fails = 0; return r; }

    static Color Key(TerrainType t)
    {
        switch (t)
        {
            case TerrainType.Road: return Hex(0xB58F5E);
            case TerrainType.Mud: return Hex(0x7A5C3E);
            case TerrainType.Ford: return Hex(0x7FB8C9);
            case TerrainType.Deep: return Hex(0x3E7FA6);
            case TerrainType.Rock: return Hex(0x8E8C86);
            case TerrainType.Forest: return Hex(0x6E8450);
            case TerrainType.Bridge: return Hex(0x8B5A2B);
            default: return Hex(0x9DB36A);
        }
    }
    static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f, 1f);

    public static string RunDataTests()
    {
        // 4x4 map over an 8 x 8 m field, so a cell is 2 m. row 0 is the south edge (z -4..-2)
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, Key(TerrainType.Grass));
        tex.SetPixel(0, 0, Key(TerrainType.Road));
        tex.SetPixel(1, 0, Key(TerrainType.Mud));
        tex.SetPixel(2, 0, Key(TerrainType.Deep));
        tex.SetPixel(3, 0, Key(TerrainType.Rock));
        tex.SetPixel(0, 1, Key(TerrainType.Ford));
        tex.SetPixel(1, 1, Key(TerrainType.Bridge));
        tex.SetPixel(2, 1, Key(TerrainType.Forest));
        tex.Apply();
        var map = TerrainMap.FromTexture(tex, 4f, 4f);

        Check(map.At(new Vector3(-3f, 0f, -3f)) == TerrainType.Road, "road at sw cell");
        Check(map.At(new Vector3(-1f, 0f, -3f)) == TerrainType.Mud, "mud");
        Check(map.At(new Vector3(1f, 0f, -3f)) == TerrainType.Deep, "deep");
        Check(map.At(new Vector3(3f, 0f, -3f)) == TerrainType.Rock, "rock");
        Check(map.At(new Vector3(-3f, 0f, -1f)) == TerrainType.Ford, "ford");
        Check(map.At(new Vector3(-1f, 0f, -1f)) == TerrainType.Bridge, "bridge");
        Check(map.At(new Vector3(1f, 0f, -1f)) == TerrainType.Forest, "forest");
        Check(map.At(new Vector3(3f, 0f, 3f)) == TerrainType.Grass, "grass");
        Check(map.At(new Vector3(-99f, 0f, -99f)) == TerrainType.Road, "outside the field clamps to the edge cell");
        Check(map.At(new Vector3(99f, 0f, -99f)) == TerrainType.Rock, "outside the field clamps (east)");

        Check(Mathf.Approximately(map.SpeedAt(new Vector3(-3f, 0f, -3f)), 1.15f), "road speed 1.15");
        Check(Mathf.Approximately(map.SpeedAt(new Vector3(-1f, 0f, -3f)), 0.6f), "mud speed 0.6");
        Check(Mathf.Approximately(map.SpeedAt(new Vector3(3f, 0f, 3f)), 1f), "grass speed 1");
        Check(Mathf.Approximately(map.SpeedAt(new Vector3(1f, 0f, -1f)), 1.15f * 0f + TerrainRules.Speed(TerrainType.Forest)), "forest speed via table");
        Check(Mathf.Approximately(TerrainRules.Speed(TerrainType.Ford), 0.5f), "ford speed 0.5");
        Check(Mathf.Approximately(TerrainRules.Speed(TerrainType.Forest), 0.7f), "forest speed 0.7");

        foreach (TerrainType t in System.Enum.GetValues(typeof(TerrainType)))
        {
            bool want = t == TerrainType.Deep || t == TerrainType.Rock;
            Check(TerrainRules.Blocked(t) == want, "blocked " + t);
        }
        Check(map.Blocked(new Vector3(1f, 0f, -3f)) && map.Blocked(new Vector3(3f, 0f, -3f)), "map blocked deep and rock");
        Check(!map.Blocked(new Vector3(-1f, 0f, -1f)) && !map.Blocked(new Vector3(-3f, 0f, -1f)), "bridge and ford never blocked");
        Check(map.ConcealedAt(new Vector3(1f, 0f, -1f)) && !map.ConcealedAt(new Vector3(3f, 0f, 3f)), "forest concealed, grass not");
        Check(map.Width == 4 && map.Height == 4, "grid size");
        var c = map.CellCenter(0, 0);
        Check(Mathf.Approximately(c.x, -3f) && Mathf.Approximately(c.z, -3f), "cell center");
        UnityEngine.Object.DestroyImmediate(map); UnityEngine.Object.DestroyImmediate(tex);
        return Done("RunDataTests");
    }

    // ---- art import and the default map ----
    const string ForestSrc = "/private/tmp/claude-501/-Users-josephgonzaludo-AncientArmies/b084da92-9a51-4c1e-8762-5e3b0f4577e9/scratchpad/kaykit_forest_extract/KayKit_Forest_Nature_Pack_1.0_FREE";
    const string ForestDst = "Assets/ThirdParty/KayKit/Forest";

    // copies the free kaykit forest pack (unity fbx variants, texture, license) into the project
    public static string ImportForest()
    {
        if (!Directory.Exists(ForestSrc)) return "MISSING " + ForestSrc;
        Directory.CreateDirectory(ForestDst + "/Models");
        Directory.CreateDirectory(ForestDst + "/Textures");
        int n = 0;
        foreach (var f in Directory.GetFiles(ForestSrc + "/Assets/fbx(unity)", "*.fbx")) { File.Copy(f, ForestDst + "/Models/" + Path.GetFileName(f), true); n++; }
        File.Copy(ForestSrc + "/Textures/forest_texture.png", ForestDst + "/Textures/forest_texture.png", true);
        File.Copy(ForestSrc + "/License.txt", "Assets/ThirdParty/KayKit/Licenses/License_Forest.txt", true);
        AssetDatabase.Refresh();
        return "imported " + n + " models";
    }

    // ---- map painting ----
    static float Fbm(float x, float z, float f, float off) => 0.65f * Mathf.PerlinNoise(x * f + off, z * f + off * 0.7f) + 0.35f * Mathf.PerlinNoise(x * f * 2.3f + off + 9f, z * f * 2.3f + off);

    sealed class MapSpec
    {
        public string name; public float amp, wave, phase, baseHalf;   // river meander
        public float bridgeX, bridgeHalfW; public bool hasFord; public float fordX, fordX2;
        public Vector4[] forests;   // cx, cz, rx, rz
        public Vector3[] rocks;     // cx, cz, r
        public bool marsh; public float marshReach;   // wide marsh on the north bank near the bridge (Stirling)
        public float marshX;
    }

    static float RiverZ(MapSpec m, float x) => m.amp * Mathf.Sin(x / m.wave + m.phase) + 0.35f * m.amp * Mathf.Sin(x / (m.wave * 0.37f) + m.phase * 2.1f) + 6f * (Mathf.PerlinNoise(x * 0.01f + 3f, 7f) - 0.5f);
    static float RiverHalf(MapSpec m, float x) => m.baseHalf + 1.8f * Mathf.Sin(x / 31f + 1.3f) + 1.2f * (Mathf.PerlinNoise(x * 0.03f, 11f) - 0.5f);

    // the x in [lo, hi] where the river runs flattest, so an axis-aligned crossing sits square to the water
    static float FlatSpot(MapSpec m, float lo, float hi)
    {
        float best = lo, bs = float.MaxValue;
        for (float x = lo; x <= hi; x += 2f) { float sl = Mathf.Abs(RiverZ(m, x + 6f) - RiverZ(m, x - 6f)); if (sl < bs) { bs = sl; best = x; } }
        return best;
    }

    static TerrainType Decide(MapSpec m, float x, float z)
    {
        var t = TerrainType.Grass;
        float zc = RiverZ(m, x), hw = RiverHalf(m, x);
        float dBank = Mathf.Abs(z - zc) - hw;   // metres outside the water's edge (negative = in the river)
        // forests: ragged blobs, never inside the river's flood margin
        foreach (var f in m.forests)
        {
            float nx = (x - f.x) / f.z, nz = (z - f.y) / f.w;
            float v = 1f - Mathf.Sqrt(nx * nx + nz * nz) + 0.55f * (Fbm(x, z, 0.035f, 17f) - 0.5f);
            if (v > 0f && dBank > 6f) t = TerrainType.Forest;
        }
        // wet ground along the banks: a floodplain strip of mud with a ragged outer edge, thicker on the inside of bends
        // (crossings keep clean approaches, so the mud thins out near a bridge or ford)
        bool nearCrossing = Mathf.Abs(x - m.bridgeX) < 20f || (m.hasFord && (Mathf.Abs(x - m.fordX) < 22f || (m.fordX2 != 0f && Mathf.Abs(x - m.fordX2) < 15f)));
        float wet = 3f + 7f * Fbm(x, z, 0.04f, 41f);
        if (!nearCrossing && dBank > 0f && dBank < wet && Fbm(x, z, 0.06f, 5f) > 0.38f && t == TerrainType.Grass) t = TerrainType.Mud;
        if (m.marsh && z > zc && z < 36f + 10f * Fbm(x, z, 0.05f, 61f) && dBank < m.marshReach * (0.5f + Fbm(x, z, 0.05f, 23f)) * Mathf.Clamp01(1.25f - Mathf.Abs(x - m.marshX) / 55f) && t != TerrainType.Forest) t = TerrainType.Mud;
        // rocks: outcrops at forest edges and a few boulders on the banks
        foreach (var r in m.rocks)
            if ((x - r.x) * (x - r.x) + (z - r.y) * (z - r.y) < r.z * r.z * (0.7f + 0.6f * Fbm(x, z, 0.12f, 3f))) t = TerrainType.Rock;
        // the road winds, but runs straight onto the bridge: the wiggle fades out within 25 m of the crossing
        float dz = z - RiverZ(m, m.bridgeX);
        float roadX = m.bridgeX + 12f * Mathf.Sin(dz / 38f) * Mathf.Clamp01((Mathf.Abs(dz) - 6f) / 22f);
        if (Mathf.Abs(x - roadX) <= 4f && Mathf.Abs(z) <= 140f) t = TerrainType.Road;
        if (dBank <= 0f) t = TerrainType.Deep;
        if (m.hasFord && Mathf.Abs(x - m.fordX) <= 15f && dBank <= 1.5f) t = TerrainType.Ford;
        if (m.hasFord && m.fordX2 != 0f && Mathf.Abs(x - m.fordX2) <= 9f && dBank <= 1.5f) t = TerrainType.Ford;
        if (Mathf.Abs(x - m.bridgeX) <= m.bridgeHalfW && dBank <= 1.5f) t = TerrainType.Bridge;
        // deployment zones: clear walkable ground, road allowed
        if (Mathf.Abs(z) >= 50f && Mathf.Abs(x) <= 130f && t != TerrainType.Road) t = TerrainType.Grass;
        return t;
    }

    static string PaintMap(MapSpec m)
    {
        const int W = 205, H = 170;
        var px = new Color32[W * H];
        for (int z = 0; z < H; z++) for (int x = 0; x < W; x++)
            px[z * W + x] = (Color32)Key(Decide(m, -205f + (x + 0.5f) * 2f, -170f + (z + 0.5f) * 2f));
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.SetPixels32(px); tex.Apply();
        Directory.CreateDirectory("Assets/Resources/Terrain");
        string png = "Assets/Resources/Terrain/" + m.name + "_painted.png";
        File.WriteAllBytes(png, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(png);
        imp.isReadable = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.npotScale = TextureImporterNPOTScale.None;
        imp.maxTextureSize = 256; imp.sRGBTexture = true; imp.alphaSource = TextureImporterAlphaSource.None;
        imp.SaveAndReimport();
        string asset = "Assets/Resources/Terrain/" + m.name + ".asset";
        var map = AssetDatabase.LoadAssetAtPath<TerrainMap>(asset);
        if (map == null) { map = ScriptableObject.CreateInstance<TerrainMap>(); AssetDatabase.CreateAsset(map, asset); }
        map.painted = AssetDatabase.LoadAssetAtPath<Texture2D>(png); map.halfX = 205f; map.halfZ = 170f;
        EditorUtility.SetDirty(map);
        return asset;
    }

    // Map_Default: a river across the whole field (meanders, three ways over: a bridge on the road, a wide ford, a
    // second ford further out), mud on the banks, forests and copses away from the water, rock at forest edges.
    // Map_Stirling: after Stirling Bridge 1297, one narrow bridge on a looping river, a marsh on the far bank, forest
    // on the flanks. a chokepoint test map. deployment zones (|z| 50..150, |x| <= 130) are always clear.
    public static string BuildDefaultMap()
    {
        var def = new MapSpec
        {
            name = "Map_Default", amp = 14f, wave = 70f, phase = 0.4f, baseHalf = 6.5f, bridgeHalfW = 8f, hasFord = true,
            forests = new[] { new Vector4(-190f, 10f, 62f, 82f), new Vector4(172f, 68f, 40f, 46f), new Vector4(-118f, -30f, 15f, 11f), new Vector4(98f, 36f, 17f, 12f), new Vector4(160f, -60f, 28f, 24f) },
            rocks = new[] { new Vector3(-136f, 62f, 7f), new Vector3(-142f, -48f, 6f), new Vector3(132f, 44f, 7f), new Vector3(150f, -120f, 8f), new Vector3(-170f, 118f, 8f), new Vector3(168f, 112f, 8f) },
        };
        def.bridgeX = FlatSpot(def, -66f, -34f); def.fordX = FlatSpot(def, 44f, 80f);
        def.fordX2 = FlatSpot(def, -170f, -140f);   // a second, narrower ford far to the west
        string a = PaintMap(def);
        var st = new MapSpec
        {
            name = "Map_Stirling", amp = 26f, wave = 48f, phase = 0.7f, baseHalf = 7f, bridgeHalfW = 4f, hasFord = false, marsh = true, marshReach = 40f,
            forests = new[] { new Vector4(-185f, 0f, 55f, 120f), new Vector4(185f, 0f, 55f, 120f) },
            rocks = new[] { new Vector3(-136f, 60f, 6f), new Vector3(140f, -56f, 6f), new Vector3(110f, 42f, 5f) },
        };
        st.bridgeX = FlatSpot(st, -12f, 12f); st.marshX = st.bridgeX;
        string b = PaintMap(st);
        AssetDatabase.SaveAssets();
        return "built " + a + " and " + b;
    }

    // the painted prop atlas: import it as a normal alpha texture with mipmaps, and make the two materials the terrain
    // view loads (one asset, so the sprite shader is always included in a build)
    public static string ImportTerrainSprites()
    {
        const string dir = "Assets/Resources/Terrain/Sprites";
        string png = dir + "/terrain_atlas.png";
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(png);
        if (imp == null) return "MISSING " + png;
        imp.textureType = TextureImporterType.Default; imp.alphaSource = TextureImporterAlphaSource.FromInput; imp.alphaIsTransparency = true;
        imp.isReadable = false; imp.mipmapEnabled = true; imp.filterMode = FilterMode.Bilinear; imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 2048; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.sRGBTexture = true;
        imp.SaveAndReimport();
        string matPath = dir + "/MAT_TerrainSprites.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
        return "imported sprite atlas, shader " + mat.shader.name;
    }

    // the ground material: the splat shader plus the five painted tile textures (repeat, mipmapped, a little anisotropy)
    public static string BuildGroundMaterial()
    {
        const string dir = "Assets/Resources/Terrain/Ground";
        foreach (var n in new[] { "grass", "road", "mud", "forest", "water" })
        {
            string p = dir + "/" + n + ".png";
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(p);
            if (imp == null) return "MISSING " + p;
            imp.textureType = TextureImporterType.Default; imp.wrapMode = TextureWrapMode.Repeat; imp.filterMode = FilterMode.Trilinear;
            imp.mipmapEnabled = true; imp.anisoLevel = 4; imp.sRGBTexture = true; imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.alphaSource = TextureImporterAlphaSource.None;
            imp.SaveAndReimport();
        }
        var shader = Shader.Find("AncientArmies/TerrainSplat");
        if (shader == null) return "MISSING shader AncientArmies/TerrainSplat";
        string matPath = dir + "/MAT_TerrainGround.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = shader;
        mat.SetTexture("_Grass", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/grass.png"));
        mat.SetTexture("_Road", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/road.png"));
        mat.SetTexture("_Mud", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/mud.png"));
        mat.SetTexture("_Forest", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/forest.png"));
        mat.SetTexture("_Water", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/water.png"));
        EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
        return "built " + matPath;
    }

    static TerrainMap LoadDefault() => Resources.Load<TerrainMap>("Terrain/Map_Default");

    // longest run of one type along a row of cells
    static int Run(TerrainMap m, int z, TerrainType t)
    {
        int best = 0, cur = 0;
        for (int x = 0; x < m.Width; x++) { if (m.AtCell(x, z) == t) { cur++; best = Mathf.Max(best, cur); } else cur = 0; }
        return best;
    }

    public static string RunSpeedTests()
    {
        // the smoothed terrain speed never jumps: a border crossing eases over about half a second
        float v = 1f, prev = 1f, worst = 0f;
        for (int i = 0; i < 60; i++) { v = TerrainRules.Smooth(v, 0.6f, 1f / 60f); worst = Mathf.Max(worst, Mathf.Abs(v - prev)); Check(v <= prev + 1e-6f, "monotonic toward a slower target"); prev = v; }
        Check(worst <= 0.06f, "no frame changes the speed by more than 0.06 (was " + worst + ")");
        Check(Mathf.Abs(v - 0.6f) < 0.01f, "reaches the target within a second (was " + v + ")");
        float up = 0.6f;
        for (int i = 0; i < 60; i++) up = TerrainRules.Smooth(up, 1.15f, 1f / 60f);
        Check(Mathf.Abs(up - 1.15f) < 0.01f, "eases up onto road too");
        Check(Mathf.Approximately(TerrainRules.Smooth(1f, 1f, 0.016f), 1f), "no change when already at target");
        return Done("RunSpeedTests");
    }

    // a small map from rows of characters: 'D' deep, 'B' bridge, 'R' rock, anything else grass. row 0 is the north edge
    static TerrainMap MapFrom(string[] rows, float half)
    {
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int r = 0; r < h; r++) for (int x = 0; x < w; x++)
        {
            char ch = rows[r][x];
            tex.SetPixel(x, h - 1 - r, Key(ch == 'D' ? TerrainType.Deep : ch == 'B' ? TerrainType.Bridge : ch == 'R' ? TerrainType.Rock : TerrainType.Grass));
        }
        tex.Apply();
        return TerrainMap.FromTexture(tex, half, half);
    }

    public static string RunPlannerTests()
    {
        var map = Resources.Load<TerrainMap>("Terrain/Map_Default");
        Check(map != null, "default map loads");
        if (map == null) return Done("RunPlannerTests");
        var pts = new System.Collections.Generic.List<Vector3>();

        // find landmarks on whatever map this is: a bridge cell, a deep-water cell near the middle
        Vector3 bridge = Vector3.zero, deep = Vector3.zero; bool haveB = false, haveD = false;
        for (int z = 0; z < map.Height; z++) for (int x = 0; x < map.Width; x++)
        {
            var ty = map.AtCell(x, z);
            if (!haveB && ty == TerrainType.Bridge) { bridge = map.CellCenter(x, z); haveB = true; }
            if (ty == TerrainType.Deep && Mathf.Abs(map.CellCenter(x, z).x) < 6f) { deep = map.CellCenter(x, z); haveD = true; }
        }
        Check(haveB && haveD, "found a bridge and deep water to test with");
        if (!haveB || !haveD) return Done("RunPlannerTests");

        // open grass: one straight leg
        Check(TerrainGridPlanner.TryPlan(new Vector3(0f, 0f, -100f), new Vector3(0f, 0f, -60f), 4f, pts) && pts.Count == 1, "open grass is one leg");

        // across the river: never through deep water, and over a crossing
        var s1 = new Vector3(deep.x, 0f, -80f); var g1 = new Vector3(deep.x, 0f, 80f);
        Check(TerrainGridPlanner.TryPlan(s1, g1, 4f, pts), "route across the river exists");
        bool usedCrossing = false, hitBlocked = false; Vector3 a = s1;
        foreach (var q in pts)
        {
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                var ty = map.At(Vector3.Lerp(a, q, t));
                if (ty == TerrainType.Bridge || ty == TerrainType.Ford) usedCrossing = true;
                if (TerrainRules.Blocked(ty)) hitBlocked = true;
            }
            a = q;
        }
        Check(usedCrossing, "river route uses a bridge or ford");
        Check(!hitBlocked, "river route never enters deep water or rock");
        Check(pts.Count > 0 && Vector3.Distance(pts[pts.Count - 1], g1) < 0.01f, "route ends exactly on the goal");

        // a goal in deep water is refused (callers snap first)
        Check(!TerrainGridPlanner.TryPlan(new Vector3(0f, 0f, -80f), deep, 4f, pts), "goal in deep water refused");

        // a formation (clearance 5) crosses the bridge, never touching water or rock
        var s2 = new Vector3(bridge.x, 0f, -60f); var g2 = new Vector3(bridge.x, 0f, 60f);
        Check(TerrainGridPlanner.TryPlan(s2, g2, 5f, pts), "wide formation crosses by the bridge");
        a = s2; bool wet = false;
        foreach (var q in pts)
        {
            for (float t = 0f; t <= 1f; t += 0.01f) if (TerrainRules.Blocked(map.At(Vector3.Lerp(a, q, t)))) wet = true;
            a = q;
        }
        Check(!wet, "wide crossing never touches water or rock");

        // nearest reachable: a point in the river snaps to dry ground close by; a dry point is unchanged
        var snapped = TerrainGridPlanner.NearestReachable(new Vector3(0f, 0f, -100f), deep);
        Check(!map.Blocked(snapped) && Vector3.Distance(snapped, deep) < 16f, "river point snaps to dry ground within 16 m (was " + snapped + ")");
        Check(Vector3.Distance(TerrainGridPlanner.NearestReachable(new Vector3(0f, 0f, -100f), new Vector3(30f, 0f, -80f)), new Vector3(30f, 0f, -80f)) < 0.01f, "a reachable point is unchanged");

        // a formation stranded in deep water plans from the nearest dry cell instead of failing into a straight line through the river
        Check(TerrainGridPlanner.TryPlan(deep, new Vector3(deep.x, 0f, -80f), 4f, pts), "stranded in deep water still plans a route out");
        Check(pts.Count > 0 && !map.Blocked(pts[0]), "the route out starts on dry ground");

        // an enclosed island: the target snaps to the mainland side, never onto the island
        var island = MapFrom(new[]
        {
            "............",
            "............",
            "..DDDDDDDD..",
            "..D......D..",
            "..D......D..",
            "..D......D..",
            "..D......D..",
            "..D......D..",
            "..D......D..",
            "..DDDDDDDD..",
            "............",
            "............",
        }, 12f);
        var from = island.CellCenter(0, 0);
        var onIsland = island.CellCenter(6, 6);
        var ns = TerrainGridPlanner.NearestReachable(from, onIsland, island);
        Check(!island.Blocked(ns) && (Mathf.Abs(ns.x) > 7f || Mathf.Abs(ns.z) > 7f), "island target snaps off the island (was " + ns + ")");
        Check(!TerrainGridPlanner.TryPlan(from, onIsland, 2f, pts, island), "no route onto an enclosed island");
        UnityEngine.Object.DestroyImmediate(island);
        return Done("RunPlannerTests");
    }

    // sizes of the connected patches of one type, as (cells across x, cells across z)
    static System.Collections.Generic.List<Vector2Int> PatchSizes(TerrainMap m, TerrainType type)
    {
        var seen = new bool[m.Width * m.Height]; var res = new System.Collections.Generic.List<Vector2Int>(); var q = new System.Collections.Generic.Queue<Vector2Int>();
        for (int z = 0; z < m.Height; z++) for (int x = 0; x < m.Width; x++)
        {
            if (seen[z * m.Width + x] || m.AtCell(x, z) != type) continue;
            int x0 = x, x1 = x, z0 = z, z1 = z; seen[z * m.Width + x] = true; q.Enqueue(new Vector2Int(x, z));
            while (q.Count > 0)
            {
                var c = q.Dequeue(); x0 = Mathf.Min(x0, c.x); x1 = Mathf.Max(x1, c.x); z0 = Mathf.Min(z0, c.y); z1 = Mathf.Max(z1, c.y);
                for (int d = 0; d < 4; d++)
                {
                    int nx = c.x + (d == 0 ? 1 : d == 1 ? -1 : 0), nz = c.y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= m.Width || nz >= m.Height || seen[nz * m.Width + nx] || m.AtCell(nx, nz) != type) continue;
                    seen[nz * m.Width + nx] = true; q.Enqueue(new Vector2Int(nx, nz));
                }
            }
            res.Add(new Vector2Int(x1 - x0 + 1, z1 - z0 + 1));
        }
        return res;
    }

    // can a flood from the blue start reach the red start, optionally treating bridges and fords as walls?
    static bool Reaches(TerrainMap m, bool crossingsOpen)
    {
        var seen = new bool[m.Width * m.Height]; var q = new System.Collections.Generic.Queue<Vector2Int>();
        var s0 = m.ToCell(new Vector3(0f, 0f, -100f)); var goal = m.ToCell(new Vector3(0f, 0f, 100f));
        q.Enqueue(s0); seen[s0.y * m.Width + s0.x] = true;
        while (q.Count > 0)
        {
            var c = q.Dequeue(); if (c == goal) return true;
            for (int d = 0; d < 4; d++)
            {
                int nx = c.x + (d == 0 ? 1 : d == 1 ? -1 : 0), nz = c.y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nz < 0 || nx >= m.Width || nz >= m.Height || seen[nz * m.Width + nx]) continue;
                var t = m.AtCell(nx, nz);
                if (TerrainRules.Blocked(t) || (!crossingsOpen && (t == TerrainType.Bridge || t == TerrainType.Ford))) continue;
                seen[nz * m.Width + nx] = true; q.Enqueue(new Vector2Int(nx, nz));
            }
        }
        return false;
    }

    static void CheckMap(string name, int minBridgeCells, bool wantFord)
    {
        var m = Resources.Load<TerrainMap>("Terrain/" + name);
        Check(m != null, name + " loads from Resources/Terrain");
        if (m == null) return;
        Check(m.Width == 205 && m.Height == 170, name + " is 205 x 170 cells (2 m)");
        for (float z = -150f; z <= 150f; z += 2f)
        {
            if (Mathf.Abs(z) < 50f) continue;
            for (float x = -130f; x <= 130f; x += 2f)
            {
                var t = m.At(new Vector3(x, 0f, z));
                if (t != TerrainType.Grass && t != TerrainType.Road) { Check(false, name + " deployment cell " + x + "," + z + " is " + t); x = 999f; z = 999f; }
            }
        }
        var bridges = PatchSizes(m, TerrainType.Bridge);
        Check(bridges.Count >= 1, name + " has a bridge");
        foreach (var b in bridges) Check(b.x >= minBridgeCells, name + " bridge at least " + minBridgeCells + " cells wide along the river (was " + b + ")");   // rivers here run east-west, so width is the x extent
        var fords = PatchSizes(m, TerrainType.Ford);
        if (wantFord) { Check(fords.Count >= 1, name + " has a ford"); foreach (var f in fords) Check(Mathf.Min(f.x, f.y) >= 6, name + " ford at least 6 cells across (was " + f + ")"); }
        Check(Reaches(m, true), name + " blue can reach red over the crossings");
        Check(!Reaches(m, false), name + " the river really blocks without its crossings");
        Check(CountOf(m, TerrainType.Deep) > 400, name + " has a river");
        Check(CountOf(m, TerrainType.Mud) > 30, name + " has mud");
        Check(CountOf(m, TerrainType.Forest) > 300, name + " has forest");
        Check(CountOf(m, TerrainType.Rock) > 10, name + " has rock");
        Check(!TerrainRules.Blocked(m.At(new Vector3(0f, 0f, -100f))) && !TerrainRules.Blocked(m.At(new Vector3(0f, 0f, 100f))), name + " both starts walkable");
    }

    public static string RunMapTests()
    {
        CheckMap("Map_Default", 8, true);
        CheckMap("Map_Stirling", 4, false);
        return Done("RunMapTests");
    }

    static int CountOf(TerrainMap m, TerrainType t)
    {
        int n = 0;
        for (int z = 0; z < m.Height; z++) for (int x = 0; x < m.Width; x++) if (m.AtCell(x, z) == t) n++;
        return n;
    }
}
