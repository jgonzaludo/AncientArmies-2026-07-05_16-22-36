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
}
