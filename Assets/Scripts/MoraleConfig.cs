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
    public float contactCeilingScale = 4.25f;
    [Tooltip("Out of melee: ceiling = 100 - lossPercent * this. How high a battered unit can recover")]
    public float restCeilingScale = 2f;
    [Tooltip("On rally, this share of the losses so far stops counting against the CONTACT ceiling, so a rallied unit can fight again. Rest ceiling keeps full losses")]
    public float rallyLossForgiveness = 0.5f;

    [Header("Shock debt (sudden pressure, subtracted from base morale)")]
    [Tooltip("Shock debt added per percent of starting strength killed within one tick")]
    public float shockMultiplier = 3f;
    [Tooltip("Shock debt removed per second, at all times — in melee too")]
    public float shockDecayPerSecond = 5f;

    [Header("Recovery")]
    [Tooltip("Morale regained per second while out of melee and not taking casualties")]
    public float recoveryPerSecond = 3f;
    [Tooltip("Seconds after any casualty (melee or arrow) before recovery resumes")]
    public float recoveryLossCooldown = 3f;

    [Header("Automatic reform (shared rule; used by rally in Chunk A)")]
    [Tooltip("Max fraction of soldiers engaged (enemy within 3 m) to START a reform. 0 = nobody in contact")]
    public float reformStartEngagedFraction = 0f;
    [Tooltip("Seconds after an aborted reform before another may start — stops abort/restart flicker")]
    public float reformRetryCooldown = 2f;

    [Header("Routing")]
    [Tooltip("Routing soldiers flee at moveSpeed times this")]
    public float fleeSpeedMultiplier = 1.2f;
    [Tooltip("How strongly fleeing bends toward the army's own back edge (0 = straight away from the nearest enemy formation)")]
    public float fleeBackBias = 0.6f;

    [Header("Debug")]
    [Tooltip("Show each formation's morale and state, plus temporary officer grades, for on-device tuning")]
    public bool showDebugMorale = true;
}
