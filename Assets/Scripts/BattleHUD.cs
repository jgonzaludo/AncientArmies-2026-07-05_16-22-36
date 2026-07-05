using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// Minimal runtime-built HUD: shows the selected formation's state and exposes
// Break Ranks / Reform commands. No prefabs or scene UI — everything is
// constructed in code against PlayerCommander/Formation.
public class BattleHUD : MonoBehaviour
{
    private PlayerCommander commander;
    private Font font;

    private Text infoText;
    private Text instructionsText;
    private Button breakButton;
    private Button reformButton;

    private void Start()
    {
        commander = GetComponent<PlayerCommander>();
        if (commander == null) commander = FindFirstObjectByType<PlayerCommander>();

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildUI();
    }

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

        // ---- Info text (bottom-left) ----
        infoText = MakeText(canvasT, "InfoText", 30, TextAnchor.LowerLeft, Color.white);
        RectTransform infoRT = infoText.rectTransform;
        infoRT.anchorMin = new Vector2(0f, 0f);
        infoRT.anchorMax = new Vector2(0f, 0f);
        infoRT.pivot = new Vector2(0f, 0f);
        infoRT.anchoredPosition = new Vector2(25f, 25f);
        infoRT.sizeDelta = new Vector2(600f, 190f);

        var infoShadow = infoText.gameObject.AddComponent<Shadow>();
        infoShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // ---- Instructions text (top-left) ----
        instructionsText = MakeText(canvasT, "InstructionsText", 24, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.75f));
        instructionsText.text = "Left click: select blue formation   |   Right click: move / attack\n" +
                                 "Move while engaged = withdraw   ·   Reform requires separation from the enemy";
        RectTransform instrRT = instructionsText.rectTransform;
        instrRT.anchorMin = new Vector2(0f, 1f);
        instrRT.anchorMax = new Vector2(0f, 1f);
        instrRT.pivot = new Vector2(0f, 1f);
        instrRT.anchoredPosition = new Vector2(25f, -20f);
        instrRT.sizeDelta = new Vector2(900f, 120f);

        var instrShadow = instructionsText.gameObject.AddComponent<Shadow>();
        instrShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // ---- Buttons (bottom-right) ----
        breakButton = MakeButton(canvasT, "BREAK RANKS", new Vector2(-500f, 40f));
        breakButton.onClick.AddListener(() =>
        {
            if (commander != null && commander.Selected != null) commander.Selected.IssueBreakRanks();
        });

        reformButton = MakeButton(canvasT, "REFORM", new Vector2(-250f, 40f));
        reformButton.onClick.AddListener(() =>
        {
            if (commander != null && commander.Selected != null) commander.Selected.IssueReform();
        });
    }

    private void Update()
    {
        Formation f = commander != null ? commander.Selected : null;

        if (f == null)
        {
            infoText.text = "No formation selected\nLeft-click a blue soldier.";
        }
        else
        {
            string text = $"{f.displayName}  ({f.stats.unitName})\n" +
                          $"State: {Formation.StateLabel(f.State)}\n" +
                          $"Soldiers: {f.soldiers.Count}/{f.TotalSpawned}";
            if (f.CanReform) text += "\nReform available";
            infoText.text = text;
        }

        bool has = f != null && f.soldiers.Count > 0;
        breakButton.interactable = has && f.State != FormationState.BrokenRanks;
        reformButton.interactable = has && f.CanReform;
    }

    // ---------------- helpers ----------------

    private Text MakeText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
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
        return text;
    }

    private Button MakeButton(Transform parent, string label, Vector2 anchoredPos)
    {
        var go = new GameObject(label + "Button");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(220f, 76f);

        var image = go.AddComponent<Image>();
        image.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);
        image.sprite = null;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.26f, 0.9f);
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);
        button.colors = colors;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        var labelText = labelGO.AddComponent<Text>();
        labelText.font = font;
        labelText.fontSize = 28;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = label;

        return button;
    }
}
