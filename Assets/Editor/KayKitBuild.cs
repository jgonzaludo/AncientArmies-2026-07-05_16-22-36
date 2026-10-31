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
    const float ShieldOutward = 0.15f;
    const float CapMinYFrac = 0.66f;       // helmet team cap: triangles above this share of the helmet's height take the team colour
    const float StripeHalfWidth = 0.065f;  // helmet team stripe: half-width in model units (the Knight is 2.4 units tall before scaling)
    const float StripeLift = 0.012f;       // lifted off the dome along its normal so it never z-fights
    const float StripeMinYFrac = 0.35f;    // ...and only above this share of the helmet's height

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
        foreach (var f in new[] { "sword_1handed", "shield_square", "bow", "arrow_bow" })
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
        var blueMats = new Material[SkinTones.Length]; var redMats = new Material[SkinTones.Length];
        for (int k = 0; k < SkinTones.Length; k++)
        {
            var bt = MakeTeamTexture(srcTex, Out + "Textures/knight_blue_" + SkinNames[k] + ".png", true, k, shieldTiles);
            var rt = MakeTeamTexture(srcTex, Out + "Textures/knight_red_" + SkinNames[k] + ".png", false, k, shieldTiles);
            blueMats[k] = MakeMaterial(Out + "Materials/Mat_KayKit_Blue_" + SkinNames[k] + ".mat", bt);
            redMats[k] = MakeMaterial(Out + "Materials/Mat_KayKit_Red_" + SkinNames[k] + ".mat", rt);
        }
        var blueMat = blueMats[0]; var redMat = redMats[0];
        log.AppendLine("team x skin textures: " + Out + "Textures/knight_{blue|red}_{default|olive|tan|brown}.png (armour-only team colour)");
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

        // ---------- 3b. arrow prefab (Projectile loads it by name) ----------
        {
            var ag = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Raw + "Props/arrow_bow.fbx"));
            PrefabUtility.UnpackPrefabInstance(ag, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var rr = ag.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rr) { r.sharedMaterial = bowMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off; }
            var ab = rr[0].bounds;
            // the Projectile root flies along +Z: turn the model's long axis onto +Z (tip direction is checked in the preview)
            var holder = new GameObject("VIS_KayKit_Arrow");
            ag.transform.SetParent(holder.transform, false);
            int axis = ab.size.x >= ab.size.y && ab.size.x >= ab.size.z ? 0 : (ab.size.y >= ab.size.z ? 1 : 2);
            ag.transform.localRotation = axis == 0 ? Quaternion.Euler(0f, -90f, 0f) : axis == 1 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            if (File.Exists(Res + "VIS_KayKit_Arrow.prefab")) AssetDatabase.DeleteAsset(Res + "VIS_KayKit_Arrow.prefab");
            PrefabUtility.SaveAsPrefabAsset(holder, Res + "VIS_KayKit_Arrow.prefab");
            UnityEngine.Object.DestroyImmediate(holder);
            log.AppendLine("arrow prefab: size=" + ab.size + " longAxis=" + axis);
        }

        // ---------- 4. controllers ----------
        MakeController(Out + "Controllers/AC_KayKit_Melee.controller", melee: true, log);
        MakeController(Out + "Controllers/AC_KayKit_Archer.controller", melee: false, log);
        AssignControllers(log);

        // ---------- 5. config asset ----------
        string cfgPath = Res + "KayKitVisualConfig.asset";
        if (AssetDatabase.LoadAssetAtPath<KayKitVisualConfig>(cfgPath) == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<KayKitVisualConfig>(), cfgPath);
        var cfg = AssetDatabase.LoadAssetAtPath<KayKitVisualConfig>(cfgPath);
        cfg.blueMaterials = blueMats; cfg.redMaterials = redMats; cfg.bowMaterial = bowMat;
        EditorUtility.SetDirty(cfg);

        ki.isReadable = false; ki.SaveAndReimport();
        foreach (var pn in new[] { "shield_square", "sword_1handed" }) { var sh = (ModelImporter)AssetImporter.GetAtPath(Raw + "Props/" + pn + ".fbx"); sh.isReadable = false; sh.SaveAndReimport(); }
        AssetDatabase.SaveAssets();
        log.AppendLine("BUILD OK");
    }

    // ---------- helpers ----------

    // A clean stripe on the helmet: sample the dome's top surface along x = +-StripeHalfWidth for z from brow to nape, lift each sample a
    // hair along the surface normal and join them into a ribbon (both windings), UV on the team armour tile so it takes the team colour.
    static void AddHelmetStripe(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights, List<int> tris,
                                int baseIdx, int vcount, StringBuilder log)
    {
        float minZ = float.MaxValue, maxZ = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < vcount; i++) { var q = verts[baseIdx + i]; minZ = Mathf.Min(minZ, q.z); maxZ = Mathf.Max(maxZ, q.z); minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y); }
        float yMin = minY + StripeMinYFrac * (maxY - minY);
        Vector2 stripeUv = new Vector2((3 + 0.5f) / 8f, 1f - 0.5f / 4f);   // middle of the team-tinted steel tile (3,0)
        const int cols = 5, steps = 18;                       // 5 points across so the ribbon follows the dome's curve instead of cutting a chord through it
        var rowsIdx = new List<int[]>();
        for (int k = 0; k <= steps; k++)
        {
            float z = Mathf.Lerp(minZ + 0.08f * (maxZ - minZ), maxZ - 0.04f * (maxZ - minZ), k / (float)steps);
            var row = new int[cols]; bool ok = true;
            var hits = new SurfacePoint[cols];
            for (int j = 0; j < cols && ok; j++)
            {
                hits[j] = SurfaceHit(verts, tris, Mathf.Lerp(-StripeHalfWidth, StripeHalfWidth, j / (float)(cols - 1)), z);
                if (!hits[j].ok || hits[j].p.y < yMin) ok = false;
            }
            if (!ok) continue;
            for (int j = 0; j < cols; j++) row[j] = AddStripeVertex(verts, norms, uvs, weights, baseIdx, vcount, hits[j], stripeUv);
            rowsIdx.Add(row);
        }
        for (int i = 0; i + 1 < rowsIdx.Count; i++)
            for (int j = 0; j + 1 < cols; j++)
            {
                int a = rowsIdx[i][j], b = rowsIdx[i][j + 1], c = rowsIdx[i + 1][j], d = rowsIdx[i + 1][j + 1];
                tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c);   // one winding
                tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);   // and the other, so it shows from whichever side
            }
        int stripeSamples = rowsIdx.Count;
        log.AppendLine("helmet stripe: " + stripeSamples + " rows, width " + (2 * StripeHalfWidth).ToString("0.00") + " m");
    }

    struct SurfacePoint { public bool ok; public Vector3 p, n; }
    static SurfacePoint SurfaceHit(List<Vector3> verts, List<int> tri, float x, float z)   // over every triangle merged so far: the head and the helmet
    
    {
        var best = new SurfacePoint(); float bestY = float.MinValue;
        for (int t = 0; t + 2 < tri.Count; t += 3)
        {
            Vector3 a = verts[tri[t]], b = verts[tri[t + 1]], c = verts[tri[t + 2]];
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 1e-8f) continue;
            float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d, w = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d, vv = 1f - u - w;
            if (u < 0f || w < 0f || vv < 0f) continue;
            float y = u * a.y + w * b.y + vv * c.y;
            if (y > bestY)
            {
                bestY = y; var n = Vector3.Cross(b - a, c - a).normalized; if (n.y < 0f) n = -n;
                best = new SurfacePoint { ok = true, p = new Vector3(x, y, z), n = n };
            }
        }
        return best;
    }

    static int AddStripeVertex(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights, int baseIdx, int vcount, SurfacePoint sp, Vector2 uv)
    {
        int nearest = 0; float nd = float.MaxValue;
        for (int i = 0; i < vcount; i++) { float d = (verts[baseIdx + i] - sp.p).sqrMagnitude; if (d < nd) { nd = d; nearest = i; } }
        verts.Add(sp.p + sp.n * StripeLift); norms.Add(sp.n); uvs.Add(uv); weights.Add(weights[baseIdx + nearest]);
        return verts.Count - 1;
    }

    static Vector2 NaturalSteelUV(Vector2 uv)
    {
        int c = Mathf.Clamp((int)(uv.x * 8f), 0, 7), r = Mathf.Clamp((int)((1f - uv.y) * 4f), 0, 3);
        if (!SteelCopies.TryGetValue((c, r), out var to)) return uv;
        return new Vector2(uv.x + (to.Item1 - c) / 8f, uv.y - (to.Item2 - r) / 4f);
    }

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
                verts.Add(M.MultiplyPoint3x4(v[i])); norms.Add(M.MultiplyVector(n[i]).normalized); uvs.Add(part.name.Contains("Helmet") ? NaturalSteelUV(uv[i]) : uv[i]);
                var w = bw[i];
                weights.Add(new BoneWeight { boneIndex0 = map[w.boneIndex0], boneIndex1 = map[w.boneIndex1], boneIndex2 = map[w.boneIndex2], boneIndex3 = map[w.boneIndex3], weight0 = w.weight0, weight1 = w.weight1, weight2 = w.weight2, weight3 = w.weight3 });
            }
            if (part.name.Contains("Helmet"))
            {
                // team cap: the helmet triangles above CapMinYFrac of its height keep the ORIGINAL uv - the team-coloured armour tile - on duplicated
                // vertices; the rest use the natural-steel copy. The cap's lower edge follows a ring of the helmet's own facets, so it reads clean
                // from the game camera (a thin centre stripe was either jagged or buried in the dome).
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < v.Length; i++) { var q = verts[baseIdx + i]; minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y); }
                float cut = minY + CapMinYFrac * (maxY - minY);
                var tri = m.triangles; int capped = 0;
                for (int t = 0; t < tri.Length; t += 3)
                {
                    float cy = (verts[baseIdx + tri[t]].y + verts[baseIdx + tri[t + 1]].y + verts[baseIdx + tri[t + 2]].y) / 3f;
                    if (cy > cut)
                    {
                        capped++;
                        for (int k = 0; k < 3; k++)
                        {
                            int src = tri[t + k];
                            verts.Add(verts[baseIdx + src]); norms.Add(norms[baseIdx + src]); uvs.Add(uv[src]); weights.Add(weights[baseIdx + src]);
                            tris.Add(verts.Count - 1);
                        }
                    }
                    else { tris.Add(tri[t] + baseIdx); tris.Add(tri[t + 1] + baseIdx); tris.Add(tri[t + 2] + baseIdx); }
                }
                log.AppendLine("helmet team cap: " + capped + " of " + tri.Length / 3 + " triangles above y=" + cut.ToString("0.00"));
            }
            else foreach (int t in m.triangles) tris.Add(t + baseIdx);
        }
        // props rigidly skinned to their hand slot (slot bone, else its parent hand bone): v_root = slot->root * v_prop
        foreach (var (pm, slot) in props)
        {
            int bi = Array.IndexOf(names, slot.name);
            if (bi < 0) bi = Array.IndexOf(names, slot.parent.name);
            if (bi < 0) throw new Exception("no bone for prop slot " + slot.name);
            Matrix4x4 M = root.worldToLocalMatrix * slot.localToWorldMatrix;
            // the shield slab is centred on the hand slot, which buries the forearm armour in it (38% of the arm vertices across the
            // animation set sat inside it, measured): slide it 0.15 m out along the slot's Z so the arm is behind its back face
            if (slot.name == "handslot.l") M = M * Matrix4x4.Translate(new Vector3(0f, 0f, ShieldOutward));
            int baseIdx = verts.Count;
            var v = pm.vertices; var n = pm.normals; var uv = pm.uv;
            for (int i = 0; i < v.Length; i++)
            {
                verts.Add(M.MultiplyPoint3x4(v[i])); norms.Add(M.MultiplyVector(n[i]).normalized); uvs.Add(slot.name == "handslot.r" ? NaturalSteelUV(uv[i]) : uv[i]);   // the sword keeps natural steel
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

    // Natural-steel copies: the helmet and sword sample steel tiles that the body armour shares, so their UVs are moved
    // onto spare, untinted copies of those tiles (bottom row of the atlas, unused by the soldier).
    public static readonly Dictionary<(int, int), (int, int)> SteelCopies = new Dictionary<(int, int), (int, int)>
    { { (3, 0), (5, 3) }, { (7, 0), (6, 3) }, { (7, 1), (7, 3) }, { (4, 0), (4, 3) }, { (2, 1), (3, 3) } };

    // Four skin tones: face tile (0,0) and its shade tile (1,0). Index 0 is the KayKit default.
    static readonly (Color face, Color shade)[] SkinTones =
    {
        (new Color32(0xF6, 0xC0, 0x9C, 255), new Color32(0xDA, 0xAE, 0x7D, 255)),   // default
        (new Color32(0xE8, 0xB8, 0x8A, 255), new Color32(0xCB, 0x9A, 0x68, 255)),   // olive
        (new Color32(0xD9, 0xA0, 0x70, 255), new Color32(0xB5, 0x79, 0x4A, 255)),   // tan
        (new Color32(0xBD, 0x81, 0x56, 255), new Color32(0x98, 0x60, 0x3A, 255)),   // brown
    };
    static readonly Color[] BrowColors =
    {
        new Color32(0x5B, 0x3F, 0x26, 255),   // default skin: dark brown
        new Color32(0x2F, 0x22, 0x18, 255),   // olive: black-brown
        new Color32(0x4A, 0x2F, 0x1C, 255),   // tan: dark chestnut
        new Color32(0x1F, 0x17, 0x12, 255),   // brown: near black
    };
    static readonly string[] SkinNames = { "default", "olive", "tan", "brown" };

    static void ScaleTile(Color[] px, int W, int H, int c, int r, Color target)
    {
        int tw = W / 8, th = H / 4, x0 = c * tw, y0 = H - (r + 1) * th;
        Color mid = px[(y0 + th / 2) * W + x0 + tw / 2];
        Vector3 k = new Vector3(mid.r > 0.001f ? target.r / mid.r : 1f, mid.g > 0.001f ? target.g / mid.g : 1f, mid.b > 0.001f ? target.b / mid.b : 1f);
        for (int y = y0; y < y0 + th; y++)
            for (int x = x0; x < x0 + tw; x++)
            {
                Color p = px[y * W + x];
                px[y * W + x] = new Color(Mathf.Clamp01(p.r * k.x), Mathf.Clamp01(p.g * k.y), Mathf.Clamp01(p.b * k.z), 1f);
            }
    }

    static Texture2D MakeTeamTexture(string srcPath, string dstPath, bool blue, int skin, HashSet<(int, int)> shieldTiles)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(srcPath));
        int W = tex.width, H = tex.height, tw = W / 8, th = H / 4;
        var px = tex.GetPixels();
        var orig = (Color[])px.Clone();
        // 1. natural-steel copies into the spare tiles (before any tinting)
        foreach (var kv in SteelCopies)
        {
            int sx = kv.Key.Item1 * tw, sy = H - (kv.Key.Item2 + 1) * th, dx = kv.Value.Item1 * tw, dy = H - (kv.Value.Item2 + 1) * th;
            for (int y = 0; y < th; y++) for (int x = 0; x < tw; x++) px[(dy + y) * W + dx + x] = orig[(sy + y) * W + sx + x];
        }
        // 2. skin: only the two face tiles change, to the chosen tone (the eyes, leather and trim stay as they are)
        if (skin > 0) ScaleTile(px, W, H, 0, 0, SkinTones[skin].face);
        // eyebrows (and the hair under the helmet) sample tile (1,0): a darker brow colour per tone, all darker than KayKit's pale golden default
        ScaleTile(px, W, H, 1, 0, BrowColors[skin]);
        // 3. armour only: steel plates and the tabard take the team colour; everything else is natural
        float hue = blue ? 0.575f : 0.005f;            // blue aims a little under the banner hue (lighting pushes it toward indigo)
        float armourSat = blue ? 0.70f : 0.78f;
        var steel = new HashSet<(int, int)> { (3, 0), (7, 0), (2, 1) };
        var darkSteel = new HashSet<(int, int)> { (7, 1), (4, 0) };
        var tabard = new HashSet<(int, int)> { (0, 1), (2, 2) };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int c = x / tw, r = (H - 1 - y) / th;
                var t = (c, r);
                Color col = px[y * W + x];
                Color.RGBToHSV(col, out float h, out float sat, out float v);
                if (steel.Contains(t)) px[y * W + x] = Color.HSVToRGB(hue, armourSat, Mathf.Clamp01(v * 1.15f + 0.05f));
                else if (darkSteel.Contains(t)) px[y * W + x] = Color.HSVToRGB(hue, armourSat, Mathf.Clamp01(v * 1.15f + 0.05f));
                else if (tabard.Contains(t)) px[y * W + x] = blue ? Color.HSVToRGB(0.575f, Mathf.Clamp01(sat * 0.95f), Mathf.Clamp01(v * 1.05f))
                                                                  : Color.HSVToRGB(h, Mathf.Clamp01(sat * 0.95f), Mathf.Clamp01(v * 1.2f));
                else if (c == 4 && r == 1) px[y * W + x] = Color.HSVToRGB(0.115f, Mathf.Lerp(sat, 0.62f, 0.7f), v);   // banner gold trim
                else if (shieldTiles.Contains(t) && sat > 0.30f)
                    px[y * W + x] = Color.HSVToRGB(blue ? 0.575f : 0.005f, Mathf.Clamp01(Mathf.Max(sat, 0.65f)), Mathf.Clamp01(v * 1.15f));
            }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(dstPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(dstPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(dstPath);
        ti.mipmapEnabled = true; ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Compressed;
        ti.maxTextureSize = 512;
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
