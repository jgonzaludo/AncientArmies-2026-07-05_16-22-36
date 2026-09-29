using UnityEngine;

// Reproducible battles: a pending seed is applied to UnityEngine.Random (the
// only random source in the simulation) right before BattleSetup spawns, so
// the same seed and settings give the same battle. Always compiled — a seed
// costs nothing and device logs record it.
public static class SimSeed
{
    public static int? Pending { get; private set; }
    public static int Current { get; private set; } = -1;   // -1 = unseeded

    public static void Set(int seed) => Pending = seed;
    public static void Clear() { Pending = null; }

    // Called by BattleSetup.Awake before any soldier exists.
    public static void ApplyIfPending()
    {
        if (Pending.HasValue)
        {
            Current = Pending.Value;
            Random.InitState(Current);
        }
        else Current = -1;
    }
}

