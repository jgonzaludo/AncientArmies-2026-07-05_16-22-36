using System.Collections.Generic;
using UnityEngine;

// One adaptive floating banner per formation, built from the supplied badge
// artwork (UI_Banner_<Faction>_<Class> sprites). Purely presentational: it
// reads the formation's authoritative state (anchor, dominant cluster,
// AnchorForward facing, health, selection) and the shared
// FormationBannerManager (detail level, visibility mode, info hold, overlap
// lift) and never owns simulation logic. The whole hierarchy is created once
// and blended between presentations — nothing is rebuilt on zoom changes, and
// nothing here carries a collider, so troop tapping is untouched.
public class FormationBannerController : MonoBehaviour
{
    [Header("Anchor")]
    [Tooltip("World height above the troops")]
    [SerializeField] private float hoverHeight = 5.4f;
    [Tooltip("Structured formations bias the banner this far toward their rear rank")]
    [SerializeField] private float rearOffset = 1.5f;
    [Tooltip("Banner position easing (1-exp(-k dt))")]
    [SerializeField] private float followSmoothing = 3f;

    [Header("Presentation")]
    [Tooltip("Detail/visibility blends take about this long")]
    [SerializeField] private float transitionSeconds = 0.2f;
    [SerializeField] private float closeScale = 0.55f;
    [SerializeField] private float compactScale = 0.85f;
    [SerializeField] private float expandedScale = 1.1f;
    [Tooltip("Ortho size at which scale multiplier is 1 (screen-constancy reference)")]
    [SerializeField] private float referenceOrthoSize = 26f;
    [Tooltip("Zoom-compensation clamp: keeps banners restrained at extreme zooms")]
    [SerializeField] private float minZoomCompensation = 0.45f;
    [SerializeField] private float maxZoomCompensation = 1.35f;
    [SerializeField] private float selectedScaleMultiplier = 1.08f;
    [SerializeField] private int selectedSortingBoost = 100;
    // v1.8.1 selection language: no outline — while anything is selected,
    // every OTHER banner dims to this alpha and the selected ones stay full.
    [Tooltip("Alpha applied to unselected banners while a selection exists")]
    [SerializeField] private float unselectedDimFactor = 0.4f;

    [Header("Order feedback")]
    [Tooltip("A hidden close-zoom banner appears this long after an order")]
    [SerializeField] private float orderFeedbackSeconds = 1.5f;

    [Header("Info elements")]
    [SerializeField] private float barWidth = 3.1f;
    [SerializeField] private float barHeight = 0.45f;

    private static readonly Color HealthHigh = new Color(0.38f, 0.86f, 0.38f);
    private static readonly Color HealthLow = new Color(0.95f, 0.7f, 0.15f);
    private static readonly Color BarBackground = new Color(0.08f, 0.08f, 0.1f, 0.85f);
    // Broken ranks reads as damage: badge desaturated toward grey and faded.
    private static readonly Color BrokenBadgeTint = new Color(0.75f, 0.7f, 0.7f);

    // Data-driven badge selection: future classes (cavalry, spears, siege...)
    // extend this table — never parse display strings for faction/class.
    private static readonly Dictionary<(Team, bool), string> BadgeSprites =
        new Dictionary<(Team, bool), string>
        {
            { (Team.Red, false), "UI_Banner_Red_Melee" },
            { (Team.Blue, false), "UI_Banner_Blue_Melee" },
            { (Team.Red, true), "UI_Banner_Red_Ranged" },
            { (Team.Blue, true), "UI_Banner_Blue_Ranged" },
        };

    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private static Sprite whiteSprite;
    private static Sprite iconMoving, iconEngaged, iconBroken, iconReforming;

    private Formation f;
    private PlayerCommander commander;
    private Camera cam;

    private Transform root, scaleContainer;
    private SpriteRenderer baseSR, stateIconSR, healthBgSR, healthFillSR;

    private float shownAlpha;            // 0..1 master visibility blend
    private float currentScale = 1f;
    private Vector3 pos;
    private bool hasPos;
    private float orderFeedbackUntil = -1f;
    private float infoTimer;             // interval for value refresh
    private FormationState lastState = (FormationState)(-1);
    private float overlapLiftPx;
    private bool overlapFaded;

    // read by the manager's overlap solver
    public bool IsShown => shownAlpha > 0.05f && f != null && f.soldiers.Count > 0;
    public Vector3 DesiredWorldAnchor { get; private set; }
    public Vector2 ResolvedScreenPos { get; private set; }
    public float Priority
    {
        get
        {
            if (f == null) return 0f;
            if (f.IsSelected) return 3f;
            if (commander != null && commander.InspectedEnemy == f) return 2f;
            if (f.State == FormationState.BrokenRanks) return 1.5f;
            return f.team == Team.Blue ? 1f : 0.5f;
        }
    }

    private void Start()
    {
        f = GetComponent<Formation>();
        commander = FindAnyObjectByType<PlayerCommander>();
        f.OnOrderIssued += OnOrder;
        EnsureShared();
        Build();
        if (FormationBannerManager.Instance != null)
            FormationBannerManager.Instance.RegisterBanner(this);
    }

    private void OnDestroy()
    {
        if (f != null) f.OnOrderIssued -= OnOrder;
        if (FormationBannerManager.Instance != null)
            FormationBannerManager.Instance.UnregisterBanner(this);
        if (root != null) Destroy(root.gameObject);
    }

    private void OnOrder() { orderFeedbackUntil = Time.unscaledTime + orderFeedbackSeconds; }

    public void SetOverlapResolution(Vector2 screenPos, float liftPx, bool faded)
    {
        ResolvedScreenPos = screenPos;
        overlapLiftPx = liftPx;
        overlapFaded = faded;
    }

    // ---------------- per-frame presentation ----------------

    private void LateUpdate()
    {
        var mgr = FormationBannerManager.Instance;
        if (f == null || root == null || mgr == null) return;
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        bool alive = f.soldiers.Count > 0;
        bool selected = f.IsSelected;
        bool targeted = commander != null && commander.InspectedEnemy == f;
        bool critical = f.State == FormationState.BrokenRanks ||
                        f.State == FormationState.Reforming;
        var level = mgr.CurrentLevel;

        bool wantShown = alive && ResolveVisibility(mgr, level, selected, targeted, critical);

        // master blend: opacity + a little scale, never a hierarchy rebuild
        float blendStep = Time.unscaledDeltaTime / Mathf.Max(0.05f, transitionSeconds);
        shownAlpha = Mathf.MoveTowards(shownAlpha, wantShown ? 1f : 0f, blendStep);
        bool rendered = shownAlpha > 0.01f;
        if (root.gameObject.activeSelf != rendered) root.gameObject.SetActive(rendered);
        if (!rendered) return;

        // anchor: structured formations sit slightly to the rear of the block;
        // combat deformation follows the dominant cluster. While broken or
        // reforming the banner is a fixed rally flag at the point where ranks
        // broke — it must not chase the scattering soldiers, or the player
        // loses the reference point the formation will reform around. A
        // PURSUING pack is the exception: the standard moved with the men, so
        // the banner rides the dominant cluster until Reform plants it.
        Vector3 target;
        if (f.State == FormationState.BrokenRanks && f.IsPursuing && f.DominantGroupCount > 0)
            target = f.DominantGroupCenter;
        else if (critical)
            target = f.RallyAnchor;
        else if (f.State == FormationState.Engaged && f.DominantGroupCount > 0)
            target = f.DominantGroupCenter;
        else
            target = f.AnchorPos - f.AnchorForward * rearOffset;
        target.y = hoverHeight;
        DesiredWorldAnchor = target;
        target.y += overlapLiftPx * mgr.WorldPerPixel;   // de-clutter lift

        if (!hasPos) { pos = target; hasPos = true; }
        else pos = Vector3.Lerp(pos, target, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));
        root.position = pos;
        root.rotation = cam.transform.rotation;   // billboard, upright, camera-facing

        // detail scale + screen-constancy compensation
        float detailScale = level == FormationBannerDetailLevel.Close ? closeScale
                          : level == FormationBannerDetailLevel.Expanded ? expandedScale
                          : compactScale;
        if (selected) detailScale *= selectedScaleMultiplier;
        float comp = Mathf.Clamp(cam.orthographicSize / referenceOrthoSize,
                                 minZoomCompensation, maxZoomCompensation);
        float want = detailScale * comp;
        currentScale = Mathf.MoveTowards(currentScale, want, blendStep * want * 1.5f);
        scaleContainer.localScale = Vector3.one * currentScale;

        // selection reads as focus: everything NOT selected drops opacity
        bool anySelection = commander != null && commander.Selection.Count > 0;
        float dim = anySelection && !selected ? unselectedDimFactor : 1f;
        float alpha = shownAlpha * dim *
                      (overlapFaded && !selected && !targeted ? 0.25f : 1f);
        ApplyPresentation(level, selected, alpha);

        infoTimer -= Time.unscaledDeltaTime;
        if (infoTimer <= 0f)
        {
            infoTimer = 0.25f;
            RefreshValues(level);
        }
    }

    private bool ResolveVisibility(FormationBannerManager mgr, FormationBannerDetailLevel level,
                                   bool selected, bool targeted, bool critical)
    {
        // No fog of war exists in this game: every living enemy formation is
        // "discovered" by definition, so banners never leak hidden information.
        switch (mgr.Mode)
        {
            case FormationBannerVisibilityMode.AlwaysVisible:
                return true;
            case FormationBannerVisibilityMode.SelectedOnly:
                return selected || targeted || critical;
            default:   // Adaptive
                if (level != FormationBannerDetailLevel.Close) return true;
                return selected || targeted || critical || mgr.InfoHeld ||
                       Time.unscaledTime < orderFeedbackUntil;
        }
    }

    // Child-element visibility per detail level: Close (selected marker) shows
    // badge + selection only; Compact and Expanded add health bar,
    // state icon, and the strength readout. Selection also forces the strength
    // readout at Close — a selected formation always answers "how many left".
    private void ApplyPresentation(FormationBannerDetailLevel level, bool selected, float alpha)
    {
        bool bars = level != FormationBannerDetailLevel.Close;

        // broken ranks = damaged flag: grey and faded until reforming restores it
        bool broken = f.State == FormationState.BrokenRanks;
        SetSpriteAlpha(baseSR, broken ? BrokenBadgeTint : Color.white,
                       broken ? alpha * 0.55f : alpha);
        SetSpriteAlpha(healthBgSR, BarBackground, bars ? alpha : 0f);
        float hp = Mathf.Clamp01(f.TotalHealth / Mathf.Max(1f, f.TotalMaxHealth));
        SetSpriteAlpha(healthFillSR, Color.Lerp(HealthLow, HealthHigh, hp), bars ? alpha : 0f);
        healthFillSR.transform.localScale = new Vector3(barWidth * hp, barHeight, 1f);
        healthFillSR.transform.localPosition = new Vector3(-barWidth * 0.5f * (1f - hp), -1.95f, -0.02f);

        bool icon = bars && stateIconSR.sprite != null;
        SetSpriteAlpha(stateIconSR, Color.white, icon ? alpha : 0f);

        int order = 10 + (selected ? selectedSortingBoost : (int)(Priority * 10f));
        baseSR.sortingOrder = order;
        healthBgSR.sortingOrder = order + 1;
        healthFillSR.sortingOrder = order + 2;
        stateIconSR.sortingOrder = order + 2;
    }

    // Interval refresh: only the state icon needs polling now — strength is
    // communicated entirely by the health bar (v0.9.1: count text removed).
    private void RefreshValues(FormationBannerDetailLevel level)
    {
        if (f.State != lastState || (f.State == FormationState.Ordered))
        {
            lastState = f.State;
            stateIconSR.sprite = StateIcon(f.State);
        }
    }

    private Sprite StateIcon(FormationState st)
    {
        switch (st)
        {
            case FormationState.Engaged: return iconEngaged;
            case FormationState.BrokenRanks: return iconBroken;
            case FormationState.Reforming: return iconReforming;
            case FormationState.Attacking:
            case FormationState.Charging:
            case FormationState.Withdrawing: return iconMoving;
            case FormationState.Ordered: return f.HasMoveDestination ? iconMoving : null;
            default: return null;
        }
    }

    private static void SetSpriteAlpha(SpriteRenderer sr, Color baseColor, float a)
    {
        baseColor.a *= a;
        sr.color = baseColor;
        bool on = baseColor.a > 0.01f;
        if (sr.enabled != on) sr.enabled = on;
    }

    // ---------------- one-time hierarchy construction ----------------

    private void Build()
    {
        string spriteName = BadgeSprites.TryGetValue((f.team, f.stats.isRanged), out var s)
            ? s : "UI_Banner_Blue_Melee";
        Sprite badge = LoadSprite(spriteName);

        root = new GameObject("FormationBannerRoot").transform;
        scaleContainer = new GameObject("BannerScaleContainer").transform;
        scaleContainer.SetParent(root, false);

        baseSR = MakeSprite("BaseBannerImage", badge, Vector3.zero, Vector3.one);
        healthBgSR = MakeSprite("HealthBarBg", whiteSprite, new Vector3(0f, -1.95f, -0.01f),
                                new Vector3(barWidth + 0.1f, barHeight + 0.1f, 1f));
        healthFillSR = MakeSprite("HealthBarFill", whiteSprite, new Vector3(0f, -1.95f, -0.02f),
                                  new Vector3(barWidth, barHeight, 1f));
        stateIconSR = MakeSprite("StateIcon", null, new Vector3(0f, 2.6f, -0.02f),
                                 Vector3.one * 0.9f);

        root.gameObject.SetActive(false);
    }

    private SpriteRenderer MakeSprite(string name, Sprite sprite, Vector3 localPos, Vector3 localScale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(scaleContainer, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return sr;
    }

    // ---------------- shared static art ----------------

    private static Sprite LoadSprite(string name)
    {
        if (spriteCache.TryGetValue(name, out var s) && s != null) return s;
        s = Resources.Load<Sprite>(name);
        spriteCache[name] = s;
        return s;
    }

    private static void EnsureShared()
    {
        if (whiteSprite != null) return;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var white = new Color32(255, 255, 255, 255);
        var fill = new Color32[16];
        for (int i = 0; i < 16; i++) fill[i] = white;
        tex.SetPixels32(fill);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

        iconMoving = DrawIcon(48, (x, y) =>
        {
            float yy = Mathf.Repeat(y + 1f, 0.8f) - 0.4f;   // two stacked chevrons
            return Mathf.Abs(yy - Mathf.Abs(x) * 0.6f) < 0.14f && Mathf.Abs(x) < 0.7f;
        });
        iconEngaged = DrawIcon(48, (x, y) =>
            (Mathf.Abs(y - x) < 0.14f || Mathf.Abs(y + x) < 0.14f) &&
            Mathf.Abs(x) < 0.75f && Mathf.Abs(y) < 0.75f);
        iconBroken = DrawIcon(48, (x, y) =>
        {
            // cracked banner: outer frame with a jagged missing seam
            bool frame = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) is > 0.5f and < 0.75f;
            float seam = 0.18f * Mathf.Sign(Mathf.Sin(y * 9f));
            bool crack = Mathf.Abs(x - seam) < 0.12f && Mathf.Abs(y) < 0.8f;
            return frame && !crack || (Mathf.Abs(x - seam) is > 0.12f and < 0.22f && Mathf.Abs(y) < 0.6f);
        });
        iconReforming = DrawIcon(48, (x, y) =>
            Mathf.Abs(y) < 0.14f && Mathf.Abs(x) is > 0.15f and < 0.75f ||   // two inward arrows
            (Mathf.Abs(x) is > 0.15f and < 0.45f &&
             Mathf.Abs(Mathf.Abs(y) - (0.45f - Mathf.Abs(x)) * 0.9f) < 0.12f));
    }

    private static Sprite DrawIcon(int size, System.Func<float, float, bool> inside)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x - size * 0.5f) / (size * 0.5f);
                float ny = (y - size * 0.5f) / (size * 0.5f);
                tex.SetPixel(x, y, inside(nx, ny) ? Color.white : clear);
            }
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 1.2f);
    }
}
