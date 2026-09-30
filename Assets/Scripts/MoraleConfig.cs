using UnityEngine;

// Every morale tunable (DECISIONS 2026-09-21). A ScriptableObject asset, not
// BattleSetup fields: the values live in MoraleConfig.asset, so Battle.unity
// never holds its own stale copy that silently overrides a C# default. The
// scene pins only the reference (BattleSetup.moraleConfig). The defaults below
// are what a freshly created asset starts with — at runtime the asset wins.
[CreateAssetMenu(fileName = "MoraleConfig", menuName = "Ancient Armies/Morale Config")]
public class MoraleConfig : ScriptableObject
{
    [Header("Tick")]
    [Tooltip("Seconds between morale updates per formation — throttled, not per frame")]
    public float tickInterval = 0.25f;

    [Header("State thresholds (morale 0-100)")]
    [Tooltip("Morale below this routs the formation")]
    public float routeThreshold = 15f;
    [Tooltip("A routing formation may rally once morale is above this (and it is not in melee)")]
    public float rallyThreshold = 35f;

    [Header("Ceilings (cap on base morale from cumulative losses vs starting strength)")]
    [Tooltip("In melee: ceiling = 100 - effectiveLossPercent * this. Stricter, so a slow bleed eventually routs")]
    public float contactCeilingScale = 1.1f;
    [Tooltip("Out of melee: ceiling = 100 - lossPercent * this. How high a battered unit can recover")]
    public float restCeilingScale = 1f;
    [Tooltip("On rally, this share of the losses so far stops counting against the CONTACT ceiling, so a rallied unit can fight again. Rest ceiling keeps full losses")]
    public float rallyLossForgiveness = 0.5f;

    [Header("Shock debt (sudden pressure, subtracted from base morale)")]
    [Tooltip("Shock debt added per percent of starting strength killed within one tick")]
    public float shockMultiplier = 3f;
    [Tooltip("Shock debt removed per second, at all times — in melee too")]
    public float shockDecayPerSecond = 5f;
    [Tooltip("Master multiplier on ALL shock (casualties incl. zone weights, arrows, contagion). Officer death shock is NOT scaled")]
    public float panicScale = 0.65f;
    [Tooltip("Ranged kills add only arrowShockWeight of a casualty's panic (missiles erode, they rarely break a line)")]
    public bool enableArrowShockWeight = true;
    public float arrowShockWeight = 0.5f;
    [Tooltip("Steady FRONT-zone casualties add only this share of a casualty's panic — a frontal grind should not rout by itself")]
    public float frontShockWeight = 0.25f;
    [Tooltip("Burst rule: casualties beyond burstThreshold (fraction of starting strength) inside burstWindow seconds use burstShockWeight, whatever their zone (the higher of zone and burst weight applies)")]
    public float burstWindow = 3f;
    public float burstThreshold = 0.05f;
    public float burstShockWeight = 2f;
    [Tooltip("Surrounded: every tick adds surroundShockPerSecond x (engaged zones - 1). Zones = front, left flank, right flank, rear. Unscaled, and it bypasses the per-second cap")]
    public bool enableSurroundShock = true;
    public float surroundShockPerSecond = 8f;
    [Tooltip("A zone counts toward surround only when its soldier contacts are at least this fraction of the formation's living soldiers — a neighbour brushing the edge of the line is not a second side")]
    public float surroundMinEngagedFraction = 0.10f;
    [Tooltip("Rate limit on panic intake: shock above maxShockPerSecond queues and lands over the following ticks (never discarded). Officer deaths bypass it; contagion does not")]
    public bool enableShockCap = true;
    public float maxShockPerSecond = 8f;

    [Header("Recovery")]
    [Tooltip("Morale regained per second while out of melee and not taking casualties")]
    public float recoveryPerSecond = 3f;
    [Tooltip("Seconds after any casualty (melee or arrow) before recovery resumes")]
    public float recoveryLossCooldown = 3f;

    [Header("Automatic reform (shared rule; used by rally in Chunk A)")]
    [Tooltip("Max fraction of soldiers engaged (enemy within 3 m) to START a reform. 0 = nobody in contact")]
    public float reformStartEngagedFraction = 0.15f;
    [Tooltip("How long a century stays loose (BrokenRanks) before it regroups: a charge's pursuit lasts this long. Then, if contact has dropped, it walks back to the standard; if it is still locked in melee it re-forms in place and fights on as a line")]
    public float pursuitMaxSeconds = 9f;
    [Tooltip("Seconds after an aborted reform before another may start — stops abort/restart flicker")]
    public float reformRetryCooldown = 2f;

    [Header("Routing")]
    [Tooltip("Routing soldiers flee at moveSpeed times this")]
    public float fleeSpeedMultiplier = 1.2f;
    [Tooltip("How strongly fleeing bends toward the army's own back edge (0 = straight away from the nearest enemy formation)")]
    public float fleeBackBias = 0.6f;
    [Tooltip("false: Routing is permanent — routers run off the map and are removed. true: routers stop at the field edge and may rally above rallyThreshold")]
    public bool routedCanRally = false;

    [Header("Battle end")]
    [Tooltip("A team loses when this fraction of its formations are Routing or destroyed (counted from its actual formations)")]
    public float collapseFraction = 0.5f;
    [Tooltip("Seconds of Active battle before the timer decides it — a stall backstop, not a pacing tool")]
    public float battleTimeLimit = 600f;
    [Tooltip("The HUD countdown turns bright and large inside this many final seconds")]
    public float timerWarningSeconds = 60f;

    [Header("Panic inputs (each can be switched off to isolate problems on device)")]
    [Tooltip("Casualty shock is multiplied by the zone of the killing blow")]
    public bool enableFlankShock = true;
    public float flankShockMultiplier = 2f;
    public float rearShockMultiplier = 3f;
    [Tooltip("Shock decays slower while more than one enemy formation is engaged on this one")]
    public bool enableOutnumberShock = true;
    [Tooltip("decay = shockDecayPerSecond / (1 + this * extraEnemyFormations)")]
    public float outnumberDecaySlowdown = 1f;
    [Tooltip("A formation routing shocks same-team formations nearby")]
    public bool enableContagion = true;
    [Tooltip("Anchor-to-anchor radius (m) of rout contagion")]
    public float contagionRadius = 30f;
    public float contagionShock = 15f;
    [Tooltip("An officer's death adds his OfficerRanks deathShock to his formation")]
    public bool enableOfficerShock = true;

    [Header("Tide bar (HUD)")]
    [Tooltip("Show the battle-power bar at the top of the screen while a battle is Active")]
    public bool showTideBar = true;
    [Tooltip("A wavering formation still counts for at least this share of its living men in the tide; routing and destroyed formations count for nothing")]
    public float tideMoraleFloor = 0.25f;

    [Header("Wavering halo (banner)")]
    [Tooltip("Halo shows when reported morale drops below this")]
    public float haloEnter = 50f;
    [Tooltip("Halo hides again only once morale is above this")]
    public float haloExit = 53f;
    [Tooltip("TESTING: every banner shows the halo at half intensity so the look can be judged on device")]
    public bool forceHaloForTesting = false;
    [Tooltip("TESTING: officer grade numbers show on every formation, not just the selected / inspected one")]
    public bool forceGradeMarkers = false;

    [Header("Debug")]
    [Tooltip("Show each formation's morale and state, plus temporary officer grades, for on-device tuning")]
    public bool showDebugMorale = true;
}
