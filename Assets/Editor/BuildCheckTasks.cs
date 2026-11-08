using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;

// compiles the game's scripts the way a release iOS player build would, without building one: catches code that only
// compiles in the editor or in development builds. run through the file trigger: "task=BuildCheckTasks.CompileIosRelease"
public static class BuildCheckTasks
{
    public static string CompileIosRelease()
    {
        var settings = new ScriptCompilationSettings { target = BuildTarget.iOS, group = BuildTargetGroup.iOS, options = ScriptCompilationOptions.None };
        string dir = Path.Combine("Temp", "BuildCheckIos");
        var result = PlayerBuildInterface.CompilePlayerScripts(settings, dir);
        bool ok = result.assemblies != null && result.assemblies.Contains("Assembly-CSharp.dll");
        return (ok ? "PASS" : "FAIL") + " iOS release script compile, assemblies: " + (result.assemblies == null ? "none" : string.Join(", ", result.assemblies.Where(a => a.StartsWith("Assembly-CSharp"))));
    }
}
