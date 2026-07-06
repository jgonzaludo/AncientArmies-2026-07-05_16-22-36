using System.Collections.Generic;
using UnityEngine;
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
    private const float ButtonHeight = 76f;
    private static readonly Color EnemyTint = new Color(1f, 0.45f, 0.45f);
    private static readonly Color ReasonColor = new Color(1f, 0.55f, 0.5f);

    private PlayerCommander commander;
    private Font font;

    private GameObject panelGO;
    private Text infoText;
    private Text reasonText;
    private Text hintText;

    private Button breakButton;
    private Button reformButton;
    private Button rotateButton;

    // ---------------- lifecycle ----------------

    private void Start()
    {
        commander = GetComponent<PlayerCommander>();
        if (commander == null) commander = FindFirstObjectByType<PlayerCommander>();

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildUI();
    }

    private void Update()
    {
        if (commander == null || panelGO == null) return;

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
        SetCommandButtonsVisible(true);
        UpdateButtonInteractivity(selection);

        if (liveCount == 1)
        {
            rotateButton.gameObject.SetActive(true);
            UpdateRotateButton();
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

        breakButton.interactable = breakInteractable;
        reformButton.interactable = anyCanReform;
        reasonText.text = (considered > 0 && !anyCanReform && anyBlockingState) ? "Too close to enemy" : "";
    }

    private void UpdateRotateButton()
    {
        Text label = rotateButton.GetComponentInChildren<Text>();
        if (label != null) label.text = commander.RotateMode ? "CANCEL" : "ROTATE";
        rotateButton.interactable = commander.CanEnterRotateMode || commander.RotateMode;
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

        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- EventSystem (only if none exists) ----
        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        Transform canvasT = canvasGO.transform;

        // ---- Hint text (top-left) ----
        hintText = MakeText(canvasT, "HintText", 22, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f));
        hintText.text = "Tap: select formation · Drag from selection: move / attack · Drag ground: pan · Scroll or pinch: zoom";
        RectTransform hintRT = hintText.rectTransform;
        hintRT.anchorMin = new Vector2(0f, 1f);
        hintRT.anchorMax = new Vector2(0f, 1f);
        hintRT.pivot = new Vector2(0f, 1f);
        hintRT.anchoredPosition = new Vector2(25f, -20f);
        hintRT.sizeDelta = new Vector2(1000f, 50f);

        BuildBottomPanel(canvasT);
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
        panelImage.color = new Color(0.08f, 0.08f, 0.11f, 0.92f);

        Transform panelT = panelGO.transform;

        // ---- Info text (left-middle) ----
        infoText = MakeText(panelT, "InfoText", 30, TextAnchor.MiddleLeft, Color.white);
        RectTransform infoRT = infoText.rectTransform;
        infoRT.anchorMin = new Vector2(0f, 0.5f);
        infoRT.anchorMax = new Vector2(0f, 0.5f);
        infoRT.pivot = new Vector2(0f, 0.5f);
        infoRT.anchoredPosition = new Vector2(30f, 0f);
        infoRT.sizeDelta = new Vector2(700f, 150f);

        // ---- Command buttons (right) ----
        breakButton = MakeButton(panelT, "BreakRanksButton", "BREAK RANKS", new Vector2(-690f, 47f));
        breakButton.onClick.AddListener(OnBreakRanksClicked);

        reformButton = MakeButton(panelT, "ReformButton", "REFORM", new Vector2(-460f, 47f));
        reformButton.onClick.AddListener(OnReformClicked);

        rotateButton = MakeButton(panelT, "RotateButton", "ROTATE", new Vector2(-230f, 47f));
        rotateButton.onClick.AddListener(OnRotateClicked);

        // ---- Reason text (just above Reform) ----
        reasonText = MakeText(panelT, "ReasonText", 20, TextAnchor.MiddleCenter, ReasonColor, false);
        reasonText.text = "";
        RectTransform reasonRT = reasonText.rectTransform;
        reasonRT.anchorMin = new Vector2(1f, 0f);
        reasonRT.anchorMax = new Vector2(1f, 0f);
        reasonRT.pivot = new Vector2(1f, 0f);
        reasonRT.anchoredPosition = new Vector2(-460f, 47f + ButtonHeight + 3f);
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

    private Button MakeButton(Transform parent, string name, string label, Vector2 anchoredPos)
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
        image.color = new Color(0.16f, 0.16f, 0.22f, 1f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.22f, 0.22f, 0.3f, 1f);
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        button.colors = colors;

        Text labelText = MakeText(go.transform, "Label", 26, TextAnchor.MiddleCenter, Color.white, false);
        labelText.text = label;
        RectTransform labelRT = labelText.rectTransform;
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        return button;
    }
}
