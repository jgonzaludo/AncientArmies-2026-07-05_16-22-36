using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runtime-built HUD (uGUI, no prefabs): help and banner toggle top left, score bar and clock top center, restart top
// right, a unit panel bottom left and charge / rotate bottom right. only button and pill backgrounds are raycast
// targets, so taps near the edges still reach the battlefield. every size comes from UiTheme (draft px * K).
public class BattleHUD : MonoBehaviour
{
    private const bool ShowDevButtons = false;   // speed and scenes stay built but hidden, so they can come back later

    private static float P(float x) { return UiTheme.P(x); }

    private class Btn { public Button button; public Image bg; public TextMeshProUGUI label, sub; public Image icon; public Sprite plate, disabledPlate; }

    private PlayerCommander commander;

    private Transform canvasT;
    private GameObject battleRoot;       // everything that belongs to a live battle, hidden on the result screen
    private GameObject panelGO;          // bottom command row + unit panel
    private GameObject unitPanel;
    private Image panelBanner, strengthFill, healthFill;
    private TextMeshProUGUI panelTitle, panelSub, strengthValue, healthValue;
    private float barTrackW;
    private Btn chargeBtn, rotateBtn, restartBtn, bannerBtn, helpBtn;
    private TextMeshProUGUI bannerBtnValue;
    private GameObject helpPanel;
    private float helpHideAt, restartConfirmUntil;

    private GameObject startRoot, endRoot;
    private TextMeshProUGUI resultText, endReasonText;
    private readonly TextMeshProUGUI[] endHeader = new TextMeshProUGUI[2], endTotal = new TextMeshProUGUI[2];
    private readonly GameObject[] endRow = new GameObject[16];
    private readonly TextMeshProUGUI[] endRowName = new TextMeshProUGUI[16], endRowCount = new TextMeshProUGUI[16];
    private readonly Image[] endRowIcon = new Image[16], endRowFill = new Image[16], endRowPlate = new Image[16];
    private bool endFilled;

    private TextMeshProUGUI timerText, tideLeftText, tideRightText;
    private GameObject timerClock;
    private RectTransform tideRoot, tideBlue;
    private float tideShown = 0.5f;

    private static readonly string[] SceneNames = { "Battle", "TestSkirmish" };
    private static readonly string[] SceneLabels = { "BATTLE", "TEST 1" };
    private GameObject sceneRowGO, devSpeedGO, devScenesGO;

    // ---------------- lifecycle ----------------

    private void Start()
    {
        commander = GetComponent<PlayerCommander>();
        if (commander == null) commander = FindAnyObjectByType<PlayerCommander>();
        BuildUI();
    }

    private void Update()
    {
        if (commander == null || panelGO == null) return;

        BattlePhase phase = BattleSetup.Instance != null ? BattleSetup.Instance.Phase : BattlePhase.Pre;
        if (startRoot != null) startRoot.SetActive(phase == BattlePhase.Pre);
        if (battleRoot != null) battleRoot.SetActive(phase != BattlePhase.Ended);
        if (endRoot != null)
        {
            endRoot.SetActive(phase == BattlePhase.Ended);
            if (phase != BattlePhase.Ended) endFilled = false;
            if (phase == BattlePhase.Ended && resultText != null && !endFilled) { endFilled = true; FillEndSummary(BattleSetup.Instance.ResultText); }
        }
        if (phase == BattlePhase.Ended) return;

        UpdateHelp(phase);
        if (restartBtn != null) restartBtn.label.text = Time.unscaledTime < restartConfirmUntil ? "SURE?" : "RESTART";
        UpdateTimer(phase);
        UpdateTide(phase);

        Formation enemy = commander.InspectedEnemy;
        if (enemy != null)
        {
            panelGO.SetActive(true);
            chargeBtn.button.gameObject.SetActive(false);
            rotateBtn.button.gameObject.SetActive(false);
            ShowUnit(enemy, true);
            return;
        }

        IReadOnlyList<Formation> selection = commander.Selection;
        int liveCount = CountLive(selection);
        if (liveCount == 0) { panelGO.SetActive(false); return; }
        panelGO.SetActive(true);

        // a fully broken selection regroups on its own, so offering charge or rotate there is noise
        bool allBroken = AllLiveBroken(selection);
        chargeBtn.button.gameObject.SetActive(!allBroken);
        UpdateChargeState(selection);

        if (liveCount == 1)
        {
            rotateBtn.button.gameObject.SetActive(!allBroken);
            if (!allBroken) UpdateRotateButton();
            ShowUnit(GetFirstLive(selection), false);
        }
        else
        {
            rotateBtn.button.gameObject.SetActive(false);
            ShowGroup(selection, liveCount);
        }
    }

    // ---------------- unit panel content ----------------

    // health is shown clamped, a formation can sit a few percent over its max (reported, not hidden), and 103% reads as a bug
    private static int HealthPct(float health, float max) { return Mathf.Min(100, Mathf.RoundToInt(100f * health / Mathf.Max(1f, max))); }

    private static string BannerName(Team team, bool ranged) { return "UI_Banner_" + (team == Team.Blue ? "Blue" : "Red") + "_" + (ranged ? "Ranged" : "Melee"); }

    private void ShowUnit(Formation f, bool enemy)
    {
        if (f == null) return;
        string state = Formation.StateLabel(f.State);
        string sub = enemy ? f.stats.unitName + " · <color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.EnemyTint) + ">Enemy</color> · " + state : f.stats.unitName + " · " + state;
        SetPanel(UiKit.Spr(BannerName(f.team, f.stats.isRanged)), RomanNumerals.Pretty(f.displayName), sub, f.soldiers.Count, f.TotalSpawned, HealthPct(f.TotalHealth, f.TotalMaxHealth));
    }

    private void ShowGroup(IReadOnlyList<Formation> selection, int liveCount)
    {
        int soldiers = 0, spawned = 0; float hp = 0f, max = 0f;
        for (int i = 0; i < selection.Count; i++)
        {
            Formation f = selection[i];
            if (f == null || f.soldiers.Count == 0) continue;
            soldiers += f.soldiers.Count; spawned += f.TotalSpawned; hp += f.TotalHealth; max += f.TotalMaxHealth;
        }
        Formation first = GetFirstLive(selection);
        SetPanel(UiKit.Spr(BannerName(first.team, first.stats.isRanged)), liveCount + " formations", "Selected", soldiers, spawned, HealthPct(hp, max));
    }

    private void SetPanel(Sprite banner, string title, string sub, int soldiers, int spawned, int healthPct)
    {
        panelBanner.sprite = banner; panelBanner.enabled = banner != null;
        panelTitle.text = title; panelSub.text = sub;
        strengthValue.text = UiTheme.Tab(soldiers) + "/" + UiTheme.Tab(spawned);
        healthValue.text = UiTheme.Tab(healthPct) + "%";
        SetBar(strengthFill, spawned > 0 ? (float)soldiers / spawned : 0f, barTrackW, UiTheme.HealthGreen);
        float h = healthPct / 100f;
        SetBar(healthFill, h, barTrackW, h > 0.5f ? UiTheme.HealthGreen : h > 0.25f ? UiTheme.HealthAmber : UiTheme.HealthRed);
    }

    private static void SetBar(Image fill, float frac, float trackW, Color c)
    {
        float h = fill.rectTransform.sizeDelta.y;
        fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(h, trackW * Mathf.Clamp01(frac)), h);   // never narrower than a round end
        fill.color = c;
    }

    private void UpdateChargeState(IReadOnlyList<Formation> selection)
    {
        bool chargeOk = false, anyNoTarget = false, anyBroken = false; int considered = 0;
        for (int i = 0; i < selection.Count; i++)
        {
            Formation f = selection[i];
            if (f == null || f.soldiers.Count == 0) continue;
            considered++;
            Formation.ChargeBlock block = f.GetChargeBlock();
            if (block == Formation.ChargeBlock.None) chargeOk = true;
            else if (block == Formation.ChargeBlock.NoTarget) anyNoTarget = true;
            if (f.State == FormationState.BrokenRanks || f.State == FormationState.Reforming) anyBroken = true;
        }
        string reason = "";
        if (considered > 0 && anyBroken) reason = "Regroups when clear";
        else if (considered > 0 && !chargeOk && anyNoTarget) reason = "No enemy in range";
        SetBtnEnabled(chargeBtn, chargeOk);
        chargeBtn.sub.text = reason; chargeBtn.sub.gameObject.SetActive(reason.Length > 0 && !chargeOk);   // the reason lives inside the button
    }

    private void UpdateRotateButton()
    {
        rotateBtn.label.text = commander.RotateMode ? "CANCEL" : "ROTATE";
        SetBtnEnabled(rotateBtn, commander.CanEnterRotateMode || commander.RotateMode);
    }

    private static void SetBtnEnabled(Btn b, bool on)
    {
        b.button.interactable = on;
        b.bg.sprite = on ? b.plate : b.disabledPlate;
        b.label.color = on ? UiTheme.Ink : UiTheme.InkMuted;
    }

    private void UpdateHelp(BattlePhase phase)
    {
        if (helpPanel == null) return;
        if (phase == BattlePhase.Active && PlayerPrefs.GetInt("HelpSeen", 0) == 0)
        {
            PlayerPrefs.SetInt("HelpSeen", 1);   // one-time card on the first battle
            helpPanel.SetActive(true); helpHideAt = Time.unscaledTime + 10f;
        }
        if (helpHideAt > 0f && Time.unscaledTime > helpHideAt) { helpPanel.SetActive(false); helpHideAt = 0f; }
    }

    // ---------------- selection helpers ----------------

    private static int CountLive(IReadOnlyList<Formation> selection)
    {
        if (selection == null) return 0;
        int count = 0;
        for (int i = 0; i < selection.Count; i++) if (selection[i] != null && selection[i].soldiers.Count > 0) count++;
        return count;
    }

    private static bool AllLiveBroken(IReadOnlyList<Formation> selection)
    {
        bool any = false;
        for (int i = 0; i < selection.Count; i++)
        {
            Formation f = selection[i];
            if (f == null || f.soldiers.Count == 0) continue;
            if (f.State != FormationState.BrokenRanks) return false;
            any = true;
        }
        return any;
    }

    private static Formation GetFirstLive(IReadOnlyList<Formation> selection)
    {
        for (int i = 0; i < selection.Count; i++) if (selection[i] != null && selection[i].soldiers.Count > 0) return selection[i];
        return null;
    }

    private void OnChargeClicked()
    {
        if (commander == null || commander.Selection == null) return;
        foreach (var f in commander.Selection) if (f != null && f.soldiers.Count > 0) f.IssueCharge();
    }

    private void OnRotateClicked() { if (commander != null) commander.ToggleRotateMode(); }

    // ---------------- construction ----------------

    private void BuildUI()
    {
        var canvasGO = new GameObject("HUDCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;   // scale by height: stable landscape sizing
        canvasGO.AddComponent<GraphicRaycaster>();

        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        // soft vignette first, so it sits under everything else
        var vig = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        vig.transform.SetParent(canvasGO.transform, false); vig.raycastTarget = false;
        var vt = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f, d = Mathf.Sqrt(dx * dx + dy * dy);
            vt.SetPixel(x, y, new Color(0.05f, 0.06f, 0.1f, 0.2f * Mathf.SmoothStep(0.62f, 1.25f, d)));
        }
        vt.Apply(); vig.texture = vt; UiKit.Fill(vig.rectTransform);

        // world-anchored unit labels and selection rings live on their own layer, under the controls
        new GameObject("UnitLabels").AddComponent<UnitLabels>().Init(canvasGO.transform);

        var safeGO = new GameObject("SafeArea", typeof(RectTransform));
        safeGO.transform.SetParent(canvasGO.transform, false);
        safeGO.AddComponent<SafeAreaFitter>();
        canvasT = safeGO.transform;

        battleRoot = new GameObject("BattleHud", typeof(RectTransform));
        battleRoot.transform.SetParent(canvasT, false);
        UiKit.Fill(battleRoot.GetComponent<RectTransform>());
        Transform bt = battleRoot.transform;

        BuildTopLeft(bt);
        BuildTop(bt);
        BuildBottom(bt);
        BuildDevButtons(canvasT);
        BuildStartUI(canvasT);
        BuildEndUI(canvasT);
    }

    // ----- buttons -----

    private Btn MakeBtn(Transform parent, string name, string label, string plate, float w, float h, string icon = null, bool twoLine = false, float fontSize = 0f, float tracking = 7f)
    {
        var b = new Btn { plate = UiKit.Spr(plate), disabledPlate = UiKit.Spr("UI_PlateDisabled") };
        var bg = UiKit.Img(parent, name, b.plate, Color.white, h);
        bg.raycastTarget = true;
        float pad = Mathf.Max(0f, (UiTheme.HitH - h) * 0.5f);
        bg.raycastPadding = new Vector4(0f, pad, 0f, pad);   // 44pt tall hit area, the art stays smaller
        UiKit.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(w, h));
        b.bg = bg;
        var btn = bg.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.97f, 0.88f); cb.pressedColor = new Color(0.78f, 0.8f, 0.88f); cb.disabledColor = Color.white; cb.fadeDuration = 0.08f;
        btn.colors = cb;
        b.button = btn;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(bg.transform, false);
        UiKit.Fill(content.GetComponent<RectTransform>(), P(10f), 0f, P(10f), 0f);
        float fs = fontSize > 0f ? fontSize : P(20f);
        if (twoLine)
        {
            var v = content.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.MiddleCenter; v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = v.childForceExpandHeight = false; v.spacing = P(1f);
            b.label = UiKit.Tmp(content.transform, "Label", label, UiTheme.SemiBold, fs, UiTheme.Ink, TextAlignmentOptions.Center, tracking);
            b.sub = UiKit.Tmp(content.transform, "Reason", "", UiTheme.Medium, P(15f), UiTheme.InkMuted, TextAlignmentOptions.Center, 0f);
            b.sub.gameObject.SetActive(false);
        }
        else
        {
            var hl = content.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = hl.childForceExpandHeight = false; hl.spacing = P(9f);
            if (icon != null)
            {
                b.icon = UiKit.Img(content.transform, "Icon", UiKit.Spr(icon), UiTheme.Ink);
                var le = b.icon.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = P(17f);
            }
            b.label = UiKit.Tmp(content.transform, "Label", label, UiTheme.SemiBold, fs, UiTheme.Ink, TextAlignmentOptions.Midline, tracking);
        }
        return b;
    }

    // ----- top left: help and banner mode -----

    private void BuildTopLeft(Transform parent)
    {
        float h = UiTheme.BtnH, m = UiTheme.Margin;
        helpBtn = MakeBtn(parent, "HelpButton", "?", "UI_Plate", h, h, null, false, P(24f), 0f);
        helpBtn.bg.rectTransform.anchoredPosition = new Vector2(m, -m);
        helpBtn.button.onClick.AddListener(() => { helpPanel.SetActive(!helpPanel.activeSelf); helpHideAt = 0f; });

        bannerBtn = MakeBtn(parent, "BannerModeButton", "BANNERS", "UI_Plate", P(222f), h);
        bannerBtn.bg.rectTransform.anchoredPosition = new Vector2(m + h + UiTheme.GapIn, -m);
        bannerBtn.label.color = UiTheme.InkMuted;
        bannerBtnValue = UiKit.Tmp(bannerBtn.label.transform.parent, "Value", "AUTO", UiTheme.Bold, P(20f), UiTheme.GoldHi, TextAlignmentOptions.Midline, 7f);
        var chev = UiKit.Img(bannerBtn.label.transform.parent, "Chevron", UiKit.Spr("UI_Icon_Chevron"), UiTheme.InkMuted);
        var cle = chev.gameObject.AddComponent<LayoutElement>(); cle.preferredWidth = cle.preferredHeight = P(15f);
        bannerBtn.button.onClick.AddListener(() =>
        {
            if (FormationBannerManager.Instance != null) FormationBannerManager.Instance.CycleMode();
            RefreshBannerModeLabel();
        });
        RefreshBannerModeLabel();

        helpPanel = UiKit.Img(parent, "HelpPanel", UiKit.Spr("UI_Panel"), Color.white, P(40f)).gameObject;   // radius tuned by the multiplier
        var hp = helpPanel.GetComponent<Image>(); hp.pixelsPerUnitMultiplier = 2.4f;
        UiKit.Place(hp.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(m, -(m + h + UiTheme.GapIn)), new Vector2(P(330f), P(244f)));
        var ht = UiKit.Tmp(helpPanel.transform, "HelpText", "Tap: select\nDouble-tap: add to group\nDrag from unit: move\nTwo fingers: rotate\nDrag ground: pan\nPinch: zoom", UiTheme.Medium, P(19f), UiTheme.Ink, TextAlignmentOptions.MidlineLeft);
        ht.lineSpacing = 18f; UiKit.Fill(ht.rectTransform, P(22f), P(14f), P(14f), P(14f));
        helpPanel.SetActive(false);
    }

    private void RefreshBannerModeLabel()
    {
        if (bannerBtnValue == null || FormationBannerManager.Instance == null) return;
        switch (FormationBannerManager.Instance.Mode)
        {
            case FormationBannerVisibilityMode.AlwaysVisible: bannerBtnValue.text = "ALL"; break;
            case FormationBannerVisibilityMode.SelectedOnly: bannerBtnValue.text = "PICKED"; break;
            default: bannerBtnValue.text = "AUTO"; break;
        }
    }

    // ----- top center: score bar and clock; top right: restart -----

    private void BuildTop(Transform parent)
    {
        float m = UiTheme.Margin, barW = P(520f), barH = UiTheme.BtnH, inset = P(5f);
        var tide = new GameObject("TideBar", typeof(RectTransform));
        tide.transform.SetParent(parent, false);
        tideRoot = tide.GetComponent<RectTransform>();
        UiKit.Place(tideRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -m), new Vector2(barW, barH));
        var frame = UiKit.Img(tide.transform, "Frame", UiKit.Spr("UI_Row"), Color.white, barH); UiKit.Fill(frame.rectTransform);
        var track = UiKit.Img(tide.transform, "Track", UiKit.Spr("UI_Pill"), Color.white, barH - inset * 2f); UiKit.Fill(track.rectTransform, inset, inset, inset, inset);
        track.gameObject.AddComponent<Mask>().showMaskGraphic = false;   // clips both fills to the pill shape
        var red = UiKit.Img(track.transform, "Red", null, UiTheme.BannerRed); UiKit.Fill(red.rectTransform);
        var blue = UiKit.Img(track.transform, "Blue", null, UiTheme.BannerBlue);
        tideBlue = blue.rectTransform; tideBlue.anchorMin = Vector2.zero; tideBlue.anchorMax = new Vector2(0.5f, 1f); tideBlue.offsetMin = tideBlue.offsetMax = Vector2.zero;
        var divider = UiKit.Img(track.transform, "Divider", null, new Color(0.07f, 0.09f, 0.16f, 0.95f));
        UiKit.Place(divider.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(P(4f), barH));
        tideLeftText = UiKit.Tmp(track.transform, "YouCount", "", UiTheme.Bold, P(26f), UiTheme.Ink, TextAlignmentOptions.MidlineLeft);
        UiKit.Place(tideLeftText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(P(22f), 0f), new Vector2(P(220f), barH));
        tideRightText = UiKit.Tmp(track.transform, "EnemyCount", "", UiTheme.Bold, P(26f), UiTheme.Ink, TextAlignmentOptions.MidlineRight);
        UiKit.Place(tideRightText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-P(22f), 0f), new Vector2(P(220f), barH));
        tide.SetActive(false);

        // clock on its own pill under the bar, tabular digits
        var clock = UiKit.Img(parent, "ClockPill", UiKit.Spr("UI_Plate"), Color.white, P(44f));
        timerClock = clock.gameObject;
        UiKit.Place(clock.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(m + barH + P(8f))), new Vector2(P(138f), P(44f)));
        var cc = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup)); cc.transform.SetParent(clock.transform, false); UiKit.Fill(cc.GetComponent<RectTransform>());
        var hl = cc.GetComponent<HorizontalLayoutGroup>(); hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = hl.childForceExpandHeight = false; hl.spacing = P(8f);
        var ci = UiKit.Img(cc.transform, "Icon", UiKit.Spr("UI_Icon_Clock"), UiTheme.Ink); var cle = ci.gameObject.AddComponent<LayoutElement>(); cle.preferredWidth = cle.preferredHeight = P(18f);
        timerText = UiKit.Tmp(cc.transform, "TimerText", "", UiTheme.Bold, P(28f), UiTheme.Ink, TextAlignmentOptions.Midline);

        // restart, danger styled, needs a second tap
        restartBtn = MakeBtn(parent, "RestartMiniButton", "RESTART", "UI_PlateDanger", P(166f), UiTheme.BtnH, "UI_Icon_Restart");
        UiKit.Place(restartBtn.bg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-m, -m), new Vector2(P(166f), UiTheme.BtnH));
        restartBtn.button.onClick.AddListener(() =>
        {
            if (Time.unscaledTime < restartConfirmUntil) { if (BattleSetup.Instance != null) BattleSetup.Instance.RestartBattle(); }
            else restartConfirmUntil = Time.unscaledTime + 3f;
        });
    }

    private void UpdateTide(BattlePhase phase)
    {
        if (tideRoot == null) return;
        var bs = BattleSetup.Instance;
        bool show = phase == BattlePhase.Active && bs != null && bs.moraleConfig != null && bs.moraleConfig.showTideBar;
        if (tideRoot.gameObject.activeSelf != show) tideRoot.gameObject.SetActive(show);
        if (!show) return;
        float blue = bs.BattlePower(Team.Blue), red = bs.BattlePower(Team.Red);
        float target = blue + red > 0f ? blue / (blue + red) : 0.5f;
        tideShown = Mathf.MoveTowards(tideShown, target, Time.unscaledDeltaTime * 0.35f);   // eased, never jumpy
        tideBlue.anchorMax = new Vector2(tideShown, 1f);
        int bn = 0, rn = 0;
        foreach (var f in bs.formations) { if (f == null) continue; if (f.team == Team.Blue) bn += f.soldiers.Count; else rn += f.soldiers.Count; }
        tideLeftText.text = "<size=70%><color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.InkMuted) + ">YOU</color></size> " + UiTheme.Tab(bn);
        tideRightText.text = "<size=70%><color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.InkMuted) + ">ENEMY</color></size> " + UiTheme.Tab(rn);
    }

    private void UpdateTimer(BattlePhase phase)
    {
        if (timerText == null) return;
        var bs = BattleSetup.Instance;
        bool show = phase != BattlePhase.Ended && bs != null && bs.moraleConfig != null;
        if (timerClock.activeSelf != show) timerClock.SetActive(show);
        if (!show) return;
        float remaining = bs.TimeRemaining;
        bool warning = remaining <= bs.moraleConfig.timerWarningSeconds;
        int total = Mathf.CeilToInt(remaining);
        timerText.text = UiTheme.Tab(total / 60) + ":" + UiTheme.Tab((total % 60).ToString("00"));
        timerText.color = warning ? UiTheme.GoldHi : UiTheme.Ink;
    }

    // ----- bottom: unit panel and the command row -----

    private void BuildBottom(Transform parent)
    {
        float m = UiTheme.Margin;
        panelGO = new GameObject("BottomPanel", typeof(RectTransform));
        panelGO.transform.SetParent(parent, false);
        UiKit.Fill(panelGO.GetComponent<RectTransform>());   // a container only, it has no graphic so it never eats taps
        Transform pt = panelGO.transform;

        // unit panel, bottom left
        float pw = P(360f), ph = P(188f), pad = P(18f);
        var panel = UiKit.Img(pt, "UnitPanel", UiKit.Spr("UI_Panel"), Color.white); panel.pixelsPerUnitMultiplier = 2.3f;
        unitPanel = panel.gameObject;
        UiKit.Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(m, m), new Vector2(pw, ph));
        panelBanner = UiKit.Img(unitPanel.transform, "Banner", null, Color.white);
        panelBanner.preserveAspect = true;
        UiKit.Place(panelBanner.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -P(15f)), new Vector2(P(34f), P(58f)));
        panelTitle = UiKit.Tmp(unitPanel.transform, "Title", "", UiTheme.Cinzel, P(26f), UiTheme.Ink, TextAlignmentOptions.TopLeft, 2f);
        UiKit.Place(panelTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad + P(34f) + P(12f), -P(15f)), new Vector2(pw - pad * 2f - P(46f), P(32f)));
        panelSub = UiKit.Tmp(unitPanel.transform, "Sub", "", UiTheme.Medium, P(17f), UiTheme.InkMuted, TextAlignmentOptions.TopLeft);
        UiKit.Place(panelSub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad + P(34f) + P(12f), -P(15f) - P(34f)), new Vector2(pw - pad * 2f - P(46f), P(24f)));
        var div = UiKit.Img(unitPanel.transform, "Divider", null, new Color(1f, 1f, 1f, 0.13f));
        UiKit.Place(div.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -P(82f)), new Vector2(pw - pad * 2f, 1f));
        barTrackW = (pw - pad * 2f - P(18f)) * 0.5f;
        for (int i = 0; i < 2; i++)
        {
            float x = pad + i * (barTrackW + P(18f));
            var lab = UiKit.Tmp(unitPanel.transform, i == 0 ? "StrengthLabel" : "HealthLabel", i == 0 ? "STRENGTH" : "HEALTH", UiTheme.SemiBold, P(13f), UiTheme.InkMuted, TextAlignmentOptions.TopLeft, 8f);
            UiKit.Place(lab.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -P(92f)), new Vector2(barTrackW, P(18f)));
            var val = UiKit.Tmp(unitPanel.transform, i == 0 ? "StrengthValue" : "HealthValue", "", UiTheme.Bold, P(24f), UiTheme.Ink, TextAlignmentOptions.TopLeft);
            UiKit.Place(val.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -P(108f)), new Vector2(barTrackW, P(32f)));
            var tr = UiKit.Img(unitPanel.transform, "Track", UiKit.Spr("UI_Pill"), UiTheme.TrackDark, P(7f));
            UiKit.Place(tr.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -P(146f)), new Vector2(barTrackW, P(7f)));
            var fl = UiKit.Img(unitPanel.transform, "Fill", UiKit.Spr("UI_Pill"), UiTheme.HealthGreen, P(7f));
            UiKit.Place(fl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -P(146f)), new Vector2(barTrackW, P(7f)));
            if (i == 0) { strengthValue = val; strengthFill = fl; } else { healthValue = val; healthFill = fl; }
        }

        // command row, bottom right, sharing the panel's bottom edge
        float bh = UiTheme.BtnHBig, gap = UiTheme.GapIn;
        rotateBtn = MakeBtn(pt, "RotateButton", "ROTATE", "UI_Plate", P(170f), bh, "UI_Icon_Rotate");
        UiKit.Place(rotateBtn.bg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-m, m), new Vector2(P(170f), bh));
        rotateBtn.button.onClick.AddListener(OnRotateClicked);
        chargeBtn = MakeBtn(pt, "ChargeButton", "CHARGE", "UI_PlateBronze", P(230f), bh, null, true);
        UiKit.Place(chargeBtn.bg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-m - P(170f) - gap, m), new Vector2(P(230f), bh));
        chargeBtn.button.onClick.AddListener(OnChargeClicked);
        panelGO.SetActive(false);
    }

    // ----- dev buttons: built, then hidden -----

    private void BuildDevButtons(Transform parent)
    {
        float m = UiTheme.Margin;
        var scenes = MakeBtn(parent, "ScenesButton", "SCENES", "UI_Plate", P(130f), UiTheme.BtnH);
        UiKit.Place(scenes.bg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-m, -(m + UiTheme.BtnH + UiTheme.GapIn)), new Vector2(P(130f), UiTheme.BtnH));
        devScenesGO = scenes.bg.gameObject;
        sceneRowGO = new GameObject("SceneRow", typeof(RectTransform));
        sceneRowGO.transform.SetParent(parent, false);
        UiKit.Place(sceneRowGO.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-m, -(m + (UiTheme.BtnH + UiTheme.GapIn) * 2f)), new Vector2(SceneNames.Length * P(140f), UiTheme.BtnH));
        string current = gameObject.scene.name;
        for (int i = 0; i < SceneNames.Length; i++)
        {
            string sn = SceneNames[i];
            var b = MakeBtn(sceneRowGO.transform, "Scene_" + sn, SceneLabels[i], sn == current ? "UI_PlateBlue" : "UI_Plate", P(130f), UiTheme.BtnH);
            b.bg.rectTransform.anchoredPosition = new Vector2(i * P(140f), 0f);
            if (sn == current) SetBtnEnabled(b, false); else b.button.onClick.AddListener(() => SceneManager.LoadScene(sn));
        }
        sceneRowGO.SetActive(false);
        scenes.button.onClick.AddListener(() => sceneRowGO.SetActive(!sceneRowGO.activeSelf));
        devScenesGO.SetActive(ShowDevButtons);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var speed = MakeBtn(parent, "DevSpeedButton", "SPEED 1x", "UI_Plate", P(150f), UiTheme.BtnH);
        UiKit.Place(speed.bg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-(m + P(166f) + UiTheme.GapIn), -m), new Vector2(P(150f), UiTheme.BtnH));
        speed.button.onClick.AddListener(() =>
        {
            float next = SimSpeed.Multiplier >= 2f ? 1f : 2f;
            if (next == 1f) SimSpeed.Reset(); else SimSpeed.Set(next, false);
            speed.label.text = "SPEED " + next + "x";
        });
        devSpeedGO = speed.bg.gameObject;
        devSpeedGO.SetActive(ShowDevButtons);
        BuildAdminPanel(parent);
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // admin panel: an ADMIN toggle under the top-right buttons opens a column of instant test actions
    private GameObject adminColumn;

    private void BuildAdminPanel(Transform parent)
    {
        float m = UiTheme.Margin, w = P(190f), h = UiTheme.BtnH * 0.85f, gap = P(8f);
        var toggle = MakeBtn(parent, "AdminToggle", "ADMIN", "UI_Plate", w, h, null, false, P(17f), 5f);
        UiKit.Place(toggle.bg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-m, -(m + UiTheme.BtnH + UiTheme.GapIn)), new Vector2(w, h));
        adminColumn = new GameObject("AdminColumn", typeof(RectTransform));
        adminColumn.transform.SetParent(parent, false);
        UiKit.Place(adminColumn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-m, -(m + UiTheme.BtnH + UiTheme.GapIn + h + gap)), new Vector2(w, 11 * (h + gap)));
        var items = new (string, string, System.Action)[]
        {
            ("START", "UI_PlateBronze", () => { if (BattleSetup.Instance != null) BattleSetup.Instance.StartBattle(); }),
            ("WIN NOW", "UI_PlateBronze", AdminCommands.Win),
            ("LOSE NOW", "UI_PlateDanger", AdminCommands.Lose),
            ("DRAW NOW", "UI_Plate", AdminCommands.Draw),
            ("ROUT ENEMY 5", "UI_Plate", () => AdminCommands.Rout(Team.Red, 5)),
            ("ROUT ENEMY ALL", "UI_Plate", () => AdminCommands.Rout(Team.Red, 8)),
            ("ROUT MINE 5", "UI_Plate", () => AdminCommands.Rout(Team.Blue, 5)),
            ("WIPE ENEMY", "UI_PlateDanger", () => AdminCommands.Wipe(Team.Red)),
            ("WIPE MINE", "UI_PlateDanger", () => AdminCommands.Wipe(Team.Blue)),
            ("NORMAL SPEED", "UI_Plate", () => AdminCommands.Speed(1f)),
            ("DOUBLE SPEED", "UI_Plate", () => AdminCommands.Speed(2f)),
        };
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            var b = MakeBtn(adminColumn.transform, "Admin_" + it.Item1, it.Item1, it.Item2, w, h, null, false, P(16f), 4f);
            b.bg.rectTransform.anchoredPosition = new Vector2(0f, -i * (h + gap));
            b.bg.rectTransform.anchorMin = b.bg.rectTransform.anchorMax = new Vector2(0f, 1f);
            var act = it.Item3;
            b.button.onClick.AddListener(() => act());
        }
        adminColumn.SetActive(false);
        toggle.button.onClick.AddListener(() => adminColumn.SetActive(!adminColumn.activeSelf));
    }
#endif

    private void BuildStartUI(Transform parent)
    {
        startRoot = new GameObject("StartUI", typeof(RectTransform));
        startRoot.transform.SetParent(parent, false);
        UiKit.Place(startRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, UiTheme.Margin), new Vector2(P(260f), P(76f)));
        var start = MakeBtn(startRoot.transform, "StartButton", "START", "UI_PlateBronze", P(260f), P(76f), null, false, P(26f), 9f);
        start.bg.rectTransform.anchoredPosition = Vector2.zero;
        start.button.onClick.AddListener(() => { if (BattleSetup.Instance != null) BattleSetup.Instance.StartBattle(); });
    }

    // ----- result screen -----

    private void BuildEndUI(Transform parent)
    {
        endRoot = new GameObject("EndUI", typeof(RectTransform));
        endRoot.transform.SetParent(parent, false);
        UiKit.Fill(endRoot.GetComponent<RectTransform>());
        Transform et = endRoot.transform;
        var blocker = UiKit.Img(et, "EndBlocker", null, new Color(0.04f, 0.05f, 0.08f, 0.72f));
        blocker.raycastTarget = true; blocker.rectTransform.anchorMin = Vector2.zero; blocker.rectTransform.anchorMax = Vector2.one; blocker.rectTransform.offsetMin = blocker.rectTransform.offsetMax = Vector2.zero;   // dims the field and swallows taps

        Vector2 top = new Vector2(0.5f, 1f);
        float panelW = P(1100f), panelH = P(704f);
        var panel = UiKit.Img(et, "EndPanel", UiKit.Spr("UI_Panel"), Color.white); panel.pixelsPerUnitMultiplier = 1.8f;
        UiKit.Place(panel.rectTransform, top, top, new Vector2(0f, -P(116f)), new Vector2(panelW, panelH));
        var ribbon = UiKit.Img(et, "EndRibbon", UiKit.Spr("UI_Ribbon"), Color.white);
        UiKit.Place(ribbon.rectTransform, top, top, new Vector2(0f, -P(52f)), new Vector2(P(620f), P(112f)));
        resultText = UiKit.Tmp(et, "ResultText", "VICTORY", UiTheme.Cinzel, P(54f), UiTheme.PanelNavy, TextAlignmentOptions.Midline, 14f);
        UiKit.Place(resultText.rectTransform, top, top, new Vector2(0f, -P(52f) - P(2f)), new Vector2(P(560f), P(104f)));
        endReasonText = UiKit.Tmp(et, "EndReasonText", "", UiTheme.Medium, P(19f), UiTheme.InkMuted, TextAlignmentOptions.Midline);
        UiKit.Place(endReasonText.rectTransform, top, top, new Vector2(0f, -P(210f) + P(14f)), new Vector2(P(900f), P(30f)));

        float colW = P(500f), rowH = P(44f), rowGap = P(8f);
        for (int side = 0; side < 2; side++)
        {
            float cx = (side == 0 ? -1f : 1f) * P(265f);
            endHeader[side] = UiKit.Tmp(et, "EndHeader" + side, side == 0 ? "YOUR ARMY" : "ENEMY", UiTheme.SemiBold, P(17f), side == 0 ? new Color(0.62f, 0.8f, 1f) : UiTheme.EnemyTint, TextAlignmentOptions.MidlineRight, 14f);
            UiKit.Place(endHeader[side].rectTransform, top, new Vector2(1f, 0.5f), new Vector2(cx - P(4f), -P(264f)), new Vector2(P(240f), P(34f)));
            endTotal[side] = UiKit.Tmp(et, "EndTotal" + side, "", UiTheme.Bold, P(26f), UiTheme.Ink, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(endTotal[side].rectTransform, top, new Vector2(0f, 0.5f), new Vector2(cx + P(4f), -P(264f)), new Vector2(P(240f), P(36f)));
            for (int r = 0; r < 8; r++)
            {
                int k = side * 8 + r; float cy = -(P(293f) + r * (rowH + rowGap) + rowH * 0.5f);
                var plate = UiKit.Img(et, "EndRow" + k, UiKit.Spr("UI_Row"), Color.white, rowH);
                UiKit.Place(plate.rectTransform, top, new Vector2(0.5f, 0.5f), new Vector2(cx, cy), new Vector2(colW, rowH));
                endRowPlate[k] = plate; endRow[k] = plate.gameObject;
                endRowIcon[k] = UiKit.Img(plate.transform, "Icon", null, Color.white); endRowIcon[k].preserveAspect = true;
                UiKit.Place(endRowIcon[k].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(P(14f), 0f), new Vector2(P(21f), P(36f)));
                endRowName[k] = UiKit.Tmp(plate.transform, "Name", "", UiTheme.SemiBold, P(20f), UiTheme.Ink, TextAlignmentOptions.MidlineLeft);
                UiKit.Place(endRowName[k].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(P(46f), 0f), new Vector2(P(170f), rowH));
                var tr = UiKit.Img(plate.transform, "BarTrack", UiKit.Spr("UI_Pill"), UiTheme.TrackDark, P(8f));
                UiKit.Place(tr.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(P(233f), 0f), new Vector2(P(150f), P(8f)));
                endRowFill[k] = UiKit.Img(plate.transform, "BarFill", UiKit.Spr("UI_Pill"), side == 0 ? Color.Lerp(UiTheme.BannerBlue, UiTheme.BannerBlueHi, 0.55f) : Color.Lerp(UiTheme.BannerRed, UiTheme.BannerRedHi, 0.55f), P(8f));
                UiKit.Place(endRowFill[k].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(P(233f), 0f), new Vector2(P(150f), P(8f)));
                endRowCount[k] = UiKit.Tmp(plate.transform, "Count", "", UiTheme.Bold, P(19f), UiTheme.Ink, TextAlignmentOptions.MidlineRight);
                UiKit.Place(endRowCount[k].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-P(18f), 0f), new Vector2(P(84f), rowH));
            }
        }
        var rematch = MakeBtn(et, "RestartButton", "REMATCH", "UI_PlateBronze", P(230f), P(60f), "UI_Icon_Restart", false, P(21f), 8f);
        UiKit.Place(rematch.bg.rectTransform, top, top, new Vector2(0f, -(P(116f) + panelH - P(26f) - P(60f))), new Vector2(P(230f), P(60f)));
        rematch.button.onClick.AddListener(() => { if (BattleSetup.Instance != null) BattleSetup.Instance.RestartBattle(); });
        // the rect pivot is top-left for buttons, so centre it
        rematch.bg.rectTransform.pivot = new Vector2(0.5f, 1f);
        startRoot.SetActive(true);
        endRoot.SetActive(false);
    }

    // result screen content, from the player's side (Blue): VICTORY / DEFEAT / DRAW, why it ended, and the soldiers still standing per formation
    private void FillEndSummary(string result)
    {
        var bs = BattleSetup.Instance;
        if (bs == null) return;
        int Total(Team t) { int n = 0; foreach (var f in bs.formations) if (f.team == t) n++; return n; }
        int Broken(Team t) { int n = 0; foreach (var f in bs.formations) if (f.team == t && (f.soldiers.Count == 0 || f.State == FormationState.Routing)) n++; return n; }
        bool won = result.StartsWith("BLUE WINS"), lost = result.StartsWith("RED WINS");
        resultText.text = won ? "VICTORY" : lost ? "DEFEAT" : "DRAW";
        string reason;
        if (result.Contains("COLLAPSED"))
        {
            Team loser = won ? Team.Red : Team.Blue;
            reason = (!won && !lost ? "Both lines collapsed: " + Broken(Team.Blue) + " and " + Broken(Team.Red) + " formations routed or destroyed"
                   : (loser == Team.Red ? "The enemy line collapsed" : "Your line collapsed") + ": " + Broken(loser) + " of " + Total(loser) + " formations routed or destroyed");
        }
        else if (result.Contains("TIME"))
        {
            int b = bs.CountIntact(Team.Blue), r = bs.CountIntact(Team.Red);
            reason = b == r ? "Time expired: both armies equal at " + b + " intact formations"
                            : "Time expired: " + (b > r ? "you held" : "the enemy held") + " more intact formations (" + Mathf.Max(b, r) + " to " + Mathf.Min(b, r) + ")";
        }
        else reason = result;
        endReasonText.text = reason;

        for (int side = 0; side < 2; side++)
        {
            Team team = side == 0 ? Team.Blue : Team.Red;
            int spawned = 0, alive = 0, row = 0;
            foreach (var f in bs.formations)
            {
                if (f.team != team) continue;
                spawned += f.TotalSpawned; alive += f.soldiers.Count;
                if (row >= 8) continue;
                int k = side * 8 + row++;
                endRow[k].SetActive(true);
                endRowName[k].text = RomanNumerals.Pretty(f.displayName.Replace("Blue ", "").Replace("Red ", ""));
                endRowCount[k].text = UiTheme.Tab(f.soldiers.Count) + " / " + UiTheme.Tab(f.TotalSpawned);
                float frac = f.TotalSpawned > 0 ? Mathf.Clamp01((float)f.soldiers.Count / f.TotalSpawned) : 0f;
                float bar = P(150f);
                endRowFill[k].rectTransform.sizeDelta = new Vector2(Mathf.Max(P(8f), bar * frac), P(8f));
                endRowFill[k].enabled = f.soldiers.Count > 0;
                var icon = UiKit.Spr(BannerName(team, f.stats.isRanged));
                endRowIcon[k].sprite = icon; endRowIcon[k].enabled = icon != null;
                float a = f.soldiers.Count == 0 ? 0.5f : 1f;   // a wiped-out formation is dimmed
                endRowPlate[k].color = new Color(1f, 1f, 1f, a); endRowName[k].color = new Color(1f, 1f, 1f, a); endRowCount[k].color = new Color(1f, 1f, 1f, a);
            }
            for (; row < 8; row++) endRow[side * 8 + row].SetActive(false);
            endTotal[side].text = UiTheme.Tab(alive) + " / " + UiTheme.Tab(spawned);
        }
    }
}

// Keeps a full-canvas RectTransform inside Screen.safeArea (phone notches, rounded corners). Applied once on creation
// and re-applied if the safe area changes (orientation, foldables).
public class SafeAreaFitter : MonoBehaviour
{
    private Rect applied = new Rect(-1f, -1f, -1f, -1f);

    private void Awake() => Apply();

    private void Update()
    {
        if (Screen.safeArea != applied) Apply();
    }

    private void Apply()
    {
        applied = Screen.safeArea;
        var rt = (RectTransform)transform;
        Vector2 min = applied.position;
        Vector2 max = applied.position + applied.size;
        min.x /= Screen.width; min.y /= Screen.height; max.x /= Screen.width; max.y /= Screen.height;
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
