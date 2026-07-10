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
        rangedRange = 20f,
        rangedPreferredRange = 16f,   // ~80% of max range: a visible second line
        rangedMinRange = 2.5f,
        projectileSpeed = 13f
    };

    [Header("Battle layout")]
    public int meleeCount = 50;
    public int archerCount = 40;
    public int formationColumns = 10;
    [Tooltip("Distance between melee soldiers — near shoulder-to-shoulder for a dense, continuous front")]
    public float meleeSpacing = 0.95f;
    [Tooltip("Distance between archers — visibly looser than melee")]
    public float archerSpacing = 1.7f;
    public int formationsPerSide = 5;
    [Range(0f, 1f)]
    [Tooltip("Chance each formation slot rolls Archers instead of Swordsmen (re-rolled every battle)")]
    public float archerChance = 0.5f;
    public float lineZ = 24f;
    public float lineSpacingX = 16f;

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
        SpawnSide(Team.Blue, -lineZ, 0f, false);
        SpawnSide(Team.Red, lineZ, 180f, true);
        if (GetComponent<EnemyCommander>() == null)
            gameObject.AddComponent<EnemyCommander>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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

    // One line of formationsPerSide formations per army; each slot randomly
    // rolls Swordsmen or Archers, so every battle (and every Restart, which
    // reloads the scene) fields a different army composition. Archer slots sit
    // slightly behind the line, away from the enemy.
    private void SpawnSide(Team team, float z, float yaw, bool auto)
    {
        string p = team == Team.Blue ? "Blue" : "Red";
        int swordIdx = 0, archerIdx = 0;
        for (int i = 0; i < formationsPerSide; i++)
        {
            float x = (i - (formationsPerSide - 1) * 0.5f) * lineSpacingX;
            bool ranged = Random.value < archerChance;
            UnitStats stats = ranged ? archerStats : meleeStats;
            int count = ranged ? archerCount : meleeCount;
            float zPos = z + (ranged ? (team == Team.Blue ? -6f : 6f) : 0f);
            string label = ranged ? $"{p} Archers {++archerIdx}"
                                  : $"{p} Swords {++swordIdx}";
            CreateFormation(label, team, stats, count, formationColumns,
                            new Vector3(x, 0f, zPos), yaw, auto);
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
        for (int i = 0; i < count; i++)
        {
            var s = SoldierFactory.Create(f, i, f.GetSlotWorldPos(i));
            f.AddSoldier(s);
            Register(s);
        }
        go.AddComponent<FormationLabel>();
        go.AddComponent<FormationArrow>();
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
            ground.transform.localScale = new Vector3(12f, 1f, 8f);   // 120 x 80 field
            var grass = SoldierFactory.Lit(new Color(0.45f, 0.56f, 0.34f));
            grass.SetFloat("_Smoothness", 0.12f);   // matte, no specular glare
            ground.GetComponent<Renderer>().sharedMaterial = grass;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 26f;   // wide default framing for the 5v5 line
            cam.transform.position = new Vector3(0f, 42f, -30f);
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            if (cam.GetComponent<BattleCamera>() == null)
                cam.gameObject.AddComponent<BattleCamera>();
        }
    }
}
