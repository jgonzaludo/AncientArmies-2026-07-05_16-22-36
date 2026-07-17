using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// Minimal runtime-built HUD: a top-left control hint plus a contextual bottom
// panel that shows friendly formation info + commands (single or multi
// selection) or read-only enemy inspection info. Everything is constructed in
// code at Start() against PlayerCommander/Formation — no prefabs, no scene UI.
public class BattleHUD : MonoBehaviour
{
    private const float ButtonWidth = 215f;
    private const float ButtonHeight = 84f;
    private const float ButtonGap = 14f;
    private static readonly Color EnemyTint = new Color(1f, 0.45f, 0.45f);
    private static readonly Color ReasonColor = new Color(1f, 0.55f, 0.5f);
    private static readonly Color ButtonColor = new Color(0.21f, 0.23f, 0.31f);
    private static readonly Color AccentColor = new Color(0.18f, 0.42f, 0.8f);

    private static Sprite roundedSprite;   // shared 9-sliced rounded rect

    private PlayerCommander commander;
    private Font font;

    private GameObject panelGO;
    private Text infoText;
    private Text reasonText;
    private Text hintText;

    private Button breakButton;
    private Button reformButton;
    private Button rotateButton;
    private Button restartMiniButton;   // persistent: restart before/during a battle
    private Button bannerModeButton;    // cycles the formation-banner visibility mode

    private GameObject startRoot;
    private GameObject endRoot;
    private Text resultText;

    // Scene switcher: the full battle plus isolation sandboxes for observing
    // behavior with a handful of soldiers. Scenes must be in Build Settings.
    private static readonly string[] SceneNames = { "Battle", "TestSkirmish" };
    private static readonly string[] SceneLabels = { "BATTLE", "TEST 1" };
    private GameObject sceneRowGO;

    // ---------------- lifecycle ----------------

    private void Start()
    {
        commander = GetComponent<PlayerCommander>();
        if (commander == null) commander = FindAnyObjectByType<PlayerCommander>();

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildUI();
    }

    private void Update()
    {
        if (commander == null || panelGO == null) return;

        BattlePhase phase = BattleSetup.Instance != null ? BattleSetup.Instance.Phase
                                                         : BattlePhase.Pre;
        if (startRoot != null) startRoot.SetActive(phase == BattlePhase.Pre);
        // the small corner restart hides after battle end — the banner already
        // carries the primary RESTART button there
        if (restartMiniButton != null)
            restartMiniButton.gameObject.SetActive(phase != BattlePhase.Ended);
        if (endRoot != null)
        {
            endRoot.SetActive(phase == BattlePhase.Ended);
            if (phase == BattlePhase.Ended && resultText != null)
            {
                string r = BattleSetup.Instance.ResultText;
                resultText.text = r;
                resultText.color = r == "BLUE WINS" ? new Color(0.5f, 0.75f, 1f)
                                 : r == "RED WINS" ? new Color(1f, 0.5f, 0.45f)
                                 : Color.white;
            }
        }
        if (hintText != null) hintText.gameObject.SetActive(phase == BattlePhase.Active);
        // the command panel works during deployment too (move + rotate) —
        // only a finished battle hides it
        if (phase == BattlePhase.Ended)
        {
            panelGO.SetActive(false);
            return;
        }

        Formation enemy = commander.InspectedEnemy;
        if (enemy != null)
        {
            panelGO.SetActive(true);
            SetCommandButtonsVisible(false);
            rotateButton.gameObject.SetActive(false);
            reasonText.text = "";
            UpdateEnemyPanel(enemy);
            return;
        }

        IReadOnlyList<Formation> selection = commander.Selection;
        int liveCount = CountLive(selection);

        if (liveCount == 0)
        {
            panelGO.SetActive(false);
            return;
        }

        panelGO.SetActive(true);

        // A fully broken selection has exactly one meaningful order — REFORM.
        // Offering BREAK RANKS or ROTATE there is noise; hide them entirely.
        // Mixed selections keep the full row so the coherent formations stay
        // commandable.
        bool allBroken = AllLiveBroken(selection);
        SetCommandButtonsVisible(true);
        if (allBroken) breakButton.gameObject.SetActive(false);
        UpdateButtonInteractivity(selection);

        if (liveCount == 1)
        {
            rotateButton.gameObject.SetActive(!allBroken);
            if (!allBroken) UpdateRotateButton();
            UpdateSingleSelectionPanel(GetFirstLive(selection));
        }
        else
        {
            rotateButton.gameObject.SetActive(false);
            UpdateMultiSelectionPanel(selection, liveCount);
        }
    }

    // ---------------- panel content ----------------

    private void UpdateEnemyPanel(Formation f)
    {
        int healthPct = Mathf.RoundToInt(100f * f.TotalHealth / Mathf.Max(1f, f.TotalMaxHealth));
        string header = Colorize($"{f.displayName}  ({f.stats.unitName})  —  ENEMY", EnemyTint);
        infoText.text =
            $"{header}\n" +
            $"Health {healthPct}%   ·   Strength {f.soldiers.Count}/{f.TotalSpawned}\n" +
            $"State: {Formation.StateLabel(f.State)}";
    }

    private void UpdateSingleSelectionPanel(Formation f)
    {
        if (f == null) return;
        int healthPct = Mathf.RoundToInt(100f * f.TotalHealth / Mathf.Max(1f, f.TotalMaxHealth));
        infoText.text =
            $"{f.displayName}  ({f.stats.unitName})\n" +
            $"Health {healthPct}%   ·   Strength {f.soldiers.Count}/{f.TotalSpawned}\n" +
            $"State: {Formation.StateLabel(f.State)}";
    }

    private void UpdateMultiSelectionPanel(IReadOnlyList<Formation> selection, int liveCount)
    {
        int totalSoldiers = 0;
        float totalHealth = 0f;
        float totalMaxHealth = 0f;

        for (int i = 0; i < selection.Count; i++)
        {
            Formation f = selection[i];
            if (f == null || f.soldiers.Count == 0) continue;
            totalSoldiers += f.soldiers.Count;
            totalHealth += f.TotalHealth;
            totalMaxHealth += f.TotalMaxHealth;
        }

        int healthPct = Mathf.RoundToInt(100f * totalHealth / Mathf.Max(1f, totalMaxHealth));
        infoText.text =
            $"{liveCount} formations selected\n" +
            $"{totalSoldiers} soldiers   ·   Health {healthPct}%";
    }

    private void UpdateButtonInteractivity(IReadOnlyList<Formation> selection)
    {
        bool breakInteractable = false;
        bool anyCanReform = false;
        bool anyBlockingState = false;
        int considered = 0;

        for (int i = 0; i < selection.Count; i++)
        {
            Formation f = selection[i];
            if (f == null || f.soldiers.Count == 0) continue;
            considered++;

            if (f.State != FormationState.BrokenRanks) breakInteractable = true;
            if (f.CanReform) anyCanReform = true;
            if (f.State == FormationState.Engaged ||
                f.State == FormationState.BrokenRanks ||
                f.State == FormationState.Withdrawing)
                anyBlockingState = true;
        }

        SetButtonEnabled(breakButton, breakInteractable);
        SetButtonEnabled(reformButton, anyCanReform);
        reasonText.text = (considered > 0 && !anyCanReform && anyBlockingState) ? "Too close to enemy" : "";
    }

    private void UpdateRotateButton()
    {
        Text label = rotateButton.GetComponentInChildren<Text>();
        if (label != null) label.text = commander.RotateMode ? "CANCEL" : "ROTATE";
        SetButtonEnabled(rotateButton, commander.CanEnterRotateMode || commander.RotateMode);
    }

    // Disabled buttons must read as disabled: dim the label along with the
    // background instead of leaving bright text on a faded button.
    private static void SetButtonEnabled(Button b, bool on)
    {
        b.interactable = on;
        Text label = b.GetComponentInChildren<Text>();
        if (label != null)
            label.color = new Color(1f, 1f, 1f, on ? 1f : 0.4f);
    }

    private void SetCommandButtonsVisible(bool visible)
    {
        breakButton.gameObject.SetActive(visible);
        reformButton.gameObject.SetActive(visible);
    }

    // ---------------- selection helpers ----------------

    private static int CountLive(IReadOnlyList<Formation> selection)
    {
        if (selection == null) return 0;
        int count = 0;
        for (int i = 0; i < selection.Count; i++)
            if (selection[i] != null && selection[i].soldiers.Count > 0) count++;
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
        for (int i = 0; i < selection.Count; i++)
            if (selection[i] != null && selection[i].soldiers.Count > 0) return selection[i];
        return null;
    }

    private static string Colorize(string text, Color c)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";
    }

    // ---------------- button commands ----------------

    private void OnBreakRanksClicked()
    {
        if (commander == null || commander.Selection == null) return;
        foreach (var f in commander.Selection)
            if (f != null && f.soldiers.Count > 0) f.IssueBreakRanks();
    }

    private void OnReformClicked()
    {
        if (commander == null || commander.Selection == null) return;
        foreach (var f in commander.Selection)
            if (f != null && f.CanReform) f.IssueReform();
    }

    private void OnRotateClicked()
    {
        if (commander != null) commander.ToggleRotateMode();
    }

    // ---------------- UI construction ----------------

    private void BuildUI()
    {
        // ---- Canvas ----
        var canvasGO = new GameObject("HUDCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;   // scale by height: stable landscape sizing

        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- EventSystem (only if none exists) ----
        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        // ---- Safe area root: all HUD content stays clear of notches ----
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        safeGO.AddComponent<RectTransform>();
        safeGO.AddComponent<SafeAreaFitter>();
        Transform canvasT = safeGO.transform;

        // ---- Hint text (top-left) ----
        hintText = MakeText(canvasT, "HintText", 24, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f));
        hintText.text = "Tap: select unit · Drag from unit: move / attack (then deselects) · Drag ground: pan · Pinch: zoom";
        RectTransform hintRT = hintText.rectTransform;
        hintRT.anchorMin = new Vector2(0f, 1f);
        hintRT.anchorMax = new Vector2(0f, 1f);
        hintRT.pivot = new Vector2(0f, 1f);
        hintRT.anchoredPosition = new Vector2(25f, -20f);
        hintRT.sizeDelta = new Vector2(1000f, 50f);

        // ---- Persistent restart (top-right): available before Start and
        // mid-battle; the end banner has its own primary RESTART ----
        restartMiniButton = MakeButton(canvasT, "RestartMiniButton", "RESTART", Vector2.zero);
        var rmRT = restartMiniButton.GetComponent<RectTransform>();
        rmRT.anchorMin = rmRT.anchorMax = new Vector2(1f, 1f);
        rmRT.pivot = new Vector2(1f, 1f);
        rmRT.anchoredPosition = new Vector2(-25f, -20f);
        rmRT.sizeDelta = new Vector2(170f, 64f);
        var rmLabel = restartMiniButton.GetComponentInChildren<Text>();
        if (rmLabel != null) rmLabel.fontSize = 24;
        restartMiniButton.onClick.AddListener(() =>
        {
            if (BattleSetup.Instance != null) BattleSetup.Instance.RestartBattle();
        });

        // ---- Banner visibility mode cycle (top-left, under the hint) ----
        bannerModeButton = MakeButton(canvasT, "BannerModeButton", "BANNERS", Vector2.zero);
        var bmRT = bannerModeButton.GetComponent<RectTransform>();
        bmRT.anchorMin = bmRT.anchorMax = new Vector2(0f, 1f);
        bmRT.pivot = new Vector2(0f, 1f);
        bmRT.anchoredPosition = new Vector2(25f, -74f);
        bmRT.sizeDelta = new Vector2(300f, 56f);
        var bmLabel = bannerModeButton.GetComponentInChildren<Text>();
        if (bmLabel != null) bmLabel.fontSize = 22;
        bannerModeButton.onClick.AddListener(() =>
        {
            if (FormationBannerManager.Instance != null)
                FormationBannerManager.Instance.CycleMode();
            RefreshBannerModeLabel();
        });
        RefreshBannerModeLabel();

        BuildBottomPanel(canvasT);
        BuildStartAndEndUI(canvasT);
        BuildSceneSwitcher(canvasT);
    }

    private void RefreshBannerModeLabel()
    {
        var label = bannerModeButton != null ? bannerModeButton.GetComponentInChildren<Text>() : null;
        if (label == null || FormationBannerManager.Instance == null) return;
        switch (FormationBannerManager.Instance.Mode)
        {
            case FormationBannerVisibilityMode.AlwaysVisible: label.text = "BANNERS: ALWAYS"; break;
            case FormationBannerVisibilityMode.SelectedOnly: label.text = "BANNERS: SELECTED"; break;
            default: label.text = "BANNERS: ADAPTIVE"; break;
        }
    }

    // Top-center SCENES button expanding a row of scene buttons; the current
    // scene renders accented and disabled, others load on tap.
    private void BuildSceneSwitcher(Transform canvasT)
    {
        Button scenesBtn = MakeButton(canvasT, "ScenesButton", "SCENES", Vector2.zero);
        var sRT = scenesBtn.GetComponent<RectTransform>();
        sRT.anchorMin = sRT.anchorMax = new Vector2(0.5f, 1f);
        sRT.pivot = new Vector2(0.5f, 1f);
        sRT.anchoredPosition = new Vector2(0f, -20f);
        sRT.sizeDelta = new Vector2(170f, 64f);
        var sLabel = scenesBtn.GetComponentInChildren<Text>();
        if (sLabel != null) sLabel.fontSize = 24;

        sceneRowGO = new GameObject("SceneRow");
        sceneRowGO.transform.SetParent(canvasT, false);
        var rowRT = sceneRowGO.AddComponent<RectTransform>();
        rowRT.anchorMin = rowRT.anchorMax = new Vector2(0.5f, 1f);
        rowRT.pivot = new Vector2(0.5f, 1f);
        rowRT.anchoredPosition = new Vector2(0f, -94f);
        rowRT.sizeDelta = new Vector2(SceneNames.Length * 184f, 64f);

        string current = gameObject.scene.name;
        for (int i = 0; i < SceneNames.Length; i++)
        {
            string sceneName = SceneNames[i];
            bool isCurrent = sceneName == current;
            Button b = MakeButton(sceneRowGO.transform, "Scene_" + sceneName, SceneLabels[i],
                                  Vector2.zero, accent: isCurrent);
            var bRT = b.GetComponent<RectTransform>();
            bRT.anchorMin = bRT.anchorMax = new Vector2(0.5f, 1f);
            bRT.pivot = new Vector2(0.5f, 1f);
            bRT.anchoredPosition = new Vector2((i - (SceneNames.Length - 1) * 0.5f) * 184f, 0f);
            bRT.sizeDelta = new Vector2(170f, 64f);
            var lbl = b.GetComponentInChildren<Text>();
            if (lbl != null) lbl.fontSize = 24;
            if (isCurrent) SetButtonEnabled(b, false);
            else b.onClick.AddListener(() => SceneManager.LoadScene(sceneName));
        }
        sceneRowGO.SetActive(false);
        scenesBtn.onClick.AddListener(() => sceneRowGO.SetActive(!sceneRowGO.activeSelf));
    }

    // Lightweight battle entry/exit UI: a START button before the battle and a
    // result banner + RESTART button after it. No menus, no modals.
    private void BuildStartAndEndUI(Transform canvasT)
    {
        startRoot = new GameObject("StartUI");
        startRoot.transform.SetParent(canvasT, false);
        var startRT = startRoot.AddComponent<RectTransform>();
        startRT.anchorMin = startRT.anchorMax = new Vector2(0.5f, 0f);
        startRT.pivot = new Vector2(0.5f, 0f);
        startRT.anchoredPosition = new Vector2(0f, 70f);
        startRT.sizeDelta = new Vector2(360f, 120f);

        Button startBtn = MakeButton(startRoot.transform, "StartButton", "START", Vector2.zero, accent: true);
        var sbRT = startBtn.GetComponent<RectTransform>();
        sbRT.anchorMin = Vector2.zero;
        sbRT.anchorMax = Vector2.one;
        sbRT.pivot = new Vector2(0.5f, 0.5f);
        sbRT.offsetMin = Vector2.zero;
        sbRT.offsetMax = Vector2.zero;
        var startLabel = startBtn.GetComponentInChildren<Text>();
        if (startLabel != null) startLabel.fontSize = 44;
        startBtn.onClick.AddListener(() =>
        {
            if (BattleSetup.Instance != null) BattleSetup.Instance.StartBattle();
        });

        endRoot = new GameObject("EndUI");
        endRoot.transform.SetParent(canvasT, false);
        var endRT = endRoot.AddComponent<RectTransform>();
        endRT.anchorMin = endRT.anchorMax = new Vector2(0.5f, 0.5f);
        endRT.pivot = new Vector2(0.5f, 0.5f);
        endRT.anchoredPosition = Vector2.zero;
        endRT.sizeDelta = new Vector2(900f, 320f);

        resultText = MakeText(endRoot.transform, "ResultText", 84, TextAnchor.MiddleCenter, Color.white);
        var resRT = resultText.rectTransform;
        resRT.anchorMin = new Vector2(0.5f, 0.5f);
        resRT.anchorMax = new Vector2(0.5f, 0.5f);
        resRT.pivot = new Vector2(0.5f, 0.5f);
        resRT.anchoredPosition = new Vector2(0f, 90f);
        resRT.sizeDelta = new Vector2(900f, 130f);

        Button restartBtn = MakeButton(endRoot.transform, "RestartButton", "RESTART", Vector2.zero, accent: true);
        var rbRT = restartBtn.GetComponent<RectTransform>();
        rbRT.anchorMin = rbRT.anchorMax = new Vector2(0.5f, 0.5f);
        rbRT.pivot = new Vector2(0.5f, 0.5f);
        rbRT.anchoredPosition = new Vector2(0f, -60f);
        rbRT.sizeDelta = new Vector2(360f, 110f);
        var restartLabel = restartBtn.GetComponentInChildren<Text>();
        if (restartLabel != null) restartLabel.fontSize = 40;
        restartBtn.onClick.AddListener(() =>
        {
            if (BattleSetup.Instance != null) BattleSetup.Instance.RestartBattle();
        });

        startRoot.SetActive(true);
        endRoot.SetActive(false);
    }

    private void BuildBottomPanel(Transform canvasT)
    {
        panelGO = new GameObject("BottomPanel");
        panelGO.transform.SetParent(canvasT, false);

        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 0f);
        panelRT.anchorMax = new Vector2(1f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(0f, 170f);

        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0.09f, 0.1f, 0.14f, 0.94f);

        Transform panelT = panelGO.transform;

        // ---- Info text (left-middle) ----
        infoText = MakeText(panelT, "InfoText", 32, TextAnchor.MiddleLeft, Color.white);
        infoText.lineSpacing = 1.15f;   // breathing room between the three info lines
        RectTransform infoRT = infoText.rectTransform;
        infoRT.anchorMin = new Vector2(0f, 0.5f);
        infoRT.anchorMax = new Vector2(0f, 0.5f);
        infoRT.pivot = new Vector2(0f, 0.5f);
        infoRT.anchoredPosition = new Vector2(30f, 0f);
        infoRT.sizeDelta = new Vector2(700f, 150f);

        // ---- Command buttons: a right-aligned row with uniform spacing ----
        const float margin = 25f;
        float buttonY = (170f - ButtonHeight) * 0.5f;
        float step = ButtonWidth + ButtonGap;

        breakButton = MakeButton(panelT, "BreakRanksButton", "BREAK RANKS",
                                 new Vector2(-margin - 2f * step, buttonY));
        breakButton.onClick.AddListener(OnBreakRanksClicked);

        reformButton = MakeButton(panelT, "ReformButton", "REFORM",
                                  new Vector2(-margin - step, buttonY));
        reformButton.onClick.AddListener(OnReformClicked);

        rotateButton = MakeButton(panelT, "RotateButton", "ROTATE",
                                  new Vector2(-margin, buttonY));
        rotateButton.onClick.AddListener(OnRotateClicked);

        // ---- Reason text (just above Reform) ----
        reasonText = MakeText(panelT, "ReasonText", 22, TextAnchor.MiddleCenter, ReasonColor, false);
        reasonText.text = "";
        RectTransform reasonRT = reasonText.rectTransform;
        reasonRT.anchorMin = new Vector2(1f, 0f);
        reasonRT.anchorMax = new Vector2(1f, 0f);
        reasonRT.pivot = new Vector2(1f, 0f);
        reasonRT.anchoredPosition = new Vector2(-margin - step, buttonY + ButtonHeight + 3f);
        reasonRT.sizeDelta = new Vector2(ButtonWidth, 28f);

        panelGO.SetActive(false);
    }

    // ---------------- helpers ----------------

    private Text MakeText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color, bool shadow = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        if (shadow)
        {
            var sh = go.AddComponent<Shadow>();
            sh.effectDistance = new Vector2(1.5f, -1.5f);
        }

        return text;
    }

    private Button MakeButton(Transform parent, string name, string label, Vector2 anchoredPos,
                              bool accent = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

        var image = go.AddComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        Color baseColor = accent ? AccentColor : ButtonColor;
        image.color = baseColor;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.3f);
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
        button.colors = colors;

        Text labelText = MakeText(go.transform, "Label", 28, TextAnchor.MiddleCenter, Color.white, false);
        labelText.text = label;
        RectTransform labelRT = labelText.rectTransform;
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(10f, 6f);
        labelRT.offsetMax = new Vector2(-10f, -6f);

        return button;
    }

    // Shared 9-sliced rounded-rectangle sprite so every button reads as one
    // intentional family instead of raw quads. Generated once, tinted per use.
    // The corner radius stays small (square-ish, not pill) and a subtle darker
    // rim is baked just inside the edge — it survives per-use tinting because
    // the sprite is still near-white.
    private static Sprite RoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;
        const int size = 64;
        const int radius = 12;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (size - 1 - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (size - 1 - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);   // 1 inside, soft 1px edge
                float rgb = (a > 0.05f && radius - d < 2f) ? 0.85f : 1f;   // darker rim
                tex.SetPixel(x, y, new Color(rgb, rgb, rgb, a));
            }
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        roundedSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size),
                                      new Vector2(0.5f, 0.5f), 1f, 0,
                                      SpriteMeshType.FullRect,
                                      new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        return roundedSprite;
    }
}

// Keeps a full-canvas RectTransform inside Screen.safeArea (phone notches,
// rounded corners). Applied once on creation and re-applied if the safe area
// changes (orientation, foldables).
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
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
