using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Idempotent centurion asset integration that runs on domain reload (mirrors
// Patch6Integration). Once the art worker's FBX lands it:
//   1. Enforces importer conventions copied from the legionary FBX (Generic
//      rig, own avatar, same scale/axis/material handling) and non-looping
//      command clips.
//   2. Builds AC_RomanCenturion as a copy of AC_RomanInfantry plus a masked
//      upper-body "Command" layer with the three command-gesture states.
//   3. Builds the VIS_Roman_Centurion prefab in a Resources folder (same
//      mechanism as VIS_Roman_Legionary_Basic) wiring the animator,
//      RomanLegionaryVisualController and CenturionCommandGestures.
// Every step no-ops once done; results land in Logs/centurion_integration.log
// so headless tooling can verify without the console. With no FBX the run is
// a single log line — the game falls back to the legionary visual.
[InitializeOnLoad]
public static class CenturionAssetIntegration
{
    private const string FbxPath =
        "Assets/Art/Characters/Romans/Centurion/Models/RomanCenturion.fbx";
    private const string LegionaryFbxPath =
        "Assets/Art/Characters/Romans/Legionary/Models/RomanLegionary_Basic.fbx";
    private const string LegionaryControllerPath =
        "Assets/Art/Characters/Romans/Legionary/Animations/AC_RomanInfantry.controller";
    private const string AnimationsFolder =
        "Assets/Art/Characters/Romans/Centurion/Animations";
    private const string ControllerPath = AnimationsFolder + "/AC_RomanCenturion.controller";
    private const string MaskPath = AnimationsFolder + "/MASK_UpperBody.mask";
    private const string PrefabFolder =
        "Assets/Art/Characters/Romans/Centurion/Prefabs";
    private const string ResourcesFolder = PrefabFolder + "/Resources";
    private const string PrefabPath = ResourcesFolder + "/VIS_Roman_Centurion.prefab";
    private const string LogPath = "Logs/centurion_integration.log";

    private const string ClipMove = "ANIM_Roman_Centurion_CommandMove";
    private const string ClipCharge = "ANIM_Roman_Centurion_CommandCharge";
    private const string ClipReform = "ANIM_Roman_Centurion_CommandReform";
    private static readonly string[] CommandClips = { ClipMove, ClipCharge, ClipReform };
    private static readonly string[] CommandTriggers =
        { "CommandMove", "CommandCharge", "CommandReform" };

    static CenturionAssetIntegration()
    {
        EditorApplication.delayCall += RunOnce;
    }

    [MenuItem("Ancient Armies/Run Centurion Asset Integration")]
    public static void RunOnce()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Centurion integration {System.DateTime.Now:HH:mm:ss} ===");
        try
        {
            if (!File.Exists(FbxPath))
            {
                sb.AppendLine("centurion FBX not present — nothing to do");
            }
            else
            {
                // SaveAndReimport is synchronous, so the whole chain completes
                // in one pass even on the run that first configures the FBX.
                EnsureImporter(sb);
                AvatarMask mask = EnsureMask(sb);
                AnimatorController ctrl = EnsureController(sb, mask);
                if (ctrl != null) EnsurePrefab(sb, ctrl);
                AssetDatabase.SaveAssets();
            }
        }
        catch (System.Exception e)
        {
            sb.AppendLine("EXCEPTION: " + e);
        }
        File.AppendAllText(LogPath, sb.ToString());
    }

    // ---------------- importer ----------------

    // Copy the conventions that make the centurion interchangeable with the
    // legionary: Generic rig with its own avatar, identical scale/axis
    // handling, identical material resolution (name-remap into the shared
    // MAT_Roman_* set — the cloth team-swap matches on material name).
    private static void EnsureImporter(System.Text.StringBuilder sb)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        var leg = (ModelImporter)AssetImporter.GetAtPath(LegionaryFbxPath);
        if (imp == null || leg == null)
        {
            sb.AppendLine("MISSING importer (centurion or legionary)");
            return;
        }
        bool dirty = false;
        if (imp.animationType != leg.animationType)
        { imp.animationType = leg.animationType; dirty = true; }
        if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
        if (!Mathf.Approximately(imp.globalScale, leg.globalScale))
        { imp.globalScale = leg.globalScale; dirty = true; }
        if (imp.useFileScale != leg.useFileScale)
        { imp.useFileScale = leg.useFileScale; dirty = true; }
        if (imp.bakeAxisConversion != leg.bakeAxisConversion)
        { imp.bakeAxisConversion = leg.bakeAxisConversion; dirty = true; }
        if (imp.materialImportMode != leg.materialImportMode)
        { imp.materialImportMode = leg.materialImportMode; dirty = true; }
        if (imp.materialLocation != leg.materialLocation)
        { imp.materialLocation = leg.materialLocation; dirty = true; }

        // Command gestures are one-shots; loops would leave him waving forever.
        var clips = imp.clipAnimations;
        if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
        bool clipsDirty = false;
        foreach (var c in clips)
            if (CommandClips.Contains(c.name) && c.loopTime)
            { c.loopTime = false; clipsDirty = true; }
        if (clipsDirty) { imp.clipAnimations = clips; dirty = true; }

        if (dirty)
        {
            // remap by name into the shared palette exactly like the legionary
            imp.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName,
                                        ModelImporterMaterialSearch.Everywhere);
            imp.SaveAndReimport();
            sb.AppendLine("importer ENFORCED (legionary conventions) + reimported");
        }
        else sb.AppendLine("importer settings verified");
    }

    // ---------------- avatar mask ----------------

    // Generic rig => no humanoid mask API; the transform-path API is built
    // from the model's own hierarchy instead. Spine/chest/arms/head chains
    // stay active so the gesture plays over locomotion; legs, hips and the
    // root stay with the base layer so the walk is never stomped.
    private static AvatarMask EnsureMask(System.Text.StringBuilder sb)
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask != null) { sb.AppendLine("mask present"); return mask; }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null) { sb.AppendLine("MISSING model for mask"); return null; }
        EnsureFolder(AnimationsFolder);
        mask = new AvatarMask();
        mask.AddTransformPath(model.transform, true);
        int active = 0;
        for (int i = 0; i < mask.transformCount; i++)
        {
            bool on = IsUpperBodyPath(mask.GetTransformPath(i));
            mask.SetTransformActive(i, on);
            if (on) active++;
        }
        AssetDatabase.CreateAsset(mask, MaskPath);
        sb.AppendLine($"mask CREATED ({active}/{mask.transformCount} paths active)");
        return mask;
    }

    private static readonly string[] LowerBodyKeywords =
        { "leg", "thigh", "shin", "calf", "knee", "foot", "toe", "heel", "ankle" };

    private static bool IsUpperBodyPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;   // model root: base layer's
        string[] segs = path.ToLowerInvariant().Split('/');
        foreach (var seg in segs)
            foreach (var k in LowerBodyKeywords)
                if (seg.Contains(k)) return false;      // whole leg chains
        string leaf = segs[segs.Length - 1];
        if (leaf.Contains("hips") || leaf.Contains("pelvis") || leaf.Contains("root"))
            return false;                               // pelvis/root: locomotion owns them
        return true;
    }

    // ---------------- controller ----------------

    // AC_RomanCenturion = full copy of AC_RomanInfantry (an override controller
    // cannot add the Command layer) + three triggers + the masked Command
    // layer: empty default state, one state per gesture entered on its trigger
    // (no exit time, 0.1 blend) and exiting back at clip end (exit time 1.0,
    // 0.1 blend). Never Any State — a gesture must not restart itself.
    private static AnimatorController EnsureController(System.Text.StringBuilder sb,
                                                       AvatarMask mask)
    {
        if (!File.Exists(ControllerPath))
        {
            EnsureFolder(AnimationsFolder);
            if (!AssetDatabase.CopyAsset(LegionaryControllerPath, ControllerPath))
            {
                sb.AppendLine("FAILED to copy AC_RomanInfantry");
                return null;
            }
            sb.AppendLine("controller COPIED from AC_RomanInfantry");
        }
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { sb.AppendLine("no controller"); return null; }

        bool dirty = false;
        foreach (var trigger in CommandTriggers)
        {
            if (ctrl.parameters.Any(p => p.name == trigger)) continue;
            ctrl.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            dirty = true;
            sb.AppendLine($"parameter ADDED: {trigger}");
        }

        if (ctrl.layers.Any(l => l.name == "Command"))
        {
            sb.AppendLine("Command layer present");
        }
        else
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToArray();
            var move = clips.FirstOrDefault(c => c.name == ClipMove);
            var charge = clips.FirstOrDefault(c => c.name == ClipCharge);
            var reform = clips.FirstOrDefault(c => c.name == ClipReform);
            if (move == null || charge == null || reform == null)
            {
                sb.AppendLine($"MISSING command clips ({clips.Length} clips in FBX) — layer deferred");
            }
            else
            {
                var sm = new AnimatorStateMachine
                {
                    name = "Command",
                    hideFlags = HideFlags.HideInHierarchy
                };
                // persist the machine first so AddState lands in the same asset
                AssetDatabase.AddObjectToAsset(sm, ctrl);
                var idle = sm.AddState("CommandIdle", new Vector3(0f, 0f, 0f));
                sm.defaultState = idle;
                AddCommandState(sm, idle, "CommandMove", move, new Vector3(300f, -80f, 0f));
                AddCommandState(sm, idle, "CommandCharge", charge, new Vector3(300f, 0f, 0f));
                AddCommandState(sm, idle, "CommandReform", reform, new Vector3(300f, 80f, 0f));
                ctrl.AddLayer(new AnimatorControllerLayer
                {
                    name = "Command",
                    defaultWeight = 1f,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    avatarMask = mask,
                    stateMachine = sm
                });
                dirty = true;
                sb.AppendLine("Command layer ADDED (3 states)");
            }
        }
        if (dirty)
        {
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }
        return ctrl;
    }

    private static void AddCommandState(AnimatorStateMachine sm, AnimatorState idle,
                                        string trigger, AnimationClip clip, Vector3 pos)
    {
        var st = sm.AddState(trigger, pos);
        st.motion = clip;
        var enter = idle.AddTransition(st);
        enter.hasExitTime = false;
        enter.hasFixedDuration = true;
        enter.duration = 0.1f;
        enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        var exit = st.AddTransition(idle);
        exit.hasExitTime = true;
        exit.exitTime = 1f;
        exit.hasFixedDuration = true;
        exit.duration = 0.1f;
    }

    // ---------------- prefab ----------------

    // Same Resources mechanism as VIS_Roman_Legionary_Basic: the prefab asset
    // itself lives in a Resources folder, so Resources.Load("VIS_Roman_
    // Centurion") finds it and SoldierFactory lights up without a code change.
    private static void EnsurePrefab(System.Text.StringBuilder sb, AnimatorController ctrl)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            var anim = existing.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController == ctrl &&
                existing.GetComponent<RomanLegionaryVisualController>() != null &&
                existing.GetComponent<CenturionCommandGestures>() != null)
            {
                sb.AppendLine("prefab present");
                return;
            }
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null) { sb.AppendLine("MISSING model for prefab"); return; }
        EnsureFolder(PrefabFolder);
        EnsureFolder(ResourcesFolder);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely,
                                           InteractionMode.AutomatedAction);
        inst.name = "VIS_Roman_Centurion";
        var animator = inst.GetComponent<Animator>();
        if (animator == null) animator = inst.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        if (inst.GetComponent<RomanLegionaryVisualController>() == null)
            inst.AddComponent<RomanLegionaryVisualController>();
        if (inst.GetComponent<CenturionCommandGestures>() == null)
            inst.AddComponent<CenturionCommandGestures>();
        PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
        Object.DestroyImmediate(inst);
        sb.AppendLine(existing == null ? "prefab BUILT" : "prefab REFRESHED");
    }

    // ---------------- folders ----------------

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
