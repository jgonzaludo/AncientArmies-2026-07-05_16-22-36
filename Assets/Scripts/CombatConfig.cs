using UnityEngine;

// Combat tuning (the third ScriptableObject). These used to be BattleSetup
// fields, where Battle.unity pinned them; as an asset they are tuned in one
// place with no scene edit. Referenced from BattleSetup.combatConfig.
[CreateAssetMenu(fileName = "CombatConfig", menuName = "Ancient Armies/Combat Config")]
public class CombatConfig : ScriptableObject
{
    [Header("Battle pacing")]
    [Tooltip("Global multiplier on ALL damage dealt — lower = longer battles; cadence and animations are untouched")]
    public float globalDamageScale = 0.15f;

    [Header("Personal space (global)")]
    [Tooltip("Soldier body collider radius (m), applied at spawn to EVERY soldier — friend and enemy, in every state. 2x this is the minimum gap between bodies. Melee slots are 1.15 m apart, so stay under ~0.55. Was 0.35. Visuals are not scaled")]
    public float soldierColliderRadius = 0.5f;

    [Header("Line cohesion")]
    [Tooltip("Slot weight of a soldier in melee contact (was hardcoded 0.12). Higher keeps clashing lines lines instead of letting the two sides interleave")]
    public float engagedSlotWeight = 0.3f;

    [Header("Directional combat (front / flank / rear)")]
    [Tooltip("Damage multiplier when attacking a formation from its front arc. Below 1: head-on fights bleed slowly — few men died at the front")]
    public float frontDamageMultiplier = 0.5f;
    [Tooltip("Damage multiplier when attacking a formation from either side")]
    public float flankDamageMultiplier = 1.5f;
    [Tooltip("Damage multiplier when attacking a formation from behind — also what makes the pursuit of routers deadly")]
    public float rearDamageMultiplier = 2f;
    [Tooltip("Attacks within this many degrees of the defender's forward count as frontal")]
    public float frontArcHalfAngleDeg = 60f;
    [Tooltip("Attacks within this many degrees of the defender's rear count as rear attacks")]
    public float rearArcHalfAngleDeg = 60f;

    [Header("Target lock (ordered attack)")]
    [Tooltip("An Attacking formation keeps marching at its ordered target while a corner of it brushes another enemy; it only stops to fight where it stands when it is truly blocked")]
    public bool targetLock = true;
    [Tooltip("Blocked = at least this fraction of the formation's men are in contact with enemies OTHER than the ordered target...")]
    public float targetLockBlockedFraction = 0.4f;
    [Tooltip("...continuously for this many seconds (or the formation is surrounded). Contact with the ordered target itself always engages")]
    public float targetLockBlockedSeconds = 1.5f;

    [Header("Ordering")]
    [Tooltip("Formations route around friendly and enemy blocks (RoutePlanner) instead of marching a straight line")]
    public bool routePlanning = true;
    [Tooltip("Soldiers of a standing friendly block sidestep out of a marching friendly block's way instead of being pushed along by it")]
    public bool yieldToMarching = true;
    [Tooltip("Sidestep starts when a marching friend is this close (m)")]
    public float yieldDistance = 2.6f;
    [Tooltip("Max sidestep speed (m/s)")]
    public float yieldSpeed = 3f;
    [Tooltip("A move order never ends inside a friendly formation's footprint")]
    public bool destinationFix = true;

    [Header("Target stickiness (no back-and-forth)")]
    [Tooltip("A soldier holds its target for a minimum time and keeps it out to a wider range; melee men with a live target never recall to their slot, and a recall latches until the man is back within recallExit of the slot")]
    public bool stickyTargets = true;
    [Tooltip("Seconds a new target is held before it may be dropped for range reasons")]
    public float targetHoldSeconds = 1.5f;
    [Tooltip("While held, a target is kept out to this multiple of the acquire radius (normally 1.25)")]
    public float heldKeepRadiusScale = 2f;
    [Tooltip("Distance (m) from its slot at which a displaced man starts running back")]
    public float recallEnter = 3f;
    [Tooltip("...and stops running back (hysteresis)")]
    public float recallExit = 1f;

    [Header("Edge contact swing-in")]
    [Tooltip("When an edge of the formation touches the ordered target (or the formation has no other target), the rest of the formation wheels in around the contact to bring its front to bear")]
    public bool swingIn = false;   // OFF: in the edge test it pushed the unit into surround shock and a rout at <1% casualties — needs another pass
    [Tooltip("Seconds of contact before the swing starts, so a brush does not twitch the line")]
    public float swingInDelay = 0.5f;
    public float swingInDegPerSec = 35f;
    [Tooltip("Maximum total swing for one contact (degrees)")]
    public float swingInMaxDeg = 60f;
    [Tooltip("Stop swinging once this share of the men is in contact")]
    public float swingInStopFraction = 0.6f;
    [Tooltip("Do not swing when already facing the enemy mass within this many degrees")]
    public float swingInMinAngle = 12f;

    [Header("Route smoothing")]
    [Tooltip("Rounds the corners of planned routes into gentle curves; the unit follows the same curve it draws")]
    public bool smoothRoutes = true;
    [Tooltip("Longest stretch (m) before/after a corner that its rounding may use; scaled down on short legs")]
    public float routeFilletReach = 10f;
    [Tooltip("Route points are steered at this far ahead along the curve (m): bigger = smoother, looser tracking")]
    public float routeLookahead = 4.5f;
}
