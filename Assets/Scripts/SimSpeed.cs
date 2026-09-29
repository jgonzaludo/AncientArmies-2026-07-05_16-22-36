#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// Test tooling: run the simulation faster than real time. Editor and
// development builds only. Set(multiplier, renderOff) scales time and, with
// renderOff, disables every camera so the CPU goes to simulation. Reset puts
// every setting back exactly as found.
public static class SimSpeed
{
    public static float Multiplier { get; private set; } = 1f;
    public static bool RenderOff { get; private set; }

    private static bool captured;
    private static float origTimeScale, origMaxDelta;
    private static int origVSync, origTargetFps;
    private static Camera[] disabledCameras = new Camera[0];

    public static void Set(float multiplier, bool renderOff)
    {
        if (!captured)
        {
            captured = true;
            origTimeScale = Time.timeScale;
            origMaxDelta = Time.maximumDeltaTime;
            origVSync = QualitySettings.vSyncCount;
            origTargetFps = Application.targetFrameRate;
        }
        Multiplier = Mathf.Max(0.01f, multiplier);
        Time.timeScale = Multiplier;
        // let one frame advance the full scaled step instead of clamping to 1/3 s
        Time.maximumDeltaTime = Mathf.Max(origMaxDelta, Multiplier * 0.5f);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 1000;
        SetRender(!renderOff);
    }

    public static void Reset()
    {
        if (!captured) return;
        Time.timeScale = origTimeScale;
        Time.maximumDeltaTime = origMaxDelta;
        QualitySettings.vSyncCount = origVSync;
        Application.targetFrameRate = origTargetFps;
        SetRender(true);
        Multiplier = 1f;
        captured = false;
    }

    private static void SetRender(bool on)
    {
        if (!on)
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            var list = new System.Collections.Generic.List<Camera>();
            foreach (var c in cams) if (c.enabled) { c.enabled = false; list.Add(c); }
            disabledCameras = list.ToArray();
            RenderOff = true;
        }
        else
        {
            foreach (var c in disabledCameras) if (c != null) c.enabled = true;
            disabledCameras = new Camera[0];
            RenderOff = false;
        }
    }
}
#endif
