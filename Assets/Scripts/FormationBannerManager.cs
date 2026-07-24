using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Banner presentation levels by how large a soldier currently is on screen.
public enum FormationBannerDetailLevel { Close, Compact, Expanded }

// Player-selectable banner visibility policy (persisted).
public enum FormationBannerVisibilityMode { Adaptive, AlwaysVisible, SelectedOnly }

// Battlefield-level owner of everything shared between formation banners:
// the adaptive detail level (soldier screen-pixel height with hysteresis),
// the persisted visibility mode, and the temporary information-layer hold.
// Banners anchor rigidly to their own formation — no screen-space overlap
// solver; overlapping banners at far zoom are accepted over banners that
// drift off their century. The manager never owns simulation state.
public class FormationBannerManager : MonoBehaviour
{
    public static FormationBannerManager Instance { get; private set; }

    [Header("Adaptive zoom thresholds (soldier height in 1080p-reference pixels)")]
    // Calibrated to this game's camera against SoldierScreenPixels, which is
    // normalized to a 1080-tall reference screen: the orthographic zoom range
    // (BattleCamera 5.5-36) puts a soldier between ~28 px (max zoom-out) and
    // ~186 px (max zoom-in); default framing (ortho 26) is ~39 px. The bands
    // below make Close/Compact/Expanded all reachable with the default view
    // landing in Compact, the primary command zoom.
    [Tooltip("Enter Close (banners hide unless important) at or above this soldier pixel height")]
    [SerializeField] private float closeEnterPixels = 70f;
    [Tooltip("Leave Close below this (hysteresis)")]
    [SerializeField] private float closeExitPixels = 62f;
    [Tooltip("Enter Expanded (banner becomes the formation) at or below this")]
    [SerializeField] private float expandedEnterPixels = 34f;
    [Tooltip("Leave Expanded above this (hysteresis)")]
    [SerializeField] private float expandedExitPixels = 40f;

    private const float SoldierWorldHeight = 1.8f;   // capsule height, world meters
    private const string ModePrefsKey = "FormationBannerVisibilityMode";

    public FormationBannerDetailLevel CurrentLevel { get; private set; } = FormationBannerDetailLevel.Compact;
    public FormationBannerVisibilityMode Mode { get; private set; }
    public bool InfoHeld { get; private set; }
    public float SoldierScreenPixels { get; private set; }
    // world meters covered by one screen pixel at the current zoom
    public float WorldPerPixel { get; private set; } = 0.05f;
    // raw device pixels per 1080p-reference pixel
    public float PixelScale => Mathf.Max(0.5f, Screen.height / 1080f);

    private Camera cam;
    private bool externalInfoHeld;   // future mobile button hook

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

        // Soldier screen height at the camera's center-screen ground focus.
        // WorldToScreenPoint returns raw device pixels, so divide by PixelScale
        // to express the value in 1080p-reference pixels — the thresholds above
        // then hold on any screen height.
        Vector3 focus = FocusPoint();
        Vector3 a = cam.WorldToScreenPoint(focus);
        Vector3 b = cam.WorldToScreenPoint(focus + Vector3.up * SoldierWorldHeight);
        SoldierScreenPixels = Mathf.Abs(b.y - a.y) / PixelScale;
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
    }

    private Vector3 FocusPoint()
    {
        Vector3 p = cam.transform.position;
        Vector3 f = cam.transform.forward;
        if (f.y > -0.05f) return p;                 // degenerate: looking level
        float t = -p.y / f.y;                       // ray to the ground plane
        return p + f * t;
    }
}
