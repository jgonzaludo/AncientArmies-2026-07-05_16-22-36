using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum BattlePhase { Pre, Active, Ended }

// Which arc of the defending formation a blow came from. Drives the damage
// multiplier and, since Chunk C, the casualty's morale shock weight.
public enum DamageZone { Front, Flank, Rear }

// Spawns the battlefield at runtime (5v5 by default, random unit mix per
// battle), owns the battle lifecycle (Start -> Active -> Ended -> Restart),
// and keeps the per-team soldier registries used for brute-force local
// combat queries.
public class BattleSetup : MonoBehaviour
{
    public static BattleSetup Instance { get; private set; }

    public BattlePhase Phase { get; private set; } = BattlePhase.Pre;
    public string ResultText { get; private set; } = "";
    public float BattleTime { get; private set; }           // seconds of Active play
    public float TimeRemaining => moraleConfig != null
        ? Mathf.Max(0f, moraleConfig.battleTimeLimit - BattleTime) : 0f;

    private float endCheckTimer;

    [Header("Unit stats (data-driven tuning)")]
    public UnitStats meleeStats = new UnitStats
    {
        unitName = "Swordsmen",
        maxHealth = 35f,
        moveSpeed = 3.2f,
        attackDamage = 12f,
        // v1.9 pacing: kills stay lethal (3-4 hits) but exchanges happen every
        // ~4 s — a shield line measuring strikes, not a blender. This is what
        // stretches a century-vs-century grind to ~1.5-2 minutes.
        // NOTE: scene-pinned in Battle.unity — change it there too.
        attackCooldown = 4f,
        strikeRange = 1.7f
    };
    public UnitStats archerStats = new UnitStats
    {
        unitName = "Archers",
        isRanged = true,
        maxHealth = 22f,
        moveSpeed = 3f,
        // v1.9 pacing: with spread targeting eliminating overkill waste,
        // cadence governs output — damage back to 10 (4 front arrows per
        // kill), cooldown under the 8 s volley interval so every archer is
        // ready each volley (jitter 0.9-1.15 keeps 5.85-7.5 < 8).
        // NOTE: Battle.unity pins both — change them there too.
        attackDamage = 10f,
        attackCooldown = 6.5f,
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
    // Line depths (v1.8.1): a melee century is ~9 m deep (8 rows x 1.15) and
    // an archer century ~13 m (8 rows x 1.75), so the lines need real gaps —
    // archers clear of the front line's rear rank, reserves clear of the
    // archers' rear rank.
    // Depth gaps must survive the 40-degree camera: a ~1.9 m soldier model
    // consumes ~2.3 m of PERCEIVED ground depth, so the flat-footprint gap
    // needs ~2.3 m of visual allowance on top of the physical clearance.
    [Tooltip("How far behind the front line the two reserve centuries deploy")]
    public float reserveLineOffset = 40f;
    [Tooltip("How far behind the front line the archer centuries deploy")]
    public float archerLineOffset = 20f;

    // Battlefield scale (Phase 6). ~410x280 = roughly 11x the old 120x80 area:
    // room for wings, reserves, and maneuver without empty-travel tedium. At
    // formation march speed 2.6 m/s, two armies separated by armySeparation
    // and advancing on each other meet in armySeparation / 5.2 ≈ 46 s.
    [Header("V1 battlefield scale (Phase 6)")]
    public float fieldHalfX = 205f;
    // 170 (was 140): armySeparation 240 + the deeper line offsets put the
    // reserve rear rank at z=-164.6 — the old 140 left it 16.6 m OFF the
    // ground plane, jamming the rear lines against the map edge.
    public float fieldHalfZ = 170f;
    [Tooltip("Starting anchor-to-anchor separation of the two front lines")]
    public float armySeparation = 240f;
    [Tooltip("Depth of each side's deployment zone, measured from its map edge")]
    public float deploymentZoneDepth = 60f;
    [Tooltip("Minimum clearance from the field boundary for orders and AI moves")]
    public float fieldEdgeMargin = 12f;
    [Tooltip("Pull starting deployment inward from each map edge so rear ranks clear the boundary")]
    public float spawnEdgeInset = 20f;

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

    // Combat numbers (damage scale, directional multipliers, arcs) live in the
    // CombatConfig asset, not here — nothing for Battle.unity to pin.
    [Header("Combat")]
    [Tooltip("Assigned in Battle.unity: Assets/AncientArmies/Settings/CombatConfig.asset")]
    public CombatConfig combatConfig;

    // The ONLY morale field allowed on BattleSetup (DECISIONS 2026-09-21):
    // every tunable lives in the MoraleConfig asset, so the scene pins this
    // reference and nothing else. Null is a loud error, never a silent
    // fallback to default values.
    [Header("Morale")]
    [Tooltip("Assigned in Battle.unity: Assets/AncientArmies/Settings/MoraleConfig.asset")]
    public MoraleConfig moraleConfig;
    [Tooltip("Assigned in Battle.unity: Assets/AncientArmies/Settings/OfficerRanks.asset")]
    public OfficerRanks officerRanks;

    private readonly List<Soldier> blue = new List<Soldier>();
    private readonly List<Soldier> red = new List<Soldier>();
    public readonly List<Formation> formations = new List<Formation>();

    private void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
        SimSeed.ApplyIfPending();   // reproducible battles: seed before anything random spawns
        if (moraleConfig == null)
            Debug.LogError("[Morale] BattleSetup.moraleConfig is NOT ASSIGNED in Battle.unity — " +
                           "morale is DISABLED this battle (no fallback to defaults). Assign " +
                           "Assets/AncientArmies/Settings/MoraleConfig.asset on the BattleSetup object.", this);
        if (combatConfig == null)
        {
            Debug.LogError("[Combat] BattleSetup.combatConfig is NOT ASSIGNED in Battle.unity — this battle runs on " +
                           "the CombatConfig CLASS DEFAULTS, not your tuned asset. Assign " +
                           "Assets/AncientArmies/Settings/CombatConfig.asset on the BattleSetup object.", this);
            combatConfig = ScriptableObject.CreateInstance<CombatConfig>();
        }
        if (officerRanks == null)
            Debug.LogError("[Morale] BattleSetup.officerRanks is NOT ASSIGNED in Battle.unity — " +
                           "officer grades, the reform anchor and officer death shock are DISABLED this battle. " +
                           "Assign Assets/AncientArmies/Settings/OfficerRanks.asset on the BattleSetup object.", this);
        EnsureEnvironment();
        float blueLineZ = -armySeparation * 0.5f + spawnEdgeInset;
        SpawnSide(Team.Blue, blueLineZ, 0f, false);
        SpawnSide(Team.Red, armySeparation * 0.5f - spawnEdgeInset, 180f, true);
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

    // Battles end by LINE COLLAPSE, not annihilation (DECISIONS 2026-09-21):
    // a team loses the moment collapseFraction of its formations are Routing
    // or destroyed. Ending the battle right then is the latch — nothing that
    // happens afterwards can reopen it. The time limit is a stall backstop.
    private void Update()
    {
        if (Phase != BattlePhase.Active) return;
        BattleTime += Time.deltaTime;
        endCheckTimer -= Time.deltaTime;
        if (endCheckTimer > 0f) return;
        endCheckTimer = 0.5f;

        bool blueDown = HasCollapsed(Team.Blue);
        bool redDown = HasCollapsed(Team.Red);
        if (blueDown || redDown)
        {
            EndBattle(blueDown && redDown ? "DRAW — BOTH LINES COLLAPSED"
                    : blueDown ? "RED WINS — BLUE LINE COLLAPSED"
                    : "BLUE WINS — RED LINE COLLAPSED");
            return;
        }
        if (moraleConfig != null && BattleTime >= moraleConfig.battleTimeLimit)
        {
            int b = CountIntact(Team.Blue), r = CountIntact(Team.Red);
            EndBattle(b == r ? "DRAW — TIME"
                    : b > r ? "BLUE WINS — TIME" : "RED WINS — TIME");
        }
    }

    private void EndBattle(string result)
    {
        Phase = BattlePhase.Ended;
        ResultText = result;
        BattleRecorder.NoteEnd(this, result);
        var commander = GetComponent<PlayerCommander>();
        if (commander != null) commander.DeselectAll();
    }

    // Counted from the live formation list, never an assumed roster size.
    public bool HasCollapsed(Team t)
    {
        float fraction = moraleConfig != null ? moraleConfig.collapseFraction : 0.5f;
        int total = 0, fallen = 0;
        foreach (var f in formations)
        {
            if (f.team != t) continue;
            total++;
            if (f.IsRouting || f.soldiers.Count == 0) fallen++;
        }
        return total > 0 && fallen >= total * fraction;
    }

    // Battle power for the HUD tide bar: living men of formations that have not
    // routed, each formation weighted by its morale (never below tideMoraleFloor).
    // Presentation reads it; nothing in the simulation does.
    public float BattlePower(Team t)
    {
        float floor = moraleConfig != null ? moraleConfig.tideMoraleFloor : 0.25f;
        float power = 0f;
        foreach (var f in formations)
        {
            if (f.team != t || f.IsRouting || f.soldiers.Count == 0) continue;
            float m = f.Morale != null ? f.Morale.Value / 100f : 1f;
            power += f.soldiers.Count * Mathf.Max(floor, m);
        }
        return power;
    }

    // Intact = alive and not Routing. BrokenRanks counts as intact.
    public int CountIntact(Team t)
    {
        int n = 0;
        foreach (var f in formations)
            if (f.team == t && !f.IsRouting && f.soldiers.Count > 0) n++;
        return n;
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
        // archers are wider (looser spacing): pitch from their own footprint
        float archerHalfW = Formation.LineHalfWidth(soldiersPerCentury, archerColumns, archerSpacing);
        float archerPitch = Mathf.Max(lineSpacingX,
                                      2f * archerHalfW + formationGapFactor * archerSpacing);
        for (int i = 0; i < archerCenturiesPerSide; i++)
        {
            float x = (i - (archerCenturiesPerSide - 1) * 0.5f) * archerPitch;
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
    // Formed combat resolves at formation level by FACING, never by agent
    // position (engaged lines interleave, so positions lie). A formed attacker
    // facing roughly opposite the defender is frontal; roughly the same way,
    // rear; otherwise a flank, left or right by the side its facing comes from.
    // Attackers with no formed facing (BrokenRanks, Routing) fall back to the
    // POSITION of the attacking soldier (hits) or of the attacker's main pack
    // (surround) relative to the defender's facing.
    private static bool HasFormedFacing(Formation f) =>
        f.State != FormationState.BrokenRanks && f.State != FormationState.Routing;

    // 0 front, 1 right flank, 2 left flank, 3 rear
    private int ZoneIndexByFacing(Formation attacker, Formation defender)
    {
        float dot = Vector3.Dot(attacker.AnchorForward, defender.AnchorForward);
        if (dot <= -Mathf.Cos(combatConfig.frontArcHalfAngleDeg * Mathf.Deg2Rad)) return 0;
        if (dot >= Mathf.Cos(combatConfig.rearArcHalfAngleDeg * Mathf.Deg2Rad)) return 3;
        // a flanker on the defender's right faces toward its left, i.e. against defender.right
        return Vector3.Dot(defender.AnchorRot * Vector3.right, attacker.AnchorForward) < 0f ? 1 : 2;
    }

    private int ZoneIndexByPosition(Formation defender, Vector3 point)
    {
        Vector3 to = point - defender.AnchorPos;
        to.y = 0f;
        if (to.sqrMagnitude < 0.04f) return 0;
        float angle = Vector3.Angle(defender.AnchorForward, to);
        if (angle <= combatConfig.frontArcHalfAngleDeg) return 0;
        if (angle >= 180f - combatConfig.rearArcHalfAngleDeg) return 3;
        return Vector3.Dot(defender.AnchorRot * Vector3.right, to) >= 0f ? 1 : 2;
    }

    private static DamageZone ToZone(int idx) => idx == 0 ? DamageZone.Front : idx == 3 ? DamageZone.Rear : DamageZone.Flank;

    // Zone of ONE HIT: the attacking formation's facing relative to the
    // defender's; a formless attacker uses the striking soldier's position.
    public DamageZone GetDirectionalZone(Formation attacker, Formation defender, Vector3 attackerSoldierPos)
    {
        if (attacker == null || defender == null || attacker == defender) return DamageZone.Front;
        return ToZone(HasFormedFacing(attacker) ? ZoneIndexByFacing(attacker, defender)
                                                : ZoneIndexByPosition(defender, attackerSoldierPos));
    }

    // Zone an enemy FORMATION occupies around the defender (surround counting).
    public int ZoneIndexOf(Formation attacker, Formation defender)
    {
        if (HasFormedFacing(attacker)) return ZoneIndexByFacing(attacker, defender);
        return ZoneIndexByPosition(defender, attacker.DominantGroupCount > 0 ? attacker.DominantGroupCenter : attacker.AnchorPos);
    }

    public DamageZone GetDirectionalZone(Formation attacker, Formation defender) =>
        attacker == null || defender == null || attacker == defender ? DamageZone.Front : ToZone(ZoneIndexOf(attacker, defender));

    public float ZoneDamageMultiplier(DamageZone zone) =>
        zone == DamageZone.Rear ? combatConfig.rearDamageMultiplier
      : zone == DamageZone.Flank ? combatConfig.flankDamageMultiplier : combatConfig.frontDamageMultiplier;

    public float GetDirectionalMultiplier(Formation attacker, Formation defender) =>
        ZoneDamageMultiplier(GetDirectionalZone(attacker, defender));

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
            if (officerRanks != null) s.ScaleMaxHealth(officerRanks.HealthMultiplier(roles[i]));   // officers are tougher
            f.AddSoldier(s);
            Register(s);
        }
        go.AddComponent<FormationBannerController>();
        go.AddComponent<FormationArrow>();
        go.AddComponent<FormationDestinationPreview>();
        go.AddComponent<FormationImposterRenderer>();
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
            float focusZ = -armySeparation * 0.5f + spawnEdgeInset;
            cam.transform.position = new Vector3(0f, 42f, focusZ - 42f / Mathf.Tan(40f * Mathf.Deg2Rad));
            cam.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
            // Negative near plane (legal for orthographic cameras): at full
            // zoom-out the BOTTOM of the tilted frustum meets the ground
            // behind the camera plane (ray distance goes negative once
            // orthoSize * cos(pitch) exceeds the camera height), and a
            // positive near clips that strip to background void. -100 covers
            // the fit-max zoom with margin.
            cam.nearClipPlane = -100f;
            cam.farClipPlane = 600f;      // reaches the far corners of the big field
            var rig = cam.GetComponent<BattleCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<BattleCamera>();
            rig.Configure(fieldHalfX, fieldHalfZ, 6f, 120f);
        }
    }
}
