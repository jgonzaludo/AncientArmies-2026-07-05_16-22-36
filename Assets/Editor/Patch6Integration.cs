using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// One-shot, idempotent Patch 6 integration that runs on domain reload:
//   1. Registers the new ANIM_Roman_Attack_DiagonalSlash take as an imported
//      clip on the legionary FBX (copying the thrust clip's import settings).
//   2. Adds the AttackDiagonalSlash state (AttackVariant == 2) to
//      AC_RomanInfantry, mirroring AttackThrust's transitions.
// Both steps no-op once done; results land in Logs/patch6_integration.log so
// headless tooling can verify without the console.
[InitializeOnLoad]
public static class Patch6Integration
{
    private const string FbxPath =
        "Assets/Art/Characters/Romans/Legionary/Models/RomanLegionary_Basic.fbx";
    private const string ControllerPath =
        "Assets/Art/Characters/Romans/Legionary/Animations/AC_RomanInfantry.controller";
    private const string NewClip = "ANIM_Roman_Attack_DiagonalSlash";
    private const string NewState = "AttackDiagonalSlash";
    private const string LogPath = "Logs/patch6_integration.log";

    static Patch6Integration()
    {
        EditorApplication.delayCall += RunOnce;
    }

    [MenuItem("Ancient Armies/Run Patch 6 Integration")]
    public static void RunOnce()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Patch 6 integration {System.DateTime.Now:HH:mm:ss} ===");
        try
        {
            bool reimporting = EnsureClip(sb);
            // If a reimport was just requested the clip sub-asset doesn't
            // exist yet; the reimport triggers another reload and this runs
            // again, completing the controller on the second pass.
            if (!reimporting) EnsureControllerState(sb);
        }
        catch (System.Exception e)
        {
            sb.AppendLine("EXCEPTION: " + e);
        }
        File.AppendAllText(LogPath, sb.ToString());
    }

    private static bool EnsureClip(System.Text.StringBuilder sb)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        if (imp == null) { sb.AppendLine("no importer"); return false; }
        var clips = imp.clipAnimations;
        if (clips.Any(c => c.name == NewClip))
        {
            sb.AppendLine($"clip present ({clips.Length} clips)");
            return false;
        }
        var thrust = clips.FirstOrDefault(c => c.name == "ANIM_Roman_Attack_Thrust");
        var take = imp.importedTakeInfos.FirstOrDefault(
            t => t.name.EndsWith(NewClip));
        if (thrust == null || take.name == null)
        {
            sb.AppendLine($"MISSING thrust clip or take (takes: {imp.importedTakeInfos.Length})");
            return false;
        }
        var add = new ModelImporterClipAnimation
        {
            name = NewClip,
            takeName = take.name,
            firstFrame = take.startTime * take.sampleRate,
            lastFrame = take.stopTime * take.sampleRate,
            loopTime = false,
            loopPose = thrust.loopPose,
            lockRootRotation = thrust.lockRootRotation,
            lockRootHeightY = thrust.lockRootHeightY,
            lockRootPositionXZ = thrust.lockRootPositionXZ,
            keepOriginalOrientation = thrust.keepOriginalOrientation,
            keepOriginalPositionY = thrust.keepOriginalPositionY,
            keepOriginalPositionXZ = thrust.keepOriginalPositionXZ,
            wrapMode = thrust.wrapMode,
        };
        var list = clips.ToList();
        list.Add(add);
        imp.clipAnimations = list.ToArray();
        imp.SaveAndReimport();
        sb.AppendLine($"clip ADDED (take {take.name}, frames {add.firstFrame}-{add.lastFrame}); reimport requested");
        return true;
    }

    private static void EnsureControllerState(System.Text.StringBuilder sb)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { sb.AppendLine("no controller"); return; }
        var sm = ctrl.layers[0].stateMachine;
        if (sm.states.Any(s => s.state.name == NewState))
        {
            sb.AppendLine("state present");
            return;
        }
        var thrustChild = sm.states.FirstOrDefault(s => s.state.name == "AttackThrust");
        var thrustState = thrustChild.state;
        if (thrustState == null) { sb.AppendLine("MISSING AttackThrust state"); return; }

        var clip = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>().FirstOrDefault(c => c.name == NewClip);
        if (clip == null) { sb.AppendLine("MISSING clip sub-asset"); return; }

        var st = sm.AddState(NewState, thrustChild.position + new Vector3(0f, 100f, 0f));
        st.motion = clip;
        st.tag = thrustState.tag;
        st.speedParameterActive = false;
        st.writeDefaultValues = thrustState.writeDefaultValues;

        // Inbound: every transition in the machine that targets AttackThrust
        // with AttackVariant == 0 gets a mirrored twin targeting the new state
        // with AttackVariant == 2 (same source, same timing flags).
        int inbound = 0;
        foreach (var child in sm.states)
        {
            foreach (var tr in child.state.transitions.ToArray())
            {
                if (tr.destinationState != thrustState) continue;
                if (!tr.conditions.Any(c => c.parameter == "AttackVariant" &&
                                            c.mode == AnimatorConditionMode.Equals &&
                                            (int)c.threshold == 0)) continue;
                var nt = child.state.AddTransition(st);
                CopyTransition(tr, nt, retargetVariantTo2: true);
                inbound++;
            }
        }
        // AnyState transitions, if any target the thrust the same way.
        foreach (var tr in sm.anyStateTransitions.ToArray())
        {
            if (tr.destinationState != thrustState) continue;
            if (!tr.conditions.Any(c => c.parameter == "AttackVariant" &&
                                        c.mode == AnimatorConditionMode.Equals &&
                                        (int)c.threshold == 0)) continue;
            var nt = sm.AddAnyStateTransition(st);
            CopyTransition(tr, nt, retargetVariantTo2: true);
            inbound++;
        }
        // Outbound: mirror the thrust's exits (exit-time returns to locomotion).
        int outbound = 0;
        foreach (var tr in thrustState.transitions)
        {
            AnimatorStateTransition nt = tr.isExit
                ? st.AddExitTransition()
                : st.AddTransition(tr.destinationState);
            CopyTransition(tr, nt, retargetVariantTo2: false);
            outbound++;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        sb.AppendLine($"state ADDED: {inbound} inbound, {outbound} outbound transitions");
    }

    private static void CopyTransition(AnimatorStateTransition from,
                                       AnimatorStateTransition to,
                                       bool retargetVariantTo2)
    {
        to.hasExitTime = from.hasExitTime;
        to.exitTime = from.exitTime;
        to.hasFixedDuration = from.hasFixedDuration;
        to.duration = from.duration;
        to.offset = from.offset;
        to.interruptionSource = from.interruptionSource;
        to.orderedInterruption = from.orderedInterruption;
        to.canTransitionToSelf = from.canTransitionToSelf;
        foreach (var c in from.conditions)
        {
            float threshold = c.threshold;
            if (retargetVariantTo2 && c.parameter == "AttackVariant") threshold = 2f;
            to.AddCondition(c.mode, threshold, c.parameter);
        }
    }
}
