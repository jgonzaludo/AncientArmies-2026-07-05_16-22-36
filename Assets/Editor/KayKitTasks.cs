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
            (melee, cfg.blueMaterial, "Idle_A", 0.3f, -3.0f), (melee, cfg.redMaterial, "Melee_1H_Attack_Stab", 0.45f, -1.0f),
            (archer, cfg.blueMaterial, "Ranged_Bow_Draw", 0.5f, 1.0f), (archer, cfg.redMaterial, "Ranged_Bow_Aiming_Idle", 0.4f, 3.0f) };
        if (CloseUp) specs = new[] { (melee, cfg.blueMaterial, "Idle_A", 0.3f, -2.4f), (melee, cfg.blueMaterial, "Melee_1H_Attack_Stab", 0.45f, 0.0f), (archer, cfg.blueMaterial, "Ranged_Bow_Draw", 0.6f, 2.4f) };
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
