using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// KayKit asset pipeline tasks, run through the file trigger (Temp/scenario_request.txt: task=KayKitTasks.Diag).
public static class KayKitTasks
{
    public const string Root = "Assets/ThirdParty/KayKit/";

    public static string Diag()
    {
        var sb = new StringBuilder();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Characters/Knight.fbx");
        if (go == null) return "Knight.fbx not imported yet";
        int total = 0;
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            int tris = r.sharedMesh.triangles.Length / 3; total += tris;
            sb.AppendLine($"part {r.name} tris={tris} bones={r.bones.Length} verts={r.sharedMesh.vertexCount} mats={r.sharedMaterials.Length} localPos={r.transform.localPosition} localScale={r.transform.localScale} root={(r.rootBone != null ? r.rootBone.name : "-")}");
        }
        sb.AppendLine("total tris " + total);
        foreach (var t in go.GetComponentsInChildren<Transform>(true).Where(t => t.name.ToLower().Contains("hand") || t.name == "head" || t.name == "root")) sb.AppendLine("xform " + t.name + " parent=" + (t.parent != null ? t.parent.name : "-"));
        var b = new Bounds(); bool first = true;
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        sb.AppendLine("bounds size " + b.size + " center " + b.center);
        foreach (var f in new[] { "General", "CombatMelee", "CombatRanged", "MovementBasic" })
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Root + "Animations/Rig_Medium_" + f + ".fbx");
            sb.AppendLine("clips " + f + ": " + string.Join(", ", imp.defaultClipAnimations.Select(c => c.name + "(" + (c.lastFrame - c.firstFrame) / 30f + "s)")));
        }
        foreach (var f in new[] { "sword_1handed", "shield_square", "bow", "bow_withString" })
        {
            var pg = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Props/" + f + ".fbx");
            if (pg == null) { sb.AppendLine("prop " + f + " NOT LOADED"); continue; }
            var mf = pg.GetComponentsInChildren<MeshFilter>(true);
            var rr = pg.GetComponentsInChildren<Renderer>(true);
            try {
            sb.AppendLine($"prop {f} tris={mf.Sum(m => m.sharedMesh.triangles.Length / 3)} meshFilters={mf.Length} renderers={rr.Length} size={(rr.Length > 0 ? rr[0].bounds.size : Vector3.zero)} types={string.Join(",", rr.Select(x => x.GetType().Name))} mats={string.Join(",", rr.SelectMany(x => x.sharedMaterials).Select(m => m != null ? m.name : "null"))}");
            } catch (System.Exception e) { sb.AppendLine("prop " + f + " ERR " + e.Message + " mf=" + mf.Length + " rr=" + rr.Length); }
        }
        sb.AppendLine("Knight importer: " + ((ModelImporter)AssetImporter.GetAtPath(Root + "Characters/Knight.fbx")).animationType);
        return sb.ToString();
    }
}

public static class KayKitApiProbe
{
    public static string Probe()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types; try { types = a.GetTypes(); } catch { continue; }
            foreach (var t in types)
                if (t.Name.Contains("MeshLod") || t.Name == "MeshSimplifier" || t.Name.Contains("LodGeneration"))
                {
                    sb.AppendLine(t.FullName + " in " + a.GetName().Name);
                    foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                        sb.AppendLine("   " + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", System.Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name + " " + p.Name)) + ")");
                }
        }
        var imp = typeof(ModelImporter);
        foreach (var p in imp.GetProperties()) if (p.Name.ToLower().Contains("lod")) sb.AppendLine("ModelImporter." + p.Name + " : " + p.PropertyType.Name);
        foreach (var p in typeof(Mesh).GetProperties()) if (p.Name.ToLower().Contains("lod")) sb.AppendLine("Mesh." + p.Name);
        foreach (var m in typeof(Mesh).GetMethods()) if (m.Name.ToLower().Contains("lod")) sb.AppendLine("Mesh." + m.Name);
        return sb.ToString();
    }
}

public static class KayKitApiProbe2
{
    public static string Probe()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in typeof(Renderer).GetProperties()) if (p.Name.ToLower().Contains("lod")) sb.AppendLine("Renderer." + p.Name + " : " + p.PropertyType.Name);
        foreach (var p in typeof(SkinnedMeshRenderer).GetProperties()) if (p.Name.ToLower().Contains("lod")) sb.AppendLine("SMR." + p.Name + " : " + p.PropertyType.Name);
        foreach (var p in typeof(QualitySettings).GetProperties()) if (p.Name.ToLower().Contains("lod") || p.Name.ToLower().Contains("skin")) sb.AppendLine("Quality." + p.Name + " : " + p.PropertyType.Name);
        return sb.ToString();
    }
}

public static class KayKitPreview
{
    // Renders the four soldier variants in representative poses to Temp/kaykit_preview.png.
    public static bool CloseUp;
    public static string Render() { CloseUp = false; return Do(); }
    public static string RenderClose() { CloseUp = true; return Do(); }
    public static string RenderArrow()
    {
        var prefab = Resources.Load<GameObject>("VIS_KayKit_Arrow");
        if (prefab == null) return "arrow prefab missing";
        var pru = new PreviewRenderUtility();
        pru.camera.orthographic = true; pru.camera.orthographicSize = 0.8f; pru.camera.transform.position = new Vector3(3f, 1f, 0f); pru.camera.transform.LookAt(Vector3.zero);
        pru.camera.backgroundColor = new Color(0.72f, 0.69f, 0.58f); pru.camera.clearFlags = CameraClearFlags.SolidColor; pru.ambientColor = Color.white; pru.lights[0].intensity = 1f;
        var go = Object.Instantiate(prefab); pru.AddSingleGO(go);   // flies along +Z: the camera looks from +X so +Z is to the screen's left... marker below
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.transform.localScale = Vector3.one * 0.12f; marker.transform.position = new Vector3(0f, 0f, 1f); pru.AddSingleGO(marker);
        pru.BeginPreview(new Rect(0, 0, 800, 400), GUIStyle.none); pru.camera.Render();
        var tex = pru.EndPreview() as RenderTexture; var prev = RenderTexture.active; RenderTexture.active = tex;
        var t2 = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false); t2.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); t2.Apply(); RenderTexture.active = prev;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "kaykit_arrow.png"), t2.EncodeToPNG()); pru.Cleanup();
        return "wrote Temp/kaykit_arrow.png (white ball = +Z, the direction of flight)";
    }
    static string Do()
    {
        var cfg = Resources.Load<KayKitVisualConfig>("KayKitVisualConfig");
        var melee = Resources.Load<GameObject>("VIS_KayKit_Melee");
        var archer = Resources.Load<GameObject>("VIS_KayKit_Archer");
        if (cfg == null || melee == null || archer == null) return "assets missing";
        var pru = new PreviewRenderUtility();
        pru.camera.orthographic = true; pru.camera.orthographicSize = 1.6f;
        if (CloseUp) pru.camera.orthographicSize = 1.15f;
        pru.camera.transform.position = new Vector3(0f, 1.3f, 6f); pru.camera.transform.LookAt(new Vector3(0f, 1.0f, 0f));
        pru.camera.backgroundColor = new Color(0.72f, 0.69f, 0.58f); pru.camera.clearFlags = CameraClearFlags.SolidColor;
        pru.ambientColor = new Color(0.212f, 0.227f, 0.259f); pru.lights[0].intensity = 1f; pru.lights[1].intensity = 0f;   // the Battle scene's real ambient and one white sun pru.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var sb = new System.Text.StringBuilder();
        var specs = new[] {
            (melee, cfg.blueMaterials[0], "Idle_A", 0.3f, -3.0f), (melee, cfg.redMaterials[0], "Melee_1H_Attack_Stab", 0.45f, -1.0f),
            (archer, cfg.blueMaterials[0], "Ranged_Bow_Draw", 0.5f, 1.0f), (archer, cfg.redMaterials[0], "Ranged_Bow_Aiming_Idle", 0.4f, 3.0f) };
        if (CloseUp) specs = new[] { (melee, cfg.blueMaterials[0], "Idle_A", 0.3f, -2.4f), (melee, cfg.blueMaterials[0], "Melee_1H_Attack_Stab", 0.45f, 0.0f), (archer, cfg.blueMaterials[0], "Ranged_Bow_Draw", 0.6f, 2.4f) };
        var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/ThirdParty/KayKit/Animations" })
            .SelectMany(g => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)).OfType<AnimationClip>()).GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
        var insts = new System.Collections.Generic.List<GameObject>();
        foreach (var (prefab, mat, clipName, time, x) in specs)
        {
            var go = (GameObject)Object.Instantiate(prefab);
            pru.AddSingleGO(go);
            go.transform.position = new Vector3(x, 0f, 0f);
            if (CloseUp) go.transform.rotation = Quaternion.Euler(0f, 60f, 0f);   // three-quarter view
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = (r is SkinnedMeshRenderer || !r.gameObject.name.ToLowerInvariant().Contains("bow")) ? mat : cfg.bowMaterial;
            if (clips.TryGetValue(clipName, out var clip)) clip.SampleAnimation(go, time); else sb.AppendLine("no clip " + clipName);
            var b = go.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
            sb.AppendLine(prefab.name + " bounds h=" + b.size.y.ToString("0.00") + " w=" + b.size.x.ToString("0.00"));
            insts.Add(go);
        }
        pru.BeginPreview(new Rect(0, 0, 1600, 500), GUIStyle.none);
        pru.camera.Render();
        var tex = pru.EndPreview() as RenderTexture;
        var prev = RenderTexture.active; RenderTexture.active = tex;
        var t2 = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
        t2.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); t2.Apply();
        RenderTexture.active = prev;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "kaykit_preview.png"), t2.EncodeToPNG());
        pru.Cleanup();
        return sb.ToString() + "wrote Temp/kaykit_preview.png";
    }
}

public static class KayKitTiles
{
    // Which 8x4 atlas tiles each Knight part samples (by vertex count), with the tile's average colour.
    public static string ByPart()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KayKit/Characters/Knight.fbx");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/KayKit/Characters/knight_texture.png");
        var ti = (TextureImporter)AssetImporter.GetAtPath("Assets/ThirdParty/KayKit/Characters/knight_texture.png");
        var raw = new Texture2D(2, 2); raw.LoadImage(System.IO.File.ReadAllBytes("Assets/ThirdParty/KayKit/Characters/knight_texture.png"));
        var sb = new System.Text.StringBuilder();
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var counts = new System.Collections.Generic.Dictionary<(int, int), int>();
            foreach (var uv in r.sharedMesh.uv)
            {
                var k = (Mathf.Clamp((int)(uv.x * 8f), 0, 7), Mathf.Clamp((int)((1f - uv.y) * 4f), 0, 3));
                counts[k] = counts.TryGetValue(k, out int c) ? c + 1 : 1;
            }
            sb.Append(r.name).Append(": ");
            foreach (var kv in counts.OrderByDescending(k => k.Value))
            {
                int tw = raw.width / 8, th = raw.height / 4;
                var col = raw.GetPixel(kv.Key.Item1 * tw + tw / 2, raw.height - 1 - (kv.Key.Item2 * th + th / 2));
                sb.Append("(" + kv.Key.Item1 + "," + kv.Key.Item2 + ")x" + kv.Value + "=#" + ColorUtility.ToHtmlStringRGB(col) + "  ");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

public static class KayKitMeshTiles
{
    public static string Merged()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var n in new[] { "KayKit_Melee_Merged", "KayKit_Archer_Merged" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/AncientArmies/Visuals/KayKit/Meshes/" + n + ".asset");
            var counts = new System.Collections.Generic.Dictionary<(int, int), int>();
            foreach (var uv in m.uv) { var k = (Mathf.Clamp((int)(uv.x * 8f), 0, 7), Mathf.Clamp((int)((1f - uv.y) * 4f), 0, 3)); counts[k] = counts.TryGetValue(k, out int c) ? c + 1 : 1; }
            sb.Append(n + ": ").AppendLine(string.Join(" ", counts.OrderByDescending(k => k.Value).Select(k => "(" + k.Key.Item1 + "," + k.Key.Item2 + ")x" + k.Value)));
        }
        return sb.ToString();
    }
}

public static class KayKitIso
{
    // The two team prefabs seen from the game camera angle (40 deg pitch, from behind), neutral white light.
    public static string Render()
    {
        var cfg = Resources.Load<KayKitVisualConfig>("KayKitVisualConfig");
        var melee = Resources.Load<GameObject>("VIS_KayKit_Melee");
        var pru = new PreviewRenderUtility();
        pru.camera.orthographic = true; pru.camera.orthographicSize = 1.5f;
        pru.camera.transform.position = new Vector3(0f, 1f, -6f) + Quaternion.Euler(40f, 0f, 0f) * Vector3.back * 0f;
        pru.camera.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
        pru.camera.transform.position = new Vector3(0f, 1.2f, 0f) - pru.camera.transform.forward * 10f;
        pru.camera.backgroundColor = new Color(0.72f, 0.69f, 0.58f); pru.camera.clearFlags = CameraClearFlags.SolidColor;
        pru.ambientColor = new Color(0.55f, 0.55f, 0.55f); pru.lights[0].intensity = 1.0f; pru.lights[1].intensity = 0.4f;
        float x = -2.2f;
        foreach (var mats in new[] { cfg.blueMaterials, cfg.redMaterials })
            for (int k = 0; k < 4; k++)
            {
                if (k >= mats.Length) continue;
                var go = Object.Instantiate(melee); pru.AddSingleGO(go);
                go.transform.position = new Vector3(x, 0f, 0f); x += 0.62f;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mats[k];
                var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/KayKit/Animations/Rig_Medium_General.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle_A");
                if (clips != null) clips.SampleAnimation(go, 0.2f);
            }
        pru.BeginPreview(new Rect(0, 0, 1800, 500), GUIStyle.none); pru.camera.Render();
        var tex = pru.EndPreview() as RenderTexture; var prev = RenderTexture.active; RenderTexture.active = tex;
        var t2 = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false); t2.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); t2.Apply(); RenderTexture.active = prev;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "kaykit_iso.png"), t2.EncodeToPNG()); pru.Cleanup();
        return "wrote Temp/kaykit_iso.png";
    }
}

public static class KayKitAnimSheet
{
    public static readonly string[] ClipNames = { "Idle_A", "Melee_Blocking", "Walking_A", "Running_A", "Melee_1H_Attack_Stab", "Melee_1H_Attack_Chop",
        "Melee_1H_Attack_Slice_Diagonal", "Melee_Block_Hit", "Hit_A", "Death_A", "Death_A_Pose", "Cheering" };
    static readonly string[] Fbx = { "General", "CombatMelee", "MovementBasic", "MovementBasic", "CombatMelee", "CombatMelee", "CombatMelee", "CombatMelee", "General", "General", "General", "Simulation" };

    // Reads Temp/prop_offsets.txt: "shield px py pz rx ry rz" and "sword ..." lines (local to the hand slot), so the offsets can be tried without a rebuild.
    public static string Render()
    {
        var offs = new System.Collections.Generic.Dictionary<string, float[]>();
        string op = System.IO.Path.Combine(Application.dataPath, "..", "Temp", "prop_offsets.txt");
        if (System.IO.File.Exists(op))
            foreach (var line in System.IO.File.ReadAllLines(op)) { var p = line.Split(' '); if (p.Length >= 7) offs[p[0]] = p.Skip(1).Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); }
        var cfg = Resources.Load<KayKitVisualConfig>("KayKitVisualConfig");
        var mat = cfg.blueMaterials[0];
        var knight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KayKit/Characters/Knight.fbx");
        var pru = new PreviewRenderUtility();
        pru.camera.orthographic = true; pru.camera.orthographicSize = 1.75f; pru.camera.nearClipPlane = 0.1f; pru.camera.farClipPlane = 40f; pru.camera.aspect = 1f;
        pru.camera.backgroundColor = new Color(0.72f, 0.69f, 0.58f); pru.camera.clearFlags = CameraClearFlags.SolidColor;
        pru.ambientColor = new Color(0.85f, 0.85f, 0.85f); pru.lights[0].intensity = 1.1f; pru.lights[1].intensity = 0.6f;
        var go = (GameObject)Object.Instantiate(knight); pru.AddSingleGO(go);
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name.Contains("Cape") || r.name.Contains("Visor")) { r.gameObject.SetActive(false); continue; }
            r.sharedMaterial = mat;
        }
        System.Action<string, string> attach = (path, slotName) =>
        {
            var prop = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var slot = go.GetComponentsInChildren<Transform>(true).First(t => t.name == slotName);
            prop.transform.SetParent(slot, false);
            string key = slotName == "handslot.l" ? "shield" : "sword";
            var o = offs.ContainsKey(key) ? offs[key] : new float[6];
            prop.transform.localPosition = new Vector3(o[0], o[1], o[2]); prop.transform.localRotation = Quaternion.Euler(o[3], o[4], o[5]);
            foreach (var rr in prop.GetComponentsInChildren<Renderer>(true)) rr.sharedMaterial = mat;
        };
        attach("Assets/ThirdParty/KayKit/Props/sword_1handed.fbx", "handslot.r");
        attach("Assets/ThirdParty/KayKit/Props/shield_square.fbx", "handslot.l");
        var clipMap = new System.Collections.Generic.Dictionary<string, AnimationClip>();
        for (int i = 0; i < ClipNames.Length; i++)
            clipMap[ClipNames[i]] = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/KayKit/Animations/Rig_Medium_" + Fbx[i] + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == ClipNames[i]);
        int cols = 6, tw = 360, th = 360;
        Texture2D sheet = null;
        for (int i = 0; i < ClipNames.Length; i++)
            for (int k = 0; k < 2; k++)
            {
                var clip = clipMap[ClipNames[i]];
                if (clip != null) clip.SampleAnimation(go, clip.length * (k == 0 ? 0.25f : 0.6f));
                var focus = new Vector3(0f, 1.15f, 0f);
                pru.camera.transform.position = focus + Quaternion.Euler(14f, -38f, 0f) * new Vector3(0f, 0f, 8f);   // front-left three-quarter: the shield arm faces the camera
                pru.camera.transform.LookAt(focus);
                pru.BeginPreview(new Rect(0, 0, tw, th), GUIStyle.none); pru.camera.Render();
                var tex = pru.EndPreview() as RenderTexture; var prev = RenderTexture.active; RenderTexture.active = tex;
                if (sheet == null) sheet = new Texture2D(cols * tex.width, 4 * tex.height, TextureFormat.RGB24, false);   // the preview target can be 2x the requested size on a retina screen
                int idx = i * 2 + k; int cx = (idx % cols) * tex.width, cy = (3 - idx / cols) * tex.height;
                sheet.ReadPixels(new Rect(0, 0, tex.width, tex.height), cx, cy); RenderTexture.active = prev;
            }
        sheet.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "anim_sheet.png"), sheet.EncodeToPNG());
        pru.Cleanup();
        return "wrote Temp/anim_sheet.png (12 clips x 2 poses, row-major: " + string.Join(", ", ClipNames) + ")";
    }
}

public static class KayKitShieldVariants
{
    // Walking + slice poses with the shield nudged 0.14 m along each local axis of the hand slot: which direction frees the arm?
    public static string Render()
    {
        var cfg = Resources.Load<KayKitVisualConfig>("KayKitVisualConfig");
        var mat = cfg.blueMaterials[0];
        var knight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KayKit/Characters/Knight.fbx");
        var offsets = new[] { Vector3.zero, Vector3.right * 0.14f, Vector3.left * 0.14f, Vector3.up * 0.14f, Vector3.down * 0.14f, Vector3.forward * 0.14f, Vector3.back * 0.14f };
        var clips = new[] { ("MovementBasic", "Walking_A", 0.25f), ("CombatMelee", "Melee_1H_Attack_Slice_Diagonal", 0.25f), ("CombatMelee", "Melee_Blocking", 0.4f) };
        var pru = new PreviewRenderUtility();
        pru.camera.orthographic = true; pru.camera.orthographicSize = 1.5f; pru.camera.nearClipPlane = 0.1f; pru.camera.farClipPlane = 40f; pru.camera.aspect = 1f;
        pru.camera.backgroundColor = new Color(0.72f, 0.69f, 0.58f); pru.camera.clearFlags = CameraClearFlags.SolidColor;
        pru.ambientColor = new Color(0.9f, 0.9f, 0.9f); pru.lights[0].intensity = 1.1f; pru.lights[1].intensity = 0.6f;
        var go = (GameObject)Object.Instantiate(knight); pru.AddSingleGO(go);
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { if (r.name.Contains("Cape") || r.name.Contains("Visor")) r.gameObject.SetActive(false); else r.sharedMaterial = mat; }
        var slot = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "handslot.l");
        var shield = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KayKit/Props/shield_square.fbx"));
        shield.transform.SetParent(slot, false); foreach (var rr in shield.GetComponentsInChildren<Renderer>(true)) rr.sharedMaterial = mat;
        Texture2D sheet = null;
        for (int row = 0; row < clips.Length; row++)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/KayKit/Animations/Rig_Medium_" + clips[row].Item1 + ".fbx").OfType<AnimationClip>().First(c => c.name == clips[row].Item2);
            for (int col = 0; col < offsets.Length; col++)
            {
                shield.transform.localPosition = offsets[col]; shield.transform.localRotation = Quaternion.identity;
                clip.SampleAnimation(go, clip.length * clips[row].Item3);
                pru.camera.transform.position = new Vector3(0f, 1.15f, 0f) + Quaternion.Euler(12f, -50f, 0f) * new Vector3(0f, 0f, 8f);
                pru.camera.transform.LookAt(new Vector3(0f, 1.15f, 0f));
                pru.BeginPreview(new Rect(0, 0, 300, 300), GUIStyle.none); pru.camera.Render();
                var tex = pru.EndPreview() as RenderTexture; var prev = RenderTexture.active; RenderTexture.active = tex;
                if (sheet == null) sheet = new Texture2D(offsets.Length * tex.width, clips.Length * tex.height, TextureFormat.RGB24, false);
                sheet.ReadPixels(new Rect(0, 0, tex.width, tex.height), col * tex.width, (clips.Length - 1 - row) * tex.height); RenderTexture.active = prev;
            }
        }
        sheet.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "shield_variants.png"), sheet.EncodeToPNG()); pru.Cleanup();
        return "wrote Temp/shield_variants.png: columns = none, +X, -X, +Y, -Y, +Z, -Z; rows = walk, slice, block";
    }
}

public static class KayKitPrefabSheet
{
    // The REAL game prefab (merged mesh, props skinned in, Animator + controller), every state played through the Animator the way the game plays it.
    public static string Render()
    {
        var cfg = Resources.Load<KayKitVisualConfig>("KayKitVisualConfig");
        var prefab = Resources.Load<GameObject>(System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "sheet_archer.txt")) ? "VIS_KayKit_Archer" : "VIS_KayKit_Melee");
        bool archer = prefab.name.Contains("Archer");
        string[] states = archer
            ? new[] { "Ranged_Bow_Aiming_Idle", "Idle_A", "Walking_A", "Running_A", "Ranged_Bow_Draw", "Ranged_Bow_Release", "Hit_A", "Death_A", "Death_A_Pose", "Cheering" }
            : new[] { "Idle_A", "Melee_Blocking", "Walking_A", "Running_A", "Melee_1H_Attack_Stab", "Melee_1H_Attack_Chop", "Melee_1H_Attack_Slice_Diagonal", "Melee_Block_Hit", "Hit_A", "Death_A", "Death_A_Pose", "Cheering" };
        var pru = new PreviewRenderUtility();
        pru.camera.orthographic = true; pru.camera.orthographicSize = 1.7f; pru.camera.nearClipPlane = 0.1f; pru.camera.farClipPlane = 40f; pru.camera.aspect = 1f;
        pru.camera.backgroundColor = new Color(0.72f, 0.69f, 0.58f); pru.camera.clearFlags = CameraClearFlags.SolidColor;
        pru.ambientColor = new Color(0.9f, 0.9f, 0.9f); pru.lights[0].intensity = 1.1f; pru.lights[1].intensity = 0.6f;
        var go = (GameObject)Object.Instantiate(prefab); pru.AddSingleGO(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (!(r.gameObject.name.ToLower().Contains("bow"))) r.sharedMaterial = cfg.blueMaterials[0];
        var anim = go.GetComponent<Animator>(); anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(); smr.updateWhenOffscreen = true;   // editor preview: never cull the skinning
        int cols = 6, rows = Mathf.CeilToInt(states.Length * 2 / (float)cols);
        Texture2D sheet = null; var log = new System.Text.StringBuilder();
        for (int i = 0; i < states.Length; i++)
            for (int k = 0; k < 2; k++)
            {
                anim.Rebind();
                anim.Play(Animator.StringToHash(states[i]), 0, k == 0 ? 0.25f : 0.6f);
                anim.Update(0f);
                anim.Update(0.001f);
                pru.camera.transform.position = new Vector3(0f, 1.1f, 0f) + Quaternion.Euler(12f, -40f, 0f) * new Vector3(0f, 0f, 8f);
                pru.camera.transform.LookAt(new Vector3(0f, 1.1f, 0f));
                pru.BeginPreview(new Rect(0, 0, 360, 360), GUIStyle.none); pru.camera.Render();
                var tex = pru.EndPreview() as RenderTexture; var prev = RenderTexture.active; RenderTexture.active = tex;
                if (sheet == null) sheet = new Texture2D(cols * tex.width, rows * tex.height, TextureFormat.RGB24, false);
                int idx = i * 2 + k; sheet.ReadPixels(new Rect(0, 0, tex.width, tex.height), (idx % cols) * tex.width, (rows - 1 - idx / cols) * tex.height); RenderTexture.active = prev;
            }
        sheet.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Temp", archer ? "prefab_sheet_archer.png" : "prefab_sheet.png"), sheet.EncodeToPNG());
        pru.Cleanup();
        return "wrote prefab sheet (" + (archer ? "archer" : "melee") + "), order: " + string.Join(", ", states);
    }
}

public static class UiFrames
{
    // Sprite import for the drawn gold-bevel UI (ArtSource/UI/make_frames.py). Borders are 9-slice caps in source pixels.
    public static string Import()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (name, l, t, r, b) in new[] { ("UI_Plate", 44, 0, 44, 0), ("UI_PlateBlue", 44, 0, 44, 0), ("UI_PlateRed", 44, 0, 44, 0), ("UI_PlateDanger", 44, 0, 44, 0), ("UI_Pill", 44, 0, 44, 0), ("UI_PlateDisabled", 44, 0, 44, 0), ("UI_Ring", 48, 48, 48, 48), ("UI_Icon_Clock", 0, 0, 0, 0), ("UI_Icon_Restart", 0, 0, 0, 0), ("UI_Icon_Rotate", 0, 0, 0, 0), ("UI_Icon_Chevron", 0, 0, 0, 0), ("UI_Icon_Soldier", 0, 0, 0, 0), ("UI_Row", 32, 0, 32, 0), ("UI_Panel", 64, 64, 64, 64), ("UI_Ribbon", 64, 0, 64, 0) })
        {
            string path = "Assets/Art/UI/Frames/Resources/" + name + ".png";
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.filterMode = FilterMode.Bilinear; ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spriteBorder = new Vector4(l, t, r, b);
            ti.SaveAndReimport();
            sb.AppendLine(name + " border " + l + "/" + t + "/" + r + "/" + b);
        }
        return sb.ToString();
    }
}
