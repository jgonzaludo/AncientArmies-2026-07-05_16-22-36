using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum BattlePhase { Pre, Active, Ended }

// Spawns the battlefield at runtime (5v5 by default, random unit mix per
// battle), owns the battle lifecycle (Start -> Active -> Ended -> Restart),
// and keeps the per-team soldier registries used for brute-force local
// combat queries.
public class BattleSetup : MonoBehaviour
{
    public static BattleSetup Instance { get; private set; }

    public BattlePhase Phase { get; private set; } = BattlePhase.Pre;
    public string ResultText { get; private set; } = "";

    private float endCheckTimer;

    [Header("Unit stats (data-driven tuning)")]
    public UnitStats meleeStats = new UnitStats
    {
        unitName = "Swordsmen",
        maxHealth = 35f,
        moveSpeed = 3.2f,
        attackDamage = 12f,
        attackCooldown = 1.4f,
        strikeRange = 1.7f
    };
    public UnitStats archerStats = new UnitStats
    {
        unitName = "Archers",
        isRanged = true,
        maxHealth = 22f,
        moveSpeed = 3f,
        attackDamage = 10f,
        attackCooldown = 3f,
        strikeRange = 1.6f,
        rangedRange = 48f,            // capped at the melee-line spawn separation (2 x lineZ)
        rangedPreferredRange = 40.8f, // 85% of max range: a visible second line
        rangedMinRange = 2.5f,
        projectileSpeed = 20f         // keeps a 48 m extreme shot readable (~2.4 s flight)
    };

    // V1 overhaul army scale. NOTE: these are NEW field names on purpose —
    // Battle.unity serializes the old layout fields (meleeCount=50 etc.) and
    // scene values override C# defaults; new names fall back to the defaults
    // below without editing the scene.
    [Header("V1 century scale (per side: 6 melee + 2 archer centuries of 80)")]
    public int soldiersPerCentury = 80;
    public int meleeCenturiesPerSide = 6;
    public int archerCenturiesPerSide = 2;
    [Tooltip("Melee century frontage (10 wide x 8 deep at 80)")]
    public int centuryColumns = 10;
    [Tooltip("Archer century frontage — exposed separately so archer lines can go wider/shallower later")]
    public int archerColumns = 10;
    [Tooltip("Distance between melee soldiers — near shoulder-to-shoulder for a dense, continuous front")]
    public float meleeSpacing = 1.15f;
    [Tooltip("Distance between archers — visibly looser than melee")]
    public float archerSpacing = 1.75f;
    [Tooltip("Guaranteed edge-to-edge gap between neighboring formations, in multiples of the larger of the two intra-formation spacings")]
    public float formationGapFactor = 1.1f;
    public float lineSpacingX = 16f;
    [Tooltip("How far behind the front line the two reserve centuries deploy")]
    public float reserveLineOffset = 18f;
    [Tooltip("How far behind the front line the archer centuries deploy")]
    public float archerLineOffset = 11f;

    // Battlefield scale (Phase 6). ~410x280 = roughly 11x the old 120x80 area:
    // room for wings, reserves, and maneuver without empty-travel tedium. At
    // formation march speed 2.6 m/s, two armies separated by armySeparation
    // and advancing on each other meet in armySeparation / 5.2 ≈ 46 s.
    [Header("V1 battlefield scale (Phase 6)")]
    public float fieldHalfX = 205f;
    public float fieldHalfZ = 140f;
    [Tooltip("Starting anchor-to-anchor separation of the two front lines")]
    public float armySeparation = 240f;
    [Tooltip("Depth of each side's deployment zone, measured from its map edge")]
    public float deploymentZoneDepth = 60f;

    // Centralized soldier-spacing tuning (Phase 4). Soldiers read these so
    // the whole contact feel is adjustable in one place; the melee opponent
    // distance itself comes from UnitStats.strikeRange.
    [Header("V1 spacing & contact (Phase 4)")]
    [Tooltip("Minimum friendly separation while ordered, as a fraction of formation spacing")]
    public float separationFractionOrdered = 0.85f;
    [Tooltip("Minimum friendly separation while packed into melee/broken, as a fraction of formation spacing")]
    public float separationFractionPacked = 0.72f;
    [Tooltip("Cap on the local-avoidance push (m/s) — biases movement, never flings")]
    public float separationMaxPush = 1.2f;

    [Header("Directional combat (front / flank / rear)")]
    [Tooltip("Damage multiplier when attacking a formation from its front arc")]
    public float frontDamageMultiplier = 1f;
    [Tooltip("Damage multiplier when attacking a formation from either side")]
    public float flankDamageMultiplier = 1.5f;
    [Tooltip("Damage multiplier when attacking a formation from behind")]
    public float rearDamageMultiplier = 2f;
    [Tooltip("Attacks within this many degrees of the defender's forward count as frontal")]
    public float frontArcHalfAngleDeg = 60f;
    [Tooltip("Attacks within this many degrees of the defender's rear count as rear attacks")]
    public float rearArcHalfAngleDeg = 60f;

    private readonly List<Soldier> blue = new List<Soldier>();
    private readonly List<Soldier> red = new List<Soldier>();
    public readonly List<Formation> formations = new List<Formation>();

    private void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
        EnsureEnvironment();
        SpawnSide(Team.Blue, -armySeparation * 0.5f, 0f, false);
        SpawnSide(Team.Red, armySeparation * 0.5f, 180f, true);
        if (GetComponent<EnemyCommander>() == null)
            gameObject.AddComponent<EnemyCommander>();
        if (GetComponent<FormationBannerManager>() == null)
            gameObject.AddComponent<FormationBannerManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Spatial partition for all soldier neighbor queries (Phase 6F). Rebuilt
    // every physics tick — cheap index writes — so separation, targeting, and
    // engagement scans stay O(local density) at 1,280 soldiers.
    private void FixedUpdate()
    {
        if (Phase == BattlePhase.Ended) return;
        BattleGrid.Rebuild(blue, red);
    }

    private void Update()
    {
        if (Phase != BattlePhase.Active) return;
        endCheckTimer -= Time.deltaTime;
        if (endCheckTimer > 0f) return;
        endCheckTimer = 0.5f;

        bool blueAlive = blue.Count > 0;
        bool redAlive = red.Count > 0;
        if (blueAlive && redAlive) return;

        Phase = BattlePhase.Ended;
        ResultText = !blueAlive && !redAlive ? "DRAW"
                   : blueAlive ? "BLUE WINS" : "RED WINS";

        var commander = GetComponent<PlayerCommander>();
        if (commander != null) commander.DeselectAll();
    }

    public void StartBattle()
    {
        if (Phase == BattlePhase.Pre) Phase = BattlePhase.Active;
    }

    // Scene reload is the simplest reliable full reset back to the Pre state.
    public void RestartBattle()
    {
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // V1 fixed army structure per side: a four-century primary melee line,
    // two melee reserve centuries behind it, and two archer centuries as a
    // supporting missile line between the reserves — the default Roman-style
    // deployment the commander AI and the player both start from. Composition
    // is fixed (no random rerolls) so both armies are symmetric.
    private void SpawnSide(Team team, float z, float yaw, bool auto)
    {
        string p = team == Team.Blue ? "Blue" : "Red";
        float rear = team == Team.Blue ? -1f : 1f;   // away from the enemy
        int firstIdx = formations.Count;

        float meleeHalfW = Formation.LineHalfWidth(soldiersPerCentury, centuryColumns, meleeSpacing);
        float pitch = Mathf.Max(lineSpacingX,
                                2f * meleeHalfW + formationGapFactor * meleeSpacing);

        int frontCount = Mathf.Min(4, meleeCenturiesPerSide);
        int reserveCount = meleeCenturiesPerSide - frontCount;
        int centuryIdx = 0;

        for (int i = 0; i < frontCount; i++)
        {
            float x = (i - (frontCount - 1) * 0.5f) * pitch;
            CreateFormation($"{p} Century {++centuryIdx}", team, meleeStats,
                            soldiersPerCentury, centuryColumns,
                            new Vector3(x, 0f, z), yaw, auto);
        }
        for (int i = 0; i < reserveCount; i++)
        {
            float x = (i - (reserveCount - 1) * 0.5f) * pitch * 1.7f;
            CreateFormation($"{p} Century {++centuryIdx}", team, meleeStats,
                            soldiersPerCentury, centuryColumns,
                            new Vector3(x, 0f, z + rear * reserveLineOffset), yaw, auto);
        }
        for (int i = 0; i < archerCenturiesPerSide; i++)
        {
            float x = (i - (archerCenturiesPerSide - 1) * 0.5f) * pitch;
            CreateFormation($"{p} Archers {i + 1}", team, archerStats,
                            soldiersPerCentury, archerColumns,
                            new Vector3(x, 0f, z + rear * archerLineOffset), yaw, auto);
        }
        ValidateLineGaps(formations, firstIdx, formationGapFactor);
    }

    // Layout guard: cross-checks the spawned formations' actual footprints
    // against the guaranteed edge gap, catching any drift between
    // Formation.LineHalfWidth and what BuildSlots really produced.
    public static void ValidateLineGaps(List<Formation> all, int firstIdx, float gapFactor)
    {
        for (int i = firstIdx + 1; i < all.Count; i++)
        {
            Formation a = all[i - 1], b = all[i];
            float edge = Mathf.Abs(b.AnchorPos.x - a.AnchorPos.x)
                       - a.FootprintHalfExtents.x - b.FootprintHalfExtents.x;
            float need = gapFactor * Mathf.Max(a.spacing, b.spacing) - 0.001f;
            if (edge < need)
                Debug.LogWarning($"Formation gap violation: {a.displayName} <-> " +
                                 $"{b.displayName} edge {edge:F2} < required {need:F2}");
        }
    }

    // Formation-level directional damage: where is the attacker relative to the
    // defender's canonical forward? Deterministic, anchor-based, tunable above.
    public float GetDirectionalMultiplier(Formation attacker, Formation defender)
    {
        if (attacker == null || defender == null || attacker == defender)
            return frontDamageMultiplier;
        Vector3 toAttacker = attacker.AnchorPos - defender.AnchorPos;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude < 0.04f) return frontDamageMultiplier;
        float angle = Vector3.Angle(defender.AnchorForward, toAttacker);
        if (angle <= frontArcHalfAngleDeg) return frontDamageMultiplier;
        if (angle >= 180f - rearArcHalfAngleDeg) return rearDamageMultiplier;
        return flankDamageMultiplier;
    }

    public Formation CreateFormation(string name, Team team, UnitStats stats, int count,
                                     int columns, Vector3 pos, float yaw, bool auto)
    {
        var go = new GameObject($"Formation_{name.Replace(' ', '_')}");
        var f = go.AddComponent<Formation>();
        f.Init(team, name, stats, pos, yaw, columns, auto);
        // melee packs tight for a continuous front; archers stay visibly looser
        f.spacing = stats.isRanged ? archerSpacing : meleeSpacing;
        f.BuildSlots(count);
        // Century composition: roles are reserved in data (centurion, optio,
        // signifer, tesserarius, cornicen); all roles currently spawn the
        // generic visual until specialist prefabs exist.
        var roles = stats.BuildCenturyRoles(count, columns);
        for (int i = 0; i < count; i++)
        {
            var s = SoldierFactory.Create(f, i, f.GetSlotWorldPos(i));
            s.role = roles[i];
            f.AddSoldier(s);
            Register(s);
        }
        go.AddComponent<FormationBannerController>();
        go.AddComponent<FormationArrow>();
        go.AddComponent<FormationDestinationPreview>();
        formations.Add(f);
        return f;
    }

    public List<Soldier> GetSoldiers(Team t) => t == Team.Blue ? blue : red;
    public void Register(Soldier s) => GetSoldiers(s.team).Add(s);
    public void Unregister(Soldier s) => GetSoldiers(s.team).Remove(s);

    private void EnsureEnvironment()
    {
        if (GameObject.Find("Ground") == null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            // Phase 6: field is parameterized (default 410 x 280, ~11x the old
            // area) — a Unity plane is 10x10 at scale 1
            ground.transform.localScale = new Vector3(fieldHalfX * 0.2f, 1f, fieldHalfZ * 0.2f);
            BattlefieldDecor.Decorate(ground);      // light tiled grass albedo
        }

        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 34f;   // frames the player's deployment zone
            // Pitch 40 degrees; position = focus - forward * (height/sin40),
            // height 42. Initial focus sits over the BLUE deployment line so
            // the player starts looking at their own army.
            float focusZ = -armySeparation * 0.5f;
            cam.transform.position = new Vector3(0f, 42f, focusZ - 42f / Mathf.Tan(40f * Mathf.Deg2Rad));
            cam.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;      // reaches the far corners of the big field
            var rig = cam.GetComponent<BattleCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<BattleCamera>();
            rig.Configure(fieldHalfX, fieldHalfZ, 6f, 120f);
        }
    }
}
