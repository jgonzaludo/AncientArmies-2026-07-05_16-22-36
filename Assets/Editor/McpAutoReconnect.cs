using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// Dev helper: MCP-for-Unity's bridge only auto-resumes after a domain reload if it was
// running beforehand, so a dropped session stays dropped forever. This restarts the
// bridge (same call as the window's Connect button) after every reload, via reflection
// so there is no hard dependency on the package assembly.
[InitializeOnLoad]
public static class McpAutoReconnect
{
    static McpAutoReconnect()
    {
        EditorApplication.delayCall += () => TryStart(0);
    }

    private static void TryStart(int attempt)
    {
        try
        {
            var locator = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("MCPForUnity.Editor.Services.MCPServiceLocator"))
                .FirstOrDefault(t => t != null);
            if (locator == null) return;

            var bridge = locator.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (bridge == null) return;

            var isRunningProp = bridge.GetType().GetProperty("IsRunning");
            if (isRunningProp != null && (bool)isRunningProp.GetValue(bridge)) return;

            var startMethod = bridge.GetType().GetMethod("StartAsync");
            if (startMethod == null) return;

            var task = (Task<bool>)startMethod.Invoke(bridge, null);
            task.ContinueWith(t =>
            {
                bool ok = t.Status == TaskStatus.RanToCompletion && t.Result;
                Debug.Log($"[McpAutoReconnect] bridge start attempt {attempt}: {(ok ? "connected" : "failed")}");
                if (!ok && attempt < 5)
                    EditorApplication.delayCall += () => TryStart(attempt + 1);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[McpAutoReconnect] {ex.Message}");
        }
    }
}
