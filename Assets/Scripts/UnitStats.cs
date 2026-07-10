using UnityEngine;

public enum Team { Blue, Red }

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
}
