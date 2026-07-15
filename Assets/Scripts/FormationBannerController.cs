using System.Collections.Generic;
using UnityEngine;

// One adaptive floating banner per formation, built from the supplied badge
// artwork (UI_Banner_<Faction>_<Class> sprites). Purely presentational: it
// reads the formation's authoritative state (anchor, dominant cluster,
// AnchorForward facing, health, count, selection) and the shared
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
    [SerializeField] private float selectedScaleMultiplier = 1.12f;
    [SerializeField] private int selectedSortingBoost = 100;

    [Header("Order feedback")]
    [Tooltip("A hidden close-zoom banner appears this long after an order")]
    [SerializeField] private float orderFeedbackSeconds = 1.5f;

    [Header("Info elements")]
    [SerializeField] private float barWidth = 2.3f;
    [SerializeField] private float barHeight = 0.24f;
    [SerializeField] private float facingWedgeSize = 0.6f;
    [Tooltip("TextMesh character size for the remaining-count readout")]
    [SerializeField] private float countCharacterSize = 0.24f;

    private static readonly Color HealthHigh = new Color(0.38f, 0.86f, 0.38f);
    private static readonly Color HealthLow = new Color(0.95f, 0.7f, 0.15f);
    private static readonly Color BarBackground = new Color(0.08f, 0.08f, 0.1f, 0.85f);
    private static readonly Color SelectionTint = new Color(1f, 0.93f, 0.35f, 0.85f);

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
    private static Sprite wedgeSprite;
    private static Sprite iconMoving, iconEngaged, iconBroken, iconReforming;

    private Formation f;
    private PlayerCommander commander;
    private Camera cam;

    private Transform root, scaleContainer;
    private SpriteRenderer baseSR, selectionSR, stateIconSR, healthBgSR, healthFillSR, facingSR;
    private TextMesh countTM;
    private MeshRenderer countMR;

    private float shownAlpha;            // 0..1 master visibility blend
    private float currentScale = 1f;
    private Vector3 pos;
    private bool hasPos;
    private float orderFeedbackUntil = -1f;
    private float infoTimer;             // interval for value refresh
    private int lastCount = -1;
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
        // combat deformation and broken ranks follow the dominant cluster
        bool scattered = f.State == FormationState.Engaged ||
                         f.State == FormationState.BrokenRanks;
        Vector3 target = scattered && f.DominantGroupCount > 0
            ? f.DominantGroupCenter
            : f.AnchorPos - f.AnchorForward * rearOffset;
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

        float alpha = shownAlpha * (overlapFaded && !selected && !targeted ? 0.25f : 1f);
        ApplyPresentation(level, selected, alpha);

        infoTimer -= Time.unscaledDeltaTime;
        if (infoTimer <= 0f)
        {
            infoTimer = 0.25f;
            RefreshValues(level);
        }
        UpdateFacingWedge(alpha);
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
    // badge + selection + wedge only; Compact adds health bar and state icon;
    // Expanded adds the remaining-soldier count.
    private void ApplyPresentation(FormationBannerDetailLevel level, bool selected, float alpha)
    {
        bool bars = level != FormationBannerDetailLevel.Close;
        bool count = level == FormationBannerDetailLevel.Expanded;

        SetSpriteAlpha(baseSR, Color.white, alpha);
        SetSpriteAlpha(selectionSR, SelectionTint, selected ? alpha : 0f);
        SetSpriteAlpha(healthBgSR, BarBackground, bars ? alpha : 0f);
        float hp = Mathf.Clamp01(f.TotalHealth / Mathf.Max(1f, f.TotalMaxHealth));
        SetSpriteAlpha(healthFillSR, Color.Lerp(HealthLow, HealthHigh, hp), bars ? alpha : 0f);
        healthFillSR.transform.localScale = new Vector3(barWidth * hp, barHeight, 1f);
        healthFillSR.transform.localPosition = new Vector3(-barWidth * 0.5f * (1f - hp), -1.95f, -0.02f);

        bool icon = bars && stateIconSR.sprite != null;
        SetSpriteAlpha(stateIconSR, Color.white, icon ? alpha : 0f);

        if (countMR != null)
        {
            countMR.enabled = count;
            if (count) countTM.color = new Color(1f, 1f, 1f, alpha);
        }

        int order = 10 + (selected ? selectedSortingBoost : (int)(Priority * 10f));
        baseSR.sortingOrder = order;
        selectionSR.sortingOrder = order - 1;
        healthBgSR.sortingOrder = order + 1;
        healthFillSR.sortingOrder = order + 2;
        stateIconSR.sortingOrder = order + 2;
        facingSR.sortingOrder = order + 2;
        if (countMR != null) countMR.sortingOrder = order + 3;
    }

    // Interval refresh of gameplay values (count text only rebuilt on change —
    // no per-frame string construction).
    private void RefreshValues(FormationBannerDetailLevel level)
    {
        int n = f.soldiers.Count;
        if (n != lastCount)
        {
            lastCount = n;
            countTM.text = n.ToString();
        }
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
            case FormationState.Withdrawing: return iconMoving;
            case FormationState.Ordered: return f.HasMoveDestination ? iconMoving : null;
            default: return null;
        }
    }

    // The separate facing wedge orbits the badge, rotated to the formation's
    // authoritative facing after camera projection. The baked gold pointer on
    // the artwork stays static (it marks the formation location). Hidden while
    // broken (no coherent facing), muted while auto-facing steers the anchor.
    private void UpdateFacingWedge(float alpha)
    {
        bool valid = f.State != FormationState.BrokenRanks && f.soldiers.Count > 0;
        if (!valid) { SetSpriteAlpha(facingSR, Color.white, 0f); return; }

        Vector3 a = cam.WorldToScreenPoint(f.AnchorPos);
        Vector3 b = cam.WorldToScreenPoint(f.AnchorPos + f.AnchorForward * 4f);
        Vector2 d = new Vector2(b.x - a.x, b.y - a.y);
        if (d.sqrMagnitude < 1f) { SetSpriteAlpha(facingSR, Color.white, 0f); return; }
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;   // 0 = screen right

        float radius = 1.75f;
        Quaternion spin = Quaternion.Euler(0f, 0f, ang - 90f);   // wedge points +Y
        facingSR.transform.localRotation = spin;
        facingSR.transform.localPosition =
            spin * new Vector3(0f, radius, -0.02f) + new Vector3(0f, -0.55f, 0f);
        float mute = f.IsAutoFacing ? 0.6f : 1f;
        SetSpriteAlpha(facingSR, Color.white, alpha * mute);
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
        selectionSR = MakeSprite("SelectionOutline", badge, new Vector3(0f, 0f, 0.02f),
                                 Vector3.one * 1.16f);
        healthBgSR = MakeSprite("HealthBarBg", whiteSprite, new Vector3(0f, -1.95f, -0.01f),
                                new Vector3(barWidth + 0.1f, barHeight + 0.1f, 1f));
        healthFillSR = MakeSprite("HealthBarFill", whiteSprite, new Vector3(0f, -1.95f, -0.02f),
                                  new Vector3(barWidth, barHeight, 1f));
        stateIconSR = MakeSprite("StateIcon", null, new Vector3(0f, 2.6f, -0.02f),
                                 Vector3.one * 0.9f);
        facingSR = MakeSprite("FacingIndicator", wedgeSprite, new Vector3(0f, 1.2f, -0.02f),
                              Vector3.one * facingWedgeSize);

        var countGO = new GameObject("CountText");
        countGO.transform.SetParent(scaleContainer, false);
        countGO.transform.localPosition = new Vector3(0f, -2.6f, -0.02f);
        countTM = countGO.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countTM.font = font;
        countMR = countGO.GetComponent<MeshRenderer>();
        countMR.sharedMaterial = font.material;
        countMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        countTM.anchor = TextAnchor.MiddleCenter;
        countTM.alignment = TextAlignment.Center;
        countTM.characterSize = countCharacterSize;
        countTM.fontSize = 64;
        countTM.text = "";

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

        wedgeSprite = DrawIcon(48, (x, y) => y > -0.7f && Mathf.Abs(x) < (1f - (y + 0.7f) / 1.7f) * 0.55f);
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
