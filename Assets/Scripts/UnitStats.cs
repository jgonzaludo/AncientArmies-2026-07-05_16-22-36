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
    public float rangedRange = 14f;
    public float rangedMinRange = 2.5f;
    public float projectileSpeed = 13f;
}
