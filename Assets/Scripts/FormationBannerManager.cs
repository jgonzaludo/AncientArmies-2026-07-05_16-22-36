using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Banner presentation levels by how large a soldier currently is on screen.
public enum FormationBannerDetailLevel { Close, Compact, Expanded }

// Player-selectable banner visibility policy (persisted).
public enum FormationBannerVisibilityMode { Adaptive, AlwaysVisible, SelectedOnly }

// Battlefield-level owner of everything shared between formation banners:
// the adaptive detail level (soldier screen-pixel height with hysteresis),
// the persisted visibility mode, the temporary information-layer hold, and
// interval-driven screen-space overlap resolution. Controllers register here
// and read the results; the manager never owns simulation state.
public class FormationBannerManager : MonoBehaviour
{
    public static FormationBannerManager Instance { get; private set; }

    [Header("Adaptive zoom thresholds (soldier height in screen pixels)")]
    [Tooltip("Enter Close (banners hide unless important) at or above this soldier pixel height")]
    [SerializeField] private float closeEnterPixels = 32f;
    [Tooltip("Leave Close below this (hysteresis)")]
    [SerializeField] private float closeExitPixels = 28f;
    [Tooltip("Enter Expanded (banner becomes the formation) at or below this")]
    [SerializeField] private float expandedEnterPixels = 16f;
    [Tooltip("Leave Expanded above this (hysteresis)")]
    [SerializeField] private float expandedExitPixels = 20f;

    [Header("Overlap avoidance (screen space)")]
    [Tooltip("Seconds between overlap resolutions (banners interpolate in between)")]
    [SerializeField] private float overlapInterval = 0.15f;
    [Tooltip("Banners closer than this horizontally (px) can collide")]
    [SerializeField] private float overlapPaddingX = 95f;
    [Tooltip("Banners closer than this vertically (px) collide")]
    [SerializeField] private float overlapPaddingY = 80f;
    [Tooltip("Vertical push per collision step (px)")]
    [SerializeField] private float overlapStepPx = 70f;
    [Tooltip("More than this many banners stacked in one spot fades the extras")]
    [SerializeField] private int maxStackCount = 3;

    private const float SoldierWorldHeight = 1.8f;   // capsule height, world meters
    private const string ModePrefsKey = "FormationBannerVisibilityMode";

    public FormationBannerDetailLevel CurrentLevel { get; private set; } = FormationBannerDetailLevel.Compact;
    public FormationBannerVisibilityMode Mode { get; private set; }
    public bool InfoHeld { get; private set; }
    public float SoldierScreenPixels { get; private set; }
    // world meters covered by one screen pixel at the current zoom
    public float WorldPerPixel { get; private set; } = 0.05f;

    private Camera cam;
    private bool externalInfoHeld;   // future mobile button hook
    private float overlapTimer;
    private readonly List<FormationBannerController> banners = new List<FormationBannerController>(16);
    private readonly List<FormationBannerController> visibleScratch = new List<FormationBannerController>(16);
    private static readonly System.Comparison<FormationBannerController> ByPriorityDesc =
        (a, b) => b.Priority.CompareTo(a.Priority);

    private void Awake()
    {
        Instance = this;
        Mode = (FormationBannerVisibilityMode)PlayerPrefs.GetInt(
            ModePrefsKey, (int)FormationBannerVisibilityMode.Adaptive);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RegisterBanner(FormationBannerController c) { if (!banners.Contains(c)) banners.Add(c); }
    public void UnregisterBanner(FormationBannerController c) { banners.Remove(c); }

    // ShowBattleInformation hold for a future mobile button (HUD can call this).
    public void SetInfoHeld(bool held) { externalInfoHeld = held; }

    public void CycleMode()
    {
        Mode = (FormationBannerVisibilityMode)(((int)Mode + 1) % 3);
        PlayerPrefs.SetInt(ModePrefsKey, (int)Mode);
        PlayerPrefs.Save();
    }

    private void Update()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        // Soldier screen height at the camera's center-screen ground focus —
        // resolution/aspect independent, unlike raw orthographic size.
        Vector3 focus = FocusPoint();
        Vector3 a = cam.WorldToScreenPoint(focus);
        Vector3 b = cam.WorldToScreenPoint(focus + Vector3.up * SoldierWorldHeight);
        SoldierScreenPixels = Mathf.Abs(b.y - a.y);
        WorldPerPixel = cam.orthographicSize * 2f / Mathf.Max(1, Screen.height);

        // hysteretic level transitions: stable while the camera dawdles near a
        // threshold, decisive when it commits
        switch (CurrentLevel)
        {
            case FormationBannerDetailLevel.Close:
                if (SoldierScreenPixels < closeExitPixels)
                    CurrentLevel = SoldierScreenPixels <= expandedEnterPixels
                        ? FormationBannerDetailLevel.Expanded : FormationBannerDetailLevel.Compact;
                break;
            case FormationBannerDetailLevel.Compact:
                if (SoldierScreenPixels >= closeEnterPixels) CurrentLevel = FormationBannerDetailLevel.Close;
                else if (SoldierScreenPixels <= expandedEnterPixels) CurrentLevel = FormationBannerDetailLevel.Expanded;
                break;
            case FormationBannerDetailLevel.Expanded:
                if (SoldierScreenPixels > expandedExitPixels)
                    CurrentLevel = SoldierScreenPixels >= closeEnterPixels
                        ? FormationBannerDetailLevel.Close : FormationBannerDetailLevel.Compact;
                break;
        }

        var kb = Keyboard.current;
        InfoHeld = externalInfoHeld || (kb != null && kb.iKey.isPressed);

        overlapTimer -= Time.unscaledDeltaTime;
        if (overlapTimer <= 0f)
        {
            overlapTimer = overlapInterval;
            ResolveOverlaps();
        }
    }

    private Vector3 FocusPoint()
    {
        Vector3 p = cam.transform.position;
        Vector3 f = cam.transform.forward;
        if (f.y > -0.05f) return p;                 // degenerate: looking level
        float t = -p.y / f.y;                       // ray to the ground plane
        return p + f * t;
    }

    // Greedy screen-space de-clutter: highest priority banners keep their spot;
    // lower ones step upward until clear, and fade past the stack limit. Runs
    // on an interval over a handful of banners — no spatial structure needed at
    // this battle size, and the scratch lists are reused (no steady-state GC).
    private void ResolveOverlaps()
    {
        visibleScratch.Clear();
        for (int i = 0; i < banners.Count; i++)
            if (banners[i] != null && banners[i].IsShown) visibleScratch.Add(banners[i]);
        visibleScratch.Sort(ByPriorityDesc);

        for (int i = 0; i < visibleScratch.Count; i++)
        {
            var c = visibleScratch[i];
            Vector3 sp = cam.WorldToScreenPoint(c.DesiredWorldAnchor);
            float lift = 0f;
            int steps = 0;
            bool collided = true;
            while (collided && steps <= maxStackCount + 1)
            {
                collided = false;
                for (int j = 0; j < i; j++)
                {
                    var other = visibleScratch[j];
                    if (Mathf.Abs(sp.x - other.ResolvedScreenPos.x) < overlapPaddingX &&
                        Mathf.Abs(sp.y + lift - other.ResolvedScreenPos.y) < overlapPaddingY)
                    {
                        lift += overlapStepPx;
                        steps++;
                        collided = true;
                        break;
                    }
                }
            }
            c.SetOverlapResolution(new Vector2(sp.x, sp.y + lift), lift, steps >= maxStackCount);
        }
    }
}
