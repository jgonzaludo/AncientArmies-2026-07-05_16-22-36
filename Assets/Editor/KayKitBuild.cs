using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds every generated KayKit asset from the raw FBX/PNG files under
// Assets/ThirdParty/KayKit. Run through the file trigger: task=KayKitBuild.Build
// Output goes to Assets/AncientArmies/Visuals/KayKit (prefabs, materials,
// textures, controllers and the merged soldier mesh live there; Resources/ holds
// what is loaded by name at runtime).
public static class KayKitBuild
{
    const string Raw = "Assets/ThirdParty/KayKit/";
    const string Out = "Assets/AncientArmies/Visuals/KayKit/";
    const string Res = Out + "Resources/";
    const float TargetHeight = 1.9f;
    const float EmissionLevel = 0.34f;

    // clip name -> (source fbx, loops)
    static readonly (string clip, string fbx, bool loop)[] Clips =
    {
        ("Idle_A", "General", true), ("Death_A", "General", false), ("Death_A_Pose", "General", true), ("Hit_A", "General", false),
        ("Melee_Blocking", "CombatMelee", true), ("Melee_Block_Hit", "CombatMelee", false),
        ("Melee_1H_Attack_Stab", "CombatMelee", false), ("Melee_1H_Attack_Chop", "CombatMelee", false), ("Melee_1H_Attack_Slice_Diagonal", "CombatMelee", false),
        ("Ranged_Bow_Aiming_Idle", "CombatRanged", true), ("Ranged_Bow_Draw", "CombatRanged", false), ("Ranged_Bow_Release", "CombatRanged", false),
        ("Walking_A", "MovementBasic", true), ("Running_A", "MovementBasic", true),
        ("Cheering", "Simulation", true),
    };

    public static string Build()
    {
        var log = new StringBuilder();
        try { BuildInner(log); }
        catch (Exception e) { log.AppendLine("EXCEPTION " + e); }
        return log.ToString();
    }

    static void Ensure(string dir)
    {
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }

    static void BuildInner(StringBuilder log)
    {
        foreach (var d in new[] { Out, Out + "Textures", Out + "Materials", Out + "Meshes", Out + "Controllers", Res }) Ensure(d);
        AssetDatabase.Refresh();

        // ---------- 1. importers ----------
        string knightPath = Raw + "Characters/Knight.fbx";
        var ki = (ModelImporter)AssetImporter.GetAtPath(knightPath);
        ki.animationType = ModelImporterAnimationType.Generic;
        ki.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        ki.importAnimation = false;
        ki.isReadable = true;                       // needed while merging; turned off at the end
        ki.materialImportMode = ModelImporterMaterialImportMode.None;
        ki.optimizeGameObjects = false;             // full skeleton kept: the merged mesh binds to real bones
        ki.meshCompression = ModelImporterMeshCompression.Off;
        ki.SaveAndReimport();
        var knightAvatar = AssetDatabase.LoadAllAssetsAtPath(knightPath).OfType<Avatar>().FirstOrDefault();
        log.AppendLine("knight avatar: " + (knightAvatar != null ? knightAvatar.name + " valid=" + knightAvatar.isValid : "MISSING"));

        foreach (var f in Clips.Select(c => c.fbx).Distinct())
        {
            string p = Raw + "Animations/Rig_Medium_" + f + ".fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(p);
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = knightAvatar;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.isReadable = false;
            mi.importBlendShapes = false;
            mi.importCameras = false; mi.importLights = false;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            var defs = mi.defaultClipAnimations;
            var chosen = new List<ModelImporterClipAnimation>();
            foreach (var c in Clips.Where(c => c.fbx == f))
            {
                var d = defs.FirstOrDefault(x => x.name == c.clip);
                if (d == null) { log.AppendLine("MISSING clip " + c.clip + " in " + f); continue; }
                d.loopTime = c.loop; d.loopPose = c.loop;
                d.lockRootRotation = true; d.lockRootHeightY = true; d.lockRootPositionXZ = true;   // root motion off: baked in place
                d.keepOriginalOrientation = true; d.keepOriginalPositionY = true; d.keepOriginalPositionXZ = true;
                chosen.Add(d);
            }
            mi.clipAnimations = chosen.ToArray();
            mi.SaveAndReimport();
        }
        foreach (var f in new[] { "sword_1handed", "shield_square", "bow" })
        {
            var pi = (ModelImporter)AssetImporter.GetAtPath(Raw + "Props/" + f + ".fbx");
            pi.materialImportMode = ModelImporterMaterialImportMode.None;
            pi.isReadable = f != "bow"; pi.importAnimation = false;   // sword + shield stay readable during the build (merged into the body mesh)
            pi.SaveAndReimport();
        }

        // ---------- 2. team textures + materials ----------
        string srcTex = Raw + "Characters/knight_texture.png";
        // shield palette: the coloured (saturated) atlas tiles the shield UVs use are recoloured inside the SAME team atlas,
        // so body + sword + shield share ONE material per team and a melee soldier is a single draw call
        var shieldMesh = AssetDatabase.LoadAssetAtPath<GameObject>(Raw + "Props/shield_square.fbx").GetComponentInChildren<MeshFilter>(true).sharedMesh;
        var shieldTiles = new HashSet<(int, int)>();
        foreach (var uv in shieldMesh.uv) shieldTiles.Add((Mathf.Clamp((int)(uv.x * 8f), 0, 7), Mathf.Clamp((int)((1f - uv.y) * 4f), 0, 3)));
        log.AppendLine("shield atlas tiles: " + string.Join(" ", shieldTiles.Select(t => "(" + t.Item1 + "," + t.Item2 + ")")));
        var blueTex = MakeTeamTexture(srcTex, Out + "Textures/knight_team_blue.png", blue: true, shieldTiles);
        var redTex = MakeTeamTexture(srcTex, Out + "Textures/knight_team_red.png", blue: false, shieldTiles);
        var blueMat = MakeMaterial(Out + "Materials/Mat_KayKit_Blue.mat", blueTex);
        var redMat = MakeMaterial(Out + "Materials/Mat_KayKit_Red.mat", redTex);
        log.AppendLine("team textures (body + sword + shield in one atlas): " + Out + "Textures/knight_team_blue.png , " + Out + "Textures/knight_team_red.png");
        var rangerTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Raw + "Props/ranger_texture.png");
        var bowMat = MakeMaterial(Out + "Materials/Mat_KayKit_Bow.mat", rangerTex);
        log.AppendLine("team textures: " + Out + "Textures/knight_team_blue.png , " + Out + "Textures/knight_team_red.png");

        // ---------- 3. merged mesh + prefabs ----------
        var knightAsset = AssetDatabase.LoadAssetAtPath<GameObject>(knightPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(knightAsset);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.transform.position = Vector3.zero; inst.transform.rotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
        var parts = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToList();
        var keep = parts.Where(p => !p.name.Contains("Cape") && !p.name.Contains("Visor")).ToList();
        var swordMesh = AssetDatabase.LoadAssetAtPath<GameObject>(Raw + "Props/sword_1handed.fbx").GetComponentInChildren<MeshFilter>(true).sharedMesh;
        var propsMelee = new List<(Mesh, Transform)> { (swordMesh, FindByName(inst.transform, "handslot.r")), (shieldMesh, FindByName(inst.transform, "handslot.l")) };
        var mergedMelee = MergeParts(inst.transform, keep, propsMelee, log, out Transform[] bones, out float height);
        var mergedArcher = MergeParts(inst.transform, keep, new List<(Mesh, Transform)>(), new StringBuilder(), out Transform[] _, out float _);
        float scale = TargetHeight / height;
        log.AppendLine($"merged melee mesh (body+sword+shield): tris={mergedMelee.triangles.Length / 3}; archer body: tris={mergedArcher.triangles.Length / 3}; height={height:0.00}m scale={scale:0.000}");
        string meleePath = Out + "Meshes/KayKit_Melee_Merged.asset", archerPath = Out + "Meshes/KayKit_Archer_Merged.asset";
        foreach (var (mesh, mp, nm) in new[] { (mergedMelee, meleePath, "melee"), (mergedArcher, archerPath, "archer") })
        {
            mesh.name = Path.GetFileNameWithoutExtension(mp);
            if (AssetDatabase.LoadAssetAtPath<Mesh>(mp) != null) AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(mesh, mp);
            try
            {
                MeshLodUtility.GenerateMeshLods(mesh, 3);
                var sb = new StringBuilder();
                for (int l = 0; l < mesh.lodCount; l++) sb.Append("LOD" + l + "=" + mesh.GetLod(0, l).indexCount / 3 + " ");
                log.AppendLine(nm + " mesh lods: " + sb);
            }
            catch (Exception e) { log.AppendLine("LOD generation FAILED: " + e.Message); }
            EditorUtility.SetDirty(mesh);
        }
        AssetDatabase.SaveAssets();

        BuildPrefab(inst, parts, mergedMelee, bones, scale, blueMat, bowMat, melee: true, Res + "VIS_KayKit_Melee.prefab", log);
        var inst2 = (GameObject)PrefabUtility.InstantiatePrefab(knightAsset);
        PrefabUtility.UnpackPrefabInstance(inst2, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var parts2 = inst2.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToList();
        var keep2 = parts2.Where(p => !p.name.Contains("Cape") && !p.name.Contains("Visor")).ToList();
        MergeParts(inst2.transform, keep2, new List<(Mesh, Transform)>(), new StringBuilder(), out Transform[] bones2, out float _);
        BuildPrefab(inst2, parts2, mergedArcher, bones2, scale, blueMat, bowMat, melee: false, Res + "VIS_KayKit_Archer.prefab", log);

        // ---------- 4. controllers ----------
        MakeController(Out + "Controllers/AC_KayKit_Melee.controller", melee: true, log);
        MakeController(Out + "Controllers/AC_KayKit_Archer.controller", melee: false, log);
        AssignControllers(log);

        // ---------- 5. config asset ----------
        string cfgPath = Res + "KayKitVisualConfig.asset";
        if (AssetDatabase.LoadAssetAtPath<KayKitVisualConfig>(cfgPath) == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<KayKitVisualConfig>(), cfgPath);
        var cfg = AssetDatabase.LoadAssetAtPath<KayKitVisualConfig>(cfgPath);
        cfg.blueMaterial = blueMat; cfg.redMaterial = redMat; cfg.bowMaterial = bowMat;
        EditorUtility.SetDirty(cfg);

        ki.isReadable = false; ki.SaveAndReimport();
        foreach (var pn in new[] { "shield_square", "sword_1handed" }) { var sh = (ModelImporter)AssetImporter.GetAtPath(Raw + "Props/" + pn + ".fbx"); sh.isReadable = false; sh.SaveAndReimport(); }
        AssetDatabase.SaveAssets();
        log.AppendLine("BUILD OK");
    }

    // ---------- helpers ----------

    static Mesh MergeParts(Transform root, List<SkinnedMeshRenderer> parts, List<(Mesh mesh, Transform slot)> props, StringBuilder log, out Transform[] bones, out float height)
    {
        bones = parts[0].bones;
        var names = bones.Select(b => b.name).ToArray();
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
        var weights = new List<BoneWeight>(); var tris = new List<int>();
        float worstBind = 0f;
        foreach (var part in parts)
        {
            var m = part.sharedMesh;
            if (part.bones.Length != bones.Length || m.subMeshCount != 1) throw new Exception("part " + part.name + " differs (bones " + part.bones.Length + ", submeshes " + m.subMeshCount + ")");
            var map = new int[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                map[i] = Array.IndexOf(names, part.bones[i].name);
                if (map[i] < 0) throw new Exception("bone missing " + part.bones[i].name);
            }
            Matrix4x4 M = root.worldToLocalMatrix * part.transform.localToWorldMatrix;
            var bp = m.bindposes;
            for (int i = 0; i < bones.Length; i++)
            {
                Matrix4x4 expect = part.bones[i].worldToLocalMatrix * part.transform.localToWorldMatrix;
                for (int k = 0; k < 16; k++) worstBind = Mathf.Max(worstBind, Mathf.Abs(expect[k] - bp[i][k]));
            }
            int baseIdx = verts.Count;
            var v = m.vertices; var n = m.normals; var uv = m.uv; var bw = m.boneWeights;
            for (int i = 0; i < v.Length; i++)
            {
                verts.Add(M.MultiplyPoint3x4(v[i])); norms.Add(M.MultiplyVector(n[i]).normalized); uvs.Add(uv[i]);
                var w = bw[i];
                weights.Add(new BoneWeight { boneIndex0 = map[w.boneIndex0], boneIndex1 = map[w.boneIndex1], boneIndex2 = map[w.boneIndex2], boneIndex3 = map[w.boneIndex3], weight0 = w.weight0, weight1 = w.weight1, weight2 = w.weight2, weight3 = w.weight3 });
            }
            foreach (int t in m.triangles) tris.Add(t + baseIdx);
        }
        // props rigidly skinned to their hand slot (slot bone, else its parent hand bone): v_root = slot->root * v_prop
        foreach (var (pm, slot) in props)
        {
            int bi = Array.IndexOf(names, slot.name);
            if (bi < 0) bi = Array.IndexOf(names, slot.parent.name);
            if (bi < 0) throw new Exception("no bone for prop slot " + slot.name);
            Matrix4x4 M = root.worldToLocalMatrix * slot.localToWorldMatrix;
            int baseIdx = verts.Count;
            var v = pm.vertices; var n = pm.normals; var uv = pm.uv;
            for (int i = 0; i < v.Length; i++)
            {
                verts.Add(M.MultiplyPoint3x4(v[i])); norms.Add(M.MultiplyVector(n[i]).normalized); uvs.Add(uv[i]);
                weights.Add(new BoneWeight { boneIndex0 = bi, weight0 = 1f });
            }
            foreach (int t in pm.triangles) tris.Add(t + baseIdx);
        }
        log.AppendLine("bindpose deviation (part bindposes vs derived): " + worstBind.ToString("0.0000"));
        var mesh = new Mesh();
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.boneWeights = weights.ToArray();
        mesh.SetTriangles(tris, 0);
        var bind = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
        mesh.bindposes = bind;
        mesh.RecalculateBounds();
        var b = mesh.bounds;
        height = b.max.y - Mathf.Min(0f, b.min.y);   // feet at the root's y = 0 in the rest pose
        log.AppendLine("merged bounds " + b.size + " minY=" + b.min.y.ToString("0.00") + " maxY=" + b.max.y.ToString("0.00"));
        return mesh;
    }

    static void BuildPrefab(GameObject inst, List<SkinnedMeshRenderer> allParts, Mesh merged, Transform[] bones, float scale,
                            Material teamMat, Material bowMat, bool melee, string path, StringBuilder log)
    {
        inst.name = melee ? "VIS_KayKit_Melee" : "VIS_KayKit_Archer";
        // reuse the first kept part as THE renderer; destroy the rest (their GameObjects are plain children after unpack)
        SkinnedMeshRenderer body = null;
        foreach (var p in allParts)
        {
            if (body == null && !p.name.Contains("Cape") && !p.name.Contains("Visor")) { body = p; continue; }
            UnityEngine.Object.DestroyImmediate(p.gameObject);
        }
        body.gameObject.name = "SoldierBody";
        body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity; body.transform.localScale = Vector3.one;
        body.sharedMesh = merged; body.bones = bones; body.rootBone = bones[0].root == inst.transform ? FindByName(inst.transform, "root") : bones[0];
        body.sharedMaterial = teamMat;
        body.quality = SkinQuality.Bone2;
        body.updateWhenOffscreen = false;
        body.skinnedMotionVectors = false;
        body.allowOcclusionWhenDynamic = true;
        body.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        body.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        var handR = FindByName(inst.transform, "handslot.r");
        var handL = FindByName(inst.transform, "handslot.l");
        if (!melee) Attach(Raw + "Props/bow.fbx", handL, bowMat);   // bow in the left hand, string hand is the right

        var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullCompletely;
        inst.transform.localScale = Vector3.one * scale;

        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        PrefabUtility.SaveAsPrefabAsset(inst, path);
        UnityEngine.Object.DestroyImmediate(inst);
        log.AppendLine("prefab " + path + " scale=" + scale.ToString("0.000"));
    }

    static void Attach(string fbx, Transform slot, Material mat)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.SetParent(slot, false);
        go.transform.localPosition = Vector3.zero;
        // the bow's limbs must curve toward the archer: turned half a revolution about its vertical axis
        go.transform.localRotation = fbx.Contains("/bow") ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
    }

    public static Transform FindByName(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static Texture2D MakeTeamTexture(string srcPath, string dstPath, bool blue, HashSet<(int, int)> shieldTiles)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(srcPath));
        int W = tex.width, H = tex.height, tw = W / 8, th = H / 4;
        var px = tex.GetPixels();
        // Team palette: the two saturated-red tiles go blue (blue team) / stay red; the dark
        // slate cloth+armour tiles take the team hue so a century reads blue or red at a glance.
        // Skin, leather, gold trim and bright steel are left alone.
        float teamHue = blue ? 0.60f : 0.99f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color col = px[y * W + x];
                Color.RGBToHSV(col, out float h, out float sat, out float v);
                int c = x / tw, r = (H - 1 - y) / th;   // tile column, row from the top
                if (shieldTiles.Contains((c, r)) && sat > 0.30f && !((c == 0 && r == 1) || (c == 2 && r == 2)))
                { px[y * W + x] = Color.HSVToRGB(blue ? 0.60f : 0.99f, Mathf.Clamp01(Mathf.Max(sat, 0.65f)), Mathf.Clamp01(v * 1.15f)); continue; }
                bool redTile = (c == 0 && r == 1) || (c == 2 && r == 2);
                if (redTile)
                { if (blue) px[y * W + x] = Color.HSVToRGB(0.60f, Mathf.Clamp01(sat * 0.95f), v); continue; }
                bool darkCloth = sat < 0.40f && v >= 0.10f && v <= 0.55f;   // slate / near-black greys
                if (darkCloth)
                    px[y * W + x] = Color.HSVToRGB(teamHue, blue ? 0.62f : 0.72f, Mathf.Clamp01(v * (blue ? 1.45f : 1.2f) + 0.06f));
            }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(dstPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(dstPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(dstPath);
        ti.mipmapEnabled = true; ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Compressed;
        ti.maxTextureSize = 512;   // a gradient palette atlas: half resolution loses nothing
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(dstPath);
    }

    static Texture2D MakeShieldTexture(string srcPath, string dstPath, bool blue, HashSet<(int, int)> tiles)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(srcPath));
        int W = tex.width, H = tex.height, tw = W / 8, th = H / 4;
        var px = tex.GetPixels();
        foreach (var (c, r) in tiles)
        {
            int x0 = c * tw, y0 = H - (r + 1) * th;
            // only coloured tiles change; grey steel rims stay steel
            Color.RGBToHSV(px[(y0 + th / 2) * W + x0 + tw / 2], out float _, out float midSat, out float _);
            if (midSat < 0.30f) continue;
            for (int y = y0; y < y0 + th; y++)
                for (int x = x0; x < x0 + tw; x++)
                {
                    Color.RGBToHSV(px[y * W + x], out float h, out float sat, out float v);
                    px[y * W + x] = Color.HSVToRGB(blue ? 0.60f : 0.99f, Mathf.Clamp01(Mathf.Max(sat, 0.65f)), Mathf.Clamp01(v * 1.15f));
                }
        }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(dstPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(dstPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(dstPath);
        ti.mipmapEnabled = true; ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Compressed; ti.maxTextureSize = 512;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(dstPath);
    }

    static Material MakeMaterial(string path, Texture2D tex)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.1f); m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        // the Battle scene's ambient is dark (0.21/0.23/0.26), so the shaded side of a soldier would go
        // near-black: a soft self-light from the same texture keeps team colours readable there
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", new Color(EmissionLevel, EmissionLevel, EmissionLevel));
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.enableInstancing = false;   // SRP Batcher path
        EditorUtility.SetDirty(m);
        return m;
    }

    static AnimationClip Clip(string name)
    {
        foreach (var c in Clips.Where(x => x.clip == name))
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Raw + "Animations/Rig_Medium_" + c.fbx + ".fbx").OfType<AnimationClip>())
                if (a.name == name) return a;
        return null;
    }

    static void MakeController(string path, bool melee, StringBuilder log)
    {
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sm = ac.layers[0].stateMachine;
        string[] names = melee
            ? new[] { "Idle_A", "Melee_Blocking", "Walking_A", "Running_A", "Melee_1H_Attack_Stab", "Melee_1H_Attack_Chop", "Melee_1H_Attack_Slice_Diagonal", "Melee_Block_Hit", "Hit_A", "Death_A", "Death_A_Pose", "Cheering" }
            : new[] { "Ranged_Bow_Aiming_Idle", "Idle_A", "Walking_A", "Running_A", "Ranged_Bow_Draw", "Ranged_Bow_Release", "Hit_A", "Death_A", "Death_A_Pose", "Cheering" };
        AnimatorState first = null;
        foreach (var n in names)
        {
            var clip = Clip(n);
            if (clip == null) { log.AppendLine("controller: clip missing " + n); continue; }
            var st = sm.AddState(n);
            st.motion = clip;
            st.writeDefaultValues = true;
            if (n == "Walking_A" || n == "Running_A") { st.speedParameterActive = true; st.speedParameter = "Speed"; }
            if (first == null) first = st;
        }
        sm.defaultState = first;
        EditorUtility.SetDirty(ac);
    }

    static void AssignControllers(StringBuilder log)
    {
        foreach (var (prefab, ctl) in new[] { (Res + "VIS_KayKit_Melee.prefab", "AC_KayKit_Melee"), (Res + "VIS_KayKit_Archer.prefab", "AC_KayKit_Archer") })
        {
            var root = PrefabUtility.LoadPrefabContents(prefab);
            var anim = root.GetComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Out + "Controllers/" + ctl + ".controller");
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
