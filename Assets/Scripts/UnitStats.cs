using UnityEngine;

public enum Team { Blue, Red }

// Century role slots (Phase 1). Roles are data first: every role currently
// spawns the generic legionary/archer visual, but the composition system
// reserves the positions so specialist prefabs can be swapped in later
// without touching spawn logic. A century is 80 TOTAL soldiers — specialists
// occupy slots, they are not added on top.
public enum SoldierRole
{
    Legionary, Archer,                      // ordinary troops
    Centurion, Optio, Signifer, Tesserarius, Cornicen
}

[System.Serializable]
public class UnitStats
{
    public string unitName = "Swordsmen";
    public bool isRanged;
    public float maxHealth = 35f;
    public float moveSpeed = 3.2f;
    public float attackDamage = 12f;
    public float attackCooldown = 1.4f;
    public float strikeRange = 1.7f;

    [Header("Ranged (used only when isRanged)")]
    [Tooltip("Maximum distance an individual soldier can shoot")]
    public float rangedRange = 20f;
    [Tooltip("Anchor-to-anchor distance a ranged formation stops at when attacking; keep around 75-85% of rangedRange so archers visibly fight from a second line")]
    public float rangedPreferredRange = 16f;
    [Tooltip("Inside this distance a ranged soldier stops shooting and defends with its sidearm")]
    public float rangedMinRange = 2.5f;
    public float projectileSpeed = 13f;

    // Data-driven century composition: one of each command role, remainder
    // ordinary. Slot preferences: centurion at the front-right corner (his
    // historical post on the right of the first rank), signifer right of
    // center (the signum anchors the front), cornicen behind the signifer,
    // optio rear-center (his historical post), tesserarius indistinguishable
    // in the ranks. Returns one role per slot index (front row = low indices,
    // higher column = further right). The centurion REPLACES a legionary —
    // melee centuries only, never archers — and is written last so exactly
    // one exists even when a narrow frontage collides with another staff slot.
    public SoldierRole[] BuildCenturyRoles(int count, int columns)
    {
        var roles = new SoldierRole[count];
        SoldierRole ordinary = isRanged ? SoldierRole.Archer : SoldierRole.Legionary;
        for (int i = 0; i < count; i++) roles[i] = ordinary;
        if (count < columns * 2) return roles;   // tiny debug formations: no staff

        int frontCenter = columns / 2;
        int rows = Mathf.CeilToInt(count / (float)columns);
        roles[Mathf.Min(frontCenter + 1, columns - 1)] = SoldierRole.Signifer;
        roles[Mathf.Min(frontCenter + 1 + columns, count - 1)] = SoldierRole.Cornicen;
        roles[Mathf.Min((rows - 1) * columns + frontCenter, count - 1)] = SoldierRole.Optio;
        roles[Mathf.Min(columns + 1, count - 1)] = SoldierRole.Tesserarius;
        if (!isRanged) roles[columns - 1] = SoldierRole.Centurion;
        return roles;
    }
}
