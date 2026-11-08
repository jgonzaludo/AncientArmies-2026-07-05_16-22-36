using UnityEngine;

// the rules per type: speed, blocking, concealment, route cost. one table, so tuning is in one place
public static class TerrainRules
{
    public static float Speed(TerrainType t)
    {
        switch (t)
        {
            case TerrainType.Road: return 1.15f;
            case TerrainType.Forest: return 0.7f;
            case TerrainType.Mud: return 0.6f;
            case TerrainType.Ford: return 0.5f;
            case TerrainType.Bridge: return 1f;
            default: return 1f;   // grass, and blocked types never get walked on
        }
    }

    // eases the speed toward the ground under the anchor, so a cell border is not a speed pop
    public static float Smooth(float current, float target, float dt) => current + (target - current) * (1f - Mathf.Exp(-6f * dt));

    public static bool Blocked(TerrainType t) => t == TerrainType.Deep || t == TerrainType.Rock;
    public static bool Concealed(TerrainType t) => t == TerrainType.Forest;

    // route cost per metre: slow ground costs more. blocked ground is never entered
    public static float Cost(TerrainType t) => Blocked(t) ? float.PositiveInfinity : 1f / Speed(t);
}
